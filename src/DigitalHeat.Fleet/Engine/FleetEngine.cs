using System.Diagnostics;
using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>
/// Event-driven, exact fleet engine (docs/model/fleet-simulation.md §3). Between two events every
/// rate is constant; a house segment [t0, t1) is settled with Core's <c>HeatBalance.Step</c>.
/// One instance runs once.
/// </summary>
public sealed partial class FleetEngine
{
    /// <summary>All houses are in Switzerland (country_allowlist placement rule).</summary>
    internal const string HouseCountryCode = "CH";
    /// <summary>Tank tolerance in kWh below which a tank counts as full / empty / at a threshold.</summary>
    private const double TankEpsKwh = 1e-9;
    /// <summary>Relative tolerance for unit comparisons (Start, completion).</summary>
    internal const double UnitsTol = 1e-9;
    /// <summary>Float noise (in ms) tolerated before rounding a crossing time up to a whole ms.</summary>
    private const double CeilSlackMs = 1e-6;

    private readonly EngineInputs _in;
    private readonly int _runHours;
    private readonly double _maxKw, _share;
    private readonly HouseState[] _houses;
    private readonly ServerState[] _servers;
    private readonly JobState[] _jobs;
    private readonly double[] _unitsPerSPerKw;
    private readonly Dictionary<int, ChunkState> _chunks = [];
    private readonly PriorityQueue<EngineEvent, (long, long)> _heap = new();
    private long _seq;
    private int _nextChunkId;
    private int _cursor;
    private long _nowMs;
    private bool _actionsAllowed, _inApply, _ran;
    private long _eventCount, _schedulerCalls;

    private readonly List<HouseState> _dirtyHouses = [];
    private readonly List<ChunkState> _dirtyChunks = [];
    private readonly List<ChunkState> _retired = [];
    private readonly List<EngineEvent> _batch = [];
    private readonly List<FleetEvent> _events = [];
    private readonly List<FleetEvent> _buffered = [];
    private bool _batchHasTick;

    private readonly OpenJobsView _open = new();
    private readonly Fenwick _fenwick;
    private readonly int[] _openCount = new int[2];
    private readonly double[] _remainingKwh = new double[2];
    private readonly EnergyAccount _seg = new();
    private readonly FleetViewImpl _view;
    private readonly ActionsImpl _actions;
    private readonly long _periodMs;

    // network hour log
    private readonly EnergyAccount[] _netHour;
    private readonly int[] _hArrivals, _hRejections, _hCompletions, _hMisses, _hT2Count;
    private readonly double[] _hT2QuoteSum;
    private readonly NetworkHourRow[] _hourRows;
    private int _closedHours;
    private readonly List<HouseTimeseriesRow> _timeseries = [];
    private readonly List<JobEventRow> _jobEvents = [];

    private void LogJobEvent(HouseState house, JobState job, int? chunk, string kind)
    {
        if (_in.CaptureJobEvents)
            _jobEvents.Add(new JobEventRow(_nowMs, house.Input.Spec.HouseId, job.RequestValue.JobKey, job.TypeValue.Id, chunk, kind));
    }

    public FleetEngine(EngineInputs inputs)
    {
        _in = inputs ?? throw new ArgumentNullException(nameof(inputs));
        if (inputs.EndMs <= 0) throw new ArgumentException("EndMs must be positive.");
        if (inputs.Mode == RunMode.Tick && inputs.TickMs <= 0) throw new ArgumentException("TickMs must be positive in tick mode.");
        _runHours = (int)((inputs.EndMs + FleetTime.MsPerHour - 1) / FleetTime.MsPerHour);
        _maxKw = inputs.ServerParams.MaxElectricKw;
        _share = inputs.ServerParams.HeatRecoveryShare;

        var servers = new List<ServerState>();
        _houses = new HouseState[inputs.Houses.Count];
        for (int i = 0; i < _houses.Length; i++)
        {
            var eh = inputs.Houses[i];
            if (eh.Demand.TotalKw.Length < _runHours || eh.Demand.SpaceHeatKw.Length < _runHours || eh.Demand.HotWaterKw.Length < _runHours)
                throw new ArgumentException($"House {eh.Spec.HouseId}: demand has fewer than {_runHours} hourly values.");
            if (eh.Spec.TankInitialKwh < 0 || eh.Spec.TankInitialKwh > eh.Spec.TankKwh)
                throw new ArgumentException($"House {eh.Spec.HouseId}: initial tank outside [0, capacity].");
            var h = new HouseState(this, i, eh);
            h.Servers = new ServerState[eh.ServerCount];
            h.ServerIds = new ServerId[eh.ServerCount];
            for (int s = 0; s < eh.ServerCount; s++)
            {
                var ss = new ServerState(servers.Count, h);
                servers.Add(ss);
                h.Servers[s] = ss;
                h.ServerIds[s] = ss.Id;
            }
            _houses[i] = h;
        }
        _servers = servers.ToArray();

        _unitsPerSPerKw = new double[inputs.JobTypes.Count];
        for (int i = 0; i < _unitsPerSPerKw.Length; i++)
        {
            var t = inputs.JobTypes[i];
            if (!(t.EnergyKwhPerWorkUnit.Value > 0)) throw new ArgumentException($"Job type {t.Id}: energy per work unit must be > 0.");
            if (t.MaxChunks < 1) throw new ArgumentException($"Job type {t.Id}: max chunks must be >= 1.");
            _unitsPerSPerKw[i] = 1.0 / (t.EnergyKwhPerWorkUnit.Value * FleetTime.SecondsPerHour);
        }

        _jobs = new JobState[inputs.Jobs.Count];
        int maxDeadlineHour = 0;
        for (int i = 0; i < _jobs.Length; i++)
        {
            var r = inputs.Jobs[i];
            if (r.TypeIndex < 0 || r.TypeIndex >= inputs.JobTypes.Count) throw new ArgumentException($"Job {r.JobKey}: unknown type index.");
            if (r.ArrivalMs < 0 || r.DeadlineMs < r.ArrivalMs) throw new ArgumentException($"Job {r.JobKey}: invalid arrival/deadline.");
            if (i > 0 && inputs.Jobs[i - 1].ArrivalMs > r.ArrivalMs) throw new ArgumentException("Jobs must be sorted by arrival.");
            if (!(r.WorkUnits > 0)) throw new ArgumentException($"Job {r.JobKey}: work units must be > 0.");
            _jobs[i] = new JobState(i, r, inputs.JobTypes[r.TypeIndex]);
            maxDeadlineHour = Math.Max(maxDeadlineHour, FleetTime.HourOf(r.DeadlineMs));
        }
        _fenwick = new Fenwick(_runHours + maxDeadlineHour + 1);

        _periodMs = inputs.Timeseries == TimeseriesMode.Daily ? 24 * FleetTime.MsPerHour : FleetTime.MsPerHour;
        _netHour = new EnergyAccount[_runHours];
        for (int i = 0; i < _runHours; i++) _netHour[i] = new EnergyAccount();
        _hArrivals = new int[_runHours]; _hRejections = new int[_runHours];
        _hCompletions = new int[_runHours]; _hMisses = new int[_runHours]; _hT2Count = new int[_runHours];
        _hT2QuoteSum = new double[_runHours];
        _hourRows = new NetworkHourRow[_runHours];

        _view = new FleetViewImpl(this);
        _actions = new ActionsImpl(this);
    }

    internal long NowMs => _nowMs;
    internal int DemandHour(long t) => Math.Min(FleetTime.HourOf(t), _runHours - 1);
    private int NetHour(long t) => Math.Min(FleetTime.HourOf(t), _runHours - 1);
    internal EngineInputs Inputs => _in;
    internal HouseState[] Houses => _houses;
    internal ServerState[] Servers => _servers;
    internal JobState[] Jobs => _jobs;
    internal OpenJobsView Open => _open;
    internal Fenwick FenwickTree => _fenwick;
    internal ChunkState? TryChunk(int id) => _chunks.GetValueOrDefault(id);

    internal double ElectricKw(HouseState h)
    {
        double kw = 0;
        foreach (var s in h.Servers) if (s.Chunk is { } c) kw += c.LoadValue * _maxKw;
        return kw;
    }

    internal double RateUnitsPerS(ChunkState c) => c.LoadValue * _maxKw * _unitsPerSPerKw[c.JobState.RequestValue.TypeIndex];

    internal void SettleToNow(HouseState h) => Settle(h, _nowMs);

    // ------------------------------------------------------------------ run

    public FleetRunResult Run()
    {
        if (_ran) throw new InvalidOperationException("A FleetEngine instance runs once.");
        _ran = true;
        var sw = Stopwatch.StartNew();

        _actionsAllowed = true;
        try { _in.Scheduler.Initialize(_view, _actions); }
        finally { _actionsAllowed = false; }

        Push(0, EngineEventKind.HourStart, 0, 0, null, null, null);
        if (_in.Mode == RunMode.Tick) Push(0, EngineEventKind.Tick, 0, 0, null, null, null);
        RecomputeDirty();

        while (true)
        {
            long t = long.MaxValue;
            if (_heap.TryPeek(out _, out var pri)) t = pri.Item1;
            if (_cursor < _jobs.Length && _jobs[_cursor].RequestValue.ArrivalMs < t) t = _jobs[_cursor].RequestValue.ArrivalMs;
            if (t > _in.EndMs) break;
            ProcessBatch(t);
        }

        Finish();
        sw.Stop();
        return BuildResult(sw.Elapsed.TotalSeconds);
    }

    private void Push(long time, EngineEventKind kind, int version, double value, HouseState? h, ChunkState? c, JobState? j)
        => _heap.Enqueue(new EngineEvent(kind, version, value, h, c, j), (time, _seq++));

    private static bool IsStale(in EngineEvent e) => e.Kind switch
    {
        EngineEventKind.TankFull or EngineEventKind.TankEmpty or EngineEventKind.TankThreshold => e.Version != e.House!.Version,
        EngineEventKind.ChunkTimer => e.Version != e.Chunk!.Version || !e.Chunk.Attached,
        EngineEventKind.DeadlinePassed => e.Job!.StatusValue == JobStatus.Done,
        _ => false,
    };

    private void ProcessBatch(long t)
    {
        CloseHoursBefore(t);
        _nowMs = t;
        _events.Clear();
        _batch.Clear();
        _batchHasTick = false;
        while (_heap.TryPeek(out var ev, out var pri) && pri.Item1 == t)
        {
            _heap.Dequeue();
            if (!IsStale(ev)) _batch.Add(ev);
        }

        _inApply = true;
        // Deadlines last: a chunk finishing exactly at the deadline completes the job in time.
        foreach (var ev in _batch) if (ev.Kind != EngineEventKind.DeadlinePassed) { _eventCount++; Apply(ev); }
        foreach (var ev in _batch) if (ev.Kind == EngineEventKind.DeadlinePassed) { _eventCount++; Apply(ev); }
        while (_cursor < _jobs.Length && _jobs[_cursor].RequestValue.ArrivalMs == t)
        {
            _eventCount++;
            Arrive(_jobs[_cursor++]);
        }
        _inApply = false;

        bool call;
        IReadOnlyList<FleetEvent> deliver;
        if (_in.Mode == RunMode.Tick)
        {
            _buffered.AddRange(_events);
            call = _batchHasTick;
            deliver = _buffered;
        }
        else
        {
            call = _events.Count > 0;
            deliver = _events;
        }
        if (call)
        {
            _schedulerCalls++;
            _actionsAllowed = true;
            try { _in.Scheduler.OnEvents(_view, deliver, _actions); }
            finally { _actionsAllowed = false; }
            if (_in.Mode == RunMode.Tick) _buffered.Clear();
        }

        RecomputeDirty();
        foreach (var c in _retired) { c.Version++; _chunks.Remove(c.Index); }
        _retired.Clear();
    }

    private void Apply(in EngineEvent e)
    {
        long t = _nowMs;
        switch (e.Kind)
        {
            case EngineEventKind.HourStart:
            {
                foreach (var h in _houses) { Settle(h, t); MarkDirty(h); }
                int hour = FleetTime.HourOf(t);
                if ((hour + 1L) * FleetTime.MsPerHour < _in.EndMs)
                    Push((hour + 1L) * FleetTime.MsPerHour, EngineEventKind.HourStart, 0, 0, null, null, null);
                _events.Add(new FleetEvent(FleetEventKind.HourStart, t));
                break;
            }
            case EngineEventKind.Tick:
                _batchHasTick = true;
                if (t + _in.TickMs <= _in.EndMs) Push(t + _in.TickMs, EngineEventKind.Tick, 0, 0, null, null, null);
                _events.Add(new FleetEvent(FleetEventKind.Tick, t));
                break;
            case EngineEventKind.Wakeup:
                _events.Add(new FleetEvent(FleetEventKind.Wakeup, t));
                break;
            case EngineEventKind.TankFull:
            case EngineEventKind.TankEmpty:
            case EngineEventKind.TankThreshold:
            {
                var h = e.House!;
                Settle(h, t);
                MarkDirty(h);
                var kind = e.Kind switch
                {
                    EngineEventKind.TankFull => FleetEventKind.TankFull,
                    EngineEventKind.TankEmpty => FleetEventKind.TankEmpty,
                    _ => FleetEventKind.TankThreshold,
                };
                _events.Add(new FleetEvent(kind, t, House: h.Id,
                    ThresholdKwh: e.Kind == EngineEventKind.TankThreshold ? e.Value : null));
                break;
            }
            case EngineEventKind.ChunkTimer:
                ApplyChunkTimer(e.Chunk!);
                break;
            case EngineEventKind.DeadlinePassed:
            {
                var job = e.Job!;
                foreach (var id in job.ActiveIds) Settle(_chunks[id.Value].ServerState.House, t);
                _hMisses[NetHour(t)]++;
                if (job.LastHouse >= 0)
                {
                    var lh = _houses[job.LastHouse];
                    Settle(lh, t);
                    lh.Run.DeadlinesMissed++; lh.Period.DeadlinesMissed++;
                }
                _events.Add(new FleetEvent(FleetEventKind.DeadlinePassed, t, Job: job.Id));
                break;
            }
            case EngineEventKind.EmitJobCompleted:
                _events.Add(new FleetEvent(FleetEventKind.JobCompleted, t, Job: e.Job!.Id, House: e.House?.Id));
                break;
        }
    }

    private void ApplyChunkTimer(ChunkState c)
    {
        long t = _nowMs;
        var h = c.ServerState.House;
        var job = c.JobState;
        Settle(h, t);
        switch (c.PhaseValue)
        {
            case ChunkPhase.Overhead:
                c.OverheadRemainingMsValue = 0;
                c.PhaseValue = ChunkPhase.Working;
                MarkDirty(c);
                _events.Add(new FleetEvent(FleetEventKind.OverheadDone, t, h.Id, job.Id, c.Id, c.ServerState.Id));
                break;
            case ChunkPhase.Checkpointing:
                c.OverheadRemainingMsValue = 0;
                Detach(c, ChunkPhase.Stopped);
                _events.Add(new FleetEvent(FleetEventKind.OverheadDone, t, h.Id, job.Id, c.Id, c.ServerState.Id));
                break;
            case ChunkPhase.Working:
                c.Done = c.WorkUnits; // a non-stale finish event is authoritative
                Keep(c, true);
                Detach(c, ChunkPhase.Finished);
                _events.Add(new FleetEvent(FleetEventKind.ChunkFinished, t, h.Id, job.Id, c.Id, c.ServerState.Id));
                TryComplete(job, h);
                break;
        }
    }

    /// <summary>Frees the server and retires the chunk. The house's electric power changes, so it is dirty.</summary>
    internal void Detach(ChunkState c, ChunkPhase phase)
    {
        c.PhaseValue = phase;
        c.ServerState.Chunk = null;
        c.JobState.ActiveIds.Remove(c.Id);
        MarkDirty(c.ServerState.House);
        _retired.Add(c);
    }

    private void Arrive(JobState job)
    {
        long t = _nowMs;
        int hour = NetHour(t);
        var q = _in.Pricing.Quote(job, _view);
        if (!double.IsFinite(q.PriceChfPerBilledUnit) || q.PriceChfPerBilledUnit < 0)
            throw new InvalidOperationException($"Pricing {_in.Pricing.Id} returned an invalid price for job {job.RequestValue.JobKey}.");
        job.Quote = q.PriceChfPerBilledUnit;
        if (job.TypeValue.Class == JobClass.Type2) { _hT2QuoteSum[hour] += q.PriceChfPerBilledUnit; _hT2Count[hour]++; }
        _hArrivals[hour]++;
        if (!q.Accepted)
        {
            job.StatusValue = JobStatus.RejectedPrice;
            _hRejections[hour]++;
            return;
        }
        job.Accepted = true;
        job.Arrived = true;
        _open.Set.Add(job.Index);
        _openCount[(int)job.TypeValue.Class]++;
        SetContribution(job, job.RequestValue.WorkUnits * job.KwhPerUnit);
        if (job.RequestValue.DeadlineMs <= _in.EndMs)
            Push(job.RequestValue.DeadlineMs, EngineEventKind.DeadlinePassed, 0, 0, null, null, job);
        _events.Add(new FleetEvent(FleetEventKind.JobArrived, t, Job: job.Id));
    }

    // ------------------------------------------------------------------ accounting helpers

    internal void SetContribution(JobState job, double kwh)
    {
        double delta = kwh - job.Contribution;
        if (delta == 0) return;
        job.Contribution = kwh;
        _fenwick.Add(FleetTime.HourOf(job.RequestValue.DeadlineMs), delta);
        _remainingKwh[(int)job.TypeValue.Class] += delta;
    }

    /// <summary>Credits income for units not yet credited; with <paramref name="keep"/> they also count as kept.</summary>
    internal void Keep(ChunkState c, bool keep)
    {
        double delta = c.Done - c.Credited;
        if (delta <= 0) return;
        c.Credited = c.Done;
        var job = c.JobState;
        var h = c.ServerState.House;
        bool t1 = job.TypeValue.Class == JobClass.Type1;
        double income = t1
            ? delta * (job.TypeValue.Price.ChfPerWorkUnit?.Value ?? 0)
            : job.TypeValue.BilledServerHours(delta) * job.Quote;
        var net = _netHour[NetHour(_nowMs)];
        if (t1) { h.Run.IncomeT1Chf += income; h.Period.IncomeT1Chf += income; net.IncomeT1Chf += income; }
        else { h.Run.IncomeT2Chf += income; h.Period.IncomeT2Chf += income; net.IncomeT2Chf += income; }
        job.IncomeChf += income;
        job.UnitsDone += delta;
        if (keep)
        {
            job.Kept += delta;
            SetContribution(job, Math.Max(0, job.RequestValue.WorkUnits - job.Kept) * job.KwhPerUnit);
        }
    }

    internal void CountPause(HouseState h, JobState job)
    {
        job.Pauses++;
        h.Run.Pauses++; h.Period.Pauses++; _netHour[NetHour(_nowMs)].Pauses++;
    }

    internal void TryComplete(JobState job, HouseState h)
    {
        double size = job.RequestValue.WorkUnits;
        if (job.StatusValue != JobStatus.Active || job.Kept < size - UnitsTol * Math.Max(1, size)) return;
        job.StatusValue = JobStatus.Done;
        LogJobEvent(h, job, null, "completed");
        job.FinishMs = _nowMs;
        SetContribution(job, 0);
        _open.Set.Remove(job.Index);
        _openCount[(int)job.TypeValue.Class]--;
        _hCompletions[NetHour(_nowMs)]++;
        if (_nowMs <= job.RequestValue.DeadlineMs) { h.Run.JobsOnTime++; h.Period.JobsOnTime++; }
        if (_inApply) _events.Add(new FleetEvent(FleetEventKind.JobCompleted, _nowMs, Job: job.Id, House: h.Id));
        else Push(_nowMs, EngineEventKind.EmitJobCompleted, 0, 0, h, null, job);
    }

    internal void MarkDirty(HouseState h)
    {
        if (h.Dirty) return;
        h.Dirty = true;
        _dirtyHouses.Add(h);
    }

    internal void MarkDirty(ChunkState c)
    {
        if (c.Dirty) return;
        c.Dirty = true;
        _dirtyChunks.Add(c);
    }

    // ------------------------------------------------------------------ settlement (core invariant)

    /// <summary>
    /// Settles the house segment [LastMs, t1): all rates are constant in it. Electric energy per
    /// server is load x max kW x dt; produced heat = recovery share x electric; the tank follows
    /// <see cref="HeatBalance.Step"/> (exact for constant rates). Never crosses an hour boundary.
    /// </summary>
    internal void Settle(HouseState h, long t1)
    {
        long t0 = h.LastMs;
        if (t1 == t0) return;
        if (t1 < t0) throw new InvalidOperationException("Settlement moved back in time.");
        long dtMs = t1 - t0;
        int hour = (int)(t0 / FleetTime.MsPerHour);
        if (t1 > (hour + 1L) * FleetTime.MsPerHour)
            throw new InvalidOperationException("A settlement segment must not cross an hour boundary.");
        double dtH = dtMs / 3.6e6;
        double dtS = dtMs / 1000.0;
        var seg = _seg;
        ClearAccount(seg);
        double electricKwh = 0;
        var servers = h.Servers;
        for (int i = 0; i < servers.Length; i++)
        {
            seg.InstalledServerHours += dtH;
            var c = servers[i].Chunk;
            if (c is null) continue;
            double load = c.LoadValue;
            var job = c.JobState;
            double kwh = load * _maxKw * dtH;
            if (kwh > 0) { electricKwh += kwh; job.EnergyKwh += kwh; }
            if (load > 0) { seg.LoadServerHours += load * dtH; seg.BusyServerHours += dtH; }
            switch (c.PhaseValue)
            {
                case ChunkPhase.Overhead:
                    seg.ElectricOverheadKwh += kwh;
                    if (load > 0) c.OverheadRemainingMsValue = Math.Max(0, c.OverheadRemainingMsValue - dtMs);
                    break;
                case ChunkPhase.Checkpointing:
                    seg.ElectricOverheadKwh += kwh;
                    c.OverheadRemainingMsValue = Math.Max(0, c.OverheadRemainingMsValue - dtMs);
                    break;
                case ChunkPhase.Working:
                    if (job.TypeValue.Class == JobClass.Type1) seg.ElectricT1Kwh += kwh; else seg.ElectricT2Kwh += kwh;
                    if (load > 0)
                    {
                        double units = Math.Min(load * _maxKw * _unitsPerSPerKw[job.RequestValue.TypeIndex] * dtS, c.WorkUnits - c.Done);
                        if (units > 0)
                        {
                            c.Done += units;
                            if (t0 >= job.RequestValue.DeadlineMs)
                            {
                                seg.UnitsLate += units;
                                seg.LateWorkKwh += units * job.KwhPerUnit;
                                job.UnitsLate += units;
                            }
                        }
                    }
                    break;
            }
        }
        var dm = h.Demand;
        seg.SpaceHeatKwh = dm.SpaceHeatKw[hour] * dtH;
        seg.HotWaterKwh = dm.HotWaterKw[hour] * dtH;
        seg.DemandKwh = dm.TotalKw[hour] * dtH;
        double produced = _share * electricKwh;
        seg.ElectricityChf = electricKwh * h.Input.Spec.ElectricityPriceChfPerKwh;
        seg.ProducedKwh = produced;

        // The period rolls over before the tank moves, so a flushed row carries the tank at its end.
        if (_in.Timeseries is TimeseriesMode.Daily or TimeseriesMode.Hourly)
        {
            int idx = (int)(t0 / _periodMs);
            if (h.PeriodIdx != idx)
            {
                if (h.PeriodIdx >= 0) FlushPeriod(h, t0);
                h.PeriodIdx = idx;
                h.PeriodStartMs = t0;
            }
        }

        var step = HeatBalance.Step(produced, seg.DemandKwh, h.Tank, h.Input.Spec.TankKwh);
        seg.DirectKwh = step.DirectUsedKwh;
        seg.ChargeKwh = step.TankChargeKwh;
        seg.DischargeKwh = step.TankDischargeKwh;
        seg.DumpedKwh = step.DumpedHeatKwh;
        seg.BackupKwh = step.BackupHeatKwh;
        h.Tank = step.TankStoredKwh;
        h.LastMs = t1;

        h.Run.Add(seg);
        if (_in.Timeseries != TimeseriesMode.None) h.Period.Add(seg);
        _netHour[hour].Add(seg);
        if (_in.Timeseries == TimeseriesMode.Events) FlushPeriod(h, t1, t0);
    }

    private void FlushPeriod(HouseState h, long endMs, long? startMs = null)
    {
        _timeseries.Add(new HouseTimeseriesRow(h.Index, startMs ?? h.PeriodStartMs, endMs, h.Period, h.Tank));
        h.Period = new EnergyAccount();
        h.PeriodStartMs = endMs;
    }

    private static void ClearAccount(EnergyAccount a)
    {
        a.SpaceHeatKwh = a.HotWaterKwh = a.DemandKwh = 0;
        a.ElectricOverheadKwh = a.ElectricT1Kwh = a.ElectricT2Kwh = 0;
        a.ProducedKwh = a.DirectKwh = a.ChargeKwh = a.DischargeKwh = a.DumpedKwh = a.BackupKwh = 0;
        a.LoadServerHours = a.BusyServerHours = a.InstalledServerHours = 0;
        a.IncomeT1Chf = a.IncomeT2Chf = a.ElectricityChf = 0;
        a.UnitsLate = a.LateWorkKwh = 0;
        a.Pauses = 0;
        a.JobsOnTime = a.DeadlinesMissed = 0;
    }

    // ------------------------------------------------------------------ crossings

    private void RecomputeDirty()
    {
        foreach (var h in _dirtyHouses) { h.Dirty = false; ComputeHouseEvents(h); }
        _dirtyHouses.Clear();
        foreach (var c in _dirtyChunks) { c.Dirty = false; ComputeChunkEvent(c); }
        _dirtyChunks.Clear();
    }

    /// <summary>Whole-millisecond delay for a duration in hours (ceil), at least 1 ms; -1 when unreachable.</summary>
    private static long CeilDelayMs(double hours)
    {
        double ms = hours * 3.6e6;
        if (!(ms < 9e15)) return -1;
        return Math.Max(1L, (long)Math.Ceiling(ms - CeilSlackMs));
    }

    private void ComputeHouseEvents(HouseState h)
    {
        h.Version++;
        long now = _nowMs;
        Settle(h, now);
        double r = _share * ElectricKw(h) - h.Demand.TotalKw[DemandHour(now)];
        // A crossing beyond the next hour start is dropped: every house is recomputed there.
        long limit = Math.Min(_in.EndMs, (FleetTime.HourOf(now) + 1L) * FleetTime.MsPerHour);
        double cap = h.Input.Spec.TankKwh, tank = h.Tank;
        if (r > 0 && tank < cap - TankEpsKwh)
        {
            long d = CeilDelayMs((cap - tank) / r);
            if (d > 0 && now + d <= limit) Push(now + d, EngineEventKind.TankFull, h.Version, cap, h, null, null);
            foreach (double th in h.Thresholds)
            {
                if (th <= tank + TankEpsKwh) continue;
                if (th >= cap - TankEpsKwh) break; // the cap is covered by TankFull
                long dt = CeilDelayMs((th - tank) / r);
                if (dt > 0 && now + dt <= limit) Push(now + dt, EngineEventKind.TankThreshold, h.Version, th, h, null, null);
                break;
            }
        }
        else if (r < 0 && tank > TankEpsKwh)
        {
            long d = CeilDelayMs(tank / -r);
            if (d > 0 && now + d <= limit) Push(now + d, EngineEventKind.TankEmpty, h.Version, 0, h, null, null);
            for (int i = h.Thresholds.Count - 1; i >= 0; i--)
            {
                double th = h.Thresholds[i];
                if (th >= tank - TankEpsKwh) continue;
                if (th <= TankEpsKwh) break; // empty is covered by TankEmpty
                long dt = CeilDelayMs((tank - th) / -r);
                if (dt > 0 && now + dt <= limit) Push(now + dt, EngineEventKind.TankThreshold, h.Version, th, h, null, null);
                break;
            }
        }
    }

    private void ComputeChunkEvent(ChunkState c)
    {
        c.Version++;
        if (!c.Attached) return;
        long now = _nowMs;
        Settle(c.ServerState.House, now);
        long delay = -1;
        switch (c.PhaseValue)
        {
            case ChunkPhase.Working:
                double rate = RateUnitsPerS(c);
                if (c.LoadValue > 0 && rate > 0)
                {
                    double ms = Math.Max(0, c.WorkUnits - c.Done) / rate * 1000.0;
                    if (ms < 9e15) delay = Math.Max(0L, (long)Math.Ceiling(ms - CeilSlackMs));
                }
                break;
            case ChunkPhase.Overhead:
                if (c.LoadValue > 0) delay = c.OverheadRemainingMsValue;
                break;
            case ChunkPhase.Checkpointing:
                delay = c.OverheadRemainingMsValue;
                break;
        }
        if (delay >= 0 && now + delay <= _in.EndMs)
            Push(now + delay, EngineEventKind.ChunkTimer, c.Version, 0, null, c, null);
    }

    // ------------------------------------------------------------------ hour log, end of run

    private void CloseHoursBefore(long t)
    {
        int target = Math.Min(FleetTime.HourOf(t), _runHours - 1);
        while (_closedHours < target) CloseHour(_closedHours++);
    }

    private void CloseHour(int hour)
    {
        int running = 0;
        foreach (var s in _servers) if (s.Chunk is { } c && c.LoadValue > 0) running++;
        var acc = _netHour[hour];
        _hourRows[hour] = new NetworkHourRow(hour, acc, acc.ElectricKwh / 1.0, running,
            _openCount[0], _openCount[1], Math.Max(0, _remainingKwh[0]), Math.Max(0, _remainingKwh[1]),
            _hArrivals[hour], _hRejections[hour], _hCompletions[hour], _hMisses[hour],
            _hT2Count[hour] > 0 ? _hT2QuoteSum[hour] / _hT2Count[hour] : null);
    }

    private void Finish()
    {
        _nowMs = _in.EndMs;
        foreach (var h in _houses) Settle(h, _in.EndMs);
        foreach (var s in _servers) if (s.Chunk is { } c) Keep(c, false);
        while (_closedHours < _runHours) CloseHour(_closedHours++);
        foreach (var h in _houses)
        {
            if (_in.Timeseries is TimeseriesMode.Daily or TimeseriesMode.Hourly)
            {
                if (h.PeriodIdx >= 0) FlushPeriod(h, _in.EndMs);
            }
            else if (_in.Timeseries == TimeseriesMode.Events && HasPending(h.Period))
                FlushPeriod(h, _in.EndMs, _in.EndMs);
        }
    }

    private static bool HasPending(EnergyAccount a) => a.Pauses != 0 || a.IncomeT1Chf != 0 || a.IncomeT2Chf != 0
        || a.JobsOnTime != 0 || a.DeadlinesMissed != 0;

    private FleetRunResult BuildResult(double wallS)
    {
        var houses = new HouseResult[_houses.Length];
        for (int i = 0; i < houses.Length; i++)
            houses[i] = new HouseResult(_houses[i].Input.Spec, _houses[i].Servers.Length, _houses[i].Demand.HeatingDays,
                _houses[i].Run, _houses[i].Tank);
        var jobs = new JobResult[_jobs.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            var j = _jobs[i];
            var r = j.RequestValue;
            string status = j.StatusValue switch { JobStatus.Done => "done", JobStatus.RejectedPrice => "rejected_price", _ => "open" };
            bool missed; double lateS;
            if (j.StatusValue == JobStatus.Done)
            {
                missed = j.FinishMs > r.DeadlineMs;
                lateS = Math.Max(0, (j.FinishMs!.Value - r.DeadlineMs) / 1000.0);
            }
            else if (j.StatusValue == JobStatus.RejectedPrice) { missed = false; lateS = 0; }
            else
            {
                missed = r.DeadlineMs < _in.EndMs;
                lateS = Math.Max(0, (_in.EndMs - r.DeadlineMs) / 1000.0);
            }
            jobs[i] = new JobResult(r, status, j.Quote, j.Accepted, j.FirstStartMs, j.FinishMs, missed, lateS,
                j.Chunks, j.Pauses, j.Stops, j.Moves, j.HousesUsed.Count, j.UnitsDone, j.UnitsLate, j.IncomeChf, j.EnergyKwh);
        }
        return new FleetRunResult
        {
            Houses = houses, Jobs = jobs, NetworkHours = _hourRows, HouseTimeseries = _timeseries, JobEvents = _jobEvents,
            EventCount = _eventCount, SchedulerCalls = _schedulerCalls, WallTimeS = wallS,
        };
    }
}
