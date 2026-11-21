using Xunit;

namespace DigitalHeat.Fleet.Tests;

// Engine tests (docs/model/fleet-simulation.md §13.4-13.6). Units are chosen so that one work unit
// is 1 kWh on a 1 kW server: a server at load 1 does 1 unit per hour.

internal sealed record FixedDist(double Value) : IDistribution
{
    public double Sample(Rng rng) => Value;
    public double Mean => Value;
    public string Unit => "test";
    public string Source => "test";
}

internal sealed class Script : IFleetScheduler
{
    public string Id => "script";
    public Action<IFleetView, IFleetActions>? Init;
    public Action<IFleetView, IReadOnlyList<FleetEvent>, IFleetActions>? On;
    public readonly List<(long Now, FleetEvent[] Events)> Calls = [];
    public IFleetActions? Actions;

    public void Initialize(IFleetView view, IFleetActions act) { Actions = act; Init?.Invoke(view, act); }

    public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions act)
    {
        Calls.Add((view.NowMs, events.ToArray()));
        On?.Invoke(view, events, act);
    }

    public IEnumerable<FleetEvent> All(FleetEventKind kind) => Calls.SelectMany(c => c.Events).Where(e => e.Kind == kind);
}

internal sealed class TestPricing(Func<JobRequest, bool>? accept = null, double price = 0.4) : IPricing
{
    public string Id => "test";
    public Quote Quote(IJobView job, IFleetView view) => new(price, accept?.Invoke(job.Request) ?? true);
}

internal sealed class NoForecast : IForecastProvider
{
    public string Kind => "none";
    public double HeatDemandKw(HouseId house, int issuedHour, int targetHour) => 0;
}

internal static class Kit
{
    public static readonly ServerParams Params = new(1.0, 0.8, 2.5);
    public const long H = FleetTime.MsPerHour;

    public static EngineHouse House(string id, int servers, double demandKw, int hours, double cap = 35,
        double tank0 = 0, string? canton = null, double spaceShare = 1)
    {
        var demand = new HouseDemand
        {
            SpaceHeatKw = Enumerable.Repeat(demandKw * spaceShare, hours).ToArray(),
            HotWaterKw = Enumerable.Repeat(demandKw * (1 - spaceShare), hours).ToArray(),
            TotalKw = Enumerable.Repeat(demandKw, hours).ToArray(),
            HeatingDays = 0,
        };
        var spec = new HouseSpec(id, 47, 8, "cell", null, canton, "w", 10, 25.6, 12, cap, tank0, 0.3325, servers);
        return new EngineHouse(spec, servers, demand);
    }

    public static JobType Type(string id, int index, JobClass cls, int maxChunks = 1, double minChunk = 1,
        double startOverheadS = 0, bool allowPause = true, double minLoad = 0, bool checkpointable = false,
        double resumeOverheadS = 0, double checkpointOverheadS = 0, string placement = "anywhere",
        string[]? allowed = null, double chfPerUnit = 0.5) => new()
    {
        Id = id, Index = index, Class = cls, WorkUnit = "unit",
        EnergyKwhPerWorkUnit = new Quantity(1, "kWh/unit", "test"),
        Size = new FixedDist(1), BatchSize = new FixedDist(1), DeadlineS = new FixedDist(1),
        MaxChunks = maxChunks, MinChunkWorkUnits = minChunk, StartOverheadS = startOverheadS,
        Lifecycle = new JobLifecycle(allowPause, minLoad, checkpointable, resumeOverheadS, checkpointOverheadS),
        InputMbPerWorkUnit = 0, StateMb = 0,
        Placement = new FleetPlacementPolicy(placement, allowed, "test"),
        Volume = new JobVolume(0, "test", "test"),
        ArrivalShape = new ArrivalShape(new double[168], "test", "test"),
        OriginMix = new OriginMix(0, null),
        Price = new JobPrice("fixed", new Quantity(chfPerUnit, "CHF/unit", "test")),
    };

    public static JobRequest Job(string key, JobType t, long arrivalMs, long deadlineMs, double units,
        string origin = "anywhere", string? canton = null) =>
        new(key, t.Id, t.Index, arrivalMs, deadlineMs, units, 1, origin, null, null, null, canton);

    public static EngineInputs Inputs(long endMs, IReadOnlyList<EngineHouse> houses, IReadOnlyList<JobType> types,
        IReadOnlyList<JobRequest> jobs, IFleetScheduler sch, IPricing? pricing = null, RunMode mode = RunMode.Event,
        long tickMs = 1000, TimeseriesMode ts = TimeseriesMode.None) => new()
    {
        EndMs = endMs, StartHourOfWeek = 0, Mode = mode, TickMs = tickMs, ServerParams = Params,
        Houses = houses, JobTypes = types, Jobs = jobs, Scheduler = sch, Pricing = pricing ?? new TestPricing(),
        Forecast = new NoForecast(), Timeseries = ts,
    };

    public static FleetRunResult Run(EngineInputs i) => new FleetEngine(i).Run();
}

public class EngineTests
{
    private const long H = Kit.H;

    // ------------------------------------------------------------ 13.4 exactness

    [Fact]
    public void TankFullTimeAndDumpedHeatAreExact()
    {
        const int hours = 14;
        var t = Kit.Type("t", 0, JobClass.Type2);
        var jobs = Enumerable.Range(0, 4).Select(i => Kit.Job($"j{i}", t, 0, 100 * H, 1000)).ToArray();
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                foreach (var e in ev.Where(e => e.Kind == FleetEventKind.JobArrived))
                    a.Start(e.Job!.Value, v.Houses[0].Servers[e.Job.Value.Value], 1000, 1);
            },
        };
        var res = Kit.Run(Kit.Inputs(hours * H, [Kit.House("h", 4, 0.3, hours)], [t], jobs, sch));

        // produced 4 x 1 kW x 0.8 = 3.2 kW, demand 0.3 kW: net 2.9 kW into a 35 kWh tank
        double expectedMs = Math.Ceiling(35.0 / 2.9 * 3.6e6);
        var full = sch.All(FleetEventKind.TankFull).Single();
        Assert.Equal((long)expectedMs, full.TimeMs);
        double exactFullH = 35.0 / 2.9;
        double dumpedAnalytic = 2.9 * (hours - exactFullH);
        Assert.Equal(dumpedAnalytic, res.Houses[0].Account.DumpedKwh, 9);
        Assert.Equal(35, res.Houses[0].TankFinalKwh, 9);
    }

    [Fact]
    public void TankEmptyTimeIsExact()
    {
        const int hours = 20;
        var t = Kit.Type("t", 0, JobClass.Type2);
        var sch = new Script();
        var res = Kit.Run(Kit.Inputs(hours * H, [Kit.House("h", 0, 2.9, hours, tank0: 35)], [t], [], sch));
        Assert.Equal((long)Math.Ceiling(35.0 / 2.9 * 3.6e6), sch.All(FleetEventKind.TankEmpty).Single().TimeMs);
        double backup = 2.9 * hours - 35;
        Assert.Equal(backup, res.Houses[0].Account.BackupKwh, 9);
    }

    [Fact]
    public void RegisteredThresholdFiresOnceInEachDirection()
    {
        const int hours = 6;
        var t = Kit.Type("t", 0, JobClass.Type2);
        var jobs = new[] { Kit.Job("j", t, 0, 100 * H, 2) };
        var sch = new Script
        {
            Init = (v, a) => a.RegisterTankThreshold(new HouseId(0), 1.0),
            On = (v, ev, a) =>
            {
                if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(new JobId(0), v.Houses[0].Servers[0], 2, 1);
            },
        };
        // 0.8 kW produced for 2 h, demand 0.4 kW: tank rises 0.4 kW, crosses 1.0 kWh at 2.5 h?  (job ends at 2 h)
        var res = Kit.Run(Kit.Inputs(hours * H, [Kit.House("h", 1, 0.4, hours)], [t], jobs, sch));
        // after 2 h tank = 0.8 kWh < 1.0, then demand empties it: no threshold crossing at all
        Assert.Empty(sch.All(FleetEventKind.TankThreshold));
        Assert.Equal(0.0, res.Houses[0].TankFinalKwh, 9);

        var sch2 = new Script
        {
            Init = (v, a) => a.RegisterTankThreshold(new HouseId(0), 0.5),
            On = sch.On,
        };
        Kit.Run(Kit.Inputs(hours * H, [Kit.House("h", 1, 0.4, hours)], [t], jobs, sch2));
        var th = sch2.All(FleetEventKind.TankThreshold).ToArray();
        // up through 0.5 at 0.5/0.4 h = 1.25 h, back down through 0.5 after the job (tank 0.8 at 2 h, falls 0.4 kW)
        Assert.Equal(2, th.Length);
        Assert.Equal((long)Math.Ceiling(0.5 / 0.4 * 3.6e6), th[0].TimeMs);
        Assert.Equal(0.5, th[0].ThresholdKwh);
        Assert.Equal(2 * H + (long)Math.Ceiling(0.3 / 0.4 * 3.6e6), th[1].TimeMs);
    }

    // ------------------------------------------------------------ 13.5 conservation

    [Fact]
    public void ConservationPerHouseJobAndServer()
    {
        const int hours = 48;
        var t1 = Kit.Type("t1", 0, JobClass.Type1, maxChunks: 2, minChunk: 0.5, chfPerUnit: 0.5);
        var t2 = Kit.Type("t2", 1, JobClass.Type2, maxChunks: 2, minChunk: 0.5, startOverheadS: 120);
        var jobs = new List<JobRequest>();
        for (int i = 0; i < 6; i++) jobs.Add(Kit.Job($"a{i}", t1, i * 1_800_000L, 40 * H, 3 + i * 0.7));
        for (int i = 0; i < 4; i++) jobs.Add(Kit.Job($"b{i}", t2, i * 2_700_000L + 1000, 40 * H, 4 + i * 0.9));
        jobs = jobs.OrderBy(j => j.ArrivalMs).ThenBy(j => j.TypeIndex).ThenBy(j => j.JobKey).ToList();
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                foreach (var s in v.Servers)
                {
                    Assert.InRange(s.Load, 0, 1);
                    if (s.Chunk is not null) Assert.Equal(s.Id, v.Chunk(s.Chunk.Value).Server);
                }
                foreach (var jid in v.OpenJobs.ToArray())
                {
                    var j = v.Job(jid);
                    while (j.UnassignedUnits > 1e-9 && j.ActiveChunks.Count < j.Type.MaxChunks)
                    {
                        var free = v.Servers.FirstOrDefault(s => s.Chunk is null);
                        if (free is null) return;
                        a.Start(jid, free.Id, Math.Min(j.UnassignedUnits, 2), 1);
                    }
                }
            },
        };
        var houses = new[]
        {
            Kit.House("h0", 3, 1.0, hours, cap: 5, tank0: 1, spaceShare: 0.8),
            Kit.House("h1", 3, 2.0, hours, cap: 5, tank0: 0, spaceShare: 0.9),
        };
        var res = Kit.Run(Kit.Inputs(hours * H, houses, [t1, t2], jobs, sch, ts: TimeseriesMode.Hourly));

        for (int i = 0; i < 2; i++)
        {
            var a = res.Houses[i].Account;
            Assert.Equal(a.ProducedKwh, a.DirectKwh + a.ChargeKwh + a.DumpedKwh, 9);
            Assert.Equal(a.DemandKwh, a.DirectKwh + a.DischargeKwh + a.BackupKwh, 9);
            Assert.Equal(res.Houses[i].TankFinalKwh - houses[i].Spec.TankInitialKwh, a.ChargeKwh - a.DischargeKwh, 9);
            Assert.Equal(a.DemandKwh, a.SpaceHeatKwh + a.HotWaterKwh, 9);
            Assert.Equal(0.8 * a.ElectricKwh, a.ProducedKwh, 9);
            // the hourly timeseries sums to the run account
            Assert.Equal(a.ElectricKwh, res.HouseTimeseries.Where(r => r.HouseIndex == i).Sum(r => r.Account.ElectricKwh), 9);
            Assert.Equal(hours, res.HouseTimeseries.Count(r => r.HouseIndex == i));
        }
        Assert.All(res.Jobs, j => Assert.Equal("done", j.Status));
        foreach (var j in res.Jobs)
        {
            Assert.Equal(j.Request.WorkUnits, j.UnitsDone, 9);
            Assert.False(j.DeadlineMissed);
        }
        Assert.Equal(res.Jobs.Sum(j => j.EnergyKwh), res.Houses.Sum(h => h.Account.ElectricKwh), 9);
        Assert.Equal(res.Jobs.Sum(j => j.IncomeChf), res.Houses.Sum(h => h.IncomeChf), 9);
        Assert.Equal(res.Jobs.Where(j => j.Request.TypeIndex == 0).Sum(j => j.UnitsDone * 0.5),
            res.Houses.Sum(h => h.Account.IncomeT1Chf), 9);
        Assert.Equal(res.Jobs.Where(j => j.Request.TypeIndex == 1).Sum(j => j.UnitsDone * 0.4),
            res.Houses.Sum(h => h.Account.IncomeT2Chf), 9);
        // the network hours sum to the houses
        Assert.Equal(res.Houses.Sum(h => h.Account.ElectricKwh), res.NetworkHours.Sum(n => n.Account.ElectricKwh), 9);
        Assert.Equal(hours, res.NetworkHours.Count);
        Assert.Equal(10, res.NetworkHours.Sum(n => n.Completions));
        Assert.Equal(10, res.NetworkHours.Sum(n => n.Arrivals));
        Assert.True(res.Houses.Sum(h => h.Account.ElectricOverheadKwh) > 0);
    }

    // ------------------------------------------------------------ 13.6 action validation

    private static IEnumerable<(JobType, JobRequest[])> ValidationWorld(out JobType[] types, out JobRequest[] jobs)
    {
        types =
        [
            Kit.Type("split", 0, JobClass.Type1, maxChunks: 2, minChunk: 2, minLoad: 0.5, allowPause: false),
            Kit.Type("pausable", 1, JobClass.Type2, checkpointable: true, checkpointOverheadS: 600),
            Kit.Type("origin", 2, JobClass.Type2, placement: "origin_site"),
            Kit.Type("allow", 3, JobClass.Type2, placement: "country_allowlist", allowed: ["DE"]),
        ];
        jobs =
        [
            Kit.Job("j0", types[0], 0, 100 * H, 10),
            Kit.Job("j1", types[1], 0, 100 * H, 5),
            Kit.Job("j2", types[2], 0, 100 * H, 5, "client_site", "ZH"),
            Kit.Job("j3", types[3], 0, 100 * H, 5),
            Kit.Job("j4", types[0], 0, 100 * H, 5),
        ];
        return [];
    }

    private static readonly ServerId S0 = new(0), S1 = new(1), S2 = new(2);
    private static readonly JobId J0 = new(0), J1 = new(1), J2 = new(2), J3 = new(3), J4 = new(4);

    private static void RunCase(Action<IFleetView, IFleetActions> action)
    {
        ValidationWorld(out var types, out var jobs);
        var sch = new Script { On = (v, ev, a) => { if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) action(v, a); } };
        var houses = new[] { Kit.House("h0", 2, 0, 4, canton: "BS"), Kit.House("h1", 2, 0, 4) };
        Kit.Run(Kit.Inputs(4 * H, houses, types, jobs, sch, new TestPricing(r => r.JobKey != "j4")));
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        var cases = new Dictionary<string, Action<IFleetView, IFleetActions>>
        {
            ["start_unknown_job"] = (v, a) => a.Start(new JobId(99), S0, 5, 1),
            ["start_unknown_server"] = (v, a) => a.Start(J1, new ServerId(99), 5, 1),
            ["start_busy_server"] = (v, a) => { a.Start(J1, S0, 2, 1); a.Start(J0, S0, 5, 1); },
            ["start_rejected_job"] = (v, a) => a.Start(J4, S0, 5, 1),
            ["start_zero_units"] = (v, a) => a.Start(J1, S0, 0, 1),
            ["start_nan_units"] = (v, a) => a.Start(J1, S0, double.NaN, 1),
            ["start_more_than_unassigned"] = (v, a) => a.Start(J1, S0, 6, 1),
            ["start_below_min_chunk"] = (v, a) => a.Start(J0, S0, 1, 1),
            ["start_over_max_chunks"] = (v, a) => { a.Start(J0, S0, 2, 1); a.Start(J0, S1, 2, 1); a.Start(J0, S2, 2, 1); },
            ["start_load_zero"] = (v, a) => a.Start(J1, S0, 5, 0),
            ["start_load_above_one"] = (v, a) => a.Start(J1, S0, 5, 1.01),
            ["start_load_nan"] = (v, a) => a.Start(J1, S0, 5, double.NaN),
            ["start_below_min_load"] = (v, a) => a.Start(J0, S0, 5, 0.4),
            ["start_origin_site_mismatch"] = (v, a) => a.Start(J2, S0, 5, 1),
            ["start_country_not_allowed"] = (v, a) => a.Start(J3, S0, 5, 1),
            ["setload_free_server"] = (v, a) => a.SetLoad(S0, 0.5),
            ["setload_unknown_server"] = (v, a) => a.SetLoad(new ServerId(99), 0.5),
            ["setload_while_checkpointing"] = (v, a) => { var c = a.Start(J1, S0, 5, 1); a.Stop(c); a.SetLoad(S0, 0.5); },
            ["setload_nan"] = (v, a) => { a.Start(J1, S0, 5, 1); a.SetLoad(S0, double.NaN); },
            ["setload_above_one"] = (v, a) => { a.Start(J1, S0, 5, 1); a.SetLoad(S0, 1.5); },
            ["setload_negative"] = (v, a) => { a.Start(J1, S0, 5, 1); a.SetLoad(S0, -0.1); },
            ["setload_pause_not_allowed"] = (v, a) => { a.Start(J0, S0, 5, 1); a.SetLoad(S0, 0); },
            ["setload_below_min_load"] = (v, a) => { a.Start(J0, S0, 5, 1); a.SetLoad(S0, 0.4); },
            ["stop_unknown_chunk"] = (v, a) => a.Stop(new ChunkId(999)),
            ["stop_twice"] = (v, a) => { var c = a.Start(J1, S0, 5, 1); a.Stop(c); a.Stop(c); },
            ["wakeup_in_the_past"] = (v, a) => a.RequestWakeup(v.NowMs - 1),
            ["threshold_negative"] = (v, a) => a.RegisterTankThreshold(new HouseId(0), -1),
            ["threshold_above_capacity"] = (v, a) => a.RegisterTankThreshold(new HouseId(0), 35.1),
            ["threshold_unknown_house"] = (v, a) => a.RegisterTankThreshold(new HouseId(7), 1),
        };
        foreach (var kv in cases) yield return [kv.Key, kv.Value];
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void InvalidActionThrows(string name, Action<IFleetView, IFleetActions> action)
    {
        var ex = Assert.Throws<InvalidFleetActionException>(() => RunCase(action));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message), name);
    }

    [Fact]
    public void ValidActionsInTheValidationWorldDoNotThrow()
    {
        RunCase((v, a) =>
        {
            a.Start(J0, S0, 5, 0.5);               // min_load boundary
            a.Start(J0, S1, 5, 1);                 // whole remainder, second chunk
            a.Start(J1, S2, 5, 1);
            a.SetLoad(S2, 0);                      // pausable
            a.SetLoad(S2, 0.2);
            a.RegisterTankThreshold(new HouseId(0), 10);
            a.RequestWakeup(v.NowMs);
        });
    }

    [Fact]
    public void ActionsOutsideSchedulerCallsThrow()
    {
        ValidationWorld(out var types, out var jobs);
        var sch = new Script();
        Kit.Run(Kit.Inputs(2 * H, [Kit.House("h0", 2, 0, 2)], types, jobs, sch));
        Assert.Throws<InvalidFleetActionException>(() => sch.Actions!.RequestWakeup(1));
        Assert.Throws<InvalidFleetActionException>(() => sch.Actions!.Start(J1, S0, 1, 1));
    }

    [Fact]
    public void OriginSitePlacementAllowsMatchingCanton()
    {
        var types = new[] { Kit.Type("origin", 0, JobClass.Type2, placement: "origin_site") };
        var jobs = new[] { Kit.Job("j", types[0], 0, 100 * H, 1, "client_site", "BS"), Kit.Job("k", types[0], 0, 100 * H, 1) };
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                if (!ev.Any(e => e.Kind == FleetEventKind.JobArrived)) return;
                a.Start(new JobId(0), new ServerId(0), 1, 1);   // client_site in BS, house in BS
                a.Start(new JobId(1), new ServerId(1), 1, 1);   // anywhere origin: unrestricted, house without canton
            },
        };
        var res = Kit.Run(Kit.Inputs(3 * H, [Kit.House("h0", 1, 0, 3, canton: "BS"), Kit.House("h1", 1, 0, 3)], types, jobs, sch));
        Assert.All(res.Jobs, j => Assert.Equal("done", j.Status));
    }

    // ------------------------------------------------------------ lifecycle scenarios

    [Fact]
    public void PauseAndResumeWithOverhead()
    {
        var t = Kit.Type("t", 0, JobClass.Type2, allowPause: true, resumeOverheadS: 600);
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(J0, S0, 3, 1);
                if (ev.Any(e => e.Kind == FleetEventKind.HourStart) && v.HourIndex == 1) a.SetLoad(S0, 0);
                if (ev.Any(e => e.Kind == FleetEventKind.HourStart) && v.HourIndex == 2) a.SetLoad(S0, 1);
            },
        };
        var res = Kit.Run(Kit.Inputs(6 * H, [Kit.House("h", 1, 0, 6)], [t], [Kit.Job("j", t, 0, 100 * H, 3)], sch));
        var j = res.Jobs[0];
        Assert.Equal("done", j.Status);
        Assert.Equal(4 * H + 600_000, j.FinishMs);           // 1 h + pause 1 h + 10 min overhead + 2 h
        Assert.Equal(1, j.Pauses);
        Assert.Equal(1, res.Houses[0].Account.Pauses);
        Assert.Equal(3 + 600.0 / 3600, j.EnergyKwh, 9);
        Assert.Equal(600.0 / 3600, res.Houses[0].Account.ElectricOverheadKwh, 9);
        Assert.Equal(3, res.Houses[0].Account.ElectricT2Kwh, 9);
        Assert.Equal(3 * 0.4, res.Houses[0].Account.IncomeT2Chf, 9);
        Assert.Single(sch.All(FleetEventKind.OverheadDone));
        Assert.Single(sch.All(FleetEventKind.ChunkFinished));
        Assert.Single(sch.All(FleetEventKind.JobCompleted));
    }

    [Fact]
    public void CheckpointedStopAndMoveToAnotherHouse()
    {
        var t = Kit.Type("t", 0, JobClass.Type1, checkpointable: true, checkpointOverheadS: 300, chfPerUnit: 0.5);
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                foreach (var e in ev)
                {
                    if (e.Kind == FleetEventKind.JobArrived) a.Start(J0, S0, 4, 1);
                    if (e.Kind == FleetEventKind.HourStart && v.HourIndex == 1) a.Stop(v.Servers[0].Chunk!.Value);
                    if (e.Kind == FleetEventKind.OverheadDone)
                    {
                        Assert.Null(v.Servers[0].Chunk);
                        Assert.Equal(3, v.Job(J0).UnassignedUnits, 9);
                        a.Start(J0, S1, v.Job(J0).UnassignedUnits, 1);
                    }
                }
            },
        };
        var res = Kit.Run(Kit.Inputs(8 * H, [Kit.House("a", 1, 0, 8), Kit.House("b", 1, 0, 8)], [t],
            [Kit.Job("j", t, 0, 100 * H, 4)], sch));
        var j = res.Jobs[0];
        Assert.Equal("done", j.Status);
        Assert.Equal(H + 300_000 + 3 * H, j.FinishMs);
        Assert.Equal(1, j.Stops);
        Assert.Equal(1, j.Moves);
        Assert.Equal(2, j.HousesUsed);
        Assert.Equal(2, j.Chunks);
        Assert.Equal(4, j.UnitsDone, 9);
        Assert.Equal(4 * 0.5, j.IncomeChf, 9);
        Assert.Equal(4 + 300.0 / 3600, j.EnergyKwh, 9);
        Assert.Equal(1 + 3, res.Houses[0].Account.ElectricT1Kwh + res.Houses[1].Account.ElectricT1Kwh, 9);
        Assert.Equal(300.0 / 3600, res.Houses[0].Account.ElectricOverheadKwh, 9);
        Assert.Equal(0.5 * 1, res.Houses[0].Account.IncomeT1Chf, 9);   // the kept unit is credited at house a
    }

    [Fact]
    public void NonCheckpointableStopLosesProgress()
    {
        var t = Kit.Type("t", 0, JobClass.Type1, checkpointable: false, allowPause: false, chfPerUnit: 0.5);
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                foreach (var e in ev)
                {
                    if (e.Kind == FleetEventKind.JobArrived) a.Start(J0, S0, 4, 1);
                    if (e.Kind == FleetEventKind.HourStart && v.HourIndex == 2)
                    {
                        a.Stop(v.Servers[0].Chunk!.Value);
                        Assert.Null(v.Servers[0].Chunk);                    // freed at once
                        Assert.Equal(4, v.Job(J0).UnassignedUnits, 9);      // progress lost
                        Assert.Equal(4, v.Job(J0).RemainingUnits, 9);
                        a.Start(J0, S0, 4, 1);
                    }
                }
            },
        };
        var res = Kit.Run(Kit.Inputs(8 * H, [Kit.House("a", 1, 0, 8)], [t], [Kit.Job("j", t, 0, 100 * H, 4)], sch));
        var j = res.Jobs[0];
        Assert.Equal(6 * H, j.FinishMs);
        Assert.Equal(1, j.Stops);
        Assert.Equal(0, j.Moves);
        Assert.Equal(4, j.UnitsDone, 9);
        Assert.Equal(4 * 0.5, j.IncomeChf, 9);        // only kept units earn income
        Assert.Equal(6, j.EnergyKwh, 9);               // the lost 2 kWh stay counted as work energy
        Assert.Equal(6, res.Houses[0].Account.ElectricT1Kwh, 9);
    }

    [Fact]
    public void LateWorkIsFlaggedButStillPaid()
    {
        var t = Kit.Type("t", 0, JobClass.Type1, chfPerUnit: 0.5);
        var sch = new Script
        {
            On = (v, ev, a) => { if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(J0, S0, 3, 1); },
        };
        var res = Kit.Run(Kit.Inputs(6 * H, [Kit.House("a", 1, 0, 6)], [t], [Kit.Job("j", t, 0, 2 * H, 3)], sch));
        var j = res.Jobs[0];
        Assert.True(j.DeadlineMissed);
        Assert.Equal(3600, j.LatenessS, 9);
        Assert.Equal(1, j.UnitsLate, 9);
        Assert.Equal(1.5, j.IncomeChf, 9);
        Assert.Equal(1, res.Houses[0].Account.UnitsLate, 9);
        Assert.Equal(1, res.NetworkHours[2].Misses);
        Assert.Single(sch.All(FleetEventKind.DeadlinePassed));
    }

    [Fact]
    public void OpenJobAtEndAndRejectedJobAreReported()
    {
        var t = Kit.Type("t", 0, JobClass.Type2);
        var jobs = new[] { Kit.Job("a", t, 0, 2 * H, 10), Kit.Job("b", t, 0, 100 * H, 1) };
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                Assert.DoesNotContain(ev, e => e.Kind == FleetEventKind.JobArrived && e.Job == J1);
                if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(J0, S0, 10, 1);
            },
        };
        var res = Kit.Run(Kit.Inputs(4 * H, [Kit.House("a", 1, 0, 4)], [t], jobs, sch, new TestPricing(r => r.JobKey == "a")));
        Assert.Equal("open", res.Jobs[0].Status);
        Assert.True(res.Jobs[0].DeadlineMissed);
        Assert.Equal(2 * 3600, res.Jobs[0].LatenessS, 9);
        Assert.Equal(4, res.Jobs[0].UnitsDone, 9);          // in-flight progress credited at run end
        Assert.Equal(4 * 0.4, res.Jobs[0].IncomeChf, 9);
        Assert.Equal("rejected_price", res.Jobs[1].Status);
        Assert.False(res.Jobs[1].Accepted);
        Assert.Equal(1, res.NetworkHours[0].Rejections);
    }

    [Fact]
    public void CommittedKwhHasHourGranularity()
    {
        var t = Kit.Type("t", 0, JobClass.Type2);
        var jobs = new[] { Kit.Job("a", t, 0, 5 * H, 3), Kit.Job("b", t, 0, 10 * H, 2) };
        double[] seen = [];
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                if (seen.Length == 0)
                    seen = [v.CommittedKwh(5 * H - 1), v.CommittedKwh(5 * H), v.CommittedKwh(5 * H + 1_800_000), v.CommittedKwh(10 * H)];
            },
        };
        Kit.Run(Kit.Inputs(2 * H, [Kit.House("a", 1, 0, 2)], [t], jobs, sch));
        Assert.Equal([0.0, 3.0, 3.0, 5.0], seen);
    }

    [Fact]
    public void RunTwiceThrows()
    {
        var t = Kit.Type("t", 0, JobClass.Type2);
        var e = new FleetEngine(Kit.Inputs(H, [Kit.House("a", 1, 0, 1)], [t], [], new Script()));
        e.Run();
        Assert.Throws<InvalidOperationException>(() => e.Run());
    }

    // ------------------------------------------------------------ tick mode

    [Fact]
    public void TickModeBuffersEventsUntilTheNextTick()
    {
        const long tick = 1_800_000;
        var t = Kit.Type("t", 0, JobClass.Type1);
        var jobs = new[] { Kit.Job("j", t, 600_000, 100 * H, 0.5) };
        var sch = new Script
        {
            On = (v, ev, a) =>
            {
                if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(J0, S0, 0.5, 1);
            },
        };
        var res = Kit.Run(Kit.Inputs(3 * H, [Kit.House("a", 1, 0, 3)], [t], jobs, sch, mode: RunMode.Tick, tickMs: tick));
        Assert.All(sch.Calls, c => Assert.Equal(0, c.Now % tick));
        Assert.Equal(7, sch.Calls.Count);                         // ticks at 0, 0.5 h .. 3 h
        var arrivalCall = sch.Calls.Single(c => c.Events.Any(e => e.Kind == FleetEventKind.JobArrived));
        Assert.Equal(tick, arrivalCall.Now);                      // delivered at the next tick, not at 10 min
        Assert.Equal(600_000, arrivalCall.Events.Single(e => e.Kind == FleetEventKind.JobArrived).TimeMs);
        // the chunk starts at the tick (30 min), runs 30 min and finishes exactly at 1 h
        var finished = sch.Calls.Single(c => c.Events.Any(e => e.Kind == FleetEventKind.ChunkFinished));
        Assert.Equal(2 * tick, finished.Now);
        Assert.Equal(2 * tick, finished.Events.Single(e => e.Kind == FleetEventKind.ChunkFinished).TimeMs);
        Assert.Equal(2 * tick, res.Jobs[0].FinishMs);
        Assert.Equal(2 * tick, res.Jobs[0].FirstStartMs + 1_800_000);
    }

    [Fact]
    public void TickModeKeepsExactEnergyBetweenTicks()
    {
        // a chunk that finishes between two ticks: engine state changes at the exact time, so the
        // energy is exactly 0.25 kWh and the finish is not rounded to the tick
        const long tick = 1_800_000;
        var t = Kit.Type("t", 0, JobClass.Type1);
        var sch = new Script
        {
            On = (v, ev, a) => { if (ev.Any(e => e.Kind == FleetEventKind.JobArrived)) a.Start(J0, S0, 0.25, 1); },
        };
        var res = Kit.Run(Kit.Inputs(3 * H, [Kit.House("a", 1, 0, 3)], [t], [Kit.Job("j", t, 0, 100 * H, 0.25)], sch,
            mode: RunMode.Tick, tickMs: tick));
        Assert.Equal(900_000, res.Jobs[0].FinishMs);
        Assert.Equal(0.25, res.Houses[0].Account.ElectricKwh, 9);
        var finished = sch.Calls.Single(c => c.Events.Any(e => e.Kind == FleetEventKind.ChunkFinished));
        Assert.Equal(tick, finished.Now);
    }
}
