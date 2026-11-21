using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>Builds the forecast provider named in the scenario (§8): archived | perfect | persistence.</summary>
public static class ForecastProviders
{
    public const int MinLeadDays = 1;
    public const int MaxLeadDays = 3;

    public static IForecastProvider Create(string kind, IReadOnlyList<HouseSpec> houses, RunCalendar calendar,
        IReadOnlyList<HouseDemand> demands, WeatherStore weather, ReferenceParameters baseline) => kind switch
    {
        "archived" => new ArchivedForecastProvider(houses, calendar, weather, baseline),
        "perfect" => new PerfectForecastProvider(demands),
        "persistence" => new PersistenceForecastProvider(demands),
        _ => throw new ArgumentException($"Unknown forecast kind '{kind}' (archived | perfect | persistence).")
    };

    /// <summary>Lead in days: clamp(ceil((target − issued) ÷ 24 h), 1, 3). Hourly indices.</summary>
    public static int LeadDays(int issuedHour, int targetHour)
    {
        int diff = targetHour - issuedHour;
        int days = diff <= 0 ? MinLeadDays : (diff + RunCalendar.HoursPerDay - 1) / RunCalendar.HoursPerDay;
        return Math.Clamp(days, MinLeadDays, MaxLeadDays);
    }
}

/// <summary>The house's actual demand (target hours beyond the run clamp to the last hour).</summary>
public sealed class PerfectForecastProvider(IReadOnlyList<HouseDemand> demands) : IForecastProvider
{
    public string Kind => "perfect";
    public double HeatDemandKw(HouseId house, int issuedHour, int targetHour)
    {
        var d = demands[house.Value].TotalKw;
        return d[Math.Clamp(targetHour, 0, d.Length - 1)];
    }
}

/// <summary>actual[target − 24k] with k = max(1, ceil((target − issued) ÷ 24)), clamped at hour 0.</summary>
public sealed class PersistenceForecastProvider(IReadOnlyList<HouseDemand> demands) : IForecastProvider
{
    public string Kind => "persistence";
    public double HeatDemandKw(HouseId house, int issuedHour, int targetHour)
    {
        var d = demands[house.Value].TotalKw;
        int target = Math.Clamp(targetHour, 0, d.Length - 1);
        int diff = target - issuedHour;
        int k = Math.Max(1, diff <= 0 ? 1 : (diff + RunCalendar.HoursPerDay - 1) / RunCalendar.HoursPerDay);
        return d[Math.Max(0, target - RunCalendar.HoursPerDay * k)];
    }
}

/// <summary>
/// T-51 archived forecasts (§8). Forecast temperature at lead N = archive temperature of the house +
/// (temperature_2m_previous_dayN − temperature_2m) of the house's cell, i.e. the forecast error is
/// applied to the archive weather (data/raw/swiss-grid/SOURCE.md caveat). The case formula is linear
/// in the temperature given the heating-day flag: kW = design ÷ divisor × g + hot water ÷ 24 with
/// g_N[t] = heatingDay_N(t) × max(0, base − T_N(t)); g is precomputed per weather/cell and lead, so a
/// query is O(1). The heating-day flag uses the daily mean of the forecast temperatures of the
/// target's local day at the same lead N.
/// </summary>
public sealed class ArchivedForecastProvider : IForecastProvider
{
    private readonly int _offset;
    private readonly double[][][] _g;          // per house: [lead - 1][data hour]
    private readonly double[] _spaceFactor;    // design ÷ divisor
    private readonly double[] _hotWaterKw;
    public string Kind => "archived";

    public ArchivedForecastProvider(IReadOnlyList<HouseSpec> houses, RunCalendar calendar, WeatherStore weather,
        ReferenceParameters p)
    {
        _offset = calendar.OffsetHour;
        weather.Preload(houses.Select(h => h.WeatherRef), houses.Select(h => h.CellId));
        int hoursPerDay = checked((int)p.HoursPerDay.Value);
        var cache = new Dictionary<(string, string), double[][]>();
        _g = new double[houses.Count][][];
        _spaceFactor = new double[houses.Count];
        _hotWaterKw = new double[houses.Count];
        for (int i = 0; i < houses.Count; i++)
        {
            var h = houses[i];
            var key = (h.WeatherRef, h.CellId);
            if (!cache.TryGetValue(key, out var g))
            {
                var actual = weather.Actual(h.WeatherRef).TemperatureC;
                var fc = weather.Forecast(h.CellId);
                if (fc.Lead0C.Length != actual.Length)
                    throw new InvalidDataException($"Forecast of {h.CellId} and weather of {h.WeatherRef} differ in length.");
                g = new double[ForecastProviders.MaxLeadDays][];
                for (int lead = ForecastProviders.MinLeadDays; lead <= ForecastProviders.MaxLeadDays; lead++)
                    g[lead - 1] = BuildG(actual, fc.Lead0C, fc.PreviousDay(lead), hoursPerDay,
                        p.BaseTemperatureC.Value, p.HeatingDayThresholdC.Value);
                cache[key] = g;
            }
            _g[i] = g;
            _spaceFactor[i] = h.DesignHeatingKw / h.FormulaDivisorC;
            _hotWaterKw[i] = h.HotWaterKwhPerDay / hoursPerDay;
        }
    }

    private static double[] BuildG(double[] actual, double[] lead0, double[] prev, int hoursPerDay, double baseC, double thresholdC)
    {
        var t = new double[actual.Length];
        for (int i = 0; i < t.Length; i++) t[i] = actual[i] + (prev[i] - lead0[i]);
        var g = new double[t.Length];
        for (int day = 0; day * hoursPerDay < t.Length; day++)
        {
            double sum = 0;
            for (int h = 0; h < hoursPerDay; h++) sum += t[day * hoursPerDay + h];
            bool heating = sum / hoursPerDay < thresholdC;
            for (int h = 0; h < hoursPerDay; h++)
            {
                int i = day * hoursPerDay + h;
                g[i] = heating ? Math.Max(0, baseC - t[i]) : 0;
            }
        }
        return g;
    }

    public double HeatDemandKw(HouseId house, int issuedHour, int targetHour)
    {
        int lead = ForecastProviders.LeadDays(issuedHour, targetHour);
        var g = _g[house.Value][lead - 1];
        int idx = Math.Clamp(_offset + targetHour, 0, g.Length - 1);
        return _spaceFactor[house.Value] * g[idx] + _hotWaterKw[house.Value];
    }
}
