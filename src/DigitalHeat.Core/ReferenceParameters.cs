using System.Reflection;
using System.Text.Json;

namespace DigitalHeat.Core;

public sealed record Parameter(double Value, string Unit, string Source);

/// <summary>All reference and scenario numbers live in one source-labelled JSON file.</summary>
public sealed class ReferenceParameters
{
    public required Parameter HoursPerDay { get; init; }
    public required Parameter HoursPerStep { get; init; }
    public required Parameter DesignHeatingKw { get; init; }
    public required Parameter BaseTemperatureC { get; init; }
    public required Parameter FormulaDivisorC { get; init; }
    public required Parameter HeatingDayThresholdC { get; init; }
    public required Parameter HotWaterKwhPerDay { get; init; }
    public required Parameter TankCapacityKwh { get; init; }
    public required Parameter ServerElectricKw { get; init; }
    public required Parameter HeatRecoveryShare { get; init; }
    public required Parameter HeatPumpEfficiency { get; init; }
    public required Parameter ServerCount { get; init; }
    public required Parameter MinimumLoadShare { get; init; }
    public required Parameter ElectricityPriceChfPerKwh { get; init; }
    public required Parameter ComputePriceChfPerServerHour { get; init; }

    public static ReferenceParameters Load(string path)
    {
        var p = JsonSerializer.Deserialize<ReferenceParameters>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty parameter file.");
        var units = new Dictionary<string, string>
        {
            [nameof(HoursPerDay)] = "hours/day", [nameof(HoursPerStep)] = "hours",
            [nameof(DesignHeatingKw)] = "kW", [nameof(BaseTemperatureC)] = "°C",
            [nameof(FormulaDivisorC)] = "°C", [nameof(HeatingDayThresholdC)] = "°C",
            [nameof(HotWaterKwhPerDay)] = "kWh/day", [nameof(TankCapacityKwh)] = "kWh",
            [nameof(ServerElectricKw)] = "kW/server", [nameof(HeatRecoveryShare)] = "fraction",
            [nameof(HeatPumpEfficiency)] = "kWh heat/kWh electricity",
            [nameof(ServerCount)] = "servers", [nameof(MinimumLoadShare)] = "fraction/server",
            [nameof(ElectricityPriceChfPerKwh)] = "CHF/kWh",
            [nameof(ComputePriceChfPerServerHour)] = "CHF/server-hour"
        };
        foreach (var property in typeof(ReferenceParameters).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var item = (Parameter?)property.GetValue(p);
            if (item is null || !double.IsFinite(item.Value) || item.Unit != units[property.Name]
                || string.IsNullOrWhiteSpace(item.Source))
                throw new InvalidDataException($"Invalid value, unit or source for {property.Name}.");
        }
        if (p.HoursPerDay.Value <= 0 || p.HoursPerDay.Value % 1 != 0 || p.HoursPerStep.Value <= 0
            || p.ServerCount.Value <= 0 || p.ServerCount.Value % 1 != 0
            || p.DesignHeatingKw.Value <= 0 || p.FormulaDivisorC.Value <= 0
            || p.ServerElectricKw.Value <= 0 || p.HeatPumpEfficiency.Value <= 0
            || p.HeatRecoveryShare.Value is <= 0 or > 1
            || p.MinimumLoadShare.Value is < 0 or > 1
            || p.TankCapacityKwh.Value < 0 || p.HotWaterKwhPerDay.Value < 0
            || p.ElectricityPriceChfPerKwh.Value < 0 || p.ComputePriceChfPerServerHour.Value < 0)
            throw new InvalidDataException("Parameter values are outside valid bounds.");
        return p;
    }
}
