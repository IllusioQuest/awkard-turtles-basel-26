using DigitalHeat.Core;
using DigitalHeat.VoiceDemo;
using Xunit;

public class HouseReplayTests
{
    // Reference house in July (docs/case/challenge.md): no space heat, 12 kWh/day hot water,
    // 35 kWh tank, 4 servers x 1 kW x 0.8 recovery.
    private static readonly (int, double)[] ProfileL =
    [
        (7, .105), (7, 1.4), (7, .105), (7, .105), (8, 3.605), (8, .105), (8, .105), (8, .105), (9, .105), (9, .105),
        (10, .105), (11, .105), (11, .105), (12, .315), (14, .105), (15, .105), (16, .105), (18, .105), (18, .105),
        (18, .105), (19, .105), (20, .735), (21, 3.605), (21, .105)
    ];
    private static readonly double[] Demand = HouseReplay.Demand(ProfileL, 11.655, 12, new double[24]);
    private static HouseReplay House(double startHour = 11.5) => new(new HouseReplaySettings(Demand, startHour, 35, 35, 4, .8));

    [Fact]
    public void ProfileIsScaledToTheCaseDailyTotalWithAQuietHour()
    {
        Assert.Equal(12, Demand.Sum(), 9);                       // case: 12 kWh/day hot water
        Assert.Equal(0, Demand[13]);                              // EU profile L: no tapping 12:45-14:30
        Assert.Equal(.105 / 11.655 * 12, Demand[14], 9);
    }

    [Fact]
    public void BackgroundFollowsDemandSoTheTankStaysFullAndNothingIsDumped()
    {
        var house = House(0);
        house.Advance(24, 0);
        Assert.Equal(35, house.TankKwh, 6);
        Assert.Equal(0, house.DumpedKwh, 9);
        Assert.Equal(0, house.BackupKwh, 9);
    }

    [Fact]
    public void QuietHourLeavesNoUsefulHeatAndTheNextDrawBringsItBack()
    {
        var house = House();
        house.Advance(1.6, requestHeatKwhPerHour: .02);           // 13:06, request running
        Assert.Equal(0, HeatCapacity.RemainingKwh(house.Signal()), 6);
        Assert.Equal(0, new HeatFirstScheduler(0).Decide(house.Signal()).TargetLoadPerServer, 6);
        house.Advance(0.94, 0);                                   // 14:02, request frozen
        Assert.True(HeatCapacity.RemainingKwh(house.Signal()) >= .02);
    }

    [Fact]
    public void BackgroundYieldsToTheRequest()
    {
        var house = House();
        house.Advance(0.1, requestHeatKwhPerHour: .02);
        Assert.Equal(Demand[11] - .02, house.FillerHeatKwhPerHour, 6);
        Assert.Equal(.02 * .1, house.RequestHeatKwh, 9);
    }
}
