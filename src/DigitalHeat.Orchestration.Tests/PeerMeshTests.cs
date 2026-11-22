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
        /// <summary>Queue messages until Deliver(), like the asynchronous HTTP channel between real peers.</summary>
        public bool Queued;
        private readonly Queue<(string From, string To, PeerMessage Message)> pending = new();
        public void Send(string from, string to, PeerMessage message)
        {
            Messages.Add((from, to, message));
            if (Queued) { pending.Enqueue((from, to, message)); return; }
            if (Drop?.Invoke(from, to, message) != true && Peers.TryGetValue(to, out var peer)) peer.Receive(from, message);
        }
        /// <summary>Deliver queued messages in order; with <paramref name="stopAfter"/>, stop after the first match.</summary>
        public void Deliver(Func<PeerMessage, bool>? stopAfter = null)
        {
            for (int n = pending.Count; n > 0; n--)
            {
                var (from, to, message) = pending.Dequeue();
                if (Drop?.Invoke(from, to, message) != true && Peers.TryGetValue(to, out var peer)) peer.Receive(from, message);
                if (stopAfter?.Invoke(message) == true) return;
            }
        }
    }
    private WorkloadInventory Inventory(string id, double heat, long revision = 1, bool ready = true, int slots = 2) =>
        new(revision, PlacementTests.Now, PlacementTests.Now.AddHours(1), "Synthetic mesh fixture, not hardware/game measurements",
            [new(id, "CH", new(heat, 0, 0, 1, .8), 0)], [new("domain-" + id, new(2, 256, slots), [])],
            [new("node-" + id, id, "domain-" + id, ready, true, new(2, 256, slots), [], [], [], [new("player", id == "a" ? 5 : 20)])], [])
        { ResourceObservedAtUtc = clock.Now, ResourceValidUntilUtc = clock.Now.AddMinutes(1) };
    private MeshPeer Add(string id, double heat, long revision = 1, TimeSpan minimumFreeze = default, int slots = 2)
    {
        if (!runtimes.TryGetValue(id, out var runtime)) runtimes[id] = runtime = new();
        var peer = new MeshPeer(fleet, id, Path.Combine(root, id), Inventory(id, heat, revision, slots: slots),
            [CoordinatorTests.Node("node-" + id)], new Dictionary<string, ContainerProfile> { [CoordinatorTests.Profile.Id] = CoordinatorTests.Profile },
            ["a", "b", "c"], bus, runtime, clock, TimeSpan.FromSeconds(20), minimumFreeze);
        peers.Add(peer); bus.Peers[id] = peer; return peer;
    }
    private static WorkloadRequest Request(string origin, string id = "job") => PlacementTests.Request(id) with
        { OriginSiteId = origin, Lifecycle = new(false, true, false) };
    private void Publish() { foreach (var peer in peers) peer.Publish(); }

    [Fact] public void WithdrawnPeerCannotBeSelectedFromStaleOffersAndWithdrawalSurvivesRestart()
    {
        var a=Add("a",.1);var b=Add("b",10);var c=Add("c",2);Publish();
        var stale=bus.Messages.Select(m=>m.Message).OfType<PeerOffer>().First(o=>o.PeerId=="b");
        a.WithdrawPeer("b");a.Receive("b",stale with{Sequence=stale.Sequence+100});
        a.Submit(Request("a","first"),CoordinatorTests.Profile.Id);a.Tick();
        Assert.Equal("c",a.Jobs().Single().Target);Assert.Equal(0,runtimes["b"].Starts);
        a.Dispose();peers.Remove(a);var recovered=Add("a",.1);
        Assert.True(recovered.IsPeerWithdrawn("b"));
    }

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

    [Fact] public void QueuedMeshDoesNotProposeMoreThanAPeerCanTake()
    {
        // ADR 0074. With asynchronous delivery, replies and new offers arrive after the origin's next pass.
        // Each pass must count work it already proposed instead of sending every waiting job to the same
        // single-slot peer, which then rejects all but one (and journals every rejection).
        bus.Queued = true;
        var a = Add("a", 0); var b = Add("b", 50, slots: 1); var c = Add("c", 50, slots: 1);
        Publish(); bus.Deliver();
        for (int i = 1; i <= 12; i++)
            a.Submit(PlacementTests.Request($"job-{i:D2}", "batch") with { OriginSiteId = "a", Lifecycle = new(false, true, false) }, CoordinatorTests.Profile.Id);
        for (int step = 0; step < 200 && a.Jobs().Any(j => j.Status != "accepted"); step++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            a.Tick(); bus.Deliver(); bus.Deliver();
            foreach (var (peer, id) in new[] { (b, "b"), (c, "c") })
            {
                foreach (var row in peer.Attempts().Where(r => r.Phase == "running"))
                    runtimes[id].States[row.Attempt.AttemptId] = new("exited", "abcdef123456", false) { ExitCode = 0 };
                peer.Tick();
            }
            bus.Deliver();
        }
        Assert.All(a.Jobs(), j => Assert.Equal("accepted", j.Status));
        Assert.Equal(6, b.Attempts().Length); Assert.Equal(6, c.Attempts().Length); // free-share spread unchanged
        int rejected = bus.Messages.Count(m => m.Message is PeerReply { Status: "rejected" });
        Assert.True(rejected == 0, $"{rejected} proposals were rejected");
    }

    [Fact] public void AcceptedJobStillHoldsTheSlotWhenARoutineOfferOvertakesTheProposal()
    {
        // ADR 0074 review: B publishes a routine offer before it sees A's proposal. A then gets B's
        // "accepted" reply, but not yet B's next offer. B's routine offer still shows the slot free.
        bus.Queued = true;
        var a = Add("a", 0); var b = Add("b", 50, slots: 1); var c = Add("c", 50, slots: 1);
        Publish(); bus.Deliver();
        Job(a, "one"); a.Tick();                         // proposed to B (tie broken by site ID)
        b.Publish();                                     // routine offer, queued before B sees the proposal
        bus.Deliver();                                   // B accepts; reply and B's new offer are queued
        bus.Deliver(m => m is PeerReply);                // only the reply reaches A
        Assert.Equal("accepted", a.Jobs().Single(j => j.Request.Id == "a.one").Status);
        Job(a, "two"); a.Tick();
        Assert.Equal("c", a.Jobs().Single(j => j.Request.Id == "a.two").Target);
        Assert.DoesNotContain(bus.Messages, m => m.Message is PeerReply { Status: "rejected" });

        static void Job(MeshPeer origin, string id) => origin.Submit(PlacementTests.Request(id, "batch")
            with { OriginSiteId = "a", Lifecycle = new(false, true, false) }, CoordinatorTests.Profile.Id);
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
        Assert.Empty(coordinator.ActiveAttempts());Assert.Single(coordinator.Attempts());
        Assert.Equal(.8,coordinator.CommittedHeatKwh(inventory.IntervalStartUtc));
        Assert.Null(coordinator.TryStartAtSite(Request("a", "second"), nodes, CoordinatorTests.Profile, clock.Now, "a", inventory.Sites[0].Heat, 1, inventory.IntervalStartUtc));
    }

    [Fact] public void CompetingOriginsUsingStaleOffersCannotOversubscribeTarget()
    {
        // B has one slot. Origins A and C each plan from B's stale offer and propose to it; neither can see
        // the other's proposal. B must admit one and reject the other. (An origin's own unanswered proposals
        // already hold capacity in its planner since ADR 0074, so A's second job goes straight to C.)
        var a = Add("a", 1); var b = Add("b", 8, slots: 1); var c = Add("c", 6); Publish();
        bus.Drop = (_, _, message) => message is PeerProposal;
        a.Submit(Request("a", "one"), CoordinatorTests.Profile.Id); a.Tick();
        c.Submit(Request("c", "two"), CoordinatorTests.Profile.Id); c.Tick();
        a.Submit(Request("a", "three"), CoordinatorTests.Profile.Id); a.Tick();
        var proposals = bus.Messages.Where(m => m.To == "b" && m.Message is PeerProposal)
            .Select(m => (PeerProposal)m.Message).DistinctBy(p => p.Request.Id).ToArray();
        Assert.Equal(new[] { "a", "c" }, proposals.Select(p => p.Origin).Order().ToArray());
        foreach (var proposal in proposals) b.Receive(proposal.Origin, proposal);
        Assert.Equal(1, runtimes["b"].Starts);
        bus.Drop = null; a.Tick(); c.Tick();
        // The rejected job is re-planned to a peer with room (free-share rank, ADR 0056), never back onto B.
        Assert.Equal(2, runtimes["a"].Starts + runtimes["c"].Starts); Assert.Single(b.Attempts());
        Assert.All(a.Jobs().Concat(c.Jobs()), j => Assert.Equal("accepted", j.Status));
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

    [Fact] public void BatchFreezesWhenItsHeatExceedsWhatCanBeUsedOrStored()
    {
        // Decision 0068. 0.8 kWh/h job; the fixture site has no tank, so nothing can be stored.
        var a = Add("a", .5); var b = Add("b", 8); Publish();
        var batch = Request("a", "batch") with { Kind = "batch", Lifecycle = new(true, true, false) };
        a.Submit(batch, CoordinatorTests.Profile.Id); a.Tick();
        Assert.Equal("running", b.Attempts().Single().Phase);
        b.Update(Inventory("b", .5, 2));   // useful heat > 0 but below the job's heat: would dump 0.3 kWh/h
        Assert.Equal("paused", b.Attempts().Single().Phase);
        b.Update(Inventory("b", .79, 3));  // still too little: no thaw-then-refreeze
        Assert.Equal("paused", b.Attempts().Single().Phase);
        b.Update(Inventory("b", .8, 4));   // fits exactly
        Assert.Equal("running", b.Attempts().Single().Phase);
    }

    private WorkloadInventory WithTank(string id, double demand, double stored, long revision) => Inventory(id, demand, revision) with
        { Sites = [new(id, "CH", new(demand, stored, 35, 1, .8), 0)] };
    private (MeshPeer A, MeshPeer B) TwoBatchJobsOnB()
    {
        var a = Add("a", .5); var b = Add("b", 8); Publish();
        foreach (var name in new[] { "batch1", "batch2" })
        { a.Submit(Request("a", name) with { Kind = "batch", Lifecycle = new(true, true, false) }, CoordinatorTests.Profile.Id); a.Tick(); }
        Assert.All(b.Attempts(), r => Assert.Equal("running", r.Phase));
        return (a, b);
    }
    private static string Phase(MeshPeer peer, string job) => peer.Attempts().Single(r => r.Attempt.Request.Id == "a." + job).Phase;

    [Fact] public void BatchKeepsRunningWhileTheTankCanStoreTheSurplus()
    {
        var a = Add("a", .5); var b = Add("b", 8); Publish();
        var batch = Request("a", "batch") with { Kind = "batch", Lifecycle = new(true, true, false) };
        a.Submit(batch, CoordinatorTests.Profile.Id); a.Tick();
        b.Update(WithTank("b", .1, 30, 2));     // demand below job heat, but 5 kWh of room: store it
        Assert.Equal("running", b.Attempts().Single().Phase);
        b.Update(WithTank("b", .1, 34.5, 3));   // 0.1 + 0.5 kWh room < 0.8 kWh/h: the rest would be dumped
        Assert.Equal("paused", b.Attempts().Single().Phase);
        b.Update(WithTank("b", .1, 34.3, 4));   // 0.8 fits again
        Assert.Equal("running", b.Attempts().Single().Phase);
    }

    [Fact] public void LowestPriorityBatchFreezesFirstAndOnlyWhatFitsThaws()
    {
        var (_, b) = TwoBatchJobsOnB();
        b.Update(Inventory("b", 1.0, 2));      // room for one 0.8 kWh/h job: the later ID freezes
        Assert.Equal("running", Phase(b, "batch1")); Assert.Equal("paused", Phase(b, "batch2"));
        b.Update(Inventory("b", .5, 3));       // neither fits
        Assert.Equal("paused", Phase(b, "batch1")); Assert.Equal("paused", Phase(b, "batch2"));
        // Same hour: frozen jobs keep their hourly commitment (pause does not reopen the budget).
        b.Update(Inventory("b", 1.2, 4));
        Assert.Equal("paused", Phase(b, "batch1")); Assert.Equal("paused", Phase(b, "batch2"));
        // Next hour, commitments reset: one fits, so only the higher priority thaws.
        clock.Now = PlacementTests.Now.AddHours(1);
        b.Update(Inventory("b", 1.2, 5) with { IntervalStartUtc = clock.Now, IntervalEndUtc = clock.Now.AddHours(1),
            ResourceObservedAtUtc = clock.Now, ResourceValidUntilUtc = clock.Now.AddMinutes(1) });
        Assert.Equal("running", Phase(b, "batch1")); Assert.Equal("paused", Phase(b, "batch2"));
    }

    [Fact] public void FrozenBatchWaitsTheMinimumFreezeTime()
    {
        var a = Add("a", .5); var b = Add("b", 8, minimumFreeze: TimeSpan.FromSeconds(30)); Publish();
        a.Submit(Request("a", "batch") with { Kind = "batch", Lifecycle = new(true, true, false) }, CoordinatorTests.Profile.Id); a.Tick();
        b.Update(Inventory("b", .5, 2)); Assert.Equal("paused", b.Attempts().Single().Phase);
        clock.Now = clock.Now.AddSeconds(10);
        b.Update(Inventory("b", 8, 3)); Assert.Equal("paused", b.Attempts().Single().Phase);
        clock.Now = clock.Now.AddSeconds(25);
        b.Update(Inventory("b", 8, 4)); Assert.Equal("running", b.Attempts().Single().Phase);
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

    [Fact] public void OperatorHoldSurvivesRestartAndHeatCannotResumeIt()
    {
        var a=Add("a",1);var b=Add("b",8);Publish();
        a.Submit(Request("a","render") with {Kind="batch",Lifecycle=new(true,true,false)},CoordinatorTests.Profile.Id);a.Tick();
        b.SetHold(true);Assert.Equal("paused",b.Attempts().Single().Phase);
        b.Update(Inventory("b",9,2));b.Tick();Assert.Equal("paused",b.Attempts().Single().Phase);
        b.Dispose();peers.Remove(b);b=Add("b",9,3);
        Assert.True(b.Held);b.Tick();Assert.Equal("paused",b.Attempts().Single().Phase);
        b.SetHold(false);b.Tick();Assert.Equal("running",b.Attempts().Single().Phase);
        Assert.Equal(1,runtimes["b"].Creates);
    }

    [Fact] public void HeldOriginDoesNotDispatchQueuedFrame()
    {
        var a=Add("a",1);Add("b",8);Publish();a.SetHold(true);
        a.Submit(Request("a","queued"),CoordinatorTests.Profile.Id);a.Tick();
        Assert.Null(a.Jobs().Single().Target);Assert.All(runtimes.Values,r=>Assert.Equal(0,r.Starts));
        a.SetHold(false);a.Tick();Assert.Equal("b",a.Jobs().Single().Target);
    }

    [Fact] public void CancelledBatchRejectsDelayedProposalAndReplyAfterRestart()
    {
        var a=Add("a",1);var b=Add("b",8);Publish();
        bus.Drop=(_,_,m)=>m is PeerProposal;
        string id=a.Submit(Request("a","cancel") with{Kind="batch",Lifecycle=new(true,true,false)},CoordinatorTests.Profile.Id);a.Tick();
        var proposal=bus.Messages.Select(m=>m.Message).OfType<PeerProposal>().First();
        a.SetHold(true);b.SetHold(true);a.CancelBatchWorkloads([id]);b.CancelBatchWorkloads([id]);
        b.Dispose();peers.Remove(b);b=Add("b",8,2);bus.Drop=null;
        b.Receive("a",proposal);a.Receive("b",new PeerReply(id,proposal.Generation,proposal.Identity,"accepted"));
        a.SetHold(false);b.SetHold(false);a.Tick();b.Tick();
        Assert.Equal("cancelled",a.Jobs().Single().Status);Assert.Equal(0,runtimes["b"].Creates);
    }
    [Fact] public void CancellationNeverStopsProtectedInteractiveWork()
    {
        var a=Add("a",1);var b=Add("b",8);Publish();string id=a.Submit(Request("a"),CoordinatorTests.Profile.Id);a.Tick();
        b.SetHold(true);Assert.Throws<InvalidOperationException>(()=>b.CancelBatchWorkloads([id]));
        Assert.Equal("running",b.Attempts().Single().Phase);
    }
    [Fact] public void CancellationFenceBlocksSubmissionArrivingAfterSnapshot()
    {
        var a=Add("a",8);Publish();a.SetHold(true);a.CancelBatchWorkloads(["a.late"]);a.SetHold(false);
        a.Submit(Request("a","late") with{Kind="batch",Lifecycle=new(true,true,false)},CoordinatorTests.Profile.Id);
        a.Tick();Assert.Empty(a.Jobs());Assert.Equal(0,runtimes["a"].Creates);
    }

    public void Dispose()
    {
        foreach (var peer in peers) peer.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
