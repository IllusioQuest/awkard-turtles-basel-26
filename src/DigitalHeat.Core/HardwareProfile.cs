using System.Reflection;
using System.Text.Json;

namespace DigitalHeat.Core;

/// <summary>A profile value: <c>Value</c> is null only for a visible <c>TODO(source)</c>.</summary>
public sealed record ProfileValue(double? Value, string Unit, string Source)
{
    public bool IsKnown => Value is not null;
}

/// <summary>
/// Hardware variant normalised to one case "server" (1 kW electric load), from
/// <c>config/hardware/*.json</c> (ticket T-15). Variants only: scored reference-house figures keep
/// the abstract server of <see cref="ReferenceParameters"/>.
/// </summary>
public sealed class HardwareProfile
{
    /// <summary>Relative tolerance for derived values that are rounded in the JSON file.</summary>
    public const double DerivedValueRelativeTolerance = 0.005;

    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Status { get; init; }
    public required string Description { get; init; }
    public required string TranscriptionModel { get; init; }

    public required ProfileValue UnitsPerServer { get; init; }
    public required ProfileValue UnitElectricKw { get; init; }
    public required ProfileValue UnitPurchasePriceChf { get; init; }
    public required ProfileValue HostElectricKw { get; init; }
    public required ProfileValue HostPurchasePriceChf { get; init; }
    public required ProfileValue ServerElectricKw { get; init; }
    public required ProfileValue IdleElectricKw { get; init; }
    public required ProfileValue HeatRecoveryShare { get; init; }
    public required ProfileValue PurchasePriceChfPerServer { get; init; }
    public required ProfileValue LifetimeYears { get; init; }
    public required ProfileValue OpsChfPerServerYear { get; init; }
    public required ProfileValue TranscriptionAudioHoursPerServerHour { get; init; }
    /// <summary>LLM inference (7–8B model, batched), input and output tokens counted alike, per 1 kW server-hour.</summary>
    public required ProfileValue LlmTokensPerServerHour { get; init; }
    public required ProfileValue WebhostingSitesPerServer { get; init; }
    public required ProfileValue SpotPriceChfPerServerHour { get; init; }
    public required ProfileValue OnDemandPriceChfPerServerHour { get; init; }
    public required ProfileValue HomeHostPriceChfPerServerHour { get; init; }

    private static readonly Dictionary<string, string> Units = new()
    {
        [nameof(UnitsPerServer)] = "units/server", [nameof(UnitElectricKw)] = "kW/unit",
        [nameof(UnitPurchasePriceChf)] = "CHF/unit", [nameof(HostElectricKw)] = "kW/server",
        [nameof(HostPurchasePriceChf)] = "CHF/server", [nameof(ServerElectricKw)] = "kW/server",
        [nameof(IdleElectricKw)] = "kW/server", [nameof(HeatRecoveryShare)] = "fraction",
        [nameof(PurchasePriceChfPerServer)] = "CHF/server", [nameof(LifetimeYears)] = "years",
        [nameof(OpsChfPerServerYear)] = "CHF/server-year",
        [nameof(TranscriptionAudioHoursPerServerHour)] = "audio hours/server-hour",
        [nameof(LlmTokensPerServerHour)] = "tokens/server-hour",
        [nameof(WebhostingSitesPerServer)] = "sites/server",
        [nameof(SpotPriceChfPerServerHour)] = "CHF/server-hour",
        [nameof(OnDemandPriceChfPerServerHour)] = "CHF/server-hour",
        [nameof(HomeHostPriceChfPerServerHour)] = "CHF/server-hour"
    };

    /// <summary>Values the economics cannot do without; these may not be TODO(source).</summary>
    private static readonly string[] Required =
    [
        nameof(ServerElectricKw), nameof(HeatRecoveryShare), nameof(PurchasePriceChfPerServer),
        nameof(LifetimeYears)
    ];

    /// <summary>Straight-line depreciation: purchase price ÷ lifetime (no residual value).</summary>
    public double DepreciationChfPerServerYear =>
        PurchasePriceChfPerServer.Value!.Value / LifetimeYears.Value!.Value;

    public static HardwareProfile Load(string path)
    {
        var p = JsonSerializer.Deserialize<HardwareProfile>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty hardware profile.");
        if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Status))
            throw new InvalidDataException("Hardware profile needs an id and a status.");
        foreach (var property in typeof(HardwareProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(x => x.PropertyType == typeof(ProfileValue)))
        {
            var item = (ProfileValue?)property.GetValue(p);
            if (item is null || item.Unit != Units[property.Name] || string.IsNullOrWhiteSpace(item.Source))
                throw new InvalidDataException($"Invalid unit or source for {property.Name} in {p.Id}.");
            if (item.Value is null && !item.Source.StartsWith("TODO(source)", StringComparison.Ordinal))
                throw new InvalidDataException($"{property.Name} in {p.Id} has no value and no TODO(source).");
            if (item.Value is { } v && (!double.IsFinite(v) || v < 0))
                throw new InvalidDataException($"{property.Name} in {p.Id} must be finite and non-negative.");
            if (item.Value is null && Required.Contains(property.Name))
                throw new InvalidDataException($"{property.Name} in {p.Id} is required.");
        }
        if (p.ServerElectricKw.Value <= 0 || p.LifetimeYears.Value <= 0 || p.HeatRecoveryShare.Value > 1
            || p.IdleElectricKw.Value > p.ServerElectricKw.Value)
            throw new InvalidDataException($"Values of {p.Id} are outside valid bounds.");
        Check(p.Id, "electric power", p.ServerElectricKw.Value!.Value,
            p.UnitsPerServer.Value * p.UnitElectricKw.Value + p.HostElectricKw.Value);
        Check(p.Id, "purchase price", p.PurchasePriceChfPerServer.Value!.Value,
            p.UnitsPerServer.Value * p.UnitPurchasePriceChf.Value + p.HostPurchasePriceChf.Value);
        return p;
    }

    /// <summary>Loads every profile in a directory, skipping JSON schema files.</summary>
    public static IReadOnlyList<HardwareProfile> LoadDirectory(string directory) =>
        Directory.GetFiles(directory, "*.json")
            .Where(x => !x.EndsWith(".schema.json", StringComparison.Ordinal))
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(Load).ToArray();

    private static void Check(string id, string what, double stated, double? fromParts)
    {
        if (fromParts is { } parts && Math.Abs(parts - stated) > DerivedValueRelativeTolerance * Math.Max(Math.Abs(stated), 1))
            throw new InvalidDataException($"{id}: stated {what} {stated} does not match its parts {parts}.");
    }
}
