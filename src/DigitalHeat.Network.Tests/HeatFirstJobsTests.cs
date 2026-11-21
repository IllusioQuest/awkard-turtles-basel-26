using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Network.Tests;

public class HeatFirstJobsTests
{
    private static NetworkState State(string policy = "anywhere", double remoteHeat = 4)
    {
        NetworkSite Site(string id, double heat) => new(id, id, 0, 0, "CH", 1, 1, 1, 0, 0, 1, .3, .1,
            [new("2025-01-01T00:00", 0, true, heat, 0), new("2025-01-01T01:00", 0, true, heat, 0)]);
        NetworkSite[] sites = [Site("z-origin", 1), Site("a-peer", remoteHeat)];
        var states = sites.Select(s => new SiteHeatState(s.Id, new(s.Demand[0].TotalHeatKwh, 0, 0, 1, 1),
            HeatForecast.Build(s.Demand, "Synthetic mesh policy fixture", 0, 1))).ToArray();
        var ledger = new WorkLedger(sites, [new(1, .5, 24, new(policy, "Synthetic fixture", null)), null], [0, 0], 2, 24);
        ledger.BeginHour(0);
        return new(0, "2025-01-01T00:00", sites, states, ledger.View());
    }

    [Fact] public void HeatFirstIgnoresOriginPreference()
    {
        var state = State();
        Assert.Equal(1, new HeatFirstNetworkScheduler().Decide(state).Allocations!.Single().SiteIndex);
    }
    [Fact] public void LocalityEligibilityRemainsMandatoryDespiteRemoteHeat()
        => Assert.Equal(0, new HeatFirstNetworkScheduler().Decide(State("origin-site")).Allocations!.Single().SiteIndex);
    [Fact] public void HeatTiesUseSharedOrdinalSiteOrder()
        => Assert.Equal(1, new HeatFirstNetworkScheduler().Decide(State(remoteHeat: 1)).Allocations!.Single().SiteIndex);
}
