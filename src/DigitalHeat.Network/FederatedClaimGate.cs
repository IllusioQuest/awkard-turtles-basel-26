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
        if (claim is null || evidence.PlacementPolicy is null
            || !evidence.QuorumConfirmed || string.IsNullOrWhiteSpace(claim.JobId)
            || claim.Generation < 1 || string.IsNullOrWhiteSpace(claim.OwnerSiteId)
            || string.IsNullOrWhiteSpace(claim.PolicyRevision)
            || string.IsNullOrWhiteSpace(evidence.CurrentPolicyRevision)
            || claim.ValidFrom >= claim.ValidUntil
            || !string.Equals(claim.OwnerSiteId, nodeSiteId, StringComparison.Ordinal)
            || !string.Equals(claim.PolicyRevision, evidence.CurrentPolicyRevision, StringComparison.Ordinal)
            || !FederatedTime.Contains(claim.ValidFrom, claim.ValidUntil, now, maximumClockError))
            return false;

        if (!JobPlacement.IsEligible(evidence.PlacementPolicy,
                new JobPlacementContext(evidence.OriginSiteId, nodeSiteId, evidence.ExecutionCountryCode)))
            return false;

        var prior = evidence.Previous;
        if (prior is null) return claim.Generation == 1;
        if (prior.Claim is null || prior.Claim.ValidFrom >= prior.Claim.ValidUntil
            || prior.Claim.Generation < 1 || string.IsNullOrWhiteSpace(prior.Claim.OwnerSiteId)
            || prior.Claim.JobId != claim.JobId || prior.Claim.Generation >= claim.Generation)
            return false;
        if (prior.TerminationConfirmed) return true;
        // Interruptibility alone proves no stop deadline. A trusted bound must include watchdog,
        // scheduling and process-stop latency, even for a pausable job.
        if (prior.ProvenExecutionEnd is null || prior.ProvenExecutionEnd < prior.Claim.ValidFrom) return false;
        var previousEnd = prior.ProvenExecutionEnd > prior.Claim.ValidUntil
            ? prior.ProvenExecutionEnd.Value : prior.Claim.ValidUntil;
        long elapsedTicks = now.UtcTicks - previousEnd.UtcTicks;
        return elapsedTicks >= 0 && maximumClockError.Ticks <= elapsedTicks / 2;
    }
}

/// <summary>Compare intervals without overflowing DateTimeOffset or doubling a TimeSpan.</summary>
internal static class FederatedTime
{
    internal static bool Contains(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now,
        TimeSpan error) => now.UtcTicks - start.UtcTicks >= error.Ticks
            && end.UtcTicks - now.UtcTicks > error.Ticks;
}
