using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class HouseholdDemoTests
{
    private readonly string root = Scenario.FindRepoRoot(AppContext.BaseDirectory);

    private FleetRun Run(string season) => FleetSimulation.Run(Scenario.Load(
        Path.Combine(root, $"config/fleet/scenarios/reference-{season}-jobs.json")));

    [Fact]
    public void Winter_has_two_jobs_and_both_complete()
    {
        var run = Run("winter");
        Assert.Equal(2, run.Jobs.Count);
        Assert.All(run.Result.Jobs, j => Assert.Equal("done", j.Status));
        Assert.Equal(2, run.Result.JobEvents.Count(e => e.Kind == "completed"));
        Assert.Equal(24, run.Calendar.RunHours);
    }

    [Fact]
    public void Summer_uses_case_house_and_same_chunk_really_pauses_and_resumes()
    {
        var run = Run("summer");
        Assert.Single(run.Jobs);
        Assert.Single(run.Houses);
        Assert.Equal(12, run.Result.Houses[0].Account.DemandKwh, 1e-7);
        Assert.Equal(35, run.Houses[0].Spec.TankKwh);
        var events = run.Result.JobEvents;
        Assert.Equal(new[] { "started", "paused", "resumed", "completed" }, events.Select(e => e.Kind));
        Assert.Equal(1, Assert.Single(run.Result.Jobs).Chunks);
        var pause = Assert.Single(events, e => e.Kind == "paused");
        var resume = Assert.Single(events, e => e.Kind == "resumed");
        Assert.Equal(pause.JobId, resume.JobId);
        Assert.Equal(pause.ChunkId, resume.ChunkId);
        Assert.True(resume.TimeMs > pause.TimeMs);
        Assert.Equal("done", Assert.Single(run.Result.Jobs).Status);
    }
}
