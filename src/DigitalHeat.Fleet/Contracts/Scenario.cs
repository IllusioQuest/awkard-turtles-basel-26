using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalHeat.Fleet;

/// <summary>
/// Scenario file config/fleet/scenarios/&lt;name&gt;.json (§12). Paths inside are relative to the
/// repository root (<see cref="RepoRoot"/>, the directory holding AGENTS.md).
/// </summary>
public sealed class Scenario
{
    [JsonPropertyName("run")] public RunSection Run { get; set; } = new();
    [JsonPropertyName("houses")] public HousesSection Houses { get; set; } = new();
    [JsonPropertyName("servers")] public ServersSection Servers { get; set; } = new();
    [JsonPropertyName("jobTypes")] public List<string> JobTypes { get; set; } = [];
    [JsonPropertyName("demand")] public DemandSection Demand { get; set; } = new();
    [JsonPropertyName("pricing")] public PricingSection Pricing { get; set; } = new();
    [JsonPropertyName("forecast")] public ForecastSection Forecast { get; set; } = new();
    [JsonPropertyName("scheduler")] public SchedulerSection Scheduler { get; set; } = new();
    [JsonPropertyName("logging")] public LoggingSection Logging { get; set; } = new();

    /// <summary>Scenario name (file name without extension); default run id.</summary>
    [JsonIgnore] public string Name { get; set; } = "scenario";
    [JsonIgnore] public string RepoRoot { get; set; } = ".";
    [JsonIgnore] public string SourcePath { get; set; } = "";

    public string Resolve(string repoRelativePath) =>
        Path.IsPathRooted(repoRelativePath) ? repoRelativePath : Path.Combine(RepoRoot, repoRelativePath);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Scenario Load(string path)
    {
        var s = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Empty scenario {path}.");
        s.SourcePath = Path.GetFullPath(path);
        s.Name = Path.GetFileNameWithoutExtension(path);
        s.RepoRoot = FindRepoRoot(Path.GetDirectoryName(s.SourcePath)!);
        return s;
    }

    public static string FindRepoRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName;
        throw new DirectoryNotFoundException($"No repository root (AGENTS.md) above {start}.");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed class RunSection
{
    /// <summary>Local time Europe/Zurich, "yyyy-MM-ddTHH:mm"; must lie in the weather year (2025).</summary>
    [JsonPropertyName("start_local")] public string StartLocal { get; set; } = "2025-01-01T00:00";
    [JsonPropertyName("end_local")] public string EndLocal { get; set; } = "2026-01-01T00:00";
    /// <summary>"event" | "tick".</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "event";
    [JsonPropertyName("tick_ms")] public long TickMs { get; set; } = 1000;
    [JsonPropertyName("seed")] public long Seed { get; set; } = 20261121;
}

public sealed class HousesSection
{
    /// <summary>House file (.json or .csv, §4.1).</summary>
    [JsonPropertyName("file")] public string? File { get; set; }
    /// <summary>Generator spec (§4.2), used when File is null.</summary>
    [JsonPropertyName("generate")] public JsonElement? Generate { get; set; }
}

public sealed class ServersSection
{
    /// <summary>"fixed" | "share_of_peak" | "from_file".</summary>
    [JsonPropertyName("policy")] public string Policy { get; set; } = "fixed";
    [JsonPropertyName("n")] public int? N { get; set; }
    [JsonPropertyName("f")] public double? F { get; set; }
    /// <summary>Default: config/reference-house.json ServerElectricKw (case, 1 kW).</summary>
    [JsonPropertyName("max_electric_kw")] public double? MaxElectricKw { get; set; }
    /// <summary>Default: config/reference-house.json HeatRecoveryShare (case, 0.8).</summary>
    [JsonPropertyName("heat_recovery_share")] public double? HeatRecoveryShare { get; set; }
    /// <summary>
    /// Hardware profile (config/hardware/*.json) the job types' energy per work unit is derived from;
    /// the full-cost step (<c>FleetRun post</c>) uses the same profile for capex and ops. Not read by the engine.
    /// </summary>
    [JsonPropertyName("hardware_profile")] public string? HardwareProfile { get; set; }
}

public sealed class DemandSection
{
    /// <summary>Read jobs from a jobs-input.csv instead of generating them (§6).</summary>
    [JsonPropertyName("from_file")] public string? FromFile { get; set; }
    /// <summary>Optional per-type multiplier on the job type's volume, keyed by job type id.</summary>
    [JsonPropertyName("volume_scale")] public Dictionary<string, double>? VolumeScale { get; set; }
}

public sealed class PricingSection
{
    /// <summary>"mock-load" (§7) | "fixed-only" (Type 1 fixed price; Type 2 at p_min, always accepted).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "mock-load";
    [JsonPropertyName("p_min_chf")] public double PMinChf { get; set; } = 0.226;
    [JsonPropertyName("p_max_chf")] public double PMaxChf { get; set; } = 1.20;
    [JsonPropertyName("gamma")] public double Gamma { get; set; } = 1;
    [JsonPropertyName("p_ref_chf")] public double PRefChf { get; set; } = 0.82;
    [JsonPropertyName("p_ref_noise_sd")] public double PRefNoiseSd { get; set; } = 0.079;
    [JsonPropertyName("elasticity")] public double Elasticity { get; set; } = -0.8;
}

public sealed class ForecastSection
{
    /// <summary>"archived" (default) | "perfect" | "persistence".</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "archived";
}

public sealed class SchedulerSection
{
    [JsonPropertyName("id")] public string Id { get; set; } = "mesh-heat-first";
    [JsonPropertyName("chunk_target_s")] public double ChunkTargetS { get; set; } = 3600;
}

public sealed class LoggingSection
{
    /// <summary>"none" | "daily" | "hourly" | "events".</summary>
    [JsonPropertyName("house_timeseries")] public string HouseTimeseries { get; set; } = "hourly";
    [JsonPropertyName("out_dir")] public string OutDir { get; set; } = "data/derived/fleet";
}
