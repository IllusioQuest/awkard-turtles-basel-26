using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>A reported opportunity, not authorization to execute a job (decision 0035).</summary>
public sealed record FederatedCapacityOffer(string SiteId, string CountryCode,
    string PolicyRevision, DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt,
    HeatSignal HeatSignal, double CommittedRecoverableHeatKwh);

/// <summary>
/// Deterministic, side-effect-free candidate ranking. A later ownership service must confirm
/// a unique claim before the chosen site may start. No network or Docker access occurs here.
/// </summary>
public static class FederatedOfferSelection
{
    public static FederatedCapacityOffer? BestEligible(IReadOnlyList<FederatedCapacityOffer> offers,
        JobPlacementPolicy policy, string originSiteId, string policyRevision,
        DateTimeOffset now, TimeSpan maximumClockError)
    {
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        if (maximumClockError < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumClockError));
        if (string.IsNullOrWhiteSpace(policyRevision)) throw new ArgumentException("Policy revision is required.", nameof(policyRevision));

        // Conflicting reports for one site cannot establish a trusted current inventory.
        var distinctOffers = offers.Distinct().ToArray();
        var ambiguousSites = distinctOffers.Where(o => o is not null && !string.IsNullOrWhiteSpace(o.SiteId))
            .GroupBy(o => o.SiteId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var candidates = new List<(FederatedCapacityOffer Offer, double HeadroomKwh)>();
        foreach (var offer in distinctOffers)
        {
            if (offer is null || offer.HeatSignal is null || string.IsNullOrWhiteSpace(offer.SiteId)
                || ambiguousSites.Contains(offer.SiteId)
                || !string.Equals(offer.PolicyRevision, policyRevision, StringComparison.Ordinal)
                || !FederatedTime.Contains(offer.ObservedAt, offer.ExpiresAt, now, maximumClockError)
                || offer.ExpiresAt <= offer.ObservedAt
                || !JobPlacement.IsEligible(policy,
                    new JobPlacementContext(originSiteId, offer.SiteId, offer.CountryCode)))
                continue;
            try
            {
                double headroom = HeatCapacity.RemainingKwh(offer.HeatSignal,
                    offer.CommittedRecoverableHeatKwh);
                if (headroom > 0) candidates.Add((offer, headroom));
            }
            catch (ArgumentOutOfRangeException) { /* Untrusted or invalid inventory fails closed. */ }
        }
        return candidates.OrderByDescending(c => c.HeadroomKwh)
            .ThenBy(c => c.Offer.SiteId, StringComparer.Ordinal)
            .Select(c => c.Offer).FirstOrDefault();
    }
}
