using DigitalHeat.Core;
using DigitalHeat.Fleet;
using DigitalHeat.FleetApi;
using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class ApiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fleet-api-" + Guid.NewGuid().ToString("N"));
    private readonly string _root = Scenario.FindRepoRoot(AppContext.BaseDirectory);
    private readonly RunStore _store;

    public ApiTests()
    {
        var s = Scenario.Load(Path.Combine(_root, "config/fleet/scenarios/reference.json"));
        s.Run.EndLocal = "2025-01-08T00:00";
        s.Demand.VolumeScale = s.JobTypes.ToDictionary(p => Path.GetFileNameWithoutExtension(p), _ => 0.01);
        FleetOutput.Write(FleetSimulation.Run(s), Path.Combine(_dir, "mini"));
        _store = new RunStore(_dir, _root);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Run_is_listed_with_hourly_periods()
    {
        var r = Assert.Single(_store.Runs());
        Assert.Equal("mini", r.Id);
        Assert.Equal(1, r.PeriodHours);
        Assert.Equal(168, r.Hours);
        Assert.Equal("2025-01-01T00", r.StartLocal[..13]);
        Assert.False(r.Mock);
    }

    [Fact]
    public void Series_and_network_sums_equal_the_summary()
    {
        var h = Assert.Single(_store.Houses("mini"));
        var ser = _store.HouseSeries("mini", h.Id);
        Assert.Equal(168, ser.Count);
        var net = _store.Network("mini");
        Assert.Equal(168, net.Count);
        void Same(double expected, double a, double b)
        {
            Assert.Equal(expected, a, 1e-6);
            Assert.Equal(expected, b, 1e-6);
        }
        var s = h.Summary;
        Same(s.HeatUsedKwh, ser.Sum(p => p.HeatUsedKwh), net.Sum(n => n.HeatUsedKwh));
        Same(s.ElectricKwh, ser.Sum(p => p.ElectricKwh), net.Sum(n => n.ElectricKwh));
        Same(s.IncomeChf, ser.Sum(p => p.IncomeChf), net.Sum(n => n.IncomeChf));
        Same(s.ElectricityCostChf, ser.Sum(p => p.ElectricityCostChf), net.Sum(n => n.ElectricityCostChf));
        Assert.True(s.ElectricityCostChf > 0);
    }

    [Fact]
    public void Snapshot_equals_series_period_and_temperature_matches_weather_file()
    {
        var h = _store.Houses("mini")[0];
        var ser = _store.HouseSeries("mini", h.Id);
        var snaps = _store.Snapshots("mini", 10, 14);
        Assert.Equal(4, snaps.Count);
        for (int i = 0; i < snaps.Count; i++)
        {
            Assert.Equal(10 + i, snaps[i].H);
            Assert.Equal(10 + i, snaps[i].PeriodStartH);
            Assert.Equal(ser[10 + i], snaps[i].Houses[0]);
        }
        var basel = WeatherCsv.Read(Path.Combine(_root, "data/raw/open-meteo/basel-2025-hourly.csv"));
        Assert.Equal(basel[0].TemperatureC, ser[0].TemperatureC, 1e-9);
    }

    [Fact]
    public void Unknown_run_house_and_bad_range()
    {
        Assert.Throws<NotFoundException>(() => _store.Houses("nope"));
        Assert.Throws<NotFoundException>(() => _store.HouseSeries("mini", "nope"));
        Assert.Throws<BadRangeException>(() => _store.Snapshots("mini", 5, 5));
    }
}
