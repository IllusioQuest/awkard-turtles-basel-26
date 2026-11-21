using DigitalHeat.Core;
using DigitalHeat.Gaming;
using Xunit;

public class SessionPlacementTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static GamingSite Site(string id, double demand, double stored = 0, double capacity = 0, double external = 0) =>
        new(id, "CH", new(demand, stored, capacity, 1, 0.8), external);
    private static SessionRequest Request(string id, double heat = 1) =>
        new(id, "home", new("anywhere", "Synthetic lab test", null), heat);
    private static GamingSnapshot Snapshot(GamingSite[] sites, GamingNode[]? nodes = null) =>
        new(1, Now, Now.AddHours(1), "Synthetic test", sites,
            nodes ?? sites.Select(s => new GamingNode(s.Id + "-a", s.Id, true, true, 10)).ToArray());
    private static SessionPlacement Start(GamingSnapshot state)
    { var p = new SessionPlacement(); p.Update(state, Now); return p; }

    [Fact] public void RanksAbsoluteHeatInsteadOfSaturatedLoadFraction()
    {
        var low = Site("hotel", 2); var high = Site("spa", 4);
        var scheduler = new GreedyHeatScheduler(0);
        Assert.Equal(scheduler.Decide(low.Heat), scheduler.Decide(high.Heat));
        Assert.Equal("spa", Start(Snapshot([low, high])).TryReserve(Request("one"), Now)!.SiteId);
    }
    [Fact] public void CountsFreeTankCapacityAndCurrentDemand()
    {
        Assert.Equal(4, HeatCapacity.RemainingKwh(Site("spa", 1, 2, 5).Heat));
        Assert.Equal(1, HeatCapacity.RemainingKwh(Site("full", 1, 5, 5).Heat));
    }
    [Fact] public void SubtractsExternalAndOwnedReservationsOncePerSite()
    {
        var state = Snapshot([Site("hotel", 4, external: 1), Site("spa", 4)]);
        var p = Start(state);
        Assert.Equal("spa", p.TryReserve(Request("one", 2), Now)!.SiteId);
        Assert.Equal("hotel", p.TryReserve(Request("two", 2), Now)!.SiteId);
        Assert.Equal("spa", p.TryReserve(Request("three", 2), Now)!.SiteId);
        Assert.Null(p.TryReserve(Request("four", 2), Now));
    }
    [Fact] public void SiteHeatCannotBeReusedAcrossMultipleNodes()
    {
        var p = Start(Snapshot([Site("spa", 1)], [new("a", "spa", true, true, 1), new("b", "spa", true, true, 1)]));
        Assert.NotNull(p.TryReserve(Request("one"), Now));
        Assert.Null(p.TryReserve(Request("two"), Now));
    }
    [Fact] public void LocalityReadinessNetworkAndCapacityGateRanking()
    {
        var p = Start(Snapshot([Site("home", 2), Site("spa", 100)],
            [new("a", "home", true, true, 1), new("b", "spa", false, true, 1)]));
        Assert.Equal("home", p.TryReserve(Request("one"), Now)!.SiteId);
        Assert.Null(p.TryReserve(Request("two"), Now));
        var restricted = Request("three") with { PlacementPolicy = new("country-allowlist", "Contract", ["DE"]) };
        Assert.Null(p.TryReserve(restricted, Now));
        Assert.Null(Start(Snapshot([Site("spa", 10)], [new("a", "spa", true, false, 1)]))
            .TryReserve(Request("network"), Now));
    }
    [Fact] public void HeatUpdatesKeepExistingSessionsPinnedAndCountSlotsOnce()
    {
        var state = Snapshot([Site("home", 1), Site("spa", 10)],
            [new("a", "home", true, true, 2), new("b", "spa", true, true, 2)]);
        var p = Start(state); var first = p.TryReserve(Request("one"), Now);
        p.Update(state with { Revision = 2, Sites = [Site("home", 20), Site("spa", 0)] }, Now);
        Assert.Equal(first, p.TryReserve(Request("one"), Now));
        Assert.Equal("home", p.TryReserve(Request("two"), Now)!.SiteId);
        p.Update(state with { Revision = 3 }, Now);
        Assert.Equal("spa", p.TryReserve(Request("three"), Now)!.SiteId);
        Assert.Null(p.TryReserve(Request("four", 30), Now));
    }
    [Fact] public async Task ConcurrentLastSlotCanOnlyBeReservedOnce()
    {
        var p = Start(Snapshot([Site("spa", 100)], [new("a", "spa", true, true, 1)]));
        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() => p.TryReserve(Request(i.ToString()), Now))));
        Assert.Single(results, x => x is not null);
    }
    [Fact] public void RequestRetryIsIdempotentButChangedIdentityFails()
    {
        var p = Start(Snapshot([Site("spa", 10)])); var one = p.TryReserve(Request("one"), Now);
        Assert.Equal(one, p.TryReserve(Request("one"), Now));
        Assert.Throws<InvalidDataException>(() => p.TryReserve(Request("one", 2), Now));
        Assert.Single(p.Reservations());
    }
    [Fact] public void ExpiredSnapshotsBlockNewAdmissionsButKeepExistingReservations()
    {
        var p = Start(Snapshot([Site("spa", 10)])); var one = p.TryReserve(Request("one"), Now);
        Assert.Null(p.TryReserve(Request("two"), Now.AddHours(1)));
        Assert.Equal(one, p.TryReserve(Request("one"), Now.AddHours(1)));
    }
    [Fact] public void NextHourCarriesHeldHeatAndComputeReservations()
    {
        var state = Snapshot([Site("spa", 2)]); var p = Start(state);
        p.TryReserve(Request("one"), Now);
        p.Update(state with { Revision = 2, IntervalStartUtc = Now.AddHours(1), IntervalEndUtc = Now.AddHours(2) }, Now.AddHours(1));
        Assert.NotNull(p.TryReserve(Request("two"), Now.AddHours(1)));
        Assert.Null(p.TryReserve(Request("three"), Now.AddHours(1)));
    }
    [Fact] public void RejectsSupersededMisalignedAndNonfiniteSnapshots()
    {
        var state = Snapshot([Site("spa", 10)]); var p = Start(state);
        Assert.Throws<InvalidDataException>(() => p.Update(state, Now));
        Assert.Throws<InvalidDataException>(() => p.Update(state with { Revision = 2, IntervalStartUtc = Now.AddMinutes(1), IntervalEndUtc = Now.AddMinutes(61) }, Now.AddMinutes(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Update(state with { Revision = 2, Sites = [Site("spa", double.NaN)] }, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatCapacity.RemainingKwh(new(double.MaxValue, 0, double.MaxValue, 1, 0.8)));
    }
    [Fact] public void EqualHeatUsesStableSiteAndNodeIds()
    {
        var p = Start(Snapshot([Site("b", 2), Site("a", 2)],
            [new("z", "a", true, true, 1), new("y", "a", true, true, 1), new("x", "b", true, true, 1)]));
        Assert.Equal("y", p.TryReserve(Request("one"), Now)!.NodeId);
    }
    [Fact] public void ExplicitReleaseReturnsClaimAndCannotMoveReservedNode()
    {
        var state = Snapshot([Site("spa", 1), Site("hotel", 0)]);
        var p = Start(state); p.TryReserve(Request("one"), Now);
        Assert.Throws<InvalidDataException>(() => p.Update(state with { Revision = 2,
            Nodes = [new("spa-a", "hotel", true, true, 10)] }, Now));
        Assert.True(p.Release("one")); Assert.False(p.Release("one"));
        Assert.NotNull(p.TryReserve(Request("two"), Now));
    }
    [Fact] public void SharedCapacityPreservesPreviousSchedulerResultsForValidSignals()
    {
        foreach (double demand in new[] { 0, 0.5, 10.5 })
        foreach (double stored in new[] { 0, 17.5, 35 })
        foreach (double minimum in new[] { 0, 0.2, 1 })
        {
            var signal = new HeatSignal(demand, stored, 35, 4, 0.8);
            double previous = Math.Clamp((demand + 35 - stored) / 0.8, minimum * 4, 4) / 4;
            Assert.Equal(previous, new GreedyHeatScheduler(minimum).Decide(signal).TargetLoadPerServer);
        }
    }
}
