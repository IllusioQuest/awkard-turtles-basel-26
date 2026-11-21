namespace DigitalHeat.Fleet;

/// <summary>
/// Mock heat-led scheduler (docs/model/fleet-simulation.md §9.2): simple, deterministic and cheap.
/// It ignores origin (beyond the placement rule the engine enforces), latency, data sizes and forecasts.
/// <list type="number">
/// <item>Per house a mode: charge (all servers at full power) while the tank is below high_share × capacity,
/// hold (electric kW = demand ÷ heat_recovery_share, at most all servers) above it; hysteresis at
/// low_share × capacity, driven by registered tank thresholds.</item>
/// <item>Fill a house to its target power: running chunks first (queue order, loads set so the target is
/// hit exactly, the rest paused), then new chunks from the queue (Type 1 earliest deadline first, then
/// Type 2 earliest deadline first, ties by job id), sized chunk_target_s at load 1.</item>
/// <item>Urgent Type 1 jobs (slack below urgent_margin_s) run at load 1 regardless of heat on the first house
/// with a free server, else by stopping a checkpointable Type 2 chunk (set of victim houses). Evaluated once, when
/// the job becomes urgent; afterwards it is sticky, sorts first in the queue and its chunks stay at load 1.</item>
/// </list>
/// Cost per event is O(change): own sorted queue of open jobs with unassigned units, a set of houses with
/// a free server, a set of houses below their target (deficit) and a sorted set of Type 1 urgency checks.
/// HourStart re-evaluates every house.
/// </summary>
public sealed class MockHeatLedScheduler : IFleetScheduler
{
    private const double Eps = 1e-9;          // work units
    private const double LoadEps = 1e-12;     // load changes below this are ignored
    private const double RemainingKwEps = 1e-6;
    private const double MinPositiveLoad = 1e-3;   // for jobs that cannot pause and have min_load 0

    private readonly SchedulerSection _cfg;
    private IFleetView _v = null!;
    private IFleetActions _a = null!;
    private double _maxKw, _share;

    private bool[] _charge = [];
    private double[] _cap = [];

    // Open jobs with unassigned units that may start another chunk: (class 0 = T1 / 1 = T2, deadline ms, job id).
    // Key = (0 urgent / 1 not, class, deadline ms, job id): urgent jobs sort first.
    private readonly SortedSet<(int Urg, int Cls, long DeadlineMs, int Job)> _queue = [];
    // Houses holding a checkpointable Type 2 chunk that is not checkpointing yet (preemption victims).
    private readonly SortedSet<int> _victimHouses = [];
    private readonly SortedSet<int> _deficit = [];
    private readonly SortedSet<int> _freeHouses = [];
    private readonly SortedSet<(long DueMs, int Job)> _dueSet = [];
    private readonly Dictionary<int, long> _dueOf = [];
    private readonly HashSet<long> _wakeups = [];
    private readonly HashSet<int> _urgent = [];
    private readonly Dictionary<int, int> _reserved = [];   // server id -> urgent job id
    private readonly SortedSet<int> _eval = [];
    private readonly HashSet<int> _filled = [];

    public MockHeatLedScheduler(SchedulerSection cfg) { _cfg = cfg; }

    public string Id => "mock-heat-led";

    public void Initialize(IFleetView view, IFleetActions act)
    {
        _v = view; _a = act;
        _maxKw = view.ServerParams.MaxElectricKw;
        _share = view.ServerParams.HeatRecoveryShare;
        int n = view.Houses.Count;
        _charge = new bool[n];
        _cap = new double[n];
        for (int i = 0; i < n; i++)
        {
            var hv = view.Houses[i];
            _cap[i] = hv.TankCapacityKwh;
            if (_cap[i] > 0)
            {
                act.RegisterTankThreshold(hv.Id, _cfg.LowShare * _cap[i]);
                act.RegisterTankThreshold(hv.Id, _cfg.HighShare * _cap[i]);
            }
            _charge[i] = hv.TankKwh < _cfg.HighShare * _cap[i];
            if (hv.Servers.Count > 0) { _deficit.Add(i); _freeHouses.Add(i); }
        }
    }

    // ------------------------------------------------------------------ events

    public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions act)
    {
        _v = view; _a = act;
        bool all = false, arrived = false;
        _eval.Clear();
        _filled.Clear();

        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case FleetEventKind.HourStart:
                    all = true;
                    break;
                case FleetEventKind.JobArrived:
                    if (e.Job is JobId aj)
                    {
                        var job = view.Job(aj);
                        UpdateQueue(job);
                        if (job.Type.Class == JobClass.Type1) ScheduleInitialCheck(job);
                        arrived = true;
                    }
                    break;
                case FleetEventKind.TankFull:
                case FleetEventKind.TankEmpty:
                case FleetEventKind.TankThreshold:
                    if (e.House is HouseId th) { UpdateMode(th.Value); _eval.Add(th.Value); }
                    break;
                case FleetEventKind.JobCompleted:
                    if (e.Job is JobId cj) Forget(view.Job(cj));
                    AddHouseOf(e);
                    break;
                case FleetEventKind.ChunkFinished:
                    AddHouseOf(e);
                    if (JobOf(e) is IJobView fj) UpdateQueue(fj);
                    break;
                case FleetEventKind.OverheadDone:
                    if (e.Chunk is ChunkId oc && view.Chunk(oc).Phase == ChunkPhase.Stopped)
                    {
                        AddHouseOf(e);
                        UpdateQueue(view.Job(view.Chunk(oc).Job));   // checkpoint done: units are unassigned again
                    }
                    break;
            }
        }

        if (all) for (int i = 0; i < _charge.Length; i++) UpdateMode(i);

        ProcessReservations();
        ProcessDue();

        if (all)
        {
            for (int i = 0; i < _charge.Length; i++) Fill(i);
        }
        else
        {
            foreach (int hi in _eval.ToArray()) Fill(hi);
            if (arrived && _queue.Count > 0)
                foreach (int hi in _deficit.ToArray())
                {
                    if (_queue.Count == 0) break;
                    if (!_filled.Contains(hi)) Fill(hi);
                }
        }
    }

    private void AddHouseOf(in FleetEvent e)
    {
        int? h = e.House?.Value;
        if (h is null && e.Chunk is ChunkId c) h = _v.Chunk(c).House.Value;
        if (h is null && e.Server is ServerId s) h = _v.Servers[s.Value].House.Value;
        if (h is int hi) { _eval.Add(hi); RefreshFree(hi); }
    }

    private IJobView? JobOf(in FleetEvent e)
    {
        if (e.Job is JobId j) return _v.Job(j);
        if (e.Chunk is ChunkId c) return _v.Job(_v.Chunk(c).Job);
        return null;
    }

    private void UpdateMode(int hi)
    {
        var hv = _v.Houses[hi];
        double tank = hv.TankKwh;
        if (tank >= _cfg.HighShare * _cap[hi] - 1e-9) _charge[hi] = false;
        else if (tank <= _cfg.LowShare * _cap[hi] + 1e-9) _charge[hi] = true;
    }

    private double Target(int hi)
    {
        var hv = _v.Houses[hi];
        double all = hv.Servers.Count * _maxKw;
        return _charge[hi] ? all : Math.Min(all, hv.DemandKwNow / _share);
    }

    // ------------------------------------------------------------------ queue

    private (int, int, long, int) Key(IJobView j) =>
        (_urgent.Contains(j.Id.Value) ? 0 : 1, j.Type.Class == JobClass.Type1 ? 0 : 1, j.Request.DeadlineMs, j.Id.Value);

    private void UpdateQueue(IJobView job)
    {
        bool want = job.Status is JobStatus.Waiting or JobStatus.Active
            && job.UnassignedUnits > Eps && job.ActiveChunks.Count < job.Type.MaxChunks;
        if (want) _queue.Add(Key(job)); else _queue.Remove(Key(job));
    }

    private void Forget(IJobView job)
    {
        _queue.Remove(Key(job));
        _urgent.Remove(job.Id.Value);
        if (_dueOf.Remove(job.Id.Value, out long due)) _dueSet.Remove((due, job.Id.Value));
    }

    private static bool Allowed(HouseSpec spec, IJobView job)
    {
        var p = job.Type.Placement;
        switch (p.Mode)
        {
            case "origin_site":
                return job.Request.OriginKind != "client_site"
                    || (spec.Canton is not null && spec.Canton == job.Request.OriginCanton);
            case "country_allowlist":
                // All houses are Swiss (§4.1 has no country column).
                return p.AllowedCountryCodes is null
                    || p.AllowedCountryCodes.Contains("CH", StringComparer.OrdinalIgnoreCase);
            default:
                return true;
        }
    }

    private ServerId? FreeServer(IHouseView hv)
    {
        foreach (var sid in hv.Servers)
            if (_v.Servers[sid.Value].Chunk is null && !_reserved.ContainsKey(sid.Value)) return sid;
        return null;
    }

    private void RefreshFree(int hi)
    {
        if (FreeServer(_v.Houses[hi]) is null) _freeHouses.Remove(hi); else _freeHouses.Add(hi);
        bool victim = false;
        foreach (var sid in _v.Houses[hi].Servers)
        {
            if (_v.Servers[sid.Value].Chunk is not ChunkId cid) continue;
            var c = _v.Chunk(cid);
            if (c.Phase == ChunkPhase.Checkpointing) continue;
            var j = _v.Job(c.Job);
            if (j.Type.Class == JobClass.Type2 && j.Type.Lifecycle.Checkpointable) { victim = true; break; }
        }
        if (victim) _victimHouses.Add(hi); else _victimHouses.Remove(hi);
    }

    private double ChunkUnits(IJobView job)
    {
        double rate1 = _maxKw / (job.Type.EnergyKwhPerWorkUnit.Value * FleetTime.SecondsPerHour);   // units/s at load 1
        double min = job.Type.MinChunkWorkUnits;
        double u = Math.Min(job.UnassignedUnits, Math.Max(min, _cfg.ChunkTargetS * rate1));
        double left = job.UnassignedUnits - u;
        return left > Eps && left < min ? job.UnassignedUnits : u;
    }

    // ------------------------------------------------------------------ filling a house

    private void Fill(int hi)
    {
        _filled.Add(hi);
        var hv = _v.Houses[hi];
        if (hv.Servers.Count == 0) { _deficit.Remove(hi); _freeHouses.Remove(hi); return; }

        double rem = Target(hi);

        var running = new List<(IChunkView Chunk, IJobView Job)>(hv.Servers.Count);
        foreach (var sid in hv.Servers)
            if (_v.Servers[sid.Value].Chunk is ChunkId cid)
            {
                var c = _v.Chunk(cid);
                running.Add((c, _v.Job(c.Job)));
            }
        running.Sort((x, y) =>
        {
            int ux = _urgent.Contains(x.Job.Id.Value) ? 0 : 1, uy = _urgent.Contains(y.Job.Id.Value) ? 0 : 1;
            int c = ux.CompareTo(uy);
            if (c == 0) c = Key(x.Job).CompareTo(Key(y.Job));
            return c != 0 ? c : x.Chunk.Id.Value.CompareTo(y.Chunk.Id.Value);
        });

        foreach (var (c, job) in running)
        {
            if (c.Phase == ChunkPhase.Checkpointing) { rem -= c.Load * _maxKw; continue; }
            var lc = job.Type.Lifecycle;
            double desired = _urgent.Contains(job.Id.Value) ? 1.0 : Math.Min(1.0, Math.Max(0.0, rem) / _maxKw);
            if (desired < Math.Max(lc.MinLoad, LoadEps))
                desired = lc.AllowPause ? 0.0 : Math.Max(lc.MinLoad, MinPositiveLoad);
            if (Math.Abs(desired - c.Load) > LoadEps) _a.SetLoad(c.Server, desired);
            rem -= desired * _maxKw;
        }

        while (FreeServer(hv) is ServerId free)
        {
            double load = Math.Min(1.0, rem / _maxKw);
            IJobView? pick = null;
            bool urgentPick = false;
            foreach (var key in _queue)
            {
                if (key.Urg != 0 && rem <= RemainingKwEps) break;   // only urgent jobs run without heat demand
                var job = _v.Job(new JobId(key.Job));
                if (job.UnassignedUnits <= Eps || job.ActiveChunks.Count >= job.Type.MaxChunks) continue;
                if (!Allowed(hv.Spec, job)) continue;
                if (key.Urg == 0) { pick = job; urgentPick = true; break; }
                if (load < job.Type.Lifecycle.MinLoad - LoadEps) continue;
                pick = job;
                break;
            }
            if (pick is null) break;
            if (urgentPick) load = 1.0;
            _a.Start(pick.Id, free, ChunkUnits(pick), load);
            UpdateQueue(pick);
            rem -= load * _maxKw;
        }

        if (rem > RemainingKwEps && FreeServer(hv) is not null) _deficit.Add(hi); else _deficit.Remove(hi);
        RefreshFree(hi);
    }

    // ------------------------------------------------------------------ urgent Type 1

    private double Rate1(IJobView job) => _maxKw / (job.Type.EnergyKwhPerWorkUnit.Value * FleetTime.SecondsPerHour);

    private double CurrentRate(IJobView job)
    {
        double sum = 0;
        foreach (var cid in job.ActiveChunks) sum += _v.Chunk(cid).UnitsPerSecond;
        return sum > 1e-15 ? sum : Rate1(job);
    }

    private long NextHourMs() => (_v.NowMs / FleetTime.MsPerHour + 1) * FleetTime.MsPerHour;

    private void ScheduleInitialCheck(IJobView job)
    {
        long due = job.Request.DeadlineMs - (long)(_cfg.UrgentMarginS * 1000)
            - (long)Math.Ceiling(job.RemainingUnits / Rate1(job) * 1000);
        ScheduleDue(job, due, _v.NowMs);
    }

    /// <summary>Next check of an open Type 1 job at due (clamped to [minDueMs, next hour start]); HourStart checks too.</summary>
    private void ScheduleDue(IJobView job, long dueMs, long minDueMs)
    {
        long next = NextHourMs();
        dueMs = Math.Min(Math.Max(dueMs, minDueMs), next);
        if (_dueOf.Remove(job.Id.Value, out long old)) _dueSet.Remove((old, job.Id.Value));
        _dueSet.Add((dueMs, job.Id.Value));
        _dueOf[job.Id.Value] = dueMs;
        if (dueMs > _v.NowMs && dueMs < next && _wakeups.Add(dueMs)) _a.RequestWakeup(dueMs);
    }

    private void ProcessDue()
    {
        while (_dueSet.Count > 0 && _dueSet.Min.DueMs <= _v.NowMs)
        {
            var (due, jid) = _dueSet.Min;
            _dueSet.Remove((due, jid));
            _dueOf.Remove(jid);
            var job = _v.Job(new JobId(jid));
            if (job.Status is not (JobStatus.Waiting or JobStatus.Active)) { _queue.Remove(Key(job)); _urgent.Remove(jid); continue; }
            EvaluateUrgent(job);
        }
    }

    private void EvaluateUrgent(IJobView job)
    {
        long now = _v.NowMs;
        double rate = CurrentRate(job);
        double slackS = (job.Request.DeadlineMs - now) / 1000.0 - job.RemainingUnits / rate;
        // Slack exactly at the margin (the time a wakeup was requested for) counts as urgent; 1 µs tolerance for float noise.
        if (slackS > _cfg.UrgentMarginS + 1e-6 && !_urgent.Contains(job.Id.Value))
        {
            long due = job.Request.DeadlineMs - (long)(_cfg.UrgentMarginS * 1000)
                - (long)Math.Ceiling(job.RemainingUnits / rate * 1000);
            ScheduleDue(job, due, now + 1000);
            return;
        }

        // Evaluated for preemption once, now. From here on the job is sticky urgent and served through the queue.
        _queue.Remove(Key(job));
        _urgent.Add(job.Id.Value);
        foreach (var cid in job.ActiveChunks) _eval.Add(_v.Chunk(cid).House.Value);   // raise their loads to 1
        if (job.UnassignedUnits > Eps && job.ActiveChunks.Count < job.Type.MaxChunks && StartUrgent(job) is int hi)
            _eval.Add(hi);
        UpdateQueue(job);
    }

    /// <summary>Starts an urgent chunk at load 1; returns the house, or null if it must wait (a server was reserved or none found).</summary>
    private int? StartUrgent(IJobView job)
    {
        double units = ChunkUnits(job);

        int found = -1;
        foreach (int hi in _freeHouses)
            if (Allowed(_v.Houses[hi].Spec, job)) { found = hi; break; }
        if (found >= 0)
        {
            var hv = _v.Houses[found];
            _a.Start(job.Id, FreeServer(hv)!.Value, units, 1.0);
            UpdateQueue(job);
            RefreshFree(found);
            return found;
        }

        // No free server: stop a checkpointable Type 2 chunk on the first such house by id.
        foreach (int hi in _victimHouses)
        {
            var hv = _v.Houses[hi];
            if (!Allowed(hv.Spec, job)) continue;
            foreach (var sid in hv.Servers)
            {
                if (_v.Servers[sid.Value].Chunk is not ChunkId cid) continue;
                var c = _v.Chunk(cid);
                if (c.Phase == ChunkPhase.Checkpointing) continue;
                var victim = _v.Job(c.Job);
                if (victim.Type.Class != JobClass.Type2 || !victim.Type.Lifecycle.Checkpointable) continue;

                _a.Stop(cid);
                UpdateQueue(victim);
                if (_v.Servers[sid.Value].Chunk is null)
                {
                    _a.Start(job.Id, sid, units, 1.0);
                    UpdateQueue(job);
                    RefreshFree(hi);
                    return hi;
                }
                _reserved[sid.Value] = job.Id.Value;   // started when the checkpoint is done
                RefreshFree(hi);
                return null;
            }
        }
        return null;
    }

    private void ProcessReservations()
    {
        if (_reserved.Count == 0) return;
        foreach (var (server, jid) in _reserved.ToArray())
        {
            if (_v.Servers[server].Chunk is not null) continue;
            _reserved.Remove(server);
            var job = _v.Job(new JobId(jid));
            int hi = _v.Servers[server].House.Value;
            if (job.Status is JobStatus.Waiting or JobStatus.Active
                && job.UnassignedUnits > Eps && job.ActiveChunks.Count < job.Type.MaxChunks
                && Allowed(_v.Houses[hi].Spec, job))
            {
                _a.Start(job.Id, new ServerId(server), ChunkUnits(job), 1.0);
                UpdateQueue(job);
            }
            _eval.Add(hi);
            RefreshFree(hi);
        }
    }
}
