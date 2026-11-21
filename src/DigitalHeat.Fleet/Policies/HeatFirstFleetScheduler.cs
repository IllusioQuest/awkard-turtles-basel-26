using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>Event-driven chunk adapter. Core owns heat load/rank and EDF order; the engine owns
/// work, physical tank settlement and lifecycle. A completion never reopens committed hourly heat.
/// Sites are ranked by free share of their hourly heat budget (decision 0056, Core HeatShareRank).</summary>
public sealed class HeatFirstFleetScheduler(SchedulerSection config) : IFleetScheduler
{
    private const double Epsilon = 1e-9;
    private IFleetView view = null!;
    private IFleetActions actions = null!;
    private readonly SortedSet<(JobPriority Rank, int Job)> queue = [];
    private readonly SortedSet<HeatShareRank> candidates = [];
    private HeatShareRank?[] ranks = [];
    private double[] budget = [], committed = [], spareLoad = [];
    private readonly Dictionary<int, double> chunkClaims = [];
    private int hour = -1;
    public string Id => "mesh-heat-first";

    public void Initialize(IFleetView view, IFleetActions actions)
    {
        this.view = view; this.actions = actions;
        int n = view.Houses.Count;
        ranks = new HeatShareRank?[n]; budget = new double[n]; committed = new double[n]; spareLoad = new double[n];
        foreach (var house in view.Houses)
            actions.RegisterTankThreshold(house.Id, house.TankCapacityKwh);
    }

    private static JobPriority Priority(IJobView job) => new(job.Request.DeadlineMs, job.Request.JobKey);
    private void UpdateQueue(IJobView job)
    {
        var key = (Priority(job), job.Id.Value);
        if (job.Status is JobStatus.Waiting or JobStatus.Active && job.Request.DeadlineMs > view.NowMs
            && job.UnassignedUnits > Epsilon && job.ActiveChunks.Count < job.Type.MaxChunks) queue.Add(key);
        else queue.Remove(key);
    }

    public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions actions)
    {
        this.view = view; this.actions = actions;
        bool newHour = hour != view.HourIndex;
        var affected = new HashSet<int>();
        foreach (var e in events)
        {
            if (e.Job is {} jid) UpdateQueue(view.Job(jid));
            // Completion events already contain job/house IDs; ended chunks have left the engine.
            if (e.Chunk is {} cid && e.House is null && e.Job is null)
            { var c = view.Chunk(cid); affected.Add(c.House.Value); UpdateQueue(view.Job(c.Job)); }
            if (e.House is {} hid) affected.Add(hid.Value);
            if (e.Server is {} sid) affected.Add(view.Servers[sid.Value].House.Value);
        }
        if (newHour)
        {
            hour = view.HourIndex; chunkClaims.Clear(); Array.Clear(committed);
            foreach (var house in view.Houses)
            {
                budget[house.Id.Value] = HeatCapacity.RemainingKwh(Signal(house));
                affected.Add(house.Id.Value);
            }
        }
        // Tick-only wakeups with no state changes do not scan houses or the pending queue.
        if (events.All(e => e.Kind is FleetEventKind.Tick or FleetEventKind.Wakeup) && !newHour) return;
        foreach (int hi in affected) Refresh(hi);
        if (candidates.Count == 0 || queue.Count == 0) return;
        foreach (var key in queue.ToArray())
        {
            if (candidates.Count == 0) break;
            var job = view.Job(new(key.Job));
            UpdateQueue(job);
            if (!queue.Contains(key)) continue;
            while (job.UnassignedUnits > Epsilon && job.ActiveChunks.Count < job.Type.MaxChunks)
            {
                // Decision 0056: prefer the best-ranked site that can run a server at full load, so
                // small budgets do not fragment work into low-load chunks; only then partial budgets.
                int best = -1, partial = -1;
                foreach (var rank in candidates.Reverse())
                {
                    int hi = int.Parse(rank.NodeId, System.Globalization.CultureInfo.InvariantCulture);
                    if (!Allowed(view.Houses[hi].Spec, job)
                        || spareLoad[hi] + Epsilon < Math.Max(Epsilon, job.Type.Lifecycle.MinLoad)) continue;
                    if (spareLoad[hi] + Epsilon >= 1) { best = hi; break; }
                    if (partial < 0) partial = hi;
                }
                if (best < 0) best = partial;
                if (best < 0) break;
                var house = view.Houses[best];
                var server = house.Servers.First(s => view.Servers[s.Value].Chunk is null);
                double rate = view.ServerParams.MaxElectricKw / (job.Type.EnergyKwhPerWorkUnit.Value * FleetTime.SecondsPerHour);
                double units = Math.Min(job.UnassignedUnits, Math.Max(job.Type.MinChunkWorkUnits, config.ChunkTargetS * rate));
                if (job.UnassignedUnits - units < job.Type.MinChunkWorkUnits) units = job.UnassignedUnits;
                double load = Math.Min(1, spareLoad[best]);
                var chunk = actions.Start(job.Id, server, units, load);
                Claim(best, chunk.Value, load);
                Refresh(best); UpdateQueue(job);
            }
        }
    }

    private HeatSignal Signal(IHouseView h) => new(h.DemandKwNow, h.TankKwh, h.TankCapacityKwh,
        Math.Max(1, h.Servers.Count), view.ServerParams.MaxElectricKw * view.ServerParams.HeatRecoveryShare);
    private double PerServerHeat => view.ServerParams.MaxElectricKw * view.ServerParams.HeatRecoveryShare;
    private void Claim(int hi, int chunkId, double load)
    {
        double next = load * PerServerHeat;
        double old = chunkClaims.GetValueOrDefault(chunkId);
        if (next <= old) return;
        committed[hi] += next - old; chunkClaims[chunkId] = next;
    }

    private void Refresh(int hi)
    {
        if (ranks[hi] is {} prior) candidates.Remove(prior);
        var house = view.Houses[hi];
        if (house.Servers.Count == 0) { ranks[hi] = null; return; }
        double desired = new HeatFirstScheduler(0).Decide(Signal(house)).TargetLoadPerServer * house.Servers.Count;
        var running = house.Servers.Where(s => view.Servers[s.Value].Chunk is not null)
            .Select(s => view.Chunk(view.Servers[s.Value].Chunk!.Value))
            .OrderBy(c => view.Job(c.Job).Type.Lifecycle.AllowPause ? 1 : 0)
            .ThenBy(c => Priority(view.Job(c.Job))).ThenBy(c => c.Id.Value).ToArray();
        foreach (var chunk in running)
        {
            var job = view.Job(chunk.Job);
            double load = chunk.Load;
            // Noninterruptible work/checkpoint overhead continues; its heat remains in engine balances.
            if (job.Type.Lifecycle.AllowPause && chunk.Phase != ChunkPhase.Checkpointing)
            {
                double previousClaim = chunkClaims.GetValueOrDefault(chunk.Id.Value) / PerServerHeat;
                double allowed = previousClaim + Math.Max(0, budget[hi] - committed[hi]) / PerServerHeat;
                load = Math.Min(1, Math.Min(Math.Max(0, desired), allowed));
                if (load < job.Type.Lifecycle.MinLoad || load < Epsilon) load = 0;
                if (Math.Abs(load - chunk.Load) > Epsilon) actions.SetLoad(chunk.Server, load);
            }
            Claim(hi, chunk.Id.Value, load); desired -= load;
        }
        spareLoad[hi] = Math.Max(0, Math.Min(desired, (budget[hi] - committed[hi]) / PerServerHeat));
        var rank = HeatShareRank.From(budget[hi], committed[hi], house.Spec.HouseId,
            hi.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ranks[hi] = rank;
        if (spareLoad[hi] > Epsilon && house.Servers.Any(s => view.Servers[s.Value].Chunk is null)) candidates.Add(rank);
    }

    private static bool Allowed(HouseSpec house, IJobView job) => job.Type.Placement.Mode switch
    {
        "origin_site" => job.Request.OriginKind != "client_site" || house.Canton == job.Request.OriginCanton,
        "country_allowlist" => job.Type.Placement.AllowedCountryCodes?.Contains("CH", StringComparer.OrdinalIgnoreCase) ?? true,
        _ => true
    };
}

