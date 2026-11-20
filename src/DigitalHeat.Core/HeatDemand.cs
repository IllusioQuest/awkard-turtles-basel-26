namespace DigitalHeat.Core;

public sealed record WeatherHour(string LocalTime, double TemperatureC);
public sealed record DemandHour(string LocalTime, double TemperatureC, bool HeatingDay,
    double SpaceHeatKwh, double HotWaterKwh)
{
    public double TotalHeatKwh => SpaceHeatKwh + HotWaterKwh;
}

public static class HeatDemand
{
    /// <summary>Case formula: max(0, 10 kW × (20 °C − T) / 25.6 °C), only on a heating day.</summary>
    public static IReadOnlyList<DemandHour> Build(IReadOnlyList<WeatherHour> weather, ReferenceParameters p)
    {
        int hoursPerDay = checked((int)p.HoursPerDay.Value);
        if (weather.Count == 0 || weather.Count % hoursPerDay != 0)
            throw new ArgumentException("Weather must contain complete local days.", nameof(weather));
        var result = new List<DemandHour>(weather.Count);
        for (int start = 0; start < weather.Count; start += hoursPerDay)
        {
            var day = weather.Skip(start).Take(hoursPerDay).ToArray();
            string date = day[0].LocalTime[..10];
            if (day.Any(h => h.LocalTime.Length < 13 || h.LocalTime[..10] != date || !double.IsFinite(h.TemperatureC)))
                throw new ArgumentException("Each local day needs finite temperatures and matching dates.", nameof(weather));
            bool heating = day.Average(h => h.TemperatureC) < p.HeatingDayThresholdC.Value;
            foreach (var h in day)
            {
                double spaceKw = heating ? Math.Max(0, p.DesignHeatingKw.Value
                    * (p.BaseTemperatureC.Value - h.TemperatureC) / p.FormulaDivisorC.Value) : 0;
                result.Add(new DemandHour(h.LocalTime, h.TemperatureC, heating,
                    spaceKw * p.HoursPerStep.Value,
                    p.HotWaterKwhPerDay.Value / p.HoursPerDay.Value));
            }
        }
        return result;
    }
}
