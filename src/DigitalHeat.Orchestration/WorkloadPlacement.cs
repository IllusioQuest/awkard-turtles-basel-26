using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

/// <summary>One atomic heat/resource admission path for real runtimes, tests and the legacy gaming adapter.</summary>
public sealed class WorkloadPlacement
{
    private readonly object gate = new();
    private WorkloadInventory? inventory;
    private AdmissionIndex? index;
    private readonly Dictionary<string, AllocationView> held = new(StringComparer.Ordinal);
    private const int HoursPerInterval = 1;

    public void Update(WorkloadInventory next, DateTimeOffset nowUtc)
    {
        var copy = WorkloadJson.Copy(next);
        ValidateInventory(copy, nowUtc);
        var nextIndex = new AdmissionIndex(copy);
        lock (gate)
        {
            if (inventory is {} old && (copy.Revision <= old.Revision || copy.IntervalStartUtc < old.IntervalStartUtc))
                throw new InvalidDataException("Cannot regress inventory revision or hour.");
            foreach (var claim in held.Values)
            {
                nextIndex.NodesById.TryGetValue(claim.Reservation.NodeId, out var node);
                if (node is not null && (node.SiteId != claim.Reservation.SiteId || node.DomainId != claim.Reservation.DomainId))
                    throw new InvalidDataException("Cannot move an allocated node to another site/resource domain.");
                if (nextIndex.ExternalIds.Contains(claim.Request.Id))
                    throw new InvalidDataException("Owned allocation counted again as external.");
            }
            inventory = copy; // Missing nodes retain their claims; disappearance never proves release.
            index = nextIndex;
        }
    }

    public WorkloadReservation? TryReserve(WorkloadRequest request, DateTimeOffset nowUtc)
        => TryReserveCore(request, nowUtc, null);

    /// <summary>
    /// Admit only at the site selected by the network scheduler. The admission layer may choose
    /// an eligible node within that site, but never redirects work to another site.
    /// </summary>
    public WorkloadReservation? TryReserveAtSite(WorkloadRequest request, string selectedSiteId,
        HeatSignal expectedHeatSignal, long expectedInventoryRevision, DateTimeOffset expectedHourStartUtc, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(selectedSiteId)) throw new ArgumentException("A selected site is required.", nameof(selectedSiteId));
        ArgumentNullException.ThrowIfNull(expectedHeatSignal);
        return TryReserveCore(request, nowUtc, selectedSiteId, expectedInventoryRevision, expectedHourStartUtc, expectedHeatSignal);
    }

    public bool MatchesSnapshot(string siteId, HeatSignal signal, long revision, DateTimeOffset hourStartUtc, DateTimeOffset nowUtc)
    { lock (gate) return MatchesSnapshotUnsafe(siteId, signal, revision, hourStartUtc, nowUtc); }

    private bool MatchesSnapshotUnsafe(string siteId, HeatSignal signal, long revision, DateTimeOffset hourStartUtc, DateTimeOffset nowUtc)
        => inventory is { } i && i.Revision == revision && i.IntervalStartUtc == hourStartUtc && Fresh(nowUtc)
            && i.Sites.Any(s => s.Id == siteId && s.Heat == signal);

    private WorkloadReservation? TryReserveCore(WorkloadRequest request, DateTimeOffset nowUtc, string? selectedSiteId,
        long? expectedRevision = null, DateTimeOffset? expectedHourStartUtc = null, HeatSignal? expectedHeatSignal = null)
    {
        request = WorkloadJson.Copy(request);
        ValidateRequest(request);
        lock (gate)
        {
            if (expectedRevision is not null && !MatchesSnapshotUnsafe(selectedSiteId!, expectedHeatSignal!, expectedRevision.Value, expectedHourStartUtc!.Value, nowUtc)) return null;
            if (held.TryGetValue(request.Id, out var existing))
            {
                if (WorkloadJson.Identity(existing.Request) != WorkloadJson.Identity(request))
                    throw new InvalidDataException("Workload ID reused with changed parameters.");
                if (selectedSiteId is not null && existing.Reservation.SiteId != selectedSiteId)
                    throw new InvalidOperationException("Existing workload claim is at a different site than the network decision.");
                return existing.Reservation;
            }
            if (!DeadlineOpen(request, nowUtc) || !Fresh(nowUtc) || request.Kind != "legacy" && !ResourcesFresh(nowUtc)) return null;
            if (index!.ExternalIds.Contains(request.Id))
                throw new InvalidDataException("Workload ID is already externally allocated.");
            WorkloadReservation? best = null;
            HeatShareRank bestRank = default;
            // Heat is the ranking priority (decision 0056: free share of the site's hourly heat budget,
            // then free kWh, then IDs). Latency is only a configured eligibility limit in Candidates;
            // a faster eligible node must not outrank one with more useful heat.
            var budgets = inventory!.Sites.ToDictionary(s => s.Id, s => s.Heat, StringComparer.Ordinal);
            foreach (var candidate in Candidates(request))
            {
                if (selectedSiteId is not null && candidate.SiteId != selectedSiteId) continue;
                double budget = HeatCapacity.RemainingKwh(budgets[candidate.SiteId]);
                var rank = HeatShareRank.From(budget, Math.Max(0, budget - candidate.AvailableHeatKwhBeforeReservation),
                    candidate.SiteId, candidate.NodeId);
                if (best is null || rank.CompareTo(bestRank) > 0) { best = candidate; bestRank = rank; }
            }
            if (best is not null) held.Add(request.Id, new(request, best, false));
            return best;
        }
    }

    public AllocationView[] Allocations() { lock (gate) return WorkloadJson.Copy(held.Values.ToArray()); }

    public bool CanAdmitAfterBatchPause(WorkloadRequest request, DateTimeOffset nowUtc)
    {
        request = WorkloadJson.Copy(request);
        ValidateRequest(request);
        lock (gate)
        {
            if (!DeadlineOpen(request, nowUtc) || !Fresh(nowUtc) || !ResourcesFresh(nowUtc)) return false;
            var original = held.ToArray();
            try
            {
                foreach (var pair in original.Where(p => p.Value.Request.Kind == "batch" && p.Value.Request.Lifecycle.AllowPause))
                    held[pair.Key] = pair.Value with { Paused = true };
                return Candidates(request).Any();
            }
            finally { foreach (var pair in original) held[pair.Key] = pair.Value; }
        }
    }

    /// <summary>Caller must observe a paused owned batch container first. RAM/GPU/slot remain held.</summary>
    public void ObservePaused(string id)
    {
        lock (gate)
        {
            var claim = held[id];
            if (claim.Request.Kind != "batch" || !claim.Request.Lifecycle.AllowPause)
                throw new InvalidOperationException("Only owner-authorized batch work can be paused.");
            held[id] = claim with { Paused = true };
        }
    }

    /// <summary>Reconciliation must retain externally resumed resource ownership, even above current capacity.</summary>
    public void ObserveRunning(string id) { lock (gate) held[id] = held[id] with { Paused = false }; }

    /// <summary>Reclaims CPU and heat before unpausing; on uncertain resume retain the full claim.</summary>
    public bool TryClaimResume(string id, DateTimeOffset nowUtc)
    {
        lock (gate)
        {
            var claim = held[id];
            if (!DeadlineOpen(claim.Request, nowUtc)) return false;
            if (!claim.Paused) return true;
            if (!Fresh(nowUtc) || !ResourcesFresh(nowUtc)) return false;
            if (!Candidates(claim.Request, id).Any(c => c.NodeId == claim.Reservation.NodeId && c.GpuDeviceId == claim.Reservation.GpuDeviceId)) return false;
            held[id] = claim with { Paused = false };
            return true;
        }
    }

    public bool CanStartHeld(string id, DateTimeOffset nowUtc)
    {
        lock (gate)
        {
            if (!Fresh(nowUtc) || !ResourcesFresh(nowUtc)) return false;
            var claim = held[id];
            return DeadlineOpen(claim.Request, nowUtc) && Candidates(claim.Request, id).Any(c => c.NodeId == claim.Reservation.NodeId && c.GpuDeviceId == claim.Reservation.GpuDeviceId);
        }
    }

    /// <summary>Runtime boundary: only confirmed terminal containers or never-dispatched reservations may release.</summary>
    public bool Release(string id) { lock (gate) return held.Remove(id); }

    /// <summary>Restores conservative ownership from a local journal before runtime reconciliation.</summary>
    public void Restore(IEnumerable<AllocationView> claims)
    {
        lock (gate)
        {
            if (held.Count != 0) throw new InvalidOperationException("Restore only into an empty ledger.");
            var copy = WorkloadJson.Copy(claims.ToArray());
            if (copy.Select(c => c.Request.Id).Distinct().Count() != copy.Length) throw new InvalidDataException("Duplicate journal claims.");
            foreach (var c in copy)
            {
                ValidateRequest(c.Request);
                var r = c.Reservation;
                if (r.WorkloadId != c.Request.Id || string.IsNullOrWhiteSpace(r.NodeId) || string.IsNullOrWhiteSpace(r.DomainId)
                    || string.IsNullOrWhiteSpace(r.SiteId) || r.RecoverableHeatKwhPerHour != c.Request.RecoverableHeatKwhPerHour
                    || !double.IsFinite(r.AvailableHeatKwhBeforeReservation) || r.AvailableHeatKwhBeforeReservation < 0)
                    throw new InvalidDataException("Invalid journal reservation.");
                if (c.Paused && (c.Request.Kind != "batch" || !c.Request.Lifecycle.AllowPause))
                    throw new InvalidDataException("Invalid paused journal claim.");
            }
            foreach (var c in copy) held.Add(c.Request.Id, c);
        }
    }

    private bool Fresh(DateTimeOffset now) => inventory is {} i && now >= i.IntervalStartUtc && now < i.IntervalEndUtc;
    private bool ResourcesFresh(DateTimeOffset now) => inventory is { ResourceObservedAtUtc: {} observed, ResourceValidUntilUtc: {} until }
        && now >= observed && now < until;
    public static bool DeadlineOpen(WorkloadRequest request, DateTimeOffset now) => request.Deadline is not {} d || now >= d.SubmittedAtUtc && now < d.DueAtUtc;

    private IEnumerable<WorkloadReservation> Candidates(WorkloadRequest request, string? ignoreId = null)
    {
        var current = inventory!;
        var lookup = index!;
        var owned = held.Values.Where(c => c.Request.Id != ignoreId).ToArray();
        var bySite = owned.Where(c => !c.Paused).GroupBy(c => c.Reservation.SiteId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Request.RecoverableHeatKwhPerHour), StringComparer.Ordinal);
        var byNode = owned.GroupBy(c => c.Reservation.NodeId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ResourceUsage.Owned(g.ToArray(), includeGpus: false), StringComparer.Ordinal);
        // Stored domain identity includes allocations whose node disappeared from the current inventory.
        var byDomain = owned.GroupBy(c => c.Reservation.DomainId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ResourceUsage.Owned(g.ToArray()), StringComparer.Ordinal);
        foreach (var site in current.Sites)
        {
            if (!JobPlacement.IsEligible(request.PlacementPolicy, new(request.OriginSiteId, site.Id, site.CountryCode))) continue;
            double committed = site.ExternalCommittedHeatKwh + bySite.GetValueOrDefault(site.Id);
            if (!double.IsFinite(committed)) continue;
            double heat = HeatCapacity.RemainingKwh(site.Heat, committed);
            if (heat < request.RecoverableHeatKwhPerHour) continue;
            foreach (var node in lookup.NodesBySite.GetValueOrDefault(site.Id, []).Where(n => n.Ready && n.NetworkEligible))
            {
                if (request.PinnedNodeId is {} pin && pin != node.Id || request.StorageId is {} storage && !node.StorageIds.Contains(storage)
                    || request.RequiredCapabilities.Except(node.Capabilities, StringComparer.Ordinal).Any()
                    || request.LatencyLimits.Any(limit => !node.Latencies.Any(l => l.PeerId == limit.PeerId && l.RttMs <= limit.MaxRttMs))) continue;
                var domain = lookup.DomainsById[node.DomainId];
                var domainOwned = byDomain.GetValueOrDefault(domain.Id, ResourceUsage.Empty);
                var domainExternal = lookup.ExternalByDomain.GetValueOrDefault(domain.Id, ResourceUsage.Empty);
                if (!Fits(node.Capacity, request.Resources, byNode.GetValueOrDefault(node.Id, ResourceUsage.Empty), lookup.ExternalByNode.GetValueOrDefault(node.Id, ResourceUsage.Empty))
                    || !Fits(domain.Capacity, request.Resources, domainOwned, domainExternal)) continue;
                string? gpu = null;
                if (request.GpuCapability is {} capability)
                {
                    gpu = domain.Gpus.Where(g => g.Capability == capability && node.GpuDeviceIds.Contains(g.Id) && !domainOwned.Gpus.Contains(g.Id) && !domainExternal.Gpus.Contains(g.Id)
                        && (request.MinGpuVramMib is null || g.VramMib >= request.MinGpuVramMib))
                        .OrderBy(g => g.Id, StringComparer.Ordinal).Select(g => g.Id).FirstOrDefault();
                    if (gpu is null) continue;
                }
                yield return new(request.Id, site.Id, node.Id, domain.Id, gpu, request.RecoverableHeatKwhPerHour, heat);
            }
        }
    }

    private static bool Fits(ResourceAmount capacity, ResourceAmount request, ResourceUsage owned, ResourceUsage external)
    {
        // Decimal aggregation prevents long/integer overflow from admitting impossible RAM/slot sums.
        return request.CpuCores + owned.Cpu + external.Cpu <= capacity.CpuCores
            && (decimal)request.RamMib + owned.RamMib + external.RamMib <= capacity.RamMib
            && (decimal)request.Slots + owned.Slots + external.Slots <= capacity.Slots;
    }

    public static void ValidateRequest(WorkloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.OriginSiteId) || request.Kind is not ("interactive" or "batch" or "legacy")
            || !ValidResources(request.Resources) || request.Resources.Slots < 1 || string.IsNullOrWhiteSpace(request.ResourceSource) || request.ResourceSource.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)
            || !double.IsFinite(request.RecoverableHeatKwhPerHour) || request.RecoverableHeatKwhPerHour <= 0 || string.IsNullOrWhiteSpace(request.HeatSource) || request.HeatSource.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)
            || request.PlacementPolicy is null || request.RequiredCapabilities is null || request.LatencyLimits is null || request.Lifecycle is null
            || request.RequiredCapabilities.Any(string.IsNullOrWhiteSpace)
            || request.LatencyLimits.Any(l => l is null || string.IsNullOrWhiteSpace(l.PeerId) || !double.IsFinite(l.MaxRttMs) || l.MaxRttMs < 0)
            || request.LatencyLimits.Select(l => l.PeerId).Distinct().Count() != request.LatencyLimits.Length
            || request.GpuCapability is not null && string.IsNullOrWhiteSpace(request.GpuCapability)
            || request.MinGpuVramMib is {} vram && (vram <= 0 || request.GpuCapability is null)
            || request.PinnedNodeId is not null && string.IsNullOrWhiteSpace(request.PinnedNodeId)
            || request.StorageId is not null && (string.IsNullOrWhiteSpace(request.StorageId) || string.IsNullOrWhiteSpace(request.PinnedNodeId))
            || request.Kind != "batch" && request.Lifecycle.AllowPause
            || request.Kind != "legacy" && (request.Resources.CpuCores <= 0 || request.Resources.RamMib <= 0))
            throw new InvalidDataException("Invalid workload requirements, units, sources or lifecycle constraints.");
        request.PlacementPolicy.Validate();
        if (request.Deadline is {} deadline && (request.Kind != "batch" || deadline.SubmittedAtUtc.Offset != TimeSpan.Zero
            || deadline.DueAtUtc.Offset != TimeSpan.Zero || deadline.DueAtUtc <= deadline.SubmittedAtUtc
            || string.IsNullOrWhiteSpace(deadline.Source) || deadline.Source.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Only batch work may have a sourced, positive absolute UTC completion deadline.");
    }

    private static bool ValidResources(ResourceAmount? r) => r is not null && double.IsFinite(r.CpuCores) && r.CpuCores >= 0 && r.RamMib >= 0 && r.Slots >= 0;
    private static bool Unique(IEnumerable<string> ids) { var all = ids.ToArray(); return all.All(id => !string.IsNullOrWhiteSpace(id)) && all.Distinct(StringComparer.Ordinal).Count() == all.Length; }
    // Validation must not construct an AdmissionIndex. Only installing a snapshot needs that work.
    internal static void ValidateInventory(WorkloadInventory i, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(i);
        if (i.ResourceObservedAtUtc.HasValue != i.ResourceValidUntilUtc.HasValue
            || i.ResourceObservedAtUtc is {} at && (at.Offset != TimeSpan.Zero || i.ResourceValidUntilUtc!.Value.Offset != TimeSpan.Zero
                || i.ResourceValidUntilUtc <= at || i.ResourceObservedAtUtc > now)) throw new InvalidDataException("Invalid resource telemetry freshness interval.");
        if (i.Revision < 0 || string.IsNullOrWhiteSpace(i.Source) || i.Sites is null || i.Domains is null || i.Nodes is null || i.ExternalAllocations is null
            || i.IntervalStartUtc.Offset != TimeSpan.Zero || i.IntervalEndUtc.Offset != TimeSpan.Zero
            || i.IntervalStartUtc.Ticks % TimeSpan.TicksPerHour != 0 || i.IntervalEndUtc - i.IntervalStartUtc != TimeSpan.FromHours(HoursPerInterval)
            || now < i.IntervalStartUtc || now >= i.IntervalEndUtc || i.Sites.Any(s => s is null) || i.Domains.Any(d => d is null) || i.Nodes.Any(n => n is null)
            || i.ExternalAllocations.Any(e => e is null) || !Unique(i.Sites.Select(s => s.Id)) || !Unique(i.Nodes.Select(n => n.Id))
            || !Unique(i.Domains.Select(d => d.Id)) || !Unique(i.ExternalAllocations.Select(e => e.Id))) throw new InvalidDataException("Invalid hourly resource inventory.");
        foreach (var site in i.Sites)
        {
            if (site.CountryCode is not { Length: 2 } || site.CountryCode.Any(c => c is < 'A' or > 'Z')) throw new InvalidDataException("Invalid country code.");
            HeatCapacity.RemainingKwh(site.Heat, site.ExternalCommittedHeatKwh);
        }
        foreach (var domain in i.Domains)
            if (!ValidResources(domain.Capacity) || domain.Gpus is null || domain.Gpus.Any(g => g is null || string.IsNullOrWhiteSpace(g.Capability) || g.VramMib < 0) || !Unique(domain.Gpus.Select(g => g.Id)))
                throw new InvalidDataException("Invalid physical resource domain.");
        var domainsById = i.Domains.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var siteIds = i.Sites.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var nodesById = i.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var n in i.Nodes)
        {
            var domain = n.DomainId is not null ? domainsById.GetValueOrDefault(n.DomainId) : null;
            if (!ValidResources(n.Capacity) || domain is null || !siteIds.Contains(n.SiteId) || n.Capabilities is null || n.StorageIds is null
                || n.GpuDeviceIds is null || n.Latencies is null || !Unique(n.Capabilities) || !Unique(n.StorageIds) || !Unique(n.GpuDeviceIds)
                || n.GpuDeviceIds.Any(g => !domain.Gpus.Any(d => d.Id == g)) || n.Latencies.Any(l => l is null || !double.IsFinite(l.RttMs) || l.RttMs < 0)
                || !Unique(n.Latencies.Select(l => l.PeerId))) throw new InvalidDataException("Invalid node requirements or physical-domain reference.");
        }
        foreach (var e in i.ExternalAllocations)
        {
            var node = e.NodeId is not null ? nodesById.GetValueOrDefault(e.NodeId) : null;
            if (node is null || !ValidResources(e.Resources) || e.GpuDeviceId is {} gpu && !node.GpuDeviceIds.Contains(gpu)) throw new InvalidDataException("Invalid external allocation.");
        }
        if (i.ExternalAllocations.Where(e => e.GpuDeviceId is not null).GroupBy(e => (nodesById[e.NodeId].DomainId, e.GpuDeviceId)).Any(g => g.Count() != 1))
            throw new InvalidDataException("An exclusive external GPU is allocated more than once.");
    }
}
