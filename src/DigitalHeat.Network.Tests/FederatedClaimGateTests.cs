using DigitalHeat.Core;
using DigitalHeat.Network;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class FederatedClaimGateTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 15, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ClockError = TimeSpan.FromSeconds(1); // scenario input, not runtime default
    private static readonly JobPlacementPolicy SwissOnly = new("country-allowlist", "test contract", ["CH"]);

    private static ClaimEvidence Evidence(PreviousAttempt? previous = null) => new(
        new FederatedClaim("job-1", previous is null ? 1 : previous.Claim.Generation + 1, "basel", "policy-1", Now.AddMinutes(-1), Now.AddMinutes(1)),
        true, previous, "policy-1", "origin", "CH", SwissOnly);

    [Fact]
    public void ValidQuorumClaimAndLocalityPermitStart() =>
        Assert.True(FederatedClaimGate.CanStart(Evidence(), "basel", Now, ClockError));

    [Fact]
    public void PartitionOrStalePolicyFailsClosed()
    {
        Assert.False(FederatedClaimGate.CanStart(Evidence() with { QuorumConfirmed = false }, "basel", Now, ClockError));
        Assert.False(FederatedClaimGate.CanStart(Evidence() with { CurrentPolicyRevision = "policy-2" }, "basel", Now, ClockError));
    }

    [Fact]
    public void ContractualLocalityBlocksRemoteStart()
    {
        Assert.False(FederatedClaimGate.CanStart(Evidence() with { ExecutionCountryCode = "DE" }, "basel", Now, ClockError));
        var local = Evidence() with { PlacementPolicy = new JobPlacementPolicy("origin-site", "test contract", null) };
        Assert.False(FederatedClaimGate.CanStart(local, "basel", Now, ClockError));
    }

    [Fact]
    public void UncertainOldProcessBlocksNewGenerationUntilSafeExpiry()
    {
        var old = new FederatedClaim("job-1", 1, "zurich", "policy-1", Now.AddMinutes(-3), Now.AddSeconds(-1));
        var prior = new PreviousAttempt(old, false, true, old.ValidUntil);
        Assert.False(FederatedClaimGate.CanStart(Evidence(prior), "basel", Now, ClockError));
        Assert.True(FederatedClaimGate.CanStart(Evidence(prior), "basel", Now.AddSeconds(2), ClockError));
        Assert.False(FederatedClaimGate.CanStart(Evidence(prior with { ProvenExecutionEnd = null }), "basel", Now.AddSeconds(2), ClockError));
        Assert.True(FederatedClaimGate.CanStart(Evidence(prior with { TerminationConfirmed = true }), "basel", Now, ClockError));
    }

    [Fact]
    public void DisconnectedNodeKeepsUsingSharedLocalHeatControllerForOwnedWork()
    {
        // A partition forbids new admissions, but it does not alter the already-owned job's
        // node-local heat calculation. The demand and tank values are test scenario inputs.
        Assert.False(FederatedClaimGate.CanStart(Evidence() with { QuorumConfirmed = false }, "basel", Now, ClockError));
        var signal = new HeatSignal(0.5, 0, 1, 1, 0.8);
        var applied = SchedulerController.Apply(signal, new GreedyHeatScheduler(0), new SimulatedExecutor());
        Assert.Equal(1, applied.ActualLoadPerServer);
    }
}
