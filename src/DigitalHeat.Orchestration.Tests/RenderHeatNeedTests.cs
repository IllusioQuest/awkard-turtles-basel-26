using Xunit;
namespace DigitalHeat.Orchestration.Tests;

/// <summary>ADR 0069: render GPU weights are heat need per run, expressed through HeatSignal.DemandKwh.</summary>
public class RenderHeatNeedTests
{
    private static readonly DateTimeOffset Hour = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    private const string Run = "origin.run2-";

    private static HeatCommitment Frame(string id,double heatKwh,params DateTimeOffset[] hours)=>Frame(id,heatKwh,"released",hours);
    private static HeatCommitment Frame(string id,double heatKwh,string phase,params DateTimeOffset[] hours)
        =>new(id,heatKwh,hours,phase is "released" or "terminal");

    [Fact] public void PresentationWeightsSplitFortyEightFramesTwentyFourSixteenEight()
    {
        var split = RenderHeatNeed.Allocate(48, [("gpu-1", 1.5), ("gpu-2", 1), ("gpu-3", .5)]);
        Assert.Equal(24, split["gpu-1"]); Assert.Equal(16, split["gpu-2"]); Assert.Equal(8, split["gpu-3"]);
    }

    [Fact] public void LargestRemainderAlwaysAssignsEveryFrameDeterministically()
    {
        var split = RenderHeatNeed.Allocate(10, [("b", 1), ("a", 1), ("c", 1)]);
        Assert.Equal(10, split.Values.Sum());
        Assert.Equal(4, split["a"]); Assert.Equal(3, split["b"]); Assert.Equal(3, split["c"]); // tie -> smaller ID
        Assert.Throws<ArgumentException>(() => RenderHeatNeed.Allocate(4, [("a", 0)]));
    }

    [Fact] public void WithoutARegisteredRunTheConfiguredDemandApplies()
        => Assert.Equal(150, RenderHeatNeed.DemandKwh(null, [], Hour, 150));

    [Fact] public void FreeHeatEqualsTheRunsRemainingNeedAfterEarlierRunsThisHour()
    {
        // Earlier run this hour committed 3 frames; current run needs 8 frames of 0.8 kWh, 2 already committed now.
        var attempts = new[] { Frame("origin.run1-001", .8, Hour), Frame("origin.run1-002", .8, Hour), Frame("origin.run1-003", .8, Hour),
            Frame(Run + "001", .8, Hour), Frame(Run + "002", .8, Hour) };
        double demand = RenderHeatNeed.DemandKwh(new(Run, 8, .8), attempts, Hour, 150);
        double committed = attempts.Sum(a => a.HeatKwhPerHour);
        Assert.Equal(6 * .8, demand - committed, 5); // six frames of need remain
        Assert.True(demand - committed - 6 * .8 > 0); // epsilon lets the last frame fit
    }

    [Fact] public void RunCrossingAnHourKeepsOnlyItsUncoveredNeed()
    {
        var previous = Hour.AddHours(-1);
        // Three frames done last hour, one frame running across the boundary (committed in both hours).
        var attempts = new[] { Frame(Run + "001", .8, previous), Frame(Run + "002", .8, previous), Frame(Run + "003", .8, previous),
            Frame(Run + "004", .8, previous, Hour) };
        double demand = RenderHeatNeed.DemandKwh(new(Run, 8, .8), attempts, Hour, 150);
        double committedThisHour = .8;
        Assert.Equal(4 * .8, demand - committedThisHour, 5); // 8 needed - 3 finished earlier - 1 running now
    }

    [Fact] public void FrameStillRunningFromLastHourIsNotCountedAsCovered()
    {
        // Hour just changed: the running frame has not re-committed heat for the new hour yet (or is paused).
        var previous = Hour.AddHours(-1);
        var attempts = new[] { Frame(Run + "001", .8, previous), Frame(Run + "002", .8, "running", previous), Frame(Run + "003", .8, "paused", previous) };
        double demand = RenderHeatNeed.DemandKwh(new(Run, 8, .8), attempts, Hour, 150);
        Assert.Equal(7 * .8, demand, 5); // only the finished frame is covered; nothing is committed this hour yet
    }

    [Fact] public void RunPrefixMatchesTheOriginJobIdFormat() => Assert.Equal("origin.abc-", RenderHeatNeed.RunPrefix("origin", "abc"));
}
