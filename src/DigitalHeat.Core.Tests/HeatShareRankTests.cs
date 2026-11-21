using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

/// <summary>Decision 0056: free share first, then free kWh, then stable IDs.</summary>
public sealed class HeatShareRankTests
{
    [Fact]
    public void LargerFreeShareWinsOverMoreFreeKwh()
    {
        var big = HeatShareRank.From(107, 20, "pool");   // 87 kWh free, share 0.81
        var house = HeatShareRank.From(10, 1, "house");  // 9 kWh free, share 0.90
        Assert.True(house.CompareTo(big) > 0);
    }

    [Fact]
    public void EqualShareTiesGoToMoreFreeKwhThenLowerId()
    {
        Assert.True(HeatShareRank.From(107, 0, "pool").CompareTo(HeatShareRank.From(45, 0, "a")) > 0);
        Assert.True(HeatShareRank.From(10, 0, "a").CompareTo(HeatShareRank.From(10, 0, "b")) > 0);
        Assert.True(HeatShareRank.From(10, 0, "a", "1").CompareTo(HeatShareRank.From(10, 0, "a", "2")) > 0);
    }

    [Fact]
    public void NoBudgetAndOvercommitGiveZeroFreeHeat()
    {
        var none = HeatShareRank.From(0, 0, "a");
        Assert.Equal((0.0, 0.0), (none.FreeShare, none.FreeHeatKwh));
        var over = HeatShareRank.From(2, 3, "b");
        Assert.Equal((0.0, 0.0), (over.FreeShare, over.FreeHeatKwh));
    }

    [Fact]
    public void InvalidInputsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatShareRank.From(-1, 0, "a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatShareRank.From(1, double.NaN, "a"));
    }
}
