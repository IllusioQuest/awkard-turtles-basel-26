namespace DigitalHeat.Fleet;

// ---------------------------------------------------------------- houses (§4)

/// <summary>
/// One house (§4.1). <see cref="WeatherRef"/> is either a T-51 grid point id (e.g. "g0123",
/// "cap-bs", read from data/raw/swiss-grid/weather-2025-hourly.csv.gz) or a repo-relative path to an
/// Open-Meteo CSV ending in ".csv" (e.g. the Basel reference file). <see cref="CellId"/> is the grid
/// point used for forecasts (§8). <see cref="Canton"/> (two letters) is used by the origin_site
/// placement rule (same canton as the client); it may be null for a house file without it.
/// <see cref="ServerCount"/> null = the server-count policy decides (§4.4).
/// </summary>
public sealed record HouseSpec(
    string HouseId,
    double LatDeg,
    double LonDeg,
    string CellId,
    int? BfsMunicipalityNr,
    string? Canton,
    string WeatherRef,
    double DesignHeatingKw,
    double FormulaDivisorC,
    double HotWaterKwhPerDay,
    double TankKwh,
    double TankInitialKwh,
    double ElectricityPriceChfPerKwh,
    int? ServerCount);

/// <summary>
/// Hourly heat demand of a house over the run, one value per run hour (index 0 = run start).
/// Built with Core's HeatDemand.Build (case formula). Houses with the same weather and parameters
/// share one instance (§4.1).
/// </summary>
public sealed class HouseDemand
{
    public required double[] SpaceHeatKw { get; init; }
    public required double[] HotWaterKw { get; init; }
    public required double[] TotalKw { get; init; }
    public required int HeatingDays { get; init; }
    public HouseDemandSummary Summary => new(TotalKw.Max(), TotalKw.Sum(), TotalKw.Average());
}

/// <summary>Input to the server-count policy (§4.4). Hourly steps, so annual kWh = Σ kW × 1 h.</summary>
public sealed record HouseDemandSummary(double PeakHeatKw, double AnnualKwh, double MeanKw);

/// <summary>Server parameters (§4.3), scenario values; defaults from config/reference-house.json.</summary>
public sealed record ServerParams(double MaxElectricKw, double HeatRecoveryShare, double HeatPumpEfficiency);

// ---------------------------------------------------------------- jobs (§5)

public enum JobClass { Type1, Type2 }

/// <summary>A sampled distribution from config (§5.1). Values are in <see cref="Unit"/>.</summary>
public interface IDistribution
{
    double Sample(Rng rng);
    double Mean { get; }
    string Unit { get; }
    string Source { get; }
}

public sealed record JobLifecycle(bool AllowPause, double MinLoad, bool Checkpointable,
    double ResumeOverheadS, double CheckpointOverheadS);

/// <summary>Mode: "anywhere" | "origin_site" (same canton as the client) | "country_allowlist".</summary>
public sealed record FleetPlacementPolicy(string Mode, string[]? AllowedCountryCodes, string Source);

/// <summary>Kind "fixed": ChfPerWorkUnit set. Kind "dynamic": quoted by IPricing per billed server-hour.</summary>
public sealed record JobPrice(string Kind, Quantity? ChfPerWorkUnit);

/// <summary>Absolute volume (§6), already resolved to jobs per day by the loader.</summary>
public sealed record JobVolume(double JobsPerDay, string Derivation, string Source);

/// <summary>Hour-of-week shape: 168 values, mean normalised to 1, index 0 = Monday 00:00 local.</summary>
public sealed record ArrivalShape(double[] HourOfWeek, string File, string Column);

public sealed record OriginMix(double ClientSiteShare, string? ClientsFile);

/// <summary>A job type from config/fleet/job-types/&lt;id&gt;.json (§5.1).</summary>
public sealed class JobType
{
    public required string Id { get; init; }
    /// <summary>Position in the scenario's jobTypes list; RNG stream = run seed + Index (§6).</summary>
    public required int Index { get; init; }
    public required JobClass Class { get; init; }
    public required string WorkUnit { get; init; }
    public required Quantity EnergyKwhPerWorkUnit { get; init; }
    public required IDistribution Size { get; init; }
    public required IDistribution BatchSize { get; init; }
    public required IDistribution DeadlineS { get; init; }
    public required int MaxChunks { get; init; }
    public required double MinChunkWorkUnits { get; init; }
    public required double StartOverheadS { get; init; }
    public required JobLifecycle Lifecycle { get; init; }
    public required double InputMbPerWorkUnit { get; init; }
    public required double StateMb { get; init; }
    public required FleetPlacementPolicy Placement { get; init; }
    public double? MaxRttMs { get; init; }
    public required JobVolume Volume { get; init; }
    public required ArrivalShape ArrivalShape { get; init; }
    public required OriginMix OriginMix { get; init; }
    public required JobPrice Price { get; init; }
    /// <summary>Raw JSON of the file, copied into run.json for traceability.</summary>
    public string RawJson { get; init; } = "";
    public string SourcePath { get; init; } = "";

    /// <summary>Billed server-hours for Type 2 (§7): work units × kWh per unit ÷ 1 kW.</summary>
    public double BilledServerHours(double workUnits) => workUnits * EnergyKwhPerWorkUnit.Value / 1.0;
}

/// <summary>
/// One generated job (a row of jobs-input.csv, §6). Times are run milliseconds.
/// OriginKind: "anywhere" | "client_site". The job list is sorted by (ArrivalMs, TypeIndex, Seq)
/// and JobId.Value is the position in that list.
/// </summary>
public sealed record JobRequest(
    string JobKey,
    string TypeId,
    int TypeIndex,
    long ArrivalMs,
    long DeadlineMs,
    double WorkUnits,
    int BatchSize,
    string OriginKind,
    double? OriginLatDeg,
    double? OriginLonDeg,
    string? ClientId,
    string? OriginCanton);

public enum JobStatus { Waiting, Active, Done, RejectedPrice }

public enum ChunkPhase
{
    /// <summary>Start or resume overhead: draws power at its load, no progress.</summary>
    Overhead,
    /// <summary>Progresses at load × kW ÷ (kWh per unit × 3600) units/s; load 0 = paused.</summary>
    Working,
    /// <summary>Checkpoint after a Stop of a checkpointable job; server still occupied.</summary>
    Checkpointing,
    Finished,
    Stopped
}
