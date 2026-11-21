using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// Deterministic protocol model for T-45. A trusted quorum service must issue claims and report
/// the previous attempt; this class neither implements consensus nor starts a container.
/// </summary>
public sealed record FederatedClaim(string JobId, long Generation, string OwnerSiteId,
    string PolicyRevision, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public sealed record PreviousAttempt(FederatedClaim Claim, bool TerminationConfirmed,
    bool Interruptible, DateTimeOffset? ProvenExecutionEnd);

public sealed record ClaimEvidence(FederatedClaim Claim, bool QuorumConfirmed,
    PreviousAttempt? Previous, string CurrentPolicyRevision, string OriginSiteId,
    string ExecutionCountryCode, JobPlacementPolicy PlacementPolicy);

/// <summary>Checks whether a node may start one attempt at a supplied instant.</summary>
public static class FederatedClaimGate
{
    public static bool CanStart(ClaimEvidence evidence, string nodeSiteId, DateTimeOffset now,
        TimeSpan maximumClockError)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (maximumClockError < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumClockError));
        var claim = evidence.Claim;
        if (!evidence.QuorumConfirmed || string.IsNullOrWhiteSpace(claim.JobId)
            || claim.Generation < 1 || string.IsNullOrWhiteSpace(claim.OwnerSiteId)
            || claim.ValidFrom >= claim.ValidUntil
            || !string.Equals(claim.OwnerSiteId, nodeSiteId, StringComparison.Ordinal)
            || !string.Equals(claim.PolicyRevision, evidence.CurrentPolicyRevision, StringComparison.Ordinal)
            || now - maximumClockError < claim.ValidFrom
            || now + maximumClockError >= claim.ValidUntil)
            return false;

        if (!JobPlacement.IsEligible(evidence.PlacementPolicy,
                new JobPlacementContext(evidence.OriginSiteId, nodeSiteId, evidence.ExecutionCountryCode)))
            return false;

        var prior = evidence.Previous;
        if (prior is null) return true;
        if (prior.Claim.JobId != claim.JobId || prior.Claim.Generation >= claim.Generation)
            return false;
        if (prior.TerminationConfirmed) return true;
        // A non-interruptible old process may outlive its lease. Only a trusted execution bound
        // permits reassignment without a confirmed stop.
        if (!prior.Interruptible && prior.ProvenExecutionEnd is null) return false;
        var previousEnd = prior.Interruptible ? prior.Claim.ValidUntil : prior.ProvenExecutionEnd!.Value;
        return now - maximumClockError >= previousEnd + maximumClockError;
    }
}
