using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class SiteHeatStateTests
{
    private static DemandHour[] Series(int count = 73) => Enumerable.Range(0, count)
        .Select(i => new DemandHour(new DateTime(2025, 3, 30).AddHours(i).ToString("yyyy-MM-dd'T'HH:mm"),
            0, true, i, 0.5)).ToArray();
    private static HeatSignal Signal() => new(0.5, 1, 10, 2, 0.8);

    [Theory]
    [InlineData(24)]
    [InlineData(72)]
    public void PerfectForesightCopiesNextHoursAndLabelsIncludingNominalDstDay(int horizon)
    {
        var source = Series();
        var forecast = HeatForecast.Build(source, "synthetic-test-series", 0, horizon);
        Assert.Equal(HeatForecastKind.PerfectForesight, forecast.Kind);
        Assert.Equal("synthetic-test-series", forecast.SourceSeriesId);
        Assert.Equal(0, forecast.SourceIndex);
        Assert.Equal(horizon, forecast.Hours.Count);
        Assert.Equal(source.Skip(1).Take(horizon).Select(x => x.TotalHeatKwh), forecast.Hours.Select(x => x.NeededHeatKwh));
        Assert.Equal("2025-03-30T02:00", forecast.Hours[1].LocalTime);
        source[1] = source[1] with { SpaceHeatKwh = 999 };
        Assert.Equal(1.5, forecast.Hours[0].NeededHeatKwh);
        Assert.Throws<NotSupportedException>(() => ((IList<ForecastHour>)forecast.Hours)[0] = new("bad", 0));
    }

    [Fact]
    public void SyntheticErrorsAreExplicitAndCopied()
    {
        double[] errors = [-1, 0.2];
        var forecast = HeatForecast.Build(Series(), "fixture", 0, 2, errors);
        errors[1] = 100;
        Assert.Equal(HeatForecastKind.SyntheticError, forecast.Kind);
        Assert.Equal(0, forecast.Hours[0].NeededHeatKwh);
        Assert.Equal(3, forecast.Hours[1].NeededHeatKwh, 10);
    }

    [Fact]
    public void SiteCapacityIsDerivedAndAdapterKeepsGreedyDecision()
    {
        var state = new SiteHeatState("house-a", Signal(), HeatForecast.Build(Series(), "fixture", 0, 24));
        Assert.Equal(0.5, state.NeededHeatKwh);
        Assert.Equal(1.6, state.MaxHeatProducedKwh);
        IScheduler greedy = new HeatFirstScheduler(0);
        Assert.Equal(greedy.Decide(state.Signal), new CurrentHeatSchedulerAdapter(greedy).Decide(state));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(73)]
    public void MissingOrInvalidHorizonIsRejected(int horizon) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatForecast.Build(Series(), "fixture", 0, horizon));

    [Fact]
    public void IndexAndErrorLengthMustMatchSource()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatForecast.Build(Series(), "fixture", -1, 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatForecast.Build(Series(), "fixture", 72, 1));
        Assert.Throws<ArgumentException>(() => HeatForecast.Build(Series(), "fixture", 0, 24, new double[1]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1.1)]
    [InlineData(double.MaxValue)]
    public void InvalidOrOverflowingErrorIsRejected(double error) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatForecast.Build(Series(), "fixture", 0, 1, [error]));

    [Fact]
    public void MissingHoursNegativeDemandAndOverflowingDemandAreRejected()
    {
        var source = Series();
        source[1] = source[1] with { LocalTime = "2025-03-30T03:00" };
        Assert.Throws<ArgumentException>(() => HeatForecast.Build(source, "fixture", 0, 24));
        source = Series();
        source[1] = source[1] with { SpaceHeatKwh = -1 };
        Assert.Throws<ArgumentException>(() => HeatForecast.Build(source, "fixture", 0, 24));
        source[1] = source[1] with { SpaceHeatKwh = double.MaxValue, HotWaterKwh = double.MaxValue };
        Assert.Throws<ArgumentException>(() => HeatForecast.Build(source, "fixture", 0, 24));
    }

    [Fact]
    public void InvalidSiteHardwareStorageAndMismatchedDemandAreRejected()
    {
        var forecast = HeatForecast.Build(Series(), "fixture", 0, 24);
        HeatSignal[] invalid = [Signal() with { ServerCount = double.NaN },
            Signal() with { ServerCount = 0 }, Signal() with { RecoverableKwhPerServerAtFullLoad = -1 },
            Signal() with { TankStoredKwh = 11 }, Signal() with { TankCapacityKwh = double.PositiveInfinity },
            Signal() with { ServerCount = double.MaxValue, RecoverableKwhPerServerAtFullLoad = 2 }];
        foreach (var signal in invalid)
            Assert.Throws<ArgumentOutOfRangeException>(() => new SiteHeatState("house", signal, forecast));
        Assert.Throws<ArgumentException>(() => new SiteHeatState("house", Signal() with { DemandKwh = 2 }, forecast));
        Assert.Throws<ArgumentException>(() => new SiteHeatState(" ", Signal(), forecast));
    }
}
