using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class FleetSimulationTests
{
    private static Scenario ReferenceJanuary()
    {
        var root = Scenario.FindRepoRoot(AppContext.BaseDirectory);
        var s = Scenario.Load(Path.Combine(root, "config/fleet/scenarios/reference.json"));
        s.Run.EndLocal = "2025-02-01T00:00";
        return s;
    }

    [Fact]
    public void ReferenceHouseWholeYearMatchesCaseDemand()
    {
        var s = ReferenceJanuary();
        s.Run.EndLocal = "2026-01-01T00:00";
        s.Demand.VolumeScale = s.JobTypes.ToDictionary(p => Path.GetFileNameWithoutExtension(p)!, _ => 0.01);
        var run = FleetSimulation.Run(s);
        Assert.Equal(206, run.Result.Houses[0].HeatingDays);
        Assert.Equal(26850.5, run.Result.Houses[0].Account.SpaceHeatKwh, 1);
        // Same number everywhere: hourly network electricity cost and income add up to the house totals.
        var h = run.Result.Houses[0];
        Assert.Equal(h.ElectricityChf, run.Result.NetworkHours.Sum(n => n.Account.ElectricityChf), 6);
        Assert.Equal(h.IncomeChf, run.Result.NetworkHours.Sum(n => n.Account.IncomeT1Chf + n.Account.IncomeT2Chf), 6);
    }

    [Fact]
    public void DeadlinesAreAttributedToTheLastHouseConsistentlyWithJobResults()
    {
        var s = ReferenceJanuary();
        s.Run.EndLocal = "2026-01-01T00:00"; // whole year: January alone misses only never-started jobs
        var run = FleetSimulation.Run(s);
        var jobs = run.Result.Jobs;
        var h = run.Result.Houses.Sum(x => x.Account.JobsOnTime);
        // On time: every job done by its deadline (all ran on some house).
        Assert.Equal(jobs.Count(j => j.Status == "done" && !j.DeadlineMissed), h);
        // Missed: jobs whose deadline passed within the run while not done and that had started somewhere.
        int missedHouses = run.Result.Houses.Sum(x => x.Account.DeadlinesMissed);
        int missedStarted = jobs.Count(j => j.DeadlineMissed && j.FirstStartMs is { } f && f <= j.Request.DeadlineMs);
        Assert.Equal(missedStarted, missedHouses);
        Assert.True(missedHouses > 0, "the reference year should miss some deadlines of started jobs");
        // The timeseries periods add up to the run totals.
        Assert.Equal(missedHouses, run.Result.HouseTimeseries.Sum(r => r.Account.DeadlinesMissed));
        Assert.Equal(h, run.Result.HouseTimeseries.Sum(r => r.Account.JobsOnTime));
    }
}
