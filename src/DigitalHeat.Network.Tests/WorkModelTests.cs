using DigitalHeat.Core;
using DigitalHeat.Network;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class WorkModelTests
{
    private const string TestSource = "Unit test value (decision 0025), not a sourced figure.";
    private static readonly JobPlacementPolicy Anywhere = new("anywhere", TestSource, null);

    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static NetworkSite Site(string id, double[] demandKwh, double servers = 1, double tankKwh = 0,
        string country = "CH") =>
        new(id, id, 0, 0, country, servers, 1, 1, tankKwh, 0, 1, 0.3, 0.1,
            demandKwh.Select((d, i) => new DemandHour($"2025-01-01T{i:00}:00", 0, true, d, 0)).ToArray());

    [Fact]
    public void LocalGreedyJobsMatchesJobQueueExecutorOnOneSite()
    {
        // Reference house, 40 jobs/day of 0.5 server-hours (20 server-hours/day), 24 h deadline,
        // no filler: summer heat-led capacity (≈ 15 server-hours/day for hot water) misses deadlines.
        var p = ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));
        var demand = HeatDemand.Build(WeatherCsv.Read(Path.Combine(Root(), "data", "raw", "open-meteo",
            "basel-2025-hourly.csv")), p);
        Parameter P(double v, string unit) => new(v, unit, TestSource);
        var scenario = new JobScenario("finite", "wait", TestSource,
        [
            new JobTypeDemand("t1", "unit", true, P(40, "jobs/day"), P(1, "work units/job"),
                P(2, "work units/server-hour"), P(24, "hours"))
        ]);
        var core = YearSimulation.Run(demand, p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor(), scenario);

        var site = NetworkSite.FromReference("ref", "Reference house", 47.56, 7.59, "CH", demand, p);
        var ledger = new WorkLedger([site], [new Type1Spec(40, 0.5, 24, Anywhere)], [0.0], demand.Count, 24);
        var net = NetworkSimulation.Run([site], new LocalGreedyJobsScheduler(), new PerfectForesightForecasts(), 1,
            work: ledger);

        Assert.True(core.Summary.Jobs!.Missed > 0);
        Assert.Equal(core.Summary.Jobs.Missed, net.Work!.Type1.Missed);
        Assert.Equal(core.Summary.Jobs.DueWithinHorizon, net.Work.Type1.DueWithinHorizon);
        Assert.Equal(core.Summary.Jobs.Completed, net.Work.Type1.Completed);
        Assert.Equal(core.Hours.Select(h => h.ActualLoadPerServer), net.Sites[0].Hours.Select(h => h.ActualLoadPerServer));
    }

    [Fact]
    public void ShortfallHitsFillerFirstThenLatestDeadline()
    {
        var site = Site("a", [0, 0, 0], servers: 10);
        var ledger = new WorkLedger([site], [new Type1Spec(2, 1, 2, Anywhere)], [1.0], 3, 3);
        ledger.BeginHour(0);
        var c = ledger.View().Pending.Single();
        // Plan 2 server-hours Type 1 + 3 filler, but only 4 are achieved: filler loses 1, Type 1 keeps 2.
        var (t1, t2) = ledger.Apply(0, [new WorkAllocation(c.Id, 0, 2)], 3, 4);
        Assert.Equal(2, t1);
        Assert.Equal(2, t2);
        // Only 1 achieved: Type 1 gets it, filler nothing.
        var ledger2 = new WorkLedger([site], [new Type1Spec(2, 1, 2, Anywhere)], [1.0], 3, 3);
        ledger2.BeginHour(0);
        var c2 = ledger2.View().Pending.Single();
        Assert.Equal((1.0, 0.0), ledger2.Apply(0, [new WorkAllocation(c2.Id, 0, 2)], 3, 1));
    }

    [Fact]
    public void WorkIsDroppedAtTheDueHourAndCountsWholeJobs()
    {
        var site = Site("a", [0, 0, 0, 0], servers: 10);
        var ledger = new WorkLedger([site], [new Type1Spec(3, 1, 2, Anywhere)], [0.0], 4, 4);
        ledger.BeginHour(0);
        var c = ledger.View().Pending.Single();
        ledger.Apply(0, [new WorkAllocation(c.Id, 0, 1.5)], 0, 1.5); // 1.5 of 3 server-hours done
        ledger.BeginHour(1); ledger.Apply(0, [], 0, 0);
        ledger.BeginHour(2); ledger.Apply(0, [], 0, 0);            // due at hour 2: 1.5 left → 2 jobs missed
        ledger.BeginHour(3); ledger.Apply(0, [], 0, 0);
        var r = ledger.Finish();
        Assert.Equal(2, r.Type1.Missed);
        Assert.Equal(1, r.Type1.Completed);
        Assert.Equal(1.5, r.MissedServerHoursBySite[0][2], 9);
    }

    [Fact]
    public void NetworkGreedyMovesOverflowToTheEligibleSiteWithMostFreeCapacity()
    {
        // House "h" needs 1 kWh/h (1 server-hour of heat-led capacity), pool "p" 3 kWh/h, "x" 2 kWh/h.
        // House work: 2.5 server-hours due in 1 h → 1 at home, 1.5 at the pool (most free), none at x.
        var sites = new[] { Site("h", [1], servers: 5), Site("p", [3], servers: 5), Site("x", [2], servers: 5) };
        var ledger = new WorkLedger(sites, [new Type1Spec(5, 0.5, 1, Anywhere), null, null], [1.0, 1.0, 1.0], 1, 24);
        var r = NetworkSimulation.Run(sites, new NetworkGreedyJobsScheduler(), new PerfectForesightForecasts(), 1,
            work: ledger);
        Assert.Equal(0, r.Work!.Type1.Missed);
        var flow = Assert.Single(r.Work.Flows);
        Assert.Equal((0, 1, 1.5), (flow.FromSite, flow.ToSite, flow.ServerHours));
        Assert.Equal(1.0, r.Work.Type1ServerHoursBySite[0][0], 9);
        Assert.Equal(1.5, r.Work.Type2ServerHoursBySite[1][0], 9); // pool keeps 1.5 for filler
        Assert.All(r.Sites, s => Assert.Equal(0, s.Summary.DumpedHeatKwh, 9));
    }

    private static string Toy() => Path.Combine(Root(), "src", "DigitalHeat.Network.Tests", "Fixtures", "two-site-toy.json");

    [Fact]
    public void ScenarioLoaderReproducesTheCaseDemand()
    {
        var scenario = NetworkScenarioLoader.Load(Path.Combine(Root(), "config", "network", "reference-only.json"), Root());
        var p = ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));
        var demand = HeatDemand.Build(WeatherCsv.Read(Path.Combine(Root(), "data", "raw", "open-meteo",
            "basel-2025-hourly.csv")), p);
        var site = Assert.Single(scenario.NetworkSites);
        Assert.Equal(demand, site.Demand);
        Assert.Equal(0.8, site.RecoverableKwhPerServerHour, 12);
        var spec = Assert.Single(NetworkScenarioLoader.Type1Specs(scenario))!;
        // 0.3 × 22,661 heat-led server-hours ÷ 365 days ÷ 0.000917 server-hours per job.
        Assert.InRange(spec.JobsPerDay, (int)(0.3 * 22661.03 / 365 / 0.000917) - 1, (int)(0.3 * 22661.03 / 365 / 0.000917) + 1);
    }

    [Fact]
    public void NetworkGreedyKeepsWorkAtHomeWhenHomeCanFinishIt()
    {
        var scenario = NetworkScenarioLoader.Load(Toy(), Root());
        var specs = NetworkScenarioLoader.Type1Specs(scenario, 0.1);
        var sites = scenario.NetworkSites;
        var ledger = new WorkLedger(sites, specs, [1.0, 1.0], sites[0].Demand.Count, 24);
        var r = NetworkSimulation.Run(sites, new NetworkGreedyJobsScheduler(), new PerfectForesightForecasts(), 1, work: ledger);
        Assert.Equal(0, r.Work!.Type1.Missed);
        Assert.Empty(r.Work.Flows);
    }

    [Fact]
    public void ExportWritesTheMapContract()
    {
        var scenario = NetworkScenarioLoader.Load(Toy(), Root());
        var specs = NetworkScenarioLoader.Type1Specs(scenario);
        var sites = scenario.NetworkSites;
        var r = NetworkSimulation.Run(sites, new NetworkGreedyJobsScheduler(), new PerfectForesightForecasts(), 1,
            work: new WorkLedger(sites, specs, [1.0, 1.0], sites[0].Demand.Count, 24));
        string tmp = Directory.CreateTempSubdirectory("dh-export-").FullName;
        string dir = NetworkExport.Write(tmp, scenario, r, new NetworkRunInfo(1.0, null, specs, "test"));
        foreach (var f in new[] { "run.json", "sites.json", "site-hours.json", "flows.json", "summary.json" })
            Assert.True(File.Exists(Path.Combine(dir, f)), f);
        using var hours = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "site-hours.json")));
        Assert.Equal(8760, hours.RootElement.GetProperty("localTimes").GetArrayLength());
        Assert.Equal(8760, hours.RootElement.GetProperty("sites").GetProperty("allschwil-indoor-pool")
            .GetProperty("type1ServerHours").GetArrayLength());
        using var flows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "flows.json")));
        Assert.True(flows.RootElement.GetProperty("rows").GetArrayLength() > 0);
        Directory.Delete(tmp, true);
    }

    [Fact]
    public void WorkLpMovesOnlyWhatHomeCannotFinish()
    {
        // House "h" can run 1 server-hour per hour of heat-led work (1 kWh/h demand, no tank) for 2 hours;
        // its 3 server-hours due in 2 h need 1 server-hour moved to the pool "p" (3 kWh/h demand).
        var sites = new[] { Site("h", [1, 1, 1], servers: 5), Site("p", [3, 3, 3], servers: 5) };
        var ledger = new WorkLedger(sites, [new Type1Spec(6, 0.5, 2, Anywhere), null], [1.0, 1.0], 3, 24);
        var r = NetworkSimulation.Run(sites, new DigitalHeat.Network.Lp.RollingHorizonWorkLpScheduler(),
            new PerfectForesightForecasts(), 2, work: ledger);
        Assert.Equal(0, r.Work!.Type1.Missed);
        Assert.Equal(1.0, r.Work.Type1.MovedServerHours, 6);
        Assert.Equal(2.0, r.Work.Type1ServerHoursBySite[0].Sum(), 6);
        Assert.All(r.Sites, s => Assert.Equal(0, s.Summary.DumpedHeatKwh, 9));
        Assert.Equal(9.0, r.Sites[1].Summary.ServerHours, 6); // pool stays fully heat-led: 3 kWh/h × 3 h
    }

    [Fact]
    public void PlacementPolicyBlocksMovesAbroad()
    {
        var originSite = new JobPlacementPolicy("origin-site", TestSource, null);
        var sites = new[] { Site("h", [1], servers: 5), Site("p", [3], servers: 5) };
        var ledger = new WorkLedger(sites, [new Type1Spec(5, 0.5, 1, originSite), null], [1.0, 1.0], 1, 24);
        var r = NetworkSimulation.Run(sites, new NetworkGreedyJobsScheduler(), new PerfectForesightForecasts(), 1,
            work: ledger);
        Assert.Empty(r.Work!.Flows);
        Assert.Equal(3, r.Work.Type1.Missed); // 1 of 2.5 server-hours done → ⌈1.5 / 0.5⌉ = 3 jobs missed
    }

    // Like Site(), but with valid local timestamps beyond 24 hours (for multi-day tests).
    private static NetworkSite MultiDaySite(string id, double[] demandKwh, double servers, double tankKwh) =>
        new(id, id, 0, 0, "CH", servers, 1, 1, tankKwh, 0, 1, 0.3, 0.1,
            demandKwh.Select((d, i) => new DemandHour(new DateTime(2025, 7, 1).AddHours(i)
                .ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture), 0, false, d, 0)).ToArray());

    [Fact]
    public void ReserveKeepsTankRoomForTheNextType1Cohort()
    {
        // Summer-like site (decision 0029): drain 0.2 kWh/h = 4.8 server-hours/day, 8 jobs/day of 1 server-hour,
        // 24 h deadline, 10 kWh tank, unlimited filler. Reserve = min(10, 1 × (8 − 4.8)) = 3.2 kWh.
        // B1 fills the tank with filler after day 1's work and misses ⌈8 − 4.8⌉ = 4 jobs on days 2 and 3.
        // B1-reserve keeps 3.2 kWh free, so day 2 fits exactly (3.2 + 4.8 = 8); day 2's Type 1 work then fills
        // the tank, so day 3 misses 4 jobs too: when daily Type 1 exceeds the drain, local timing only delays
        // the shortfall by one cohort. Lasting relief needs other sites (B2).
        double[] demand = Enumerable.Repeat(0.2, 72).ToArray();
        NetworkResult Run(INetworkScheduler s)
        {
            var site = MultiDaySite("h", demand, servers: 5, tankKwh: 10);
            return NetworkSimulation.Run([site], s, new PerfectForesightForecasts(), 1,
                work: new WorkLedger([site], [new Type1Spec(8, 1, 24, Anywhere)], [1.0], 72, 24));
        }
        var b1 = Run(new LocalGreedyJobsScheduler());
        var reserve = Run(new LocalGreedyJobsScheduler(reserveForType1: true));
        Assert.Equal(8, b1.Work!.Type1.Missed);
        Assert.Equal(4, reserve.Work!.Type1.Missed);
        Assert.Equal(0, reserve.Work.MissedServerHoursBySite[0].Take(48).Sum(), 6); // day 2 fits
        Assert.Equal(b1.Sites[0].Summary.Coverage, reserve.Sites[0].Summary.Coverage, 9);
        Assert.Equal(0, reserve.Sites[0].Summary.DumpedHeatKwh, 9);
        Assert.Empty(reserve.Work.Flows);
    }

    [Fact]
    public void ReserveIsInactiveWhenTheDrainCoversType1()
    {
        // Winter-like site: drain 3 kWh/h = 72 server-hours/day far above 8 server-hours/day of Type 1, so the
        // reserve is 0 and B1-reserve must equal B1 hour by hour.
        double[] demand = Enumerable.Repeat(3.0, 72).ToArray();
        NetworkResult Run(INetworkScheduler s)
        {
            var site = MultiDaySite("h", demand, servers: 5, tankKwh: 10);
            return NetworkSimulation.Run([site], s, new PerfectForesightForecasts(), 1,
                work: new WorkLedger([site], [new Type1Spec(8, 1, 24, Anywhere)], [1.0], 72, 24));
        }
        var b1 = Run(new LocalGreedyJobsScheduler());
        var reserve = Run(new LocalGreedyJobsScheduler(reserveForType1: true));
        Assert.Equal(b1.Sites[0].Hours.Select(h => h.ActualLoadPerServer), reserve.Sites[0].Hours.Select(h => h.ActualLoadPerServer));
        Assert.Equal(b1.Work!.Type1.Missed, reserve.Work!.Type1.Missed);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 3, 0)]
    public void TransferDelayBlocksMovesUntilDataIsStaged(int delayHours, int expectedMissed, int expectedFlows)
    {
        // Same toy as above (decision 0030): 2.5 server-hours due in 1 h, 1 at home. With d = 0 the excess
        // 1.5 moves to the pool; with d = 1 it may not run elsewhere before the due hour, so
        // ⌈1.5 / 0.5⌉ = 3 jobs are missed. Checked for B2 and the LP.
        foreach (INetworkScheduler scheduler in new INetworkScheduler[]
                 { new NetworkGreedyJobsScheduler(transferDelayHours: delayHours),
                   new DigitalHeat.Network.Lp.RollingHorizonWorkLpScheduler(delayHours) })
        {
            var sites = new[] { Site("h", [1], servers: 5), Site("p", [3], servers: 5), Site("x", [2], servers: 5) };
            var ledger = new WorkLedger(sites, [new Type1Spec(5, 0.5, 1, Anywhere), null, null], [1.0, 1.0, 1.0], 1, 24);
            var r = NetworkSimulation.Run(sites, scheduler, new PerfectForesightForecasts(), 1, work: ledger);
            Assert.Equal(expectedMissed, r.Work!.Type1.Missed);
            Assert.Equal(expectedFlows, r.Work.Flows.Count);
            Assert.All(r.Sites, s => Assert.Equal(0, s.Summary.DumpedHeatKwh, 9));
        }
    }
}
