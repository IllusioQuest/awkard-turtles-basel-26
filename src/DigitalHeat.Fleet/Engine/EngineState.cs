using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

// Mutable state of the event-driven engine (docs/model/fleet-simulation.md §3). Internal; the
// scheduler sees these objects only through the read-only view interfaces of Contracts/.

internal enum EngineEventKind : byte
{
    HourStart, Tick, Wakeup, TankFull, TankEmpty, TankThreshold, ChunkTimer, DeadlinePassed,
    /// <summary>A JobCompleted event produced by an action (delivered with the next batch).</summary>
    EmitJobCompleted,
}

/// <summary>Heap element. House events carry the house version, chunk events the chunk version.</summary>
internal readonly struct EngineEvent(EngineEventKind kind, int version, double value,
    HouseState? house, ChunkState? chunk, JobState? job)
{
    public readonly EngineEventKind Kind = kind;
    public readonly int Version = version;
    public readonly double Value = value;
    public readonly HouseState? House = house;
    public readonly ChunkState? Chunk = chunk;
    public readonly JobState? Job = job;
}

internal sealed class HouseState(FleetEngine engine, int index, EngineHouse input) : IHouseView
{
    public readonly FleetEngine Engine = engine;
    public readonly int Index = index;
    public readonly EngineHouse Input = input;
    public HouseDemand Demand => Input.Demand;
    public ServerState[] Servers = [];
    public ServerId[] ServerIds = [];
    public double Tank = input.Spec.TankInitialKwh;
    public long LastMs;
    public int Version;
    public bool Dirty;
    /// <summary>Registered tank thresholds, sorted ascending.</summary>
    public readonly List<double> Thresholds = [];
    public readonly EnergyAccount Run = new();
    public EnergyAccount Period = new();
    public int PeriodIdx = -1;
    public long PeriodStartMs;

    public HouseId Id => new(Index);
    public HouseSpec Spec => Input.Spec;
    public double TankCapacityKwh => Input.Spec.TankKwh;
    public double TankKwh { get { Engine.SettleToNow(this); return Tank; } }
    public double DemandKwNow => Demand.TotalKw[Engine.DemandHour(Engine.NowMs)];
    public double ElectricKwNow => Engine.ElectricKw(this);
    IReadOnlyList<ServerId> IHouseView.Servers => ServerIds;
}

internal sealed class ServerState(int id, HouseState house) : IServerView
{
    public readonly int Index = id;
    public readonly HouseState House = house;
    public ChunkState? Chunk;
    public ServerId Id => new(Index);
    HouseId IServerView.House => House.Id;
    public double Load => Chunk?.Load ?? 0;
    ChunkId? IServerView.Chunk => Chunk is null ? null : Chunk.Id;
}

internal sealed class ChunkState(FleetEngine engine, int id, JobState job, ServerState server, double workUnits) : IChunkView
{
    public readonly FleetEngine Engine = engine;
    public readonly int Index = id;
    public readonly JobState JobState = job;
    public readonly ServerState ServerState = server;
    public readonly double WorkUnits = workUnits;
    public double Done;
    /// <summary>Units already credited as income / kept.</summary>
    public double Credited;
    public ChunkPhase PhaseValue;
    public double LoadValue;
    public long OverheadRemainingMsValue;
    public int Version;
    public bool Dirty;
    /// <summary>Phase Overhead / Working / Checkpointing: the chunk occupies its server.</summary>
    public bool Attached => PhaseValue is ChunkPhase.Overhead or ChunkPhase.Working or ChunkPhase.Checkpointing;

    public ChunkId Id => new(Index);
    public JobId Job => JobState.Id;
    ServerId IChunkView.Server => ServerState.Id;
    HouseId IChunkView.House => ServerState.House.Id;
    double IChunkView.WorkUnits => WorkUnits;
    public double UnitsDone { get { Engine.SettleToNow(ServerState.House); return Done; } }
    public ChunkPhase Phase => PhaseValue;
    public double Load => LoadValue;
    public double UnitsPerSecond => PhaseValue == ChunkPhase.Working ? Engine.RateUnitsPerS(this) : 0;
    public long OverheadRemainingMs
    {
        get
        {
            Engine.SettleToNow(ServerState.House);
            return PhaseValue is ChunkPhase.Overhead or ChunkPhase.Checkpointing ? OverheadRemainingMsValue : 0;
        }
    }
}

internal sealed class JobState(int index, JobRequest request, JobType type) : IJobView
{
    public readonly int Index = index;
    public readonly JobRequest RequestValue = request;
    public readonly JobType TypeValue = type;
    public JobStatus StatusValue = JobStatus.Waiting;
    public bool Arrived;
    /// <summary>Units kept (finished chunks and checkpointed progress).</summary>
    public double Kept;
    public double UnassignedValue = request.WorkUnits;
    public readonly List<ChunkId> ActiveIds = [];
    public double Quote;
    public bool Accepted;
    public long? FirstStartMs, FinishMs;
    public int Chunks, Pauses, Stops, Moves;
    public readonly List<int> HousesUsed = [];
    public double UnitsDone, UnitsLate, IncomeChf, EnergyKwh;
    /// <summary>Current contribution to the committed-kWh Fenwick tree and class sums.</summary>
    public double Contribution;
    public int LastCheckpointHouse = -1;
    /// <summary>House that last started a chunk of this job (deadline attribution), -1 if never started.</summary>
    public int LastHouse = -1;
    public double KwhPerUnit => TypeValue.EnergyKwhPerWorkUnit.Value;

    public JobId Id => new(Index);
    public JobRequest Request => RequestValue;
    public JobType Type => TypeValue;
    public JobStatus Status => StatusValue;
    public double RemainingUnits => Math.Max(0, RequestValue.WorkUnits - Kept);
    public double UnassignedUnits => Math.Max(0, UnassignedValue);
    public IReadOnlyList<ChunkId> ActiveChunks => ActiveIds;
    public double QuoteChfPerBilledUnit => Quote;
}

/// <summary>Fenwick tree over deadline hours: prefix sums of remaining kWh of open jobs.</summary>
internal sealed class Fenwick(int size)
{
    private readonly double[] _t = new double[size + 1];
    public int Size => size;

    public void Add(int index, double delta)
    {
        for (int i = index + 1; i <= size; i += i & -i) _t[i] += delta;
    }

    /// <summary>Sum over indices 0..index inclusive.</summary>
    public double Prefix(int index)
    {
        double s = 0;
        for (int i = Math.Min(index, size - 1) + 1; i > 0; i -= i & -i) s += _t[i];
        return s;
    }
}

/// <summary>Ascending-id read-only collection of open jobs.</summary>
internal sealed class OpenJobsView : IReadOnlyCollection<JobId>
{
    public readonly SortedSet<int> Set = [];
    public int Count => Set.Count;
    public IEnumerator<JobId> GetEnumerator() { foreach (int i in Set) yield return new JobId(i); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
