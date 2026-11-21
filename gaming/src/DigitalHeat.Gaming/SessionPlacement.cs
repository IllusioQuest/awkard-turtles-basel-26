using DigitalHeat.Core;

namespace DigitalHeat.Gaming;

/// <summary>Local admission accounting only. A reservation does not launch or migrate a game.</summary>
public sealed class SessionPlacement
{
    private readonly object gate = new();
    private GamingSnapshot? snapshot;
    private readonly Dictionary<string, (SessionRequest Request, SessionReservation Reservation)> held = new(StringComparer.Ordinal);
    private const int HoursPerInterval = 1;

    public void Update(GamingSnapshot next, DateTimeOffset nowUtc)
    {
        ValidateSnapshot(next, nowUtc);
        // Copy caller-owned arrays; input mutation must not bypass validation under the lock.
        var copy = next with { Sites = next.Sites.ToArray(), Nodes = next.Nodes.ToArray() };
        lock (gate)
        {
            if (snapshot is not null && (copy.IntervalStartUtc < snapshot.IntervalStartUtc || copy.Revision <= snapshot.Revision))
                throw new InvalidDataException("Cannot regress the inventory interval.");
            if (held.Values.Any(h => copy.Nodes.Any(n => n.Id == h.Reservation.NodeId && n.SiteId != h.Reservation.SiteId)))
                throw new InvalidDataException("Cannot move a reserved node to a different site.");
            // Reservations survive heat updates and inventory removal; they never relocate implicitly.
            snapshot = copy;
        }
    }

    public SessionReservation? TryReserve(SessionRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.OriginSiteId)
            || !double.IsFinite(request.RecoverableHeatKwhPerHour) || request.RecoverableHeatKwhPerHour <= 0
            || request.PlacementPolicy is null)
            throw new InvalidDataException("Invalid session request.");
        request.PlacementPolicy.Validate();
        request = request with { PlacementPolicy = request.PlacementPolicy with
            { AllowedCountryCodes = request.PlacementPolicy.AllowedCountryCodes?.ToArray() } };
        lock (gate)
        {
            if (held.TryGetValue(request.Id, out var existing))
            {
                if (!SameRequest(existing.Request, request))
                    throw new InvalidDataException("Session ID reused with different parameters.");
                return existing.Reservation;
            }
            var current = snapshot;
            if (current is null || nowUtc < current.IntervalStartUtc || nowUtc >= current.IntervalEndUtc) return null;
            // Selection and insertion share the lock: concurrent requests cannot claim the same reserve.
            var best = FindCandidates(current, request).OrderByDescending(x => x.AvailableHeatKwh)
                .ThenBy(x => x.Site.Id, StringComparer.Ordinal)
                .ThenBy(x => x.Node.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best is null) return null;
            var reservation = new SessionReservation(request.Id, best.Site.Id, best.Node.Id,
                request.RecoverableHeatKwhPerHour, best.AvailableHeatKwh);
            held.Add(request.Id, (request, reservation));
            return reservation;
        }
    }

    /// <summary>Call only after the runtime observes termination, or aborts an unlaunched claim.</summary>
    public bool Release(string sessionId) { lock (gate) return held.Remove(sessionId); }
    public SessionReservation[] Reservations() { lock (gate) return held.Values.Select(x => x.Reservation).ToArray(); }

    // Only enumerate while holding gate; owned reservations participate in both capacity checks.
    private IEnumerable<Candidate> FindCandidates(GamingSnapshot current, SessionRequest request)
    {
        foreach (var site in current.Sites)
        {
            var context = new JobPlacementContext(request.OriginSiteId, site.Id, site.CountryCode);
            if (!JobPlacement.IsEligible(request.PlacementPolicy, context)) continue;

            double ownedHeatKwh = held.Values.Where(x => x.Reservation.SiteId == site.Id)
                .Sum(x => x.Reservation.RecoverableHeatKwhPerHour);
            double committedHeatKwh = site.ExternalCommittedHeatKwh + ownedHeatKwh;
            if (!double.IsFinite(committedHeatKwh)) continue;
            double availableHeatKwh = HeatCapacity.RemainingKwh(site.Heat, committedHeatKwh);
            if (availableHeatKwh < request.RecoverableHeatKwhPerHour) continue;

            var node = current.Nodes.Where(n => n.SiteId == site.Id && n.Ready && n.NetworkEligible
                    && held.Values.Count(x => x.Reservation.NodeId == n.Id) < n.AllocatableSessionSlots)
                .OrderBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
            if (node is not null) yield return new Candidate(site, node, availableHeatKwh);
        }
    }

    private static void ValidateSnapshot(GamingSnapshot next, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (next.Revision < 0 || string.IsNullOrWhiteSpace(next.Source) || next.Sites is null || next.Nodes is null
            || next.IntervalStartUtc.Offset != TimeSpan.Zero || next.IntervalEndUtc.Offset != TimeSpan.Zero
            || next.IntervalEndUtc - next.IntervalStartUtc != TimeSpan.FromHours(HoursPerInterval)
            || next.IntervalStartUtc.Ticks % TimeSpan.TicksPerHour != 0
            || nowUtc < next.IntervalStartUtc || nowUtc >= next.IntervalEndUtc
            || next.Sites.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id)
                || s.CountryCode is not { Length: 2 } || s.CountryCode.Any(c => c is < 'A' or > 'Z'))
            || next.Sites.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != next.Sites.Length
            || next.Nodes.Any(n => n is null || string.IsNullOrWhiteSpace(n.Id) || n.AllocatableSessionSlots < 0
                || !next.Sites.Any(s => s.Id == n.SiteId))
            || next.Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != next.Nodes.Length)
            throw new InvalidDataException("Invalid hourly gaming inventory.");
        foreach (var site in next.Sites)
            HeatCapacity.RemainingKwh(site.Heat, site.ExternalCommittedHeatKwh);
    }

    private sealed record Candidate(GamingSite Site, GamingNode Node, double AvailableHeatKwh);

    private static bool SameRequest(SessionRequest a, SessionRequest b) => a.Id == b.Id
        && a.OriginSiteId == b.OriginSiteId && a.RecoverableHeatKwhPerHour == b.RecoverableHeatKwhPerHour
        && a.PlacementPolicy.Mode == b.PlacementPolicy.Mode && a.PlacementPolicy.Source == b.PlacementPolicy.Source
        && (a.PlacementPolicy.AllowedCountryCodes ?? []).SequenceEqual(b.PlacementPolicy.AllowedCountryCodes ?? []);
}
