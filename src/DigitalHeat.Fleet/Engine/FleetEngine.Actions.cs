namespace DigitalHeat.Fleet;

// Scheduler actions (docs/model/fleet-simulation.md §9.1): validated, applied immediately at the
// current time. An invalid action throws InvalidFleetActionException and changes nothing.

public sealed partial class FleetEngine
{
    private static InvalidFleetActionException Invalid(string message) => new(message);

    private static long SecondsToMs(double seconds) => (long)Math.Ceiling(seconds * 1000.0 - 1e-6);

    private void RequireActionsAllowed(string action)
    {
        if (!_actionsAllowed)
            throw new InvalidFleetActionException($"{action}: actions are only allowed inside Initialize/OnEvents.");
    }

    private ServerState GetServer(ServerId id, string action)
    {
        if (id.Value < 0 || id.Value >= _servers.Length) throw Invalid($"{action}: unknown server id {id.Value}.");
        return _servers[id.Value];
    }

    internal ChunkId StartChunk(JobId jobId, ServerId serverId, double workUnits, double load)
    {
        RequireActionsAllowed("Start");
        if (jobId.Value < 0 || jobId.Value >= _jobs.Length) throw Invalid($"Start: unknown job id {jobId.Value}.");
        var job = _jobs[jobId.Value];
        var server = GetServer(serverId, "Start");
        var house = server.House;
        var type = job.TypeValue;
        if (server.Chunk is not null) throw Invalid($"Start: server {serverId.Value} is not free (chunk {server.Chunk.Index}).");
        if (!job.Arrived || job.StatusValue is not (JobStatus.Waiting or JobStatus.Active))
            throw Invalid($"Start: job {jobId.Value} is not open (status {job.StatusValue}, arrived {job.Arrived}).");
        if (!double.IsFinite(workUnits) || workUnits <= 0) throw Invalid($"Start: work units {workUnits} must be finite and > 0.");
        double unassigned = job.UnassignedUnits;
        double tol = UnitsTol * Math.Max(1, unassigned);
        if (workUnits > unassigned + tol)
            throw Invalid($"Start: {workUnits} units exceed the {unassigned} unassigned units of job {jobId.Value}.");
        bool wholeRemainder = workUnits >= unassigned - tol;
        if (workUnits < type.MinChunkWorkUnits && !wholeRemainder)
            throw Invalid($"Start: {workUnits} units are below min_chunk_work_units {type.MinChunkWorkUnits} and not the whole remainder.");
        if (job.ActiveIds.Count >= type.MaxChunks)
            throw Invalid($"Start: job {jobId.Value} already has {job.ActiveIds.Count} active chunks (max_chunks {type.MaxChunks}).");
        if (!double.IsFinite(load) || load <= 0 || load > 1)
            throw Invalid($"Start: load {load} must be finite and in (0, 1].");
        if (load < type.Lifecycle.MinLoad)
            throw Invalid($"Start: load {load} is below the job's min_load {type.Lifecycle.MinLoad}.");
        CheckPlacement(job, house);

        if (wholeRemainder) workUnits = unassigned;
        Settle(house, _nowMs);
        var chunk = new ChunkState(this, _nextChunkId++, job, server, workUnits) { LoadValue = load };
        if (type.StartOverheadS > 0)
        {
            chunk.PhaseValue = ChunkPhase.Overhead;
            chunk.OverheadRemainingMsValue = SecondsToMs(type.StartOverheadS);
        }
        else chunk.PhaseValue = ChunkPhase.Working;
        _chunks[chunk.Index] = chunk;
        server.Chunk = chunk;
        job.ActiveIds.Add(chunk.Id);
        job.UnassignedValue -= workUnits;
        if (job.UnassignedValue < -tol) throw new InvalidOperationException("Invariant: chunk units exceed the job size.");
        if (job.UnassignedValue < 0) job.UnassignedValue = 0;
        job.Chunks++;
        job.FirstStartMs ??= _nowMs;
        if (!job.HousesUsed.Contains(house.Index)) job.HousesUsed.Add(house.Index);
        job.LastHouse = house.Index;
        if (job.LastCheckpointHouse >= 0)
        {
            if (job.LastCheckpointHouse != house.Index) job.Moves++;
            job.LastCheckpointHouse = -1;
        }
        job.StatusValue = JobStatus.Active;
        LogJobEvent(house, job, chunk.Index, "started");
        MarkDirty(house);
        MarkDirty(chunk);
        return chunk.Id;
    }

    private static void CheckPlacement(JobState job, HouseState house)
    {
        var p = job.TypeValue.Placement;
        switch (p.Mode)
        {
            case "anywhere":
                return;
            case "country_allowlist":
                if (p.AllowedCountryCodes is null || Array.IndexOf(p.AllowedCountryCodes, HouseCountryCode) < 0)
                    throw Invalid($"Start: placement country_allowlist does not allow country {HouseCountryCode}.");
                return;
            case "origin_site":
                if (job.RequestValue.OriginKind != "client_site") return;
                if (job.RequestValue.OriginCanton is null || house.Spec.Canton is null
                    || job.RequestValue.OriginCanton != house.Spec.Canton)
                    throw Invalid($"Start: placement origin_site needs house canton {house.Spec.Canton ?? "(none)"} " +
                                  $"= job origin canton {job.RequestValue.OriginCanton ?? "(none)"}.");
                return;
            default:
                throw Invalid($"Start: unknown placement mode '{p.Mode}'.");
        }
    }

    internal void SetServerLoad(ServerId serverId, double load)
    {
        RequireActionsAllowed("SetLoad");
        var server = GetServer(serverId, "SetLoad");
        var chunk = server.Chunk ?? throw Invalid($"SetLoad: server {serverId.Value} holds no chunk.");
        if (chunk.PhaseValue == ChunkPhase.Checkpointing) throw Invalid($"SetLoad: chunk {chunk.Index} is checkpointing.");
        if (!double.IsFinite(load) || load < 0 || load > 1) throw Invalid($"SetLoad: load {load} must be finite and in [0, 1].");
        var job = chunk.JobState;
        var life = job.TypeValue.Lifecycle;
        if (load == 0 && !life.AllowPause) throw Invalid($"SetLoad: job {job.Index} does not allow pausing (load 0).");
        if (load > 0 && load < life.MinLoad) throw Invalid($"SetLoad: load {load} is below the job's min_load {life.MinLoad}.");

        var house = server.House;
        Settle(house, _nowMs);
        double old = chunk.LoadValue;
        if (old == load) return;
        chunk.LoadValue = load;
        if (old > 0 && load == 0) CountPause(house, job);
        if (old > 0 && load == 0) LogJobEvent(house, job, chunk.Index, "paused");
        if (old == 0 && load > 0) LogJobEvent(house, job, chunk.Index, "resumed");
        if (old == 0 && load > 0 && chunk.PhaseValue == ChunkPhase.Working && life.ResumeOverheadS > 0)
        {
            chunk.PhaseValue = ChunkPhase.Overhead;
            chunk.OverheadRemainingMsValue = SecondsToMs(life.ResumeOverheadS);
        }
        MarkDirty(house);
        MarkDirty(chunk);
    }

    internal void StopChunk(ChunkId chunkId)
    {
        RequireActionsAllowed("Stop");
        var chunk = TryChunk(chunkId.Value) ?? throw Invalid($"Stop: unknown or ended chunk {chunkId.Value}.");
        if (chunk.PhaseValue is not (ChunkPhase.Overhead or ChunkPhase.Working))
            throw Invalid($"Stop: chunk {chunkId.Value} is in phase {chunk.PhaseValue}.");
        var job = chunk.JobState;
        var house = chunk.ServerState.House;
        Settle(house, _nowMs);
        job.Stops++;
        LogJobEvent(house, job, chunk.Index, "stopped");
        if (job.TypeValue.Lifecycle.Checkpointable)
        {
            double done = chunk.Done;
            Keep(chunk, true);
            job.UnassignedValue += chunk.WorkUnits - done;
            job.LastCheckpointHouse = house.Index;
            double ckpt = job.TypeValue.Lifecycle.CheckpointOverheadS;
            if (ckpt > 0)
            {
                chunk.PhaseValue = ChunkPhase.Checkpointing;
                chunk.OverheadRemainingMsValue = SecondsToMs(ckpt);
                MarkDirty(chunk);
                MarkDirty(house);
            }
            else Detach(chunk, ChunkPhase.Stopped);
            TryComplete(job, house);
        }
        else
        {
            // progress lost; the energy already drawn stays counted as work energy
            job.UnassignedValue += chunk.WorkUnits;
            Detach(chunk, ChunkPhase.Stopped);
        }
    }

    internal void Wakeup(long timeMs)
    {
        RequireActionsAllowed("RequestWakeup");
        if (timeMs < _nowMs) throw Invalid($"RequestWakeup: time {timeMs} ms is before now ({_nowMs} ms).");
        Push(timeMs, EngineEventKind.Wakeup, 0, 0, null, null, null);
    }

    internal void RegisterThreshold(HouseId houseId, double kwh)
    {
        RequireActionsAllowed("RegisterTankThreshold");
        if (houseId.Value < 0 || houseId.Value >= _houses.Length) throw Invalid($"RegisterTankThreshold: unknown house id {houseId.Value}.");
        var h = _houses[houseId.Value];
        if (!double.IsFinite(kwh) || kwh < 0 || kwh > h.Input.Spec.TankKwh)
            throw Invalid($"RegisterTankThreshold: {kwh} kWh is outside [0, {h.Input.Spec.TankKwh}].");
        Settle(h, _nowMs);
        int i = h.Thresholds.BinarySearch(kwh);
        if (i >= 0) return; // already registered
        h.Thresholds.Insert(~i, kwh);
        MarkDirty(h);
    }
}

internal sealed class ActionsImpl(FleetEngine engine) : IFleetActions
{
    public ChunkId Start(JobId job, ServerId server, double workUnits, double load) => engine.StartChunk(job, server, workUnits, load);
    public void SetLoad(ServerId server, double load) => engine.SetServerLoad(server, load);
    public void Stop(ChunkId chunk) => engine.StopChunk(chunk);
    public void RequestWakeup(long timeMs) => engine.Wakeup(timeMs);
    public void RegisterTankThreshold(HouseId house, double tankKwh) => engine.RegisterThreshold(house, tankKwh);
}

internal sealed class FleetViewImpl(FleetEngine engine) : IFleetView
{
    private readonly IHouseView[] _houses = engine.Houses.Cast<IHouseView>().ToArray();
    private readonly IServerView[] _servers = engine.Servers.Cast<IServerView>().ToArray();

    public long NowMs => engine.NowMs;
    public int HourIndex => FleetTime.HourOf(engine.NowMs);
    public long EndMs => engine.Inputs.EndMs;
    public int StartHourOfWeek => engine.Inputs.StartHourOfWeek;
    public ServerParams ServerParams => engine.Inputs.ServerParams;
    public IReadOnlyList<JobType> JobTypes => engine.Inputs.JobTypes;
    public IReadOnlyList<IHouseView> Houses => _houses;
    public IReadOnlyList<IServerView> Servers => _servers;

    public IJobView Job(JobId id)
    {
        if (id.Value < 0 || id.Value >= engine.Jobs.Length) throw new ArgumentOutOfRangeException(nameof(id), $"Unknown job id {id.Value}.");
        return engine.Jobs[id.Value];
    }

    public IChunkView Chunk(ChunkId id) =>
        engine.TryChunk(id.Value) ?? throw new ArgumentOutOfRangeException(nameof(id), $"Unknown or ended chunk {id.Value}.");

    public IReadOnlyCollection<JobId> OpenJobs => engine.Open;
    public IForecastProvider Forecast => engine.Inputs.Forecast;

    /// <summary>
    /// Hour granularity: sums the remaining kWh of all open jobs whose deadline lies in a run hour
    /// up to and including the hour of <paramref name="deadlineMs"/> (a deadline later in the same
    /// hour is included). Progress of running chunks is subtracted when the chunk ends or stops.
    /// </summary>
    public double CommittedKwh(long deadlineMs)
    {
        if (deadlineMs < 0) return 0;
        return engine.FenwickTree.Prefix(FleetTime.HourOf(deadlineMs));
    }
}
