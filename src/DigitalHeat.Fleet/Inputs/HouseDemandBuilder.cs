using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>
/// Builds hourly demand per house with Core's HeatDemand.Build (case formula, §4.1) on the full-year
/// weather, then slices the run window. Houses with equal (weather, design kW, divisor, hot water)
/// share one <see cref="HouseDemand"/> instance.
/// </summary>
public sealed class HouseDemandBuilder
{
    public const string ReferenceHouseConfig = "config/reference-house.json";

    private readonly WeatherStore _weather;
    private readonly Dictionary<(string, double, double, double), HouseDemand> _cache = new();

    public ReferenceParameters Baseline { get; }

    public HouseDemandBuilder(string repoRoot, WeatherStore weather, ReferenceParameters? baseline = null)
    {
        _weather = weather;
        Baseline = baseline ?? ReferenceParameters.Load(Path.Combine(repoRoot, ReferenceHouseConfig));
    }

    /// <summary>The case parameters with the house's own design power, divisor, hot water and tank.</summary>
    public static ReferenceParameters ParametersFor(ReferenceParameters b, double designKw, double divisorC,
        double hotWaterKwhPerDay, double tankKwh) => new()
    {
        HoursPerDay = b.HoursPerDay, HoursPerStep = b.HoursPerStep,
        DesignHeatingKw = b.DesignHeatingKw with { Value = designKw },
        BaseTemperatureC = b.BaseTemperatureC,
        FormulaDivisorC = b.FormulaDivisorC with { Value = divisorC },
        HeatingDayThresholdC = b.HeatingDayThresholdC,
        HotWaterKwhPerDay = b.HotWaterKwhPerDay with { Value = hotWaterKwhPerDay },
        TankCapacityKwh = b.TankCapacityKwh with { Value = tankKwh },
        ServerElectricKw = b.ServerElectricKw, HeatRecoveryShare = b.HeatRecoveryShare,
        HeatPumpEfficiency = b.HeatPumpEfficiency, ServerCount = b.ServerCount,
        MinimumLoadShare = b.MinimumLoadShare, ElectricityPriceChfPerKwh = b.ElectricityPriceChfPerKwh,
        ComputePriceChfPerServerHour = b.ComputePriceChfPerServerHour
    };

    public ReferenceParameters ParametersFor(HouseSpec h) =>
        ParametersFor(Baseline, h.DesignHeatingKw, h.FormulaDivisorC, h.HotWaterKwhPerDay, h.TankKwh);

    /// <summary>Preloads all weather needed by the houses, then builds one demand per house (shared where equal).</summary>
    public HouseDemand[] BuildAll(IReadOnlyList<HouseSpec> houses, RunCalendar calendar)
    {
        _weather.Preload(houses.Select(h => h.WeatherRef), []);
        return houses.Select(h => Build(h, calendar)).ToArray();
    }

    public HouseDemand Build(HouseSpec house, RunCalendar calendar)
    {
        var key = (house.WeatherRef + "|" + calendar.OffsetHour + "|" + calendar.RunHours,
            house.DesignHeatingKw, house.FormulaDivisorC, house.HotWaterKwhPerDay);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        _weather.Preload([house.WeatherRef], []);
        var w = _weather.Actual(house.WeatherRef);
        var p = ParametersFor(house);
        var hours = HeatDemand.Build(w.TimeLocal.Zip(w.TemperatureC, (t, c) => new WeatherHour(t, c)).ToList(), p);
        var demand = Slice(hours, calendar, p);
        _cache[key] = demand;
        return demand;
    }

    /// <summary>Slices the run window; kWh per step ÷ hours per step = kW.</summary>
    public static HouseDemand Slice(IReadOnlyList<DemandHour> hours, RunCalendar calendar, ReferenceParameters p)
    {
        double step = p.HoursPerStep.Value;
        var space = new double[calendar.RunHours];
        var water = new double[calendar.RunHours];
        var total = new double[calendar.RunHours];
        var heatingDates = new HashSet<string>();
        for (int i = 0; i < calendar.RunHours; i++)
        {
            var h = hours[calendar.OffsetHour + i];
            space[i] = h.SpaceHeatKwh / step;
            water[i] = h.HotWaterKwh / step;
            total[i] = h.TotalHeatKwh / step;
            if (h.HeatingDay) heatingDates.Add(h.LocalTime[..10]);
        }
        return new HouseDemand { SpaceHeatKw = space, HotWaterKw = water, TotalKw = total, HeatingDays = heatingDates.Count };
    }
}
