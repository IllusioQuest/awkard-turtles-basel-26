using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DigitalHeat.Core;
using Xunit;
using Xunit.Abstractions;

namespace DigitalHeat.Fleet.Tests;

// Spec tests docs/model/fleet-simulation.md §13.1 (case values), §13.2 (cross-check against Core),
// §13.3 (event = tick), §13.7 (determinism) and §13.8 (performance fields are reported).
public class SpecTests(ITestOutputHelper output)
{
    private static string Root => Scenario.FindRepoRoot(AppContext.BaseDirectory);

    private static Scenario LoadScenario(string name) =>
        Scenario.Load(Path.Combine(Root, "config/fleet/scenarios", name + ".json"));

    private static IReadOnlyList<DemandHour> CoreDemand(ReferenceParameters p)
    {
        // Same as CoreTests.Demand().
        var lines = File.ReadAllLines(Path.Combine(Root, "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
        int header = Array.FindIndex(lines, x => x.StartsWith("time,", StringComparison.Ordinal));
        var weather = lines.Skip(header + 1).Where(x => x.Length > 0).Select(x =>
        {
            var c = x.Split(',');
            return new WeatherHour(c[0], double.Parse(c[1], CultureInfo.InvariantCulture));
        }).ToArray();
        return HeatDemand.Build(weather, p);
    }

    private static string TempDir(string prefix)
    {
        string d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid());
        Directory.CreateDirectory(d);
        return d;
    }

    // ---------------------------------------------------------------- 13.1

    [Fact]
    public void CaseValues()
    {
        var p = ReferenceParameters.Load(Path.Combine(Root, "config", "reference-house.json"));
        var demand = CoreDemand(p);
        Assert.Equal(206, demand.Count(h => h.HeatingDay) / 24);
        Assert.InRange(demand.Sum(h => h.SpaceHeatKwh), 26850, 26852);                  // 26,850.5 kWh
        double serverKw = p.ServerElectricKw.Value, share = p.HeatRecoveryShare.Value;
        Assert.Equal(12.5, demand.Max(h => h.SpaceHeatKwh) / (serverKw * share), 6);     // 10 kW / 0.8 kW per server
        Assert.Equal(19.2, serverKw * share * 24, 9);                                   // kWh recovered per server-day
        // The fleet's reference house reproduces 206 days and 26,850.5 kWh: FleetSimulationTests.ReferenceHouseWholeYearMatchesCaseDemand.
    }

    // ---------------------------------------------------------------- 13.2

    [Fact]
    public void ReferenceHouseWithBacklogMatchesCore()
    {
        string dir = TempDir("fleet-xcheck-");
        try
        {
            // One Type 2 job type whose backlog always fills the 4 servers: one huge job (units = 1 kWh each),
            // deadline after the run end, 4 chunks, continuous load, no overheads.
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config/fleet/job-types/research-sim-montecarlo.json")))!;
            node["id"] = "backlog";
            node["energy_kwh_per_work_unit"] = JsonNode.Parse("""{"value":1,"unit":"kWh/unit","source":"test"}""");
            node["size"] = JsonNode.Parse("""{"dist":"fixed","value":1,"unit":"units","source":"test"}""");
            node["deadline_s"] = JsonNode.Parse("""{"dist":"fixed","value":1,"unit":"s","source":"test"}""");
            string typePath = Path.Combine(dir, "backlog.json");
            File.WriteAllText(typePath, node.ToJsonString());

            string jobsPath = Path.Combine(dir, "jobs.csv");
            const long yearMs = 8760L * FleetTime.MsPerHour;
            JobsInputCsv.Write(jobsPath, [new JobRequest("backlog-1", "backlog", 0, 0, 2 * yearMs, 1_000_000, 1, "anywhere", null, null, null, null)]);

            var s = LoadScenario("reference");
            s.Run.EndLocal = "2026-01-01T00:00";
            s.JobTypes = [typePath];
            s.Demand = new DemandSection { FromFile = jobsPath };
            s.Pricing.Id = "fixed-only";
            s.Forecast.Kind = "perfect";
            s.Logging.HouseTimeseries = "none";
            var run = FleetSimulation.Run(s);
            var acc = run.Result.Houses[0].Account;
            double fleetServerHours = acc.ElectricKwh / run.ServerParams.MaxElectricKw;
            double fleetUsed = acc.UsedKwh;

            var p = ReferenceParameters.Load(Path.Combine(Root, "config", "reference-house.json"));
            var core = YearSimulation.Run(CoreDemand(p), p, new GreedyHeatScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor()).Summary;
            Assert.InRange(core.ServerHours, 22660.5, 22661.5);
            Assert.InRange(core.ServerHeatUsedKwh, 18128.5, 18129.5);

            double dh = fleetServerHours / core.ServerHours - 1, du = fleetUsed / core.ServerHeatUsedKwh - 1;
            output.WriteLine($"server-hours fleet {fleetServerHours:F1} core {core.ServerHours:F1} ({dh:P2}); heat used fleet {fleetUsed:F1} core {core.ServerHeatUsedKwh:F1} ({du:P2})");
            // Tolerance 1 % (measured about 0.3 %). Reason: Core fills each hour greedily to the hour's demand (power follows demand hour by hour
            // and the tank only buffers surplus), the fleet scheduler uses hysteresis (charge to 90 % of the tank, hold at demand,
            // recharge at 30 %), so it shifts some power in time and dumps heat when the tank is full.
            Assert.InRange(dh, -0.01, 0.01);
            Assert.InRange(du, -0.01, 0.01);
            Assert.Equal(0, run.Result.Jobs.Count(j => j.Status == "rejected_price"));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------------------------------------------------------------- 13.3

    /// <summary>Wraps a scheduler and counts the events that touch each house (house, its chunks and servers; global events count for all).</summary>
    private sealed class CountingScheduler(IFleetScheduler inner, int houses) : IFleetScheduler
    {
        public string Id => inner.Id;
        public readonly long[] PerHouse = new long[houses];
        public readonly Dictionary<string, long> PerJob = [];
        public long Global;
        public long Total;

        public void Initialize(IFleetView view, IFleetActions act) => inner.Initialize(view, act);

        public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions act)
        {
            foreach (var e in events)
            {
                Total++;
                int? h = e.House?.Value;
                if (h is null && e.Chunk is ChunkId c) h = view.Chunk(c).House.Value;
                if (h is null && e.Server is ServerId sv) h = view.Servers[sv.Value].House.Value;
                if (h is int hi) PerHouse[hi]++; else Global++;
                string? job = e.Job is JobId j ? view.Job(j).Request.JobKey : e.Chunk is ChunkId c2 ? view.Job(view.Chunk(c2).Job).Request.JobKey : null;
                if (job is not null) PerJob[job] = PerJob.GetValueOrDefault(job) + 1;
            }
            inner.OnEvents(view, events, act);
        }
    }

    [Fact]
    public void EventModeEqualsTickMode()
    {
        string dir = TempDir("fleet-tick-");
        try
        {
            const int houseCount = 20;
            const long tickMs = 1000;
            var spec = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config/fleet/houses/basel-region-100.spec.json")))!;
            spec["count"] = houseCount;
            Scenario Make(string mode)
            {
                var s = LoadScenario("basel-region-100");
                s.Run.EndLocal = "2025-02-01T00:00";   // January 2025
                s.Run.Mode = mode; s.Run.TickMs = tickMs;
                s.Houses = new HousesSection { Generate = JsonDocument.Parse(spec.ToJsonString()).RootElement };
                // 20 of 100 houses: volume scaled by 0.2 so the load per house stays that of the 100-house scenario.
                s.Demand = new DemandSection { VolumeScale = s.JobTypes.ToDictionary(p => Path.GetFileNameWithoutExtension(p)!, _ => 0.2) };
                s.Logging.HouseTimeseries = "none";
                return s;
            }

            // Generate the jobs once; both runs read the file.
            var gen = Make("event");
            string jobsPath = Path.Combine(dir, "jobs-input.csv");
            JobsInputCsv.Write(jobsPath, DemandGenerator.Generate(JobTypeLoader.LoadAll(gen), RunCalendar.From(gen.Run), gen.Run.Seed, Root));
            var ev = Make("event"); ev.Demand = new DemandSection { FromFile = jobsPath };
            var tk = Make("tick"); tk.Demand = new DemandSection { FromFile = jobsPath };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var counter = new CountingScheduler(new MockHeatLedScheduler(ev.Scheduler), houseCount);
            var evRun = FleetSimulation.Run(ev, scheduler: counter);
            double evS = sw.Elapsed.TotalSeconds; sw.Restart();
            var tkRun = FleetSimulation.Run(tk);
            output.WriteLine($"event run {evS:F1} s ({evRun.Result.EventCount} events), tick run {sw.Elapsed.TotalSeconds:F1} s, jobs {evRun.Jobs.Count}");

            double maxKw = evRun.ServerParams.MaxElectricKw;
            double perEventKwh(int servers) => tickMs / (double)FleetTime.MsPerHour * servers * maxKw;   // one event delayed by a tick, all servers at max
            double maxHouseDev = 0, maxHouseBound = 0;
            for (int i = 0; i < houseCount; i++)
            {
                var a = evRun.Result.Houses[i].Account; var b = tkRun.Result.Houses[i].Account;
                double bound = (counter.PerHouse[i] + counter.Global) * perEventKwh(evRun.Result.Houses[i].ServerCount);
                double dE = Math.Abs(a.ElectricKwh - b.ElectricKwh), dU = Math.Abs(a.UsedKwh - b.UsedKwh);
                maxHouseDev = Math.Max(maxHouseDev, Math.Max(dE, dU)); maxHouseBound = Math.Max(maxHouseBound, bound);
                Assert.True(dE <= bound, $"house {i}: electric kWh differ by {dE} > bound {bound}");
                Assert.True(dU <= bound, $"house {i}: heat used differs by {dU} > bound {bound}");
            }
            output.WriteLine($"max house deviation {maxHouseDev:G4} kWh (largest bound {maxHouseBound:G4} kWh)");

            // Per job: events touching the job, each moving at most one server's power by one tick.
            var tkJobs = tkRun.Result.Jobs.ToDictionary(j => j.Request.JobKey);
            double maxJobDevKwh = 0, maxJobDevUnits = 0;
            foreach (var a in evRun.Result.Jobs)
            {
                var b = tkJobs[a.Request.JobKey];
                long n = counter.PerJob.GetValueOrDefault(a.Request.JobKey) + 1;
                double kwhBound = n * perEventKwh(1) * evRun.JobTypes[a.Request.TypeIndex].MaxChunks;
                double kwhPerUnit = evRun.JobTypes[a.Request.TypeIndex].EnergyKwhPerWorkUnit.Value;
                double dE = Math.Abs(a.EnergyKwh - b.EnergyKwh), dW = Math.Abs(a.UnitsDone - b.UnitsDone);
                maxJobDevKwh = Math.Max(maxJobDevKwh, dE); maxJobDevUnits = Math.Max(maxJobDevUnits, dW);
                Assert.True(dE <= kwhBound, $"{a.Request.JobKey}: energy differs by {dE} > bound {kwhBound}");
                Assert.True(dW <= kwhBound / kwhPerUnit, $"{a.Request.JobKey}: units differ by {dW} > bound {kwhBound / kwhPerUnit}");
            }
            output.WriteLine($"max job deviation {maxJobDevKwh:G4} kWh, {maxJobDevUnits:G4} units");
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------------------------------------------------------------- 13.7, 13.8

    private static readonly string[] TimingKeys = ["wall_time_s", "events_per_second", "phase_wall_time"];

    private static string WithoutTiming(string runJson)
    {
        var o = JsonNode.Parse(runJson)!.AsObject();
        foreach (var k in TimingKeys) o.Remove(k);
        return o.ToJsonString();
    }

    [Fact]
    public void SameScenarioGivesByteIdenticalOutputExceptTimingFields()
    {
        Scenario Jan()
        {
            var s = LoadScenario("reference");
            s.Run.EndLocal = "2025-02-01T00:00";
            s.Logging.HouseTimeseries = "events";   // the largest set of files
            return s;
        }
        string a = TempDir("fleet-det-a-"), b = TempDir("fleet-det-b-");
        try
        {
            FleetOutput.Write(FleetSimulation.Run(Jan()), a);
            FleetOutput.Write(FleetSimulation.Run(Jan()), b);
            var files = Directory.GetFiles(a, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(a, f)).Order().ToArray();
            Assert.Equal(files, Directory.GetFiles(b, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(b, f)).Order().ToArray());
            Assert.Contains("run.json", files);
            foreach (var f in files.Where(f => f != "run.json"))
                Assert.True(File.ReadAllBytes(Path.Combine(a, f)).SequenceEqual(File.ReadAllBytes(Path.Combine(b, f))), $"{f} differs");

            // §13.8: run.json carries wall time and events per second (values not asserted); the rest is identical.
            string ja = File.ReadAllText(Path.Combine(a, "run.json")), jb = File.ReadAllText(Path.Combine(b, "run.json"));
            foreach (var j in new[] { ja, jb })
            {
                var o = JsonNode.Parse(j)!.AsObject();
                Assert.True(o.ContainsKey("wall_time_s"));
                Assert.True(o.ContainsKey("events_per_second"));
            }
            Assert.Equal(WithoutTiming(ja), WithoutTiming(jb));
        }
        finally { Directory.Delete(a, true); Directory.Delete(b, true); }
    }
}
