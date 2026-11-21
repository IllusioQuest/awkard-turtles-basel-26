using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class PeerMeshTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dh-mesh-" + Guid.NewGuid().ToString("N"));
    private readonly string fleet = "mesh-" + Guid.NewGuid().ToString("N");
    private readonly Clock clock = new();
    private readonly Bus bus = new();
    private readonly List<MeshPeer> peers = [];
    private readonly Dictionary<string, CoordinatorTests.Runtime> runtimes = [];
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = PlacementTests.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Bus : IPeerMeshTransport
    {
        public readonly Dictionary<string, MeshPeer> Peers = [];
        public readonly List<(string From, string To, PeerMessage Message)> Messages = [];
        public Func<string, string, PeerMessage, bool>? Drop;
        public void Send(string from, string to, PeerMessage message)
        {
            Messages.Add((from, to, message));
            if (Drop?.Invoke(from, to, message) != true && Peers.TryGetValue(to, out var peer)) peer.Receive(from, message);
        }
    }
    private WorkloadInventory Inventory(string id, double heat, long revision = 1, bool ready = true) =>
        new(revision, PlacementTests.Now, PlacementTests.Now.AddHours(1), "Synthetic mesh fixture, not hardware/game measurements",
            [new(id, "CH", new(heat, 0, 0, 1, .8), 0)], [new("domain-" + id, new(2, 256, 2), [])],
            [new("node-" + id, id, "domain-" + id, ready, true, new(2, 256, 2), [], [], [], [new("player", id == "a" ? 5 : 20)])], [])
        { ResourceObservedAtUtc = clock.Now, ResourceValidUntilUtc = clock.Now.AddMinutes(1) };
    private MeshPeer Add(string id, double heat, long revision = 1)
    {
        if (!runtimes.TryGetValue(id, out var runtime)) runtimes[id] = runtime = new();
        var peer = new MeshPeer(fleet, id, Path.Combine(root, id), Inventory(id, heat, revision),
            [CoordinatorTests.Node("node-" + id)], new Dictionary<string, ContainerProfile> { [CoordinatorTests.Profile.Id] = CoordinatorTests.Profile },
            ["a", "b", "c"], bus, runtime, clock, TimeSpan.FromSeconds(20));
        peers.Add(peer); bus.Peers[id] = peer; return peer;
    }
    private static WorkloadRequest Request(string origin, string id = "job") => PlacementTests.Request(id) with
        { OriginSiteId = origin, Lifecycle = new(false, true, false) };
    private void Publish() { foreach (var peer in peers) peer.Publish(); }

    [Fact] public void EveryOriginCanReachEveryPeerAndHeatWinsInsideLatencyLimit()
    {
        // Decision 0056: an idle eligible peer outranks a busy one, so a and c get too little heat
        // (0.5 kWh < 0.8 kWh per job) to run the work themselves; b must be reached from both origins.
        var a = Add("a", .5); var b = Add("b", 8); var c = Add("c", .5); Publish();
        string id = a.Submit(Request("a") with { LatencyLimits = [new("player", 25)] }, CoordinatorTests.Profile.Id);
        a.Tick(); Assert.Equal("b", a.Jobs().Single().Target); Assert.Equal(id, b.Attempts().Single().Attempt.Request.Id);
        c.Submit(Request("c", "other"), CoordinatorTests.Profile.Id); c.Tick();
        Assert.Contains(bus.Messages, m => m.From == "c" && m.To == "b" && m.Message is PeerProposal);
        Assert.Equal(2, runtimes["b"].Starts);
    }

    [Fact] public void RequiredGamingLatencyCanExcludeHotterPeers()
    {
        var a = Add("a", 1); Add("b", 8); Publish();
        a.Submit(Request("a") with { LatencyLimits = [new("player", 10)] }, CoordinatorTests.Profile.Id);
        a.Tick(); Assert.Equal("a", a.Jobs().Single().Target);
    }

    [Fact] public void LostAcknowledgementAndOriginRestartNeverChooseAnotherTarget()
    {
        var a = Add("a", 1); var b = Add("b", 8); Publish();
        bus.Drop = (from, to, message) => message is PeerReply && to == "a";
        a.Submit(Request("a"), CoordinatorTests.Profile.Id); a.Tick();
        Assert.Equal("proposed", a.Jobs().Single().Status); Assert.Equal(1, runtimes["b"].Starts);
        a.Dispose(); peers.Remove(a); bus.Peers.Remove("a");
        var c = Add("c", 20); c.Publish(); // Other peers keep operating, but do not steal a's job.
        clock.Now = clock.Now.AddSeconds(21); // Original offer expires while the origin is down.
        a = Add("a", 1); bus.Drop = null; a.Tick();
        Assert.Equal("accepted", a.Jobs().Single().Status); Assert.Equal("b", a.Jobs().Single().Target);
        Assert.Equal(1, runtimes["b"].Starts); Assert.Empty(c.Attempts());
    }

    [Fact] public void RejectionTombstonePreventsDelayedDuplicateAfterReplanning()
    {
        var a = Add("a", 1); var b = Add("b", 8); var c = Add("c", 6); Publish();
        PeerProposal? delayed = null;
        bus.Drop = (from, to, message) =>
        {
            if (to != "b" || message is not PeerProposal proposal) return false;
            delayed = proposal; return true;
        };
        a.Submit(Request("a"), CoordinatorTests.Profile.Id); a.Tick(); Assert.NotNull(delayed);
        b.Update(Inventory("b", 8, 2, ready: false));
        bus.Drop = null; b.Receive("a", delayed!); // Confirmed rejection at the old target.
        a.Tick(); Assert.Equal("c", a.Jobs().Single().Target); Assert.Equal(1, runtimes["c"].Starts);
        b.Dispose(); peers.Remove(b); bus.Peers.Remove("b");
        b = Add("b", 8, 3); b.Receive("a", delayed!); // Tombstone survives target restart too.
        Assert.Equal(0, runtimes["b"].Starts); Assert.Equal("c", a.Jobs().Single().Target);
    }

    [Fact] public void ExpiredOffersAndPartitionsBlockNewWorkWithoutStoppingGames()
    {
        var a = Add("a", 0); var b = Add("b", 8); Publish();
        a.Submit(Request("a", "active"), CoordinatorTests.Profile.Id); a.Tick();
        bus.Drop = (from, to, message) => from == "b" || to == "b";
        clock.Now = clock.Now.AddSeconds(21);
        a.Submit(Request("a", "waiting"), CoordinatorTests.Profile.Id); a.Tick();
        Assert.Equal("waiting", a.Jobs().Single(j => j.Request.Id.EndsWith("waiting")).Status);
        Assert.Equal("running", b.Attempts().Single().Phase); Assert.Equal(0, runtimes["b"].Pauses);
    }

    [Fact] public void SelectedSiteAcceptsRetainedBudgetSnapshotAndRejectsSpentHeat()
    {
        var runtime = new CoordinatorTests.Runtime();
        using var coordinator = new WorkloadCoordinator(fleet, root, runtime, clock, retainHourlyHeat: true);
        var inventory = Inventory("a", .8);
        RuntimeNode[] nodes = [CoordinatorTests.Node("node-a")];
        coordinator.Update(inventory, clock.Now); coordinator.Reconcile(nodes);
        var first = coordinator.TryStartAtSite(Request("a"), nodes, CoordinatorTests.Profile, clock.Now, "a", inventory.Sites[0].Heat, 1, inventory.IntervalStartUtc);
        Assert.NotNull(first); coordinator.StopAndRelease(first!.Attempt.Request.Id);
        Assert.Null(coordinator.TryStartAtSite(Request("a", "second"), nodes, CoordinatorTests.Profile, clock.Now, "a", inventory.Sites[0].Heat, 1, inventory.IntervalStartUtc));
    }

    [Fact] public void CompetingOriginsUsingStaleOffersCannotOversubscribeTarget()
    {
        var a = Add("a", 1); var b = Add("b", 8); var c = Add("c", 6); Publish();
        bus.Drop = (_, _, message) => message is PeerProposal;
        a.Submit(Request("a", "one"), CoordinatorTests.Profile.Id); a.Tick();
        c.Submit(Request("c", "two"), CoordinatorTests.Profile.Id); c.Tick();
        a.Submit(Request("a", "three"), CoordinatorTests.Profile.Id); a.Tick();
        var proposals = bus.Messages.Where(m => m.To == "b" && m.Message is PeerProposal)
            .Select(m => (PeerProposal)m.Message).DistinctBy(p => p.Request.Id).ToArray();
        Assert.Equal(3, proposals.Length);
        foreach (var proposal in proposals) b.Receive(proposal.Origin, proposal);
        Assert.Equal(2, runtimes["b"].Starts);
        bus.Drop = null; a.Tick();
        Assert.Equal(1, runtimes["c"].Starts);
        Assert.All(a.Jobs(), j => Assert.Equal("accepted", j.Status));
    }

    [Fact] public void PeerHeatUpdatePausesBatchThroughSharedControllerButProtectsGame()
    {
        // a cannot host 0.8 kWh jobs, so both go to b even though b is already busy (decision 0056).
        var a = Add("a", .5); var b = Add("b", 8); Publish();
        var batch = Request("a", "batch") with { Kind = "batch", Lifecycle = new(true, true, false) };
        a.Submit(batch, CoordinatorTests.Profile.Id); a.Tick();
        b.Update(Inventory("b", 0, 2)); Assert.Equal("paused", b.Attempts().Single().Phase);
        b.Update(Inventory("b", 8, 3)); Assert.Equal("running", b.Attempts().Single().Phase);
        a.Submit(Request("a", "game"), CoordinatorTests.Profile.Id); a.Tick();
        b.Update(Inventory("b", 0, 4));
        Assert.Equal("running", b.Attempts().Single(a => a.Attempt.Request.Kind == "interactive").Phase);
        Assert.Equal("paused", b.Attempts().Single(a => a.Attempt.Request.Kind == "batch").Phase);
    }

    [Fact] public void RetryingLostReplyStillEnforcesTargetBatchDeadline()
    {
        var a = Add("a", 1); var b = Add("b", 8); Publish();
        bus.Drop = (_, to, message) => to == "a" && message is PeerReply;
        var batch = Request("a", "timed") with { Kind = "batch", Lifecycle = new(true, true, false),
            Deadline = new(clock.Now, clock.Now.AddSeconds(5), "Synthetic deadline fixture", true) };
        a.Submit(batch, CoordinatorTests.Profile.Id); a.Tick();
        clock.Now = clock.Now.AddSeconds(6); bus.Drop = null; a.Tick();
        Assert.Equal("missed", b.Attempts().Single().Deadline!.Status);
        Assert.Equal("released", b.Attempts().Single().Phase);
        Assert.Equal(1, runtimes["b"].Starts);
    }

    [Fact] public void TargetRecoversCreatedAttemptWithoutDuplicateCreate()
    {
        var a = Add("a", 1); var b = Add("b", 8); Publish();
        runtimes["b"].FailCreateAfterDispatch = true;
        a.Submit(Request("a"), CoordinatorTests.Profile.Id); a.Tick();
        Assert.Equal("proposed", a.Jobs().Single().Status);
        runtimes["b"].FailCreateAfterDispatch = false;
        b.Tick(); a.Tick();
        Assert.Equal("accepted", a.Jobs().Single().Status);
        Assert.Equal(1, runtimes["b"].Creates); Assert.Equal(1, runtimes["b"].Starts);
    }

    public void Dispose()
    {
        foreach (var peer in peers) peer.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
