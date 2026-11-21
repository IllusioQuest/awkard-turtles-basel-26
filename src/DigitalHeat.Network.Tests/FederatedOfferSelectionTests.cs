using DigitalHeat.Core;
using DigitalHeat.Network;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class FederatedOfferSelectionTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 15, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ClockError = TimeSpan.FromSeconds(1);
    private static readonly JobPlacementPolicy SwissOnly = new("country-allowlist", "test contract", ["CH"]);

    private static FederatedCapacityOffer Offer(string site, string country, double demandKwh) => new(
        site, country, "policy-1", Now.AddMinutes(-1), Now.AddMinutes(1),
        new HeatSignal(demandKwh, 0, 0, 1, 0.8), 0);

    private static FederatedCapacityOffer? Select(params FederatedCapacityOffer[] offers) =>
        FederatedOfferSelection.BestEligible(offers, SwissOnly, "origin", "policy-1", Now, ClockError);

    [Fact]
    public void PicksLargestUsefulHeatHeadroomWithStableTieBreak()
    {
        Assert.Equal("basel", Select(Offer("zurich", "CH", 1), Offer("basel", "CH", 2))?.SiteId);
        Assert.Equal("basel", Select(Offer("zurich", "CH", 1), Offer("basel", "CH", 1))?.SiteId);
    }

    [Fact]
    public void StaleAndAmbiguousOffersDoNotWin()
    {
        var stale = Offer("basel", "CH", 9) with { ExpiresAt = Now };
        Assert.Equal("zurich", Select(stale, Offer("zurich", "CH", 1))?.SiteId);
        Assert.Equal("zurich", Select(Offer("basel", "CH", 9), Offer("basel", "CH", 8),
            Offer("zurich", "CH", 1))?.SiteId);
    }

    [Fact]
    public void LocalityAndPolicyRevisionFailClosed()
    {
        Assert.Null(Select(Offer("berlin", "DE", 9)));
        Assert.Null(Select(Offer("basel", "CH", 9) with { PolicyRevision = "policy-0" }));
        var local = new JobPlacementPolicy("origin-site", "test contract", null);
        Assert.Equal("origin", FederatedOfferSelection.BestEligible(
            [Offer("basel", "CH", 9), Offer("origin", "CH", 1)], local,
            "origin", "policy-1", Now, ClockError)?.SiteId);
    }

    [Fact]
    public void InvalidHeatInventoryAndNoUsefulHeadroomAreSkipped()
    {
        Assert.Equal("zurich", Select(Offer("basel", "CH", double.NaN),
            Offer("zurich", "CH", 1))?.SiteId);
        Assert.Null(Select(Offer("basel", "CH", 0)));
    }
}
