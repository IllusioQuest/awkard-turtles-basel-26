using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class ServerSizingTests
{
    private static readonly Lazy<IReadOnlyList<SizingRow>> ReferenceSweep = new(() =>
        ServerSizing.Sweep(CoreTestData.Demand(), CoreTestData.Parameters()));

    [Fact]
    public void SweepReproducesExportedReferenceComparisons()
    {
        var rows = ReferenceSweep.Value;
        // data/derived/reference-house/*.json (T-06 exports of the same year model)
        Assert.Equal(7631.25, rows[1].ServerHours, 1);
        Assert.Equal(17778.85, rows[3].ServerHours, 1);
        Assert.Equal(22661.03, rows[4].ServerHours, 1);
        Assert.Equal(27275.12, rows[5].ServerHours, 1);
        Assert.Equal(0.5805, rows[4].Coverage, 4);
        Assert.All(rows, r => Assert.Equal(0, r.DumpedHeatKwh, 6));
    }

    [Fact]
    public void SweepStopsWhenCaseColdestHourCountCoversAllDemand()
    {
        var rows = ReferenceSweep.Value;
        // Case: 12.5 servers in the coldest hour, so 13 cover everything (tank bridges hot water).
        Assert.Equal(13, rows[^1].ServerCount);
        Assert.Equal(1, rows[^1].Coverage, 9);
        Assert.True(rows[12].Coverage < 1);
        Assert.True(ServerSizing.HasDiminishingReturns(rows));
    }

    [Fact]
    public void ZeroFixedCostPrefersFullCoverAndHighFixedCostPrefersNone()
    {
        var p = CoreTestData.Parameters();
        var rows = ReferenceSweep.Value;
        double elec = p.ElectricityPriceChfPerKwh.Value;
        Assert.Equal(13, ServerSizing.OptimalCount(ServerSizing.Value(rows, new(0.66, 0, 0, elec))));
        Assert.Equal(0, ServerSizing.OptimalCount(ServerSizing.Value(rows, new(0.66, 1e6, 0, elec))));
        // Price below the case break-even (0.2261 CHF/h): even free hardware loses money.
        Assert.Equal(0, ServerSizing.OptimalCount(ServerSizing.Value(rows, new(0.2, 0, 0, elec))));
    }

    [Fact]
    public void OptimumKeepsExactlyTheServersWhoseMarginalValueCoversFixedCost()
    {
        var p = CoreTestData.Parameters();
        var rows = ReferenceSweep.Value;
        var e = new SizingEconomics(2.23, 6415.34, 0, p.ElectricityPriceChfPerKwh.Value);
        var values = ServerSizing.Value(rows, e);
        int n = ServerSizing.OptimalCount(values);
        Assert.InRange(n, 1, 12);
        Assert.True(values[n].MarginalVariableContributionChf >= e.FixedChfPerServerYear);
        Assert.True(values[n + 1].MarginalVariableContributionChf < e.FixedChfPerServerYear);
        // Screening-curve check: the n-th server sells at least the break-even hours, the next fewer.
        double h = ServerSizing.BreakEvenServerHoursPerYear(e, p);
        Assert.True(rows[n].MarginalServerHours >= h);
        Assert.True(rows[n + 1].MarginalServerHours < h);
    }

    [Fact]
    public void IdleDrawLowersContributionOfLightlyUsedServers()
    {
        var p = CoreTestData.Parameters();
        var rows = ReferenceSweep.Value;
        var noIdle = ServerSizing.Value(rows, new(0.66, 0, 0, p.ElectricityPriceChfPerKwh.Value));
        var idle = ServerSizing.Value(rows, new(0.66, 0, 0.205, p.ElectricityPriceChfPerKwh.Value));
        double idleHours = 13 * rows[13].Hours - rows[13].ServerHours;
        Assert.Equal(idleHours * 0.205 * p.ElectricityPriceChfPerKwh.Value,
            noIdle[13].ContributionChf - idle[13].ContributionChf, 6);
    }

    [Fact]
    public void BreakEvenHoursIncludeIdleDrawAndMatchTheOptimum()
    {
        var p = CoreTestData.Parameters();
        var rows = ReferenceSweep.Value;
        double elec = p.ElectricityPriceChfPerKwh.Value;
        var noIdle = new SizingEconomics(2.23, 6415.34, 0, elec);
        Assert.Equal(6415.34 / (2.23 - 0.3325 + 0.8 * 0.3325 / 2.5), ServerSizing.BreakEvenServerHoursPerYear(noIdle, p), 6);
        // Idle draw raises the hours needed; T4-like profile at CHF 2.04/h.
        var idle = new SizingEconomics(2.04, 3909, 0.464, elec);
        double h = ServerSizing.BreakEvenServerHoursPerYear(idle, p);
        Assert.True(h > ServerSizing.BreakEvenServerHoursPerYear(idle with { IdleElectricKw = 0 }, p));
        int n = ServerSizing.OptimalCount(ServerSizing.Value(rows, idle));
        Assert.True(rows[n].MarginalServerHours >= h && rows[n + 1].MarginalServerHours < h);
    }
}

internal static class CoreTestData
{
    public static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    public static ReferenceParameters Parameters() =>
        ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));

    public static IReadOnlyList<DemandHour> Demand() => HeatDemand.Build(
        WeatherCsv.Read(Path.Combine(Root(), "data", "raw", "open-meteo", "basel-2025-hourly.csv")), Parameters());
}
