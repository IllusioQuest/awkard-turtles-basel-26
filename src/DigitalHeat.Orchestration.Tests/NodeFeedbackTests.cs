using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class NodeFeedbackTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dh-feedback-" + Guid.NewGuid().ToString("N"));
    private readonly string fleet = "feedback-" + Guid.NewGuid().ToString("N");
    private string Inbox => Path.Combine(root, "inbox");
    private string State => Path.Combine(root, "state");
    private static readonly RuntimeNode[] Nodes = [CoordinatorTests.Node("a"), CoordinatorTests.Node("b")];
    private static NodeFeedbackPolicy Policy => new(1, 1, 30, 65536, 100, "Synthetic operator policy", [new("home", "a"), new("spa", "b")]);
    private static NodeFeedback Report(string id, long sequence = 1, bool accepting = true, HeatSignal? heat = null) =>
        new(id, sequence, PlacementTests.Now, PlacementTests.Now.AddSeconds(20), PlacementTests.Now, accepting,
            heat ?? PlacementTests.Inventory().Sites.Single(s => s.Id == (id == "a" ? "home" : "spa")).Heat, "Synthetic fixture, not measured heat", "hourly_baseline");
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = PlacementTests.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Runtime : IWorkloadRuntime
    {
        public readonly CoordinatorTests.Runtime Inner = new();
        public Action? OnCreate;
        public IReadOnlyList<string> Discover(string fleetId, IReadOnlyList<RuntimeNode> nodes) => Inner.Discover(fleetId, nodes);
        public ContainerObservation Observe(RuntimeAttempt a) => Inner.Observe(a);
        public void Create(RuntimeAttempt a) { Inner.Create(a); OnCreate?.Invoke(); }
        public void Start(RuntimeAttempt a) => Inner.Start(a);
        public void Stop(RuntimeAttempt a) => Inner.Stop(a);
        public void Remove(RuntimeAttempt a) => Inner.Remove(a);
        public double ApplyBatchLoad(RuntimeAttempt a, double load) => Inner.ApplyBatchLoad(a, load);
    }

    [Fact] public void FullUpdateBlocksNewWorkAndFreshUpdateUsesHeatWithinSameHour()
    {
        var clock = new Clock(); var runtime = new Runtime();
        var config = new WorkloadConfiguration(fleet, PlacementTests.Inventory(), Nodes, [new(PlacementTests.Request(), CoordinatorTests.Profile)]);
        var inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, config.Inventory);
        NodeFeedbackInbox.Publish(Inbox, Report("a", accepting: false)); NodeFeedbackInbox.Publish(Inbox, Report("b", accepting: false));
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        var owner = new NodeFeedbackOwner(c, config, inbox, Policy, clock);
        Assert.Empty(owner.Tick(() => config)); Assert.Equal(0, runtime.Inner.Creates);
        var spaHeat = config.Inventory.Sites[1].Heat;
        NodeFeedbackInbox.Publish(Inbox, Report("b", 2, heat: spaHeat with { DemandKwh = 0, TankStoredKwh = spaHeat.TankCapacityKwh }));
        Assert.Empty(owner.Tick(() => config)); Assert.Equal(0, runtime.Inner.Creates);
        NodeFeedbackInbox.Publish(Inbox, Report("b", 3));
        Assert.Empty(owner.Tick(() => config)); Assert.Equal("b", c.Attempts().Single().Attempt.Reservation.NodeId);
        NodeFeedbackInbox.Publish(Inbox, Report("b", 4, accepting: false));
        Assert.Empty(owner.Tick(() => config)); Assert.Equal("running", c.Attempts().Single().Phase);
        Assert.Equal(1, runtime.Inner.Starts); Assert.Equal(0, runtime.Inner.Pauses);
    }

    [Fact] public void GamingLatencyFiltersBeforeHeatWhileUnconstrainedBatchFollowsHeat()
    {
        var clock = new Clock(); var runtime = new Runtime();
        var game = PlacementTests.Request() with { LatencyLimits = [new("player", 15)] };
        var batch = PlacementTests.Request("batch", "batch");
        var config = new WorkloadConfiguration(fleet, PlacementTests.Inventory(), Nodes,
            [new(batch, CoordinatorTests.Profile), new(game, CoordinatorTests.Profile)]);
        var inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, config.Inventory);
        NodeFeedbackInbox.Publish(Inbox, Report("a")); NodeFeedbackInbox.Publish(Inbox, Report("b"));
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        var owner = new NodeFeedbackOwner(c, config, inbox, Policy, clock);
        Assert.Empty(owner.Tick(() => config));
        Assert.Equal("a", c.Attempts().Single(a => a.Attempt.Request.Id == game.Id).Attempt.Reservation.NodeId);
        Assert.Equal("b", c.Attempts().Single(a => a.Attempt.Request.Id == batch.Id).Attempt.Reservation.NodeId);
    }

    [Fact] public void ReplayExpiryMalformedReplacementAndRestartCannotEnableNodes()
    {
        var basis = PlacementTests.Inventory(); var inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, basis);
        NodeFeedbackInbox.Publish(Inbox, Report("a")); NodeFeedbackInbox.Publish(Inbox, Report("b"));
        Assert.Empty(inbox.Read(basis, PlacementTests.Now));
        Assert.All(inbox.Project(basis, 1, PlacementTests.Now).Nodes, n => Assert.True(n.Ready));
        inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, basis);
        NodeFeedbackInbox.Publish(Inbox, Report("b") with { AcceptingWork = false });
        Assert.Single(inbox.Read(basis, PlacementTests.Now));
        Assert.False(inbox.Project(basis, 2, PlacementTests.Now).Nodes[1].Ready);
        NodeFeedbackInbox.Publish(Inbox, Report("b", 2)); Assert.Empty(inbox.Read(basis, PlacementTests.Now));
        File.WriteAllText(Path.Combine(Inbox, "node-b.json"), "{");
        Assert.Single(inbox.Read(basis, PlacementTests.Now));
        Assert.False(inbox.Project(basis, 3, PlacementTests.Now).Nodes[1].Ready);
        Assert.All(inbox.Project(basis, 4, PlacementTests.Now.AddSeconds(20)).Nodes, n => Assert.False(n.Ready));
    }

    [Fact] public void HeatCannotBeRenewedByReadinessOnlyAndCannotChangeHardware()
    {
        var basis = PlacementTests.Inventory(); var inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, basis);
        NodeFeedbackInbox.Publish(Inbox, Report("a"));
        NodeFeedbackInbox.Publish(Inbox, Report("b") with { Heat = basis.Sites[1].Heat with { ServerCount = 50 } });
        Assert.Single(inbox.Read(basis, PlacementTests.Now));
        Assert.False(inbox.Project(basis, 1, PlacementTests.Now).Nodes[1].Ready);
        NodeFeedbackInbox.Publish(Inbox, Report("b", 2) with { Heat = null });
        Assert.Empty(inbox.Read(basis, PlacementTests.Now));
        Assert.False(inbox.Project(basis, 2, PlacementTests.Now).Nodes[1].Ready);
        NodeFeedbackInbox.Publish(Inbox, Report("b", 3) with { ObservedAtUtc = PlacementTests.Now.AddSeconds(1) });
        Assert.Single(inbox.Read(basis, PlacementTests.Now));
    }

    private static WorkloadInventory SmallHeatInventory() => PlacementTests.Inventory() with
    { Sites = PlacementTests.Inventory().Sites.Select(s => s with { Heat = new(.8, 0, 0, 1, .8) }).ToArray() };

    [Fact] public void ReleasedHeatStaysCommittedAcrossUpdatesAndRestartUntilNextHour()
    {
        var clock = new Clock(); var runtime = new Runtime(); var basis = SmallHeatInventory();
        var request = PlacementTests.Request() with { PlacementPolicy = new("origin-site", "Synthetic fixture", null) };
        using (var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true))
        {
            c.Update(basis, clock.Now); c.Reconcile(Nodes);
            Assert.NotNull(c.TryStart(request, Nodes, CoordinatorTests.Profile, clock.Now));
            c.StopAndRelease(request.Id);
            c.Update(basis with { Revision = 2 }, clock.Now);
            Assert.Null(c.TryStart(request with { Id = "next" }, Nodes, CoordinatorTests.Profile, clock.Now));
        }
        Assert.Throws<InvalidDataException>(() => new WorkloadCoordinator(fleet, State, runtime, clock));
        using var restarted = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        restarted.Update(basis, clock.Now); restarted.Reconcile(Nodes);
        Assert.Null(restarted.TryStart(request with { Id = "next" }, Nodes, CoordinatorTests.Profile, clock.Now));
        clock.Now = clock.Now.AddHours(1);
        restarted.Update(basis with { Revision = 3, IntervalStartUtc = clock.Now, IntervalEndUtc = clock.Now.AddHours(1),
            ResourceObservedAtUtc = clock.Now, ResourceValidUntilUtc = clock.Now.AddMinutes(1) }, clock.Now);
        Assert.NotNull(restarted.TryStart(request with { Id = "next" }, Nodes, CoordinatorTests.Profile, clock.Now));
    }

    [Fact] public void PausingRetainsHeatButResumeDoesNotCountOwnCommitmentTwice()
    {
        var clock = new Clock(); var runtime = new Runtime();
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        c.Update(SmallHeatInventory(), clock.Now); c.Reconcile(Nodes);
        var batch = PlacementTests.Request("batch", "batch") with { PlacementPolicy = new("origin-site", "Synthetic", null) };
        c.TryStart(batch, Nodes, CoordinatorTests.Profile, clock.Now); c.PauseBatch(batch.Id);
        Assert.Null(c.TryStart(batch with { Id = "other" }, Nodes, CoordinatorTests.Profile, clock.Now));
        Assert.True(c.ResumeBatch(batch.Id, clock.Now));
    }

    [Fact] public void FeedbackExpiryDuringCreateBlocksUntimedGameStart()
    {
        var clock = new Clock(); var runtime = new Runtime { OnCreate = () => clock.Now = clock.Now.AddSeconds(2) };
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        c.Update(PlacementTests.Inventory() with { ResourceValidUntilUtc = clock.Now.AddSeconds(1) }, clock.Now); c.Reconcile(Nodes);
        c.TryStart(PlacementTests.Request(), Nodes, CoordinatorTests.Profile, PlacementTests.Now);
        Assert.Equal(0, runtime.Inner.Starts); Assert.Equal("created", c.Attempts().Single().Phase);
        Assert.Single(c.Allocations());
    }

    [Theory] [InlineData(.8, false)] [InlineData(3, true)]
    public void BatchPausePreviewRetainsHeatAndOnlyPausesWhenAdmissionCanBenefit(double heatKwh, bool shouldStart)
    {
        var clock = new Clock(); var runtime = new Runtime();
        var basis = SmallHeatInventory();
        basis = basis with { Sites = basis.Sites.Select(s => s with { Heat = s.Heat with { DemandKwh = heatKwh } }).ToArray() };
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        c.Update(basis, clock.Now); c.Reconcile(Nodes);
        var batch = PlacementTests.Request("batch", "batch", cpu: 2) with { PlacementPolicy = new("origin-site", "Synthetic", null) };
        c.TryStart(batch, Nodes, CoordinatorTests.Profile, clock.Now);
        var game = PlacementTests.Request() with { PlacementPolicy = batch.PlacementPolicy };
        Assert.Equal(shouldStart, c.TryStart(game, Nodes, CoordinatorTests.Profile, clock.Now) is not null);
        Assert.Equal(shouldStart ? 1 : 0, runtime.Inner.Pauses);
    }

    [Fact] public void MalformedFeedbackDoesNotPreventDeadlineStop()
    {
        var clock = new Clock(); var runtime = new Runtime();
        var request = PlacementTests.Request("timed", "batch") with { Deadline = new(clock.Now, clock.Now.AddSeconds(2), "Synthetic target", true) };
        var config = new WorkloadConfiguration(fleet, PlacementTests.Inventory(), Nodes, [new(request, CoordinatorTests.Profile)]);
        var inbox = new NodeFeedbackInbox(Inbox, State, fleet, Policy, config.Inventory);
        NodeFeedbackInbox.Publish(Inbox, Report("a")); NodeFeedbackInbox.Publish(Inbox, Report("b"));
        using var c = new WorkloadCoordinator(fleet, State, runtime, clock, retainHourlyHeat: true);
        var owner = new NodeFeedbackOwner(c, config, inbox, Policy, clock);
        owner.Tick(() => config);
        File.WriteAllText(Path.Combine(Inbox, "node-b.json"), "invalid"); clock.Now = clock.Now.AddSeconds(3);
        Assert.NotEmpty(owner.Tick(() => config));
        Assert.Equal("missed", c.Attempts().Single().Deadline!.Status);
        Assert.Equal("released", c.Attempts().Single().Phase);
    }

    [Fact] public async Task AtomicFileNotificationWakesBeforeLongFallbackAndBurstsAreBounded()
    {
        using var wake = new NodeFeedbackWake(Inbox);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = wake.WaitAsync(TimeSpan.FromSeconds(30), limit.Token);
        NodeFeedbackInbox.Publish(Inbox, Report("a"));
        await waiting;
        for (int i = 0; i < 10000; i++) wake.Notify();
        await wake.WaitAsync(TimeSpan.FromSeconds(30), limit.Token);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wake.WaitAsync(TimeSpan.FromSeconds(30), cancelled.Token));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
