using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.Network.Lp;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class NetworkTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static (ReferenceParameters P, IReadOnlyList<DemandHour> Demand, NetworkSite Site) Reference()
    {
        var p = ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));
        var demand = HeatDemand.Build(WeatherCsv.Read(Path.Combine(Root(), "data", "raw", "open-meteo",
            "basel-2025-hourly.csv")), p);
        return (p, demand, NetworkSite.FromReference("ref", "Reference house", 47.56, 7.59, "CH", demand, p));
    }

    private static NetworkSite TinySite(double[] demandKwh, double tankKwh, double servers = 1) =>
        new("tiny", "Tiny", 0, 0, "CH", servers, 1, 1, tankKwh, 0, 1, 0.3, 0.1,
            demandKwh.Select((d, i) => new DemandHour($"2025-01-01T{i:00}:00", 0, true, d, 0)).ToArray());

    [Fact]
    public void LocalGreedyOnOneSiteReproducesYearSimulationExactly()
    {
        var (p, demand, site) = Reference();
        var year = YearSimulation.Run(demand, p, new GreedyHeatScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor());
        var network = NetworkSimulation.Run([site], new LocalGreedyScheduler(), new PerfectForesightForecasts(), 1);
        Assert.Equal(year.Hours, network.Sites[0].Hours);
        Assert.Equal(year.Summary.ServerHours, network.Sites[0].Summary.ServerHours);
        Assert.Equal(year.Summary.Coverage, network.Sites[0].Summary.Coverage);
    }

    [Fact]
    public void NetworkB2OnOneSiteReproducesUnlimitedReferenceYearHourByHour()
    {
        var (p, demand, site) = Reference();
        var year = YearSimulation.Run(demand, p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor());
        var work = new WorkLedger([site], [null], [1.0], demand.Count, (int)p.HoursPerDay.Value);
        var network = NetworkSimulation.Run([site], new NetworkGreedyJobsScheduler(),
            new PerfectForesightForecasts(), 1, work: work);
        Assert.Equal(year.Hours, network.Sites[0].Hours);
        Assert.Equal(year.Summary.ServerHours, network.Sites[0].Summary.ServerHours);
        Assert.Equal(year.Summary.Coverage, network.Sites[0].Summary.Coverage);
        Assert.Equal(year.Summary.DumpedHeatKwh, network.Sites[0].Summary.DumpedHeatKwh);
        Assert.Equal(year.Summary.BreakEvenChfPerServerHour,
            network.Sites[0].Summary.CaseBreakEvenChfPerServerHour);

        var routedYear = YearSimulation.Run(demand, p,
            new SingleSiteUnlimitedNetworkScheduler(site, new NetworkGreedyJobsScheduler()),
            new SimulatedExecutor());
        Assert.Equal(year.Hours, routedYear.Hours);
        Assert.Equal(year.Summary, routedYear.Summary);
    }

    [Fact]
    public void FullYearUpperBoundEqualsLocalGreedyOnReferenceHouse()
    {
        // Decision 0024: with minimum load 0, a lossless tank and unlimited work, greedy is optimal.
        var (_, _, site) = Reference();
        var greedy = NetworkSimulation.Run([site], new LocalGreedyScheduler(), new PerfectForesightForecasts(), 1)
            .Sites[0].Summary;
        var ub = FullYearUpperBound.Solve([site])[0];
        Assert.Equal(greedy.ServerHeatUsedKwh, ub.UsedHeatKwh, 1e-3);
        Assert.Equal(greedy.ServerHours, ub.ServerHours, 1e-3);
        Assert.Equal(0, ub.DumpedHeatKwh, 1e-6);
    }

    [Fact]
    public void CommittedDecisionRejectsADifferentSignal()
    {
        var decided = new HeatSignal(5, 0, 35, 4, 0.8);
        var scheduler = new CommittedDecisionScheduler(decided, new SchedulerDecision(0.5));
        Assert.Equal(0.5, scheduler.Decide(decided).TargetLoadPerServer);
        Assert.Throws<InvalidOperationException>(() => scheduler.Decide(decided with { TankStoredKwh = 1 }));
    }

    [Fact]
    public void HeatBalanceServesDemandThenTankThenDumps()
    {
        var step = HeatBalance.Step(producedKwh: 10, demandKwh: 4, tankStoredKwh: 30, tankCapacityKwh: 35);
        Assert.Equal(new HeatBalanceStep(4, 5, 0, 1, 0, 35), step);
        var cold = HeatBalance.Step(producedKwh: 2, demandKwh: 10, tankStoredKwh: 3, tankCapacityKwh: 35);
        Assert.Equal(new HeatBalanceStep(2, 0, 3, 0, 5, 0), cold);
    }

    [Fact]
    public void LpPreChargesTheTankBeforeDemand()
    {
        // One 1 kWh server, tank 1 kWh: hour 0 has no demand, hour 1 needs 2 kWh.
        // Optimum: run in hour 0 to fill the tank, then serve 1 kWh direct + 1 kWh from the tank.
        var plan = HeatLedLp.Solve([new LpSiteWindow(TinySite([0, 2], 1), [0, 2], 0)])[0];
        Assert.Equal(2, plan.UsedHeatKwh, 1e-7);
        Assert.Equal(1, plan.LoadServers[0], 1e-7);
        Assert.Equal(0, plan.DumpedHeatKwh, 1e-9);
    }

    [Fact]
    public void LpNeverDumpsWhenItCanStayIdle()
    {
        var plan = HeatLedLp.Solve([new LpSiteWindow(TinySite([0, 0, 0], 1), [0, 0, 0], 1)])[0];
        Assert.Equal(0, plan.DumpedHeatKwh, 1e-9);
        Assert.All(plan.LoadServers, u => Assert.Equal(0, u, 1e-9));
    }

    [Fact]
    public void Ar1ForecastIsDeterministicPerSeedAndVariesByHour()
    {
        var (_, demand, site) = Reference();
        var f = new Ar1ErrorForecasts(0.1, 0.8, 42);
        var a = f.Build(0, site, demand, 100, 24).Hours.Select(h => h.NeededHeatKwh).ToArray();
        var b = f.Build(0, site, demand, 100, 24).Hours.Select(h => h.NeededHeatKwh).ToArray();
        var c = new Ar1ErrorForecasts(0.1, 0.8, 43).Build(0, site, demand, 100, 24).Hours
            .Select(h => h.NeededHeatKwh).ToArray();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(HeatForecastKind.SyntheticError, f.Build(0, site, demand, 100, 24).Kind);
    }
}
