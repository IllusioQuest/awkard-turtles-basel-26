using DigitalHeat.Orchestration;

namespace DigitalHeat.Gaming;

/// <summary>Compatibility adapter: historical gaming callers declare slots, not measured CPU/RAM/GPU resources.</summary>
public sealed class SessionPlacement
{
    private readonly WorkloadPlacement placement = new();

    public void Update(GamingSnapshot next, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (next.Sites is null || next.Nodes is null || next.Sites.Any(s => s is null) || next.Nodes.Any(n => n is null))
            throw new InvalidDataException("Missing gaming inventory.");
        var sites = next.Sites.Select(s => new WorkloadSite(s.Id, s.CountryCode, s.Heat, s.ExternalCommittedHeatKwh)).ToArray();
        var domains = next.Nodes.Select(n => new ResourceDomain(n.Id, new(0, 0, n.AllocatableSessionSlots), [])).ToArray();
        var nodes = next.Nodes.Select(n => new ComputeNode(n.Id, n.SiteId, n.Id, n.Ready, n.NetworkEligible,
            new(0, 0, n.AllocatableSessionSlots), [], [], [], [])).ToArray();
        placement.Update(new(next.Revision, next.IntervalStartUtc, next.IntervalEndUtc, next.Source, sites, domains, nodes, []), nowUtc);
    }

    public SessionReservation? TryReserve(SessionRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        var generic = new WorkloadRequest(request.Id, "legacy", request.OriginSiteId, request.PlacementPolicy,
            new(0, 0, 1), "Legacy gaming slot declaration; no CPU/RAM isolation claim", request.RecoverableHeatKwhPerHour,
            "Caller-supplied legacy gaming heat assumption", [], null, null, null, [], new(false, false, false));
        return placement.TryReserve(generic, nowUtc) is {} r ? Map(r) : null;
    }

    public bool Release(string sessionId) => placement.Release(sessionId);
    public SessionReservation[] Reservations() => placement.Allocations().Select(c => Map(c.Reservation)).ToArray();
    private static SessionReservation Map(WorkloadReservation r) => new(r.WorkloadId, r.SiteId, r.NodeId,
        r.RecoverableHeatKwhPerHour, r.AvailableHeatKwhBeforeReservation);
}
