using System.Text.Json;
using DigitalHeat.Core;
using Webhosting.Gateway;

namespace Webhosting.Tests;

public class HostingLifecycleTests
{
    private sealed class ReplicaControl : IHostingReplicaControl
    {
        public HashSet<string> ReadyNodes { get; } = [];
        public HashSet<string> FailedResumes { get; } = [];
        public List<string> Resumed { get; } = [];
        public List<string> Paused { get; } = [];
        public List<string[]> OtherReadyAtPause { get; } = [];
        public Func<HostingNode, bool>? ReadinessOverride { get; set; }

        public Task<bool> ReadyAsync(HostingNode node, CancellationToken token) =>
            Task.FromResult(ReadinessOverride?.Invoke(node) ?? ReadyNodes.Contains(node.Id));

        public Task ResumeAsync(HostingNode node, CancellationToken token)
        {
            Resumed.Add(node.Id);
            if (!FailedResumes.Contains(node.Id)) ReadyNodes.Add(node.Id);
            return Task.CompletedTask;
        }

        public Task PauseAsync(HostingNode node, CancellationToken token)
        {
            OtherReadyAtPause.Add(ReadyNodes.Where(id => id != node.Id).Order().ToArray());
            Paused.Add(node.Id);
            ReadyNodes.Remove(node.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"hosting-plan-test-{Guid.NewGuid():N}.json");
        public HostingPlan Plan { get; }
        public HostingAdmission Admission { get; } = new();
        public ReplicaControl Control { get; } = new();
        public HostingLifecycle Lifecycle { get; }

        public Fixture()
        {
            // Synthetic three-node component, with two required serving replicas.
            HostingNode[] nodes = [new("a", "api", "site-a", "CH", "http://127.0.0.1:8101", 8080),
                new("b", "api", "site-b", "CH", "http://127.0.0.1:8102", 8080),
                new("c", "api", "site-c", "CH", "http://127.0.0.1:8103", 8080)];
            Plan = new("fixture", "synthetic tests", DateTimeOffset.UtcNow.AddHours(1), 2, 0,
                nodes, [new("api", "site-a", new("anywhere", "test contract", null))],
                new() { ["site-a"] = new(2, 0, 0, 2, 1), ["site-b"] = new(1, 0, 0, 2, 1),
                    ["site-c"] = new(0, 0, 0, 2, 1) });
            Plan.Validate();
            Write(Plan);
            Lifecycle = NewLifecycle();
        }

        public HostingLifecycle NewLifecycle() => new(new HostingPlanReader(path), Admission, Control,
            TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(30));
        public void Write(HostingPlan plan) => File.WriteAllText(path, JsonSerializer.Serialize(plan, HostingPlan.JsonOptions));
        public IDisposable AdmitSpare()
        {
            var lease = Admission.Admit(Plan, "api", new HashSet<string> { "c" }, DateTimeOffset.UtcNow, out var node);
            Assert.NotNull(lease);
            Assert.Equal("c", node!.Id);
            return lease;
        }
        public void Dispose() => File.Delete(path);
    }

    [Fact]
    public async Task FirstReconciliationRestoresAllAndOnlyNextPausesWithTwoOtherReady()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "a", "b", "c" }, f.Control.Resumed);
        Assert.Empty(f.Control.Paused);
        Assert.All(f.Plan.Nodes, n => Assert.True(f.Admission.Ready(n.Id)));
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "c" }, f.Control.Paused);
        Assert.Equal(new[] { "a", "b" }, Assert.Single(f.Control.OtherReadyAtPause));
        Assert.False(f.Admission.Ready("c"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrStaleHeatResumesSpareAndPreventsFurtherPauses(bool stale)
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "c" }, f.Control.Paused);
        var changed = f.Plan with { HeatSignals = new(f.Plan.HeatSignals!) };
        if (stale) changed = changed with { ValidUntilUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
        else changed.HeatSignals!.Remove("site-c");
        f.Write(changed);
        f.Control.Resumed.Clear();
        f.Control.Paused.Clear();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "c" }, f.Control.Resumed);
        Assert.Empty(f.Control.Paused);
        Assert.True(f.Admission.Ready("c"));
    }

    [Fact]
    public async Task HeldLeaseTimesOutWithoutPauseAndReopensAdmission()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        using var held = f.AdmitSpare();
        var draining = f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal("drain:c", f.Admission.Phase);
        using var blocked = f.Admission.Admit(f.Plan, "api", new HashSet<string> { "c" }, DateTimeOffset.UtcNow, out var selected);
        Assert.Null(blocked);
        Assert.Null(selected);
        await draining;
        Assert.Empty(f.Control.Paused);
        Assert.Equal(1, f.Admission.InFlight("c"));
        using var reopened = f.AdmitSpare();
        Assert.Equal(2, f.Admission.InFlight("c"));
    }

    [Fact]
    public async Task FailedReplacementReadinessPreventsPausingExistingCapacity()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        f.Control.ReadyNodes.Remove("b");
        f.Control.FailedResumes.Add("b");
        f.Control.Resumed.Clear();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "b" }, f.Control.Resumed);
        Assert.Empty(f.Control.Paused);
        Assert.True(f.Admission.Ready("c"));
    }

    [Fact]
    public async Task ReadinessLostDuringFinalCheckPreventsPause()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        int checks = 0;
        f.Control.ReadinessOverride = node => node.Id != "b" || ++checks == 1;
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal(2, checks);
        Assert.Empty(f.Control.Paused);
        using var reopened = f.AdmitSpare();
    }

    [Fact]
    public async Task FileChangeDuringDrainAbortsEvenWithSameRevision()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        using var held = f.AdmitSpare();
        var draining = f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Equal("drain:c", f.Admission.Phase);
        f.Write(f.Plan with { Source = "changed test contract with same revision" });
        held.Dispose();
        await draining;
        Assert.Empty(f.Control.Paused);
        using var reopened = f.AdmitSpare();
    }

    [Fact]
    public async Task NewLifecycleRestoresPausedSpareBeforeOptimizing()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.False(f.Admission.Ready("c"));
        f.Control.Resumed.Clear();
        f.Control.Paused.Clear();
        var restarted = f.NewLifecycle();
        await restarted.ReconcileAsync(CancellationToken.None);
        Assert.Equal(new[] { "c" }, f.Control.Resumed);
        Assert.Empty(f.Control.Paused);
        Assert.True(f.Admission.Ready("c"));
    }

    [Fact]
    public async Task PolicyChangeDuringLastReadinessProbeAbortsPause()
    {
        using var f = new Fixture();
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        int checks = 0;
        f.Control.ReadinessOverride = node =>
        {
            if (node.Id == "b" && ++checks == 2)
                f.Write(f.Plan with { Source = "changed while replacement readiness was checked" });
            return true;
        };
        await f.Lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.Empty(f.Control.Paused);
    }
}
