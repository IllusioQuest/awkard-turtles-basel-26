namespace DigitalHeat.Fleet;

// Scheduler, pricing, forecast and server-count contracts (§4.4, §7, §8, §9).

public enum FleetEventKind
{
    HourStart, JobArrived, OverheadDone, ChunkFinished, JobCompleted, DeadlinePassed,
    TankFull, TankEmpty, TankThreshold, Wakeup, Tick
}

/// <summary>
/// One event delivered to the scheduler. Fields that do not apply are null. TimeMs is when the
/// event happened (in tick mode it is delivered at the next tick, §3). For TankThreshold,
/// ThresholdKwh is the registered value that was crossed.
/// </summary>
public readonly record struct FleetEvent(
    FleetEventKind Kind,
    long TimeMs,
    HouseId? House = null,
    JobId? Job = null,
    ChunkId? Chunk = null,
    ServerId? Server = null,
    double? ThresholdKwh = null);

public interface IHouseView
{
    HouseId Id { get; }
    HouseSpec Spec { get; }
    double TankCapacityKwh { get; }
    /// <summary>Tank content at the current time (the engine settles the house lazily on read).</summary>
    double TankKwh { get; }
    /// <summary>Space heat + hot water demand of the current hour.</summary>
    double DemandKwNow { get; }
    /// <summary>Σ load × max_electric_kw over the house's servers, now.</summary>
    double ElectricKwNow { get; }
    IReadOnlyList<ServerId> Servers { get; }
}

public interface IServerView
{
    ServerId Id { get; }
    HouseId House { get; }
    double Load { get; }
    /// <summary>Null = free. A paused chunk (load 0) still occupies its server.</summary>
    ChunkId? Chunk { get; }
}

public interface IJobView
{
    JobId Id { get; }
    JobRequest Request { get; }
    JobType Type { get; }
    JobStatus Status { get; }
    /// <summary>Units not yet done (includes units in active chunks that are not done yet).</summary>
    double RemainingUnits { get; }
    /// <summary>Units not in any active chunk and not done: what a new Start may take.</summary>
    double UnassignedUnits { get; }
    /// <summary>Chunks currently on a server (phase Overhead, Working or Checkpointing).</summary>
    IReadOnlyList<ChunkId> ActiveChunks { get; }
    /// <summary>Accepted quote, CHF per billed unit (Type 1: per work unit; Type 2: per server-hour).</summary>
    double QuoteChfPerBilledUnit { get; }
}

public interface IChunkView
{
    ChunkId Id { get; }
    JobId Job { get; }
    ServerId Server { get; }
    HouseId House { get; }
    double WorkUnits { get; }
    /// <summary>Units done so far (settled to the current time on read).</summary>
    double UnitsDone { get; }
    ChunkPhase Phase { get; }
    double Load { get; }
    /// <summary>Current progress rate; 0 in overhead/checkpoint phases and when paused.</summary>
    double UnitsPerSecond { get; }
    /// <summary>Remaining overhead time of the current Overhead/Checkpointing phase, else 0.</summary>
    long OverheadRemainingMs { get; }
}

/// <summary>Read-only view of the fleet for the scheduler and for pricing (§9.1).</summary>
public interface IFleetView
{
    long NowMs { get; }
    /// <summary>Run hour index of NowMs.</summary>
    int HourIndex { get; }
    long EndMs { get; }
    /// <summary>Hour of week (0 = Monday 00:00 local) of run hour 0.</summary>
    int StartHourOfWeek { get; }
    ServerParams ServerParams { get; }
    IReadOnlyList<JobType> JobTypes { get; }
    IReadOnlyList<IHouseView> Houses { get; }
    IReadOnlyList<IServerView> Servers { get; }
    IJobView Job(JobId id);
    IChunkView Chunk(ChunkId id);
    /// <summary>Accepted jobs that are not done (waiting or active), ascending JobId.</summary>
    IReadOnlyCollection<JobId> OpenJobs { get; }
    IForecastProvider Forecast { get; }
    /// <summary>
    /// Σ remaining kWh (remaining units × kWh per unit) of open jobs with deadline ≤ deadlineMs.
    /// Updated when units are kept or lost (chunk end, stop) and on accept/complete, so progress of
    /// running chunks is subtracted at chunk end (§7, labelled approximation).
    /// </summary>
    double CommittedKwh(long deadlineMs);
}

/// <summary>
/// Actions the scheduler may take (§9.1). Every call is validated and applied immediately at the
/// current time; an invalid call throws <see cref="InvalidFleetActionException"/>.
/// </summary>
public interface IFleetActions
{
    /// <summary>Server must be free; units ≤ unassigned; ≥ min_chunk_work_units unless it is the
    /// whole unassigned remainder; active chunks &lt; max_chunks; placement allowed; load valid.</summary>
    ChunkId Start(JobId job, ServerId server, double workUnits, double load);
    /// <summary>Server must hold a chunk. 0 = pause (needs allow_pause), else min_load ≤ load ≤ 1.</summary>
    void SetLoad(ServerId server, double load);
    /// <summary>Checkpointable: progress kept, server busy for checkpoint_overhead_s at the current
    /// load, then freed. Otherwise: progress lost, server freed at once.</summary>
    void Stop(ChunkId chunk);
    /// <summary>A Wakeup event at timeMs (must be ≥ now).</summary>
    void RequestWakeup(long timeMs);
    /// <summary>TankThreshold events when the house's tank crosses tankKwh in either direction.</summary>
    void RegisterTankThreshold(HouseId house, double tankKwh);
}

public sealed class InvalidFleetActionException(string message) : InvalidOperationException(message);

public interface IFleetScheduler
{
    string Id { get; }
    void Initialize(IFleetView view, IFleetActions act);
    /// <summary>Called once per batch of events with the same time (or per tick in tick mode).</summary>
    void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions act);
}

/// <summary>Quote for one job: CHF per billed unit, and whether the customer accepts (§7).</summary>
public sealed record Quote(double PriceChfPerBilledUnit, bool Accepted);

public interface IPricing
{
    string Id { get; }
    /// <summary>Called once when a job arrives, before the scheduler sees it (status Waiting).</summary>
    Quote Quote(IJobView job, IFleetView view);
}

/// <summary>Forecast heat demand (space heat + hot water) in kW, run-hour indices (§8).</summary>
public interface IForecastProvider
{
    string Kind { get; }
    double HeatDemandKw(HouseId house, int issuedHour, int targetHour);
}

public interface IServerCountPolicy
{
    int ServerCount(HouseSpec house, HouseDemandSummary demand);
}
