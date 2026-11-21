using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class FederationBoundaryTests
{
    private static readonly DateTimeOffset Epoch = new(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly JobPlacementPolicy Policy = new("country-allowlist", "test contract", ["CH"]);
    private static FederatedClaim Claim(long generation = 1) =>
        new("job", generation, "basel", "v1", Epoch.AddSeconds(-100), Epoch.AddSeconds(100));
    private static ClaimEvidence Evidence(FederatedClaim? claim = null, PreviousAttempt? previous = null) =>
        new(claim ?? Claim(), true, previous, "v1", "origin", "CH", Policy);
    private static FederatedCapacityOffer Offer(string site = "basel", double demand = 1) =>
        new(site, "CH", "v1", Epoch.AddSeconds(-100), Epoch.AddSeconds(100), new(demand, 0, 0, 1, 0.8), 0);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void MissingMatchingPolicyVersionsCannotAuthorize(string? revision)
    {
        var evidence = Evidence(Claim() with { PolicyRevision = revision! }) with { CurrentPolicyRevision = revision! };
        Assert.False(FederatedClaimGate.CanStart(evidence, "basel", Epoch, TimeSpan.Zero));
    }

    [Fact]
    public void MissingPreviousAttemptCannotAuthorizeLaterGeneration() =>
        Assert.False(FederatedClaimGate.CanStart(Evidence(Claim(2)), "basel", Epoch, TimeSpan.Zero));

    [Fact]
    public void MalformedPreviousAttemptCannotBeBypassedByTerminationFlag()
    {
        var prior = new PreviousAttempt(Claim() with { ValidUntil = Epoch.AddSeconds(-101) }, true, true, null);
        Assert.False(FederatedClaimGate.CanStart(Evidence(Claim(2), prior), "basel", Epoch, TimeSpan.Zero));
    }

    [Fact]
    public void TimeRangeExtremesAndHugeUncertaintyFailWithoutOverflow()
    {
        Assert.False(FederatedClaimGate.CanStart(Evidence(), "basel", Epoch, TimeSpan.MaxValue));
        Assert.False(FederatedClaimGate.CanStart(Evidence(), "basel", DateTimeOffset.MinValue, TimeSpan.FromSeconds(1)));
        Assert.False(FederatedClaimGate.CanStart(Evidence(), "basel", DateTimeOffset.MaxValue, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void MissingHeatSignalIsSkippedAndIdenticalRetransmissionsAreIdempotent()
    {
        var valid = Offer("zurich");
        var missing = Offer() with { HeatSignal = null! };
        var selected = FederatedOfferSelection.BestEligible([missing, valid, valid], Policy,
            "origin", "v1", Epoch, TimeSpan.Zero);
        Assert.Equal(valid, selected);
    }

    [Fact]
    public void InputOrderDoesNotChangeSelection()
    {
        FederatedCapacityOffer[] offers = [Offer("zurich", 2), Offer("basel", 2), Offer("berlin", 9) with { CountryCode = "DE" }];
        foreach (var first in offers)
            foreach (var second in offers.Where(o => o != first))
            {
                var third = offers.Single(o => o != first && o != second);
                Assert.Equal("basel", FederatedOfferSelection.BestEligible([first, second, third],
                    Policy, "origin", "v1", Epoch, TimeSpan.Zero)?.SiteId);
            }
    }

    [Fact]
    public void BoundedClockSweepNeverAdmitsSuccessorWhileOldLeaseCanStillBeValid()
    {
        // Exhaustive finite scenario: clock error 0..5 s and successor local time -15..15 s.
        // For every accepted successor, all possible old-node clocks in that bound have
        // reached expiry. This checks a conditional time predicate, not process termination.
        var old = Claim() with { ValidUntil = Epoch };
        var prior = new PreviousAttempt(old, false, true, old.ValidUntil);
        for (int error = 0; error <= 5; error++)
            for (int localSeconds = -15; localSeconds <= 15; localSeconds++)
            {
                var now = Epoch.AddSeconds(localSeconds);
                if (!FederatedClaimGate.CanStart(Evidence(Claim(2), prior), "basel", now, TimeSpan.FromSeconds(error))) continue;
                for (int newClockOffset = -error; newClockOffset <= error; newClockOffset++)
                    for (int oldClockOffset = -error; oldClockOffset <= error; oldClockOffset++)
                        Assert.True(now.AddSeconds(-newClockOffset + oldClockOffset) >= old.ValidUntil);
            }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InterruptibilityNeverSubstitutesForProvenStopBound(bool interruptible)
    {
        var old = Claim() with { ValidUntil = Epoch.AddSeconds(-20) };
        var prior = new PreviousAttempt(old, false, interruptible, null);
        Assert.False(FederatedClaimGate.CanStart(Evidence(Claim(2), prior), "basel", Epoch, TimeSpan.Zero));
        Assert.False(FederatedClaimGate.CanStart(Evidence(Claim(2), prior with { ProvenExecutionEnd = Epoch.AddSeconds(5) }), "basel", Epoch, TimeSpan.Zero));
        Assert.True(FederatedClaimGate.CanStart(Evidence(Claim(2), prior with { ProvenExecutionEnd = Epoch }), "basel", Epoch, TimeSpan.Zero));
    }
}
