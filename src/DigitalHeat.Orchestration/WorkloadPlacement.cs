using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

/// <summary>One atomic heat/resource admission path for real runtimes, tests and the legacy gaming adapter.</summary>
public sealed class WorkloadPlacement
{
    private readonly object gate = new();
    private WorkloadInventory? inventory;
    private readonly Dictionary<string, AllocationView> held = new(StringComparer.Ordinal);
    private const int HoursPerInterval = 1;

    public void Update(WorkloadInventory next, DateTimeOffset nowUtc)
    {
        ValidateInventory(next, nowUtc);
        var copy = WorkloadJson.Copy(next);
        lock (gate)
        {
            if (inventory is {} old && (copy.Revision <= old.Revision || copy.IntervalStartUtc < old.IntervalStartUtc))
                throw new InvalidDataException("Cannot regress inventory revision or hour.");
            foreach (var claim in held.Values)
            {
                var node = copy.Nodes.SingleOrDefault(n => n.Id == claim.Reservation.NodeId);
                if (node is not null && (node.SiteId != claim.Reservation.SiteId || node.DomainId != claim.Reservation.DomainId))
                    throw new InvalidDataException("Cannot move an allocated node to another site/resource domain.");
                if (copy.ExternalAllocations.Any(e => e.Id == claim.Request.Id))
                    throw new InvalidDataException("Owned allocation counted again as external.");
            }
            inventory = copy; // Missing nodes retain their claims; disappearance never proves release.
        }
    }

    public WorkloadReservation? TryReserve(WorkloadRequest request, DateTimeOffset nowUtc)
    {
        ValidateRequest(request);
        request = WorkloadJson.Copy(request);
        lock (gate)
        {
            if (held.TryGetValue(request.Id, out var existing))
            {
                if (WorkloadJson.Identity(existing.Request) != WorkloadJson.Identity(request))
                    throw new InvalidDataException("Workload ID reused with changed parameters.");
                return existing.Reservation;
            }
            if (!DeadlineOpen(request, nowUtc) || !Fresh(nowUtc) || request.Kind != "legacy" && !ResourcesFresh(nowUtc)) return null;
            if (inventory!.ExternalAllocations.Any(e => e.Id == request.Id))
                throw new InvalidDataException("Workload ID is already externally allocated.");
            var best = Candidates(request).OrderByDescending(c => c.AvailableHeatKwhBeforeReservation)
                .ThenBy(c => c.SiteId, StringComparer.Ordinal).ThenBy(c => c.NodeId, StringComparer.Ordinal).FirstOrDefault();
            if (best is not null) held.Add(request.Id, new(request, best, false));
            return best;
        }
    }

    public AllocationView[] Allocations() { lock (gate) return WorkloadJson.Copy(held.Values.ToArray()); }

    public bool CanAdmitAfterBatchPause(WorkloadRequest request, DateTimeOffset nowUtc)
    {
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
        var owned = held.Values.Where(c => c.Request.Id != ignoreId).ToArray();
        foreach (var site in current.Sites)
        {
            if (!JobPlacement.IsEligible(request.PlacementPolicy, new(request.OriginSiteId, site.Id, site.CountryCode))) continue;
            double committed = site.ExternalCommittedHeatKwh + owned.Where(c => c.Reservation.SiteId == site.Id && !c.Paused)
                .Sum(c => c.Request.RecoverableHeatKwhPerHour);
            if (!double.IsFinite(committed)) continue;
            double heat = HeatCapacity.RemainingKwh(site.Heat, committed);
            if (heat < request.RecoverableHeatKwhPerHour) continue;
            foreach (var node in current.Nodes.Where(n => n.SiteId == site.Id && n.Ready && n.NetworkEligible))
            {
                if (request.PinnedNodeId is {} pin && pin != node.Id || request.StorageId is {} storage && !node.StorageIds.Contains(storage)
                    || request.RequiredCapabilities.Except(node.Capabilities, StringComparer.Ordinal).Any()
                    || request.LatencyLimits.Any(limit => !node.Latencies.Any(l => l.PeerId == limit.PeerId && l.RttMs <= limit.MaxRttMs))) continue;
                var domain = current.Domains.Single(d => d.Id == node.DomainId);
                var domainNodeIds = current.Nodes.Where(n => n.DomainId == domain.Id).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
                // Include owned claims for nodes missing from the new snapshot using their stored domain identity.
                var domainOwned = owned.Where(c => c.Reservation.DomainId == domain.Id).ToArray();
                var external = current.ExternalAllocations.Where(e => domainNodeIds.Contains(e.NodeId)).ToArray();
                if (!Fits(node.Capacity, request.Resources, owned.Where(c => c.Reservation.NodeId == node.Id), external.Where(e => e.NodeId == node.Id))
                    || !Fits(domain.Capacity, request.Resources, domainOwned, external)) continue;
                string? gpu = null;
                if (request.GpuCapability is {} capability)
                {
                    var occupied = domainOwned.Select(c => c.Reservation.GpuDeviceId).Concat(external.Select(e => e.GpuDeviceId)).ToHashSet();
                    gpu = domain.Gpus.Where(g => g.Capability == capability && node.GpuDeviceIds.Contains(g.Id) && !occupied.Contains(g.Id)
                        && (request.MinGpuVramMib is null || g.VramMib >= request.MinGpuVramMib))
                        .OrderBy(g => g.Id, StringComparer.Ordinal).Select(g => g.Id).FirstOrDefault();
                    if (gpu is null) continue;
                }
                yield return new(request.Id, site.Id, node.Id, domain.Id, gpu, request.RecoverableHeatKwhPerHour, heat);
            }
        }
    }

    private static bool Fits(ResourceAmount capacity, ResourceAmount request, IEnumerable<AllocationView> owned, IEnumerable<ExternalAllocation> external)
    {
        var claims = owned.ToArray(); var other = external.ToArray();
        // Decimal aggregation prevents long/integer overflow from admitting impossible RAM/slot sums.
        return request.CpuCores + claims.Where(c => !c.Paused).Sum(c => c.Request.Resources.CpuCores) + other.Sum(c => c.Resources.CpuCores) <= capacity.CpuCores
            && (decimal)request.RamMib + claims.Sum(c => (decimal)c.Request.Resources.RamMib) + other.Sum(c => (decimal)c.Resources.RamMib) <= capacity.RamMib
            && (decimal)request.Slots + claims.Sum(c => (decimal)c.Request.Resources.Slots) + other.Sum(c => (decimal)c.Resources.Slots) <= capacity.Slots;
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
    private static void ValidateInventory(WorkloadInventory i, DateTimeOffset now)
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
        foreach (var n in i.Nodes)
        {
            var domain = i.Domains.SingleOrDefault(d => d.Id == n.DomainId);
            if (!ValidResources(n.Capacity) || domain is null || !i.Sites.Any(s => s.Id == n.SiteId) || n.Capabilities is null || n.StorageIds is null
                || n.GpuDeviceIds is null || n.Latencies is null || !Unique(n.Capabilities) || !Unique(n.StorageIds) || !Unique(n.GpuDeviceIds)
                || n.GpuDeviceIds.Any(g => !domain.Gpus.Any(d => d.Id == g)) || n.Latencies.Any(l => l is null || !double.IsFinite(l.RttMs) || l.RttMs < 0)
                || !Unique(n.Latencies.Select(l => l.PeerId))) throw new InvalidDataException("Invalid node requirements or physical-domain reference.");
        }
        foreach (var e in i.ExternalAllocations)
        {
            var node = i.Nodes.SingleOrDefault(n => n.Id == e.NodeId);
            if (node is null || !ValidResources(e.Resources) || e.GpuDeviceId is {} gpu && !node.GpuDeviceIds.Contains(gpu)) throw new InvalidDataException("Invalid external allocation.");
        }
        if (i.ExternalAllocations.Where(e => e.GpuDeviceId is not null).GroupBy(e => (i.Nodes.Single(n => n.Id == e.NodeId).DomainId, e.GpuDeviceId)).Any(g => g.Count() != 1))
            throw new InvalidDataException("An exclusive external GPU is allocated more than once.");
    }
}
