using System.Text.Json;
using Xunit;
namespace DigitalHeat.Orchestration.Tests;

/// <summary>T-80: the owner's run-scoped peer status must never hide unfinished work.</summary>
public class RenderStatusFilterTests
{
    private static RuntimeAttempt Attempt(string id) => new("fixture", Guid.NewGuid().ToString("N"),
        PlacementTests.Request("batch", "batch", 0.5) with { Id = id },
        new("batch", "spa", "b", "host", null, 0.8, 14), CoordinatorTests.Node("b"), CoordinatorTests.Profile);
    private static OriginJob Job(string id, string status) => new(PlacementTests.Request("batch", "batch", 0.5) with { Id = id }, "p", Status: status);
    private static RenderArtifact Result(string id) => new(id, "gpu", JsonSerializer.SerializeToElement(new { stage = "frame" }));

    [Fact] public void RunFilterKeepsCurrentRunAndAllUnfinishedWorkButDropsFinishedHistory()
    {
        const string current = "origin.new-", old = "origin.old-";
        var status = new RenderPeerStatus("gpu", "host", "recipe", false, 1, "GPU", null,
            [Job(old + "001", "accepted"), Job(old + "002", "cancelled"), Job(old + "003", "waiting"), Job(current + "001", "accepted"), Job(current + "002", "proposed")],
            [new(Attempt(old + "001"), "released"), new(Attempt(old + "002"), "running"), new(Attempt(current + "001"), "released"), new(Attempt(current + "002"), "running")],
            [Result(old + "001"), Result(current + "001")], null) { PhysicalGpuId = "gpu-0" };

        var filtered = RenderNodeHost.ForRun(status, current);

        Assert.Equal(new[] { old + "003", current + "001", current + "002" }, filtered.Jobs.Select(j => j.Request.Id).ToArray());
        Assert.Equal(new[] { old + "002", current + "001", current + "002" }, filtered.Attempts.Select(a => a.Attempt.Request.Id).ToArray());
        Assert.Equal(new[] { current + "001" }, filtered.Results.Select(r => r.JobId).ToArray());
        Assert.Equal("gpu-0", filtered.PhysicalGpuId); // Non-list fields unchanged.
        Assert.Equal(5, status.Jobs.Length); // Published snapshot is not mutated.
    }
}
