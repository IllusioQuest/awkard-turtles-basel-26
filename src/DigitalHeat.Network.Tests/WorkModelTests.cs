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
}
