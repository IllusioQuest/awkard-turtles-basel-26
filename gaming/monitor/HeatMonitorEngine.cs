using DigitalHeat.Core;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>Static facts of one heat site, taken from the network scenario (sourced there).</summary>
public sealed record MonitorSite(string Id, string Name, string CountryCode, double ServerCount,
    double RecoverableKwhPerServerHour, double TankCapacityKwh);

public sealed record MonitorNodeInfo(string NodeId, string SiteId, int SessionSlots);

/// <summary>Inputs of one site for one hour, with a label saying where each number came from.</summary>
public sealed record SiteHour(DemandHour Demand, string DemandSource, double? TemperatureC,
    double ElectricityPriceChfPerKwh, double HeatValueChfPerKwh, string PriceSource, string? TemperatureSource = null);

/// <summary>One aligned hour for all monitored sites. <c>Mode</c> is "replay" or "live".</summary>
public sealed record HourInputs(string Mode, string LocalLabel, DateTimeOffset IntervalStartUtc,
    IReadOnlyDictionary<string, SiteHour> Sites);

/// <summary>Cumulative per-site figures since the run (re)started, all kWh.</summary>
public sealed record SiteTotals(int Hours, double DemandKwh, double ProducedKwh, double UsefulKwh,
    double DumpedKwh, double BackupKwh)
{
    public static readonly SiteTotals Zero = new(0, 0, 0, 0, 0, 0);
    public SiteTotals Add(double demandKwh, double producedKwh, HeatBalanceStep s) => new(Hours + 1,
        DemandKwh + demandKwh, ProducedKwh + producedKwh, UsefulKwh + s.DirectUsedKwh + s.TankChargeKwh,
        DumpedKwh + s.DumpedHeatKwh, BackupKwh + s.BackupHeatKwh);
}

/// <summary>Tank fill and totals per site: enough to restore a run at an earlier hour.</summary>
public sealed record EngineMemory(IReadOnlyDictionary<string, double> TankStoredKwh,
    IReadOnlyDictionary<string, SiteTotals> Totals);

/// <summary>
/// What a site will do in the current hour if the held sessions keep running: the preview is
/// Core <see cref="HeatBalance.Step"/>, free capacity is Core <see cref="HeatCapacity.RemainingKwh"/>.
/// </summary>
public sealed record SiteView(MonitorSite Site, SiteHour Hour, double TankStoredKwh, double GenerationKwh,
    double FreeCapacityKwh, HeatBalanceStep Preview, double UsefulValueChfPerHour, SiteTotals Totals);

public sealed record NodeView(MonitorNodeInfo Node, bool Ready, IReadOnlyList<string> Sessions,
    double GenerationKwh, double SiteHeatNeedKwh, double SiteFreeCapacityKwh, double UsefulValueChfPerHour);

/// <summary>
/// T-42 heat monitor state. Wraps the existing gaming <see cref="SessionPlacement"/> (no second
/// placement rule) and advances each site's tank with Core <see cref="HeatBalance.Step"/> (no second
/// heat model). Generation = sum of reservations held on a node; nothing here launches a game.
/// Pure: no I/O, no clock. Not thread-safe; call from one thread.
/// </summary>
public sealed class HeatMonitorEngine
{
    private readonly MonitorSite[] sites;
    private readonly MonitorNodeInfo[] nodes;
    private readonly string originSiteId;
    private readonly JobPlacementPolicy policy;
    private readonly Dictionary<string, double> tanks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SiteTotals> totals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> ready = new(StringComparer.Ordinal);
    private readonly List<string> order = new();
    private SessionPlacement placement = new();
    private long revision;
    private int nextPlayer = 1;

    public HeatMonitorEngine(IEnumerable<MonitorSite> sites, IEnumerable<MonitorNodeInfo> nodes,
        double sessionHeatKwhPerHour, string originSiteId, JobPlacementPolicy policy)
    {
        this.sites = sites.ToArray();
        this.nodes = nodes.ToArray();
        if (this.sites.Length == 0 || this.nodes.Length == 0) throw new ArgumentException("Need at least one site and one node.");
        if (this.sites.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != this.sites.Length)
            throw new ArgumentException("Duplicate site ids.");
        if (this.nodes.Any(n => this.sites.All(s => s.Id != n.SiteId)))
            throw new ArgumentException("Every node must sit at a monitored site.");
        if (this.nodes.Select(n => n.NodeId).Distinct(StringComparer.Ordinal).Count() != this.nodes.Length)
            throw new ArgumentException("Duplicate node ids.");
        // Same preconditions as Core HeatCapacity.RemainingKwh, checked once up front.
        if (this.sites.Any(s => !(s.ServerCount > 0) || !(s.RecoverableKwhPerServerHour > 0)
                || !double.IsFinite(s.ServerCount) || !double.IsFinite(s.RecoverableKwhPerServerHour)
                || !double.IsFinite(s.TankCapacityKwh) || s.TankCapacityKwh < 0))
            throw new ArgumentException("Sites need positive servers and recoverable heat, and a non-negative tank.");
        if (string.IsNullOrWhiteSpace(originSiteId)) throw new ArgumentException("Missing player origin site.");
        if (!double.IsFinite(sessionHeatKwhPerHour) || sessionHeatKwhPerHour <= 0)
            throw new ArgumentOutOfRangeException(nameof(sessionHeatKwhPerHour));
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        SessionHeatKwhPerHour = sessionHeatKwhPerHour;
        this.originSiteId = originSiteId;
        this.policy = policy;
        foreach (var s in this.sites) { tanks[s.Id] = 0; totals[s.Id] = SiteTotals.Zero; }
        foreach (var n in this.nodes) ready[n.NodeId] = false;
    }

    public double SessionHeatKwhPerHour { get; }
    public HourInputs? Current { get; private set; }
    public IReadOnlyList<string> Sessions => order;
    public IReadOnlyList<MonitorSite> Sites => sites;

    public EngineMemory Memory => new(new Dictionary<string, double>(tanks, StringComparer.Ordinal),
        new Dictionary<string, SiteTotals>(totals, StringComparer.Ordinal));

    /// <summary>
    /// (Re)starts at an hour, e.g. after a jump or a step back. SessionPlacement never goes back in
    /// time, so a fresh one is built and the same players are placed again in their original order.
    /// Returns the players that no longer fit at this hour (their reservation is dropped).
    /// </summary>
    public IReadOnlyList<string> Begin(HourInputs inputs, EngineMemory? memory = null)
    {
        CheckInputs(inputs);
        foreach (var s in sites)
        {
            double stored = memory?.TankStoredKwh.GetValueOrDefault(s.Id) ?? 0;
            tanks[s.Id] = Math.Clamp(stored, 0, s.TankCapacityKwh);
            totals[s.Id] = memory?.Totals.GetValueOrDefault(s.Id) ?? SiteTotals.Zero;
        }
        Current = inputs;
        placement = new SessionPlacement();
        revision = 0;
        Publish();
        var players = order.ToArray();
        order.Clear();
        var dropped = new List<string>();
        foreach (var id in players)
            if (Reserve(id) is null) dropped.Add(id);
        return dropped;
    }

    /// <summary>
    /// Closes the current hour with Core HeatBalance.Step (held sessions produce heat for the whole
    /// hour) and moves to the next one. Held reservations stay on their node.
    /// </summary>
    public IReadOnlyDictionary<string, HeatBalanceStep> Advance(HourInputs next)
    {
        var current = Current ?? throw new InvalidOperationException("Call Begin first.");
        CheckInputs(next);
        if (next.IntervalStartUtc <= current.IntervalStartUtc)
            throw new InvalidOperationException("Advance only moves forward; use Begin to go back.");
        var steps = new Dictionary<string, HeatBalanceStep>(StringComparer.Ordinal);
        foreach (var s in sites)
        {
            var demand = current.Sites[s.Id].Demand.TotalHeatKwh;
            double produced = GenerationAtSite(s.Id);
            var step = HeatBalance.Step(produced, demand, tanks[s.Id], s.TankCapacityKwh);
            tanks[s.Id] = Math.Clamp(step.TankStoredKwh, 0, s.TankCapacityKwh);
            totals[s.Id] = totals[s.Id].Add(demand, produced, step);
            steps[s.Id] = step;
        }
        Current = next;
        Publish();
        return steps;
    }

    /// <summary>Changes a node's readiness (e.g. from Docker). Existing reservations are kept.</summary>
    public void SetReady(string nodeId, bool isReady)
    {
        if (!ready.ContainsKey(nodeId)) throw new ArgumentException($"Unknown node '{nodeId}'.");
        if (ready[nodeId] == isReady) return;
        ready[nodeId] = isReady;
        if (Current is not null) Publish();
    }

    public bool IsReady(string nodeId) => ready.GetValueOrDefault(nodeId);

    /// <summary>Asks the existing heat-capacity placement for one new player session.</summary>
    public SessionReservation? Reserve(string? sessionId = null)
    {
        var current = Current ?? throw new InvalidOperationException("Call Begin first.");
        if (sessionId is null)
        {
            do sessionId = $"player-{nextPlayer++}"; while (order.Contains(sessionId));
        }
        var request = new SessionRequest(sessionId, originSiteId, policy, SessionHeatKwhPerHour);
        var reservation = placement.TryReserve(request, current.IntervalStartUtc);
        if (reservation is not null && !order.Contains(sessionId)) order.Add(sessionId);
        return reservation;
    }

    public bool Release(string sessionId)
    {
        bool removed = placement.Release(sessionId);
        order.Remove(sessionId);
        return removed;
    }

    public string? ReleaseLast()
    {
        if (order.Count == 0) return null;
        var id = order[^1];
        Release(id);
        return id;
    }

    public IReadOnlyList<SiteView> SiteViews()
    {
        var current = Current ?? throw new InvalidOperationException("Call Begin first.");
        return sites.Select(s =>
        {
            var hour = current.Sites[s.Id];
            double generation = GenerationAtSite(s.Id);
            var preview = HeatBalance.Step(generation, hour.Demand.TotalHeatKwh, tanks[s.Id], s.TankCapacityKwh);
            double free = HeatCapacity.RemainingKwh(Signal(s, hour), generation);
            double value = (preview.DirectUsedKwh + preview.TankChargeKwh) * hour.HeatValueChfPerKwh;
            return new SiteView(s, hour, tanks[s.Id], generation, free, preview, value, totals[s.Id]);
        }).ToArray();
    }

    public IReadOnlyList<NodeView> NodeViews()
    {
        var bySite = SiteViews().ToDictionary(v => v.Site.Id, StringComparer.Ordinal);
        var held = placement.Reservations();
        return nodes.Select(n =>
        {
            var mine = held.Where(r => r.NodeId == n.NodeId).ToArray();
            double generation = mine.Sum(r => r.RecoverableHeatKwhPerHour);
            var site = bySite[n.SiteId];
            // Useful value is shared over the site's sessions in proportion to their heat.
            double share = site.GenerationKwh > 0 ? generation / site.GenerationKwh : 0;
            var sessions = order.Where(id => mine.Any(r => r.SessionId == id)).ToArray();
            return new NodeView(n, ready[n.NodeId], sessions, generation, site.Hour.Demand.TotalHeatKwh,
                site.FreeCapacityKwh, site.UsefulValueChfPerHour * share);
        }).ToArray();
    }

    private double GenerationAtSite(string siteId) =>
        placement.Reservations().Where(r => r.SiteId == siteId).Sum(r => r.RecoverableHeatKwhPerHour);

    private HeatSignal Signal(MonitorSite s, SiteHour hour) => new(hour.Demand.TotalHeatKwh, tanks[s.Id],
        s.TankCapacityKwh, s.ServerCount, s.RecoverableKwhPerServerHour);

    private void Publish()
    {
        var current = Current!;
        revision++;
        var gamingSites = sites.Select(s => new GamingSite(s.Id, s.CountryCode, Signal(s, current.Sites[s.Id]), 0)).ToArray();
        // Network eligibility is not probed in this lab (all nodes on one laptop): true, as in the demo.
        var gamingNodes = nodes.Select(n => new GamingNode(n.NodeId, n.SiteId, ready[n.NodeId], true, n.SessionSlots)).ToArray();
        var snapshot = new GamingSnapshot(revision, current.IntervalStartUtc, current.IntervalStartUtc.AddHours(1),
            $"T-42 heat monitor, {current.Mode} {current.LocalLabel}", gamingSites, gamingNodes);
        placement.Update(snapshot, current.IntervalStartUtc);
    }

    private void CheckInputs(HourInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.IntervalStartUtc.Offset != TimeSpan.Zero || inputs.IntervalStartUtc.Ticks % TimeSpan.TicksPerHour != 0)
            throw new ArgumentException("Interval start must be a whole UTC hour.");
        foreach (var s in sites)
            if (!inputs.Sites.TryGetValue(s.Id, out var h) || h is null || !double.IsFinite(h.Demand.TotalHeatKwh)
                || h.Demand.TotalHeatKwh < 0 || !double.IsFinite(h.HeatValueChfPerKwh) || !double.IsFinite(h.ElectricityPriceChfPerKwh))
                throw new ArgumentException($"Missing or invalid hour inputs for site '{s.Id}'.");
    }
}
