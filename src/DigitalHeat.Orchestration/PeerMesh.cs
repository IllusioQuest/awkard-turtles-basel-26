using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "message_type")]
[JsonDerivedType(typeof(PeerOffer), "offer")]
[JsonDerivedType(typeof(PeerProposal), "proposal")]
[JsonDerivedType(typeof(PeerReply), "reply")]
public abstract record PeerMessage;
public sealed record PeerOffer(string PeerId, long Sequence, DateTimeOffset ExpiresAtUtc,
    WorkloadInventory Inventory, string[] Profiles) : PeerMessage;
public sealed record PeerProposal(string Origin, long Generation, string Identity, WorkloadRequest Request,
    string ProfileId, long InventoryRevision, DateTimeOffset HourStartUtc, HeatSignal Heat) : PeerMessage;
public sealed record PeerReply(string JobId, long Generation, string Identity, string Status) : PeerMessage;

/// <summary>Simulation transport boundary. Senders are trusted configured peers; no live authentication claim.</summary>
public interface IPeerMeshTransport { void Send(string from, string to, PeerMessage message); }

public sealed record OriginJob(WorkloadRequest Request, string ProfileId, long Generation = 0,
    string? Target = null, string Status = "waiting", long InventoryRevision = 0,
    DateTimeOffset HourStartUtc = default, HeatSignal? Heat = null);
public sealed record ProposalOutcome(string Origin, string JobId, long Generation, string Identity, string Status);
public sealed record PeerJournal(string PeerId, long OfferSequence, OriginJob[] Jobs, ProposalOutcome[] Outcomes)
{
    public bool OperatorHold { get; init; }
    public string[] WithdrawnPeers { get; init; } = [];
    public string[] CancelledWorkloads { get; init; } = [];
}

/// <summary>
/// Origin-owned peer protocol, not a global scheduler or consensus service. Uses the existing placement
/// and durable executor boundary. Peers exchange snapshots; an offer never authorizes execution.
/// </summary>
public sealed class MeshPeer : IDisposable
{
    private readonly WorkloadCoordinator coordinator;
    private readonly RuntimeNode[] runtimeNodes;
    private readonly IReadOnlyDictionary<string, ContainerProfile> profiles;
    private readonly IPeerMeshTransport transport;
    private readonly TimeProvider clock;
    private readonly TimeSpan offerLifetime;
    private readonly TimeSpan minimumFreeze;
    private readonly Dictionary<string, DateTimeOffset> frozenAt = new(StringComparer.Ordinal);
    private readonly string journalPath;
    private readonly HashSet<string> peers;
    private readonly Dictionary<string, PeerOffer> offers = new(StringComparer.Ordinal);
    // ADR 0074: offer sequence each proposal was planned against (memory only; empty after restart).
    private readonly Dictionary<string, long> plannedAgainst = new(StringComparer.Ordinal);
    private WorkloadInventory inventory;
    private PeerJournal journal;
    public string Id { get; }

    public MeshPeer(string meshId, string id, string stateDirectory, WorkloadInventory inventory,
        RuntimeNode[] runtimeNodes, IReadOnlyDictionary<string, ContainerProfile> profiles,
        IEnumerable<string> peers, IPeerMeshTransport transport, IWorkloadRuntime runtime,
        TimeProvider clock, TimeSpan offerLifetime, TimeSpan minimumFreeze = default)
    {
        if (minimumFreeze < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumFreeze));
        this.minimumFreeze = minimumFreeze;
        if (inventory.Sites.Length != 1 || inventory.Sites[0].Id != id || inventory.Domains.Length != 1
            || offerLifetime <= TimeSpan.Zero) throw new ArgumentException("One physical domain/site per peer and a positive offer lifetime are required.");
        Id = id; this.runtimeNodes = runtimeNodes; this.profiles = profiles; this.transport = transport;
        this.clock = clock; this.offerLifetime = offerLifetime; this.peers = peers.ToHashSet(StringComparer.Ordinal);
        this.peers.Add(id);
        this.inventory = WorkloadJson.Copy(inventory);
        coordinator = new(meshId + "." + id, stateDirectory, runtime, clock, retainHourlyHeat: true);
        try
        {
            journalPath = Path.Combine(stateDirectory, "mesh-origin.json");
            journal = File.Exists(journalPath)
                ? JsonSerializer.Deserialize<PeerJournal>(File.ReadAllText(journalPath), WorkloadJson.Options)
                    ?? throw new InvalidDataException("Empty peer journal.")
                : new(id, 0, [], []);
            if (journal.PeerId != id) throw new InvalidDataException("Peer journal belongs to another origin.");
            coordinator.Update(this.inventory, clock.GetUtcNow());
            coordinator.Reconcile(runtimeNodes);
        }
        catch { coordinator.Dispose(); throw; }
    }

    public OriginJob[] Jobs() => WorkloadJson.Copy(journal.Jobs);
    public AttemptRecord[] Attempts() => coordinator.Attempts();
    public HeatCommitment[] HeatCommitments() => coordinator.HeatCommitments();
    public bool Held => journal.OperatorHold;
    public bool IsPeerWithdrawn(string peerId)=>journal.WithdrawnPeers.Contains(peerId,StringComparer.Ordinal);
    /// <summary>Explicit operator fencing only; not a health-timeout takeover mechanism.</summary>
    public void WithdrawPeer(string peerId)
    {
        if(peerId==Id||!peers.Contains(peerId))throw new ArgumentException("Invalid withdrawal target");
        if(!IsPeerWithdrawn(peerId)){journal=journal with{WithdrawnPeers=[..journal.WithdrawnPeers,peerId]};Save();}
        offers.Remove(peerId);
    }
    public void CancelBatchWorkloads(string[] ids)
    {
        if(!journal.OperatorHold)throw new InvalidOperationException("Hold peer before cancelling frame jobs");
        if(journal.Jobs.Any(j=>ids.Contains(j.Request.Id,StringComparer.Ordinal)
            &&(j.Request.Kind!="batch"||!j.Request.Lifecycle.AllowStop)))
            throw new InvalidOperationException("Cannot cancel protected origin work");
        var selected=coordinator.Attempts().Where(a=>ids.Contains(a.Attempt.Request.Id,StringComparer.Ordinal)&&a.Phase!="released").ToArray();
        if(selected.Any(a=>a.Attempt.Request.Kind!="batch"||!a.Attempt.Request.Lifecycle.AllowStop))
            throw new InvalidOperationException("Cannot cancel protected work");
        journal=journal with{CancelledWorkloads=journal.CancelledWorkloads.Concat(ids).Distinct(StringComparer.Ordinal).ToArray(),
            Jobs=journal.Jobs.Select(j=>ids.Contains(j.Request.Id,StringComparer.Ordinal)?j with{Status="cancelled"}:j).ToArray()};
        Save(); // Tombstones precede stop/release and survive delayed proposal/reply delivery.
        foreach(var row in selected)coordinator.StopAndRelease(row.Attempt.Request.Id);
        Publish();
    }
    /// <summary>Durable hold for a dedicated workload peer. Blocks dispatch and automatic heat resume.</summary>
    public void SetHold(bool hold)
    {
        journal = journal with { OperatorHold = hold }; Save();
        if (hold)
            foreach (var row in coordinator.Attempts().Where(a => a.Phase == "running" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowPause))
                coordinator.PauseBatch(row.Attempt.Request.Id);
    }

    public string Submit(WorkloadRequest request, string profileId)
    {
        if (request.OriginSiteId != Id) throw new InvalidDataException("Only an origin may submit its own job.");
        var global = request with { Id = Id + "." + request.Id };
        WorkloadPlacement.ValidateRequest(global);
        if(journal.CancelledWorkloads.Contains(global.Id,StringComparer.Ordinal))return global.Id;
        var prior = journal.Jobs.SingleOrDefault(j => j.Request.Id == global.Id);
        if (prior is not null)
        {
            if (Identity(prior.Request, prior.ProfileId) != Identity(global, profileId))
                throw new InvalidDataException("Origin job identity is immutable.");
            return global.Id;
        }
        journal = journal with { Jobs = [.. journal.Jobs, new(WorkloadJson.Copy(global), profileId)] };
        Save();
        return global.Id;
    }

    public void Update(WorkloadInventory next)
    {
        coordinator.Update(next, clock.GetUtcNow());
        inventory = WorkloadJson.Copy(next);
        ApplyLocalHeat();
        Publish();
    }

    public void Publish()
    {
        var now = clock.GetUtcNow();
        var claims = coordinator.Allocations();
        // Remote planners have no local ownership ledger: advertise all retained hourly heat,
        // plus actual held resources. Pausing frees CPU, never GPU/RAM/slots or committed heat.
        double committed = coordinator.CommittedHeatKwh(inventory.IntervalStartUtc);
        var snapshot = inventory with
        {
            Sites = [inventory.Sites[0] with { ExternalCommittedHeatKwh = inventory.Sites[0].ExternalCommittedHeatKwh + committed }],
            ExternalAllocations = [.. inventory.ExternalAllocations, .. claims.Select(a => new ExternalAllocation(a.Request.Id,
                a.Reservation.NodeId, a.Request.Resources with { CpuCores = a.Paused ? 0 : a.Request.Resources.CpuCores }, a.Reservation.GpuDeviceId))]
        };
        var expiry = now + offerLifetime;
        if (inventory.ResourceValidUntilUtc is {} resourcesUntil && resourcesUntil < expiry) expiry = resourcesUntil;
        if (inventory.IntervalEndUtc < expiry) expiry = inventory.IntervalEndUtc;
        journal = journal with { OfferSequence = checked(journal.OfferSequence + 1) };
        Save();
        var offer = new PeerOffer(Id, journal.OfferSequence, expiry, snapshot, profiles.Keys.Order(StringComparer.Ordinal).ToArray());
        offers[Id] = offer;
        foreach (string peer in peers.Where(p => p != Id&&!IsPeerWithdrawn(p)).Order(StringComparer.Ordinal)) transport.Send(Id, peer, offer);
    }

    public void Tick()
    {
        coordinator.Reconcile(runtimeNodes);
        coordinator.EnforceDeadlinePolicies();
        bool changed = ApplyLocalHeat();
        foreach (var row in coordinator.ActiveAttempts().Where(a => a.Phase == "terminal" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowStop))
        {
            coordinator.StopAndRelease(row.Attempt.Request.Id);
            changed = true;
        }
        if (changed) Publish();
        if (journal.OperatorHold) return;
        foreach (var row in coordinator.ActiveAttempts().Where(a => a.Phase == "created"))
            coordinator.TryStartAtSite(row.Attempt.Request, runtimeNodes, row.Attempt.Profile, clock.GetUtcNow(),
                Id, inventory.Sites[0].Heat, inventory.Revision, inventory.IntervalStartUtc);
        // ADR 0074: one planner per pass and offer set. Its held claims make jobs planned in this pass, and
        // this origin's unanswered proposals, use up capacity, instead of every waiting job being proposed to
        // the same best peer and rejected there. Ranking and admission are the unchanged WorkloadPlacement.
        var planners = new Dictionary<string, (WorkloadPlacement Placement, string[] PeerIds)>(StringComparer.Ordinal);
        foreach (var job in journal.Jobs.OrderBy(j => new JobPriority(
            j.Request.Deadline?.DueAtUtc.UtcTicks ?? long.MaxValue, j.Request.Id)).ToArray())
        {
            if (job.Status is "accepted" or "cancelled") continue;
            if (job.Target is not null) { if(!IsPeerWithdrawn(job.Target))Propose(job); continue; } // Lost reply: retry this target, never reassign.
            if (!WorkloadPlacement.DeadlineOpen(job.Request, clock.GetUtcNow())) continue;
            var now = clock.GetUtcNow();
            var available = offers.Values.Where(o => !IsPeerWithdrawn(o.PeerId)&&now < o.ExpiresAtUtc && o.Profiles.Contains(job.ProfileId)
                && o.Inventory.ResourceObservedAtUtc is {} observed && observed <= now
                && o.Inventory.ResourceValidUntilUtc is {} resourcesUntil && now < resourcesUntil
                && now >= o.Inventory.IntervalStartUtc && now < o.Inventory.IntervalEndUtc)
                .OrderBy(o => o.PeerId, StringComparer.Ordinal).ToArray();
            if (available.Length == 0) continue;
            string key = string.Join('\n', available.Select(o => o.PeerId));
            if (!planners.TryGetValue(key, out var planner)) planners[key] = planner = (Planner(available, now), available.Select(o => o.PeerId).ToArray());
            var chosen = planner.Placement.TryReserve(job.Request, now);
            if (chosen is null) continue;
            var selected = available.Single(o => o.PeerId == chosen.SiteId);
            var intent = job with { Target = chosen.SiteId, Generation = checked(job.Generation + 1), Status = "proposed",
                InventoryRevision = selected.Inventory.Revision, HourStartUtc = selected.Inventory.IntervalStartUtc,
                Heat = selected.Inventory.Sites[0].Heat };
            Put(intent); // Durable intent MUST precede send; the transport may lose its reply.
            plannedAgainst[job.Request.Id] = selected.Sequence;
            foreach (var other in planners.Values.Where(p => p.Placement != planner.Placement && p.PeerIds.Contains(chosen.SiteId)))
                other.Placement.TryReserveAtSite(job.Request, chosen.SiteId, now);
            Propose(intent);
        }
        coordinator.EnforceDeadlinePolicies();
    }

    /// <summary>
    /// Planning view over the given offers (unchanged merge), with this origin's own proposals that the
    /// target's offer does not show yet held as claims at that target (ADR 0074). A proposal counts while
    /// it is unanswered, or accepted but not yet followed by a newer offer from the target.
    /// </summary>
    private WorkloadPlacement Planner(PeerOffer[] available, DateTimeOffset now)
    {
        var first = available[0].Inventory;
        var planning = first with { Revision = 1,
            Sites = available.SelectMany(o => o.Inventory.Sites).ToArray(),
            Domains = available.SelectMany(o => o.Inventory.Domains).ToArray(),
            Nodes = available.SelectMany(o => o.Inventory.Nodes).ToArray(),
            ExternalAllocations = available.SelectMany(o => o.Inventory.ExternalAllocations).ToArray(),
            ResourceObservedAtUtc = available.Max(o => o.Inventory.ResourceObservedAtUtc),
            ResourceValidUntilUtc = available.Min(o => o.ExpiresAtUtc) };
        var placement = new WorkloadPlacement();
        placement.Update(planning, now);
        foreach (var pending in journal.Jobs.Where(j => j.Target is not null && j.Status is "proposed" or "accepted"))
        {
            var offer = Array.Find(available, o => o.PeerId == pending.Target);
            if (offer is null || planning.ExternalAllocations.Any(a => a.Id == pending.Request.Id)) continue;
            bool unanswered = pending.Status == "proposed";
            bool acceptedNotYetOffered = plannedAgainst.TryGetValue(pending.Request.Id, out long sequence) && offer.Sequence <= sequence;
            if (unanswered || acceptedNotYetOffered) placement.TryReserveAtSite(pending.Request, pending.Target!, now);
        }
        return placement;
    }

    private bool ApplyLocalHeat()
    {
        var scheduler = new HeatFirstScheduler(0);
        var heat = inventory.Sites[0].Heat;
        var now = clock.GetUtcNow();
        bool noHeat = scheduler.Decide(heat).TargetLoadPerServer == 0;
        bool changed = false;
        var attempts = coordinator.ActiveAttempts();
        // Decision 0068: freeze-only batch containers run at their full declared heat, so the shared
        // heat-first load is rounded down to whole jobs. The budget is the same useful heat the shared
        // policy and admission use (HeatCapacity.RemainingKwh: demand plus tank room). Running heat
        // above it would be dumped, so the lowest-priority jobs freeze until the rest fits.
        double budget = HeatCapacity.RemainingKwh(heat) + HeatTolerance;
        double load = attempts.Where(a => a.Phase == "running" && !Pausable(a)).Sum(Heat);
        foreach (var row in attempts.Where(a => a.Phase == "running" && Pausable(a)).OrderBy(Priority))
        {
            string id = row.Attempt.Request.Id;
            if (journal.OperatorHold) { coordinator.PauseBatch(id); changed = true; continue; }
            if (noHeat) { changed |= coordinator.ApplyBatchHeat(id, heat, scheduler, now); MarkFrozen(id, now); continue; }
            if (load + Heat(row) > budget) { coordinator.PauseBatch(id); MarkFrozen(id, now); changed = true; continue; }
            load += Heat(row);
        }
        if (noHeat || journal.OperatorHold) return changed;
        // Thaw in priority order, counting each thawed job, after at least the minimum freeze time.
        foreach (var row in attempts.Where(a => a.Phase == "paused" && Pausable(a)).OrderBy(Priority))
        {
            string id = row.Attempt.Request.Id;
            if (frozenAt.TryGetValue(id, out var since) && now - since < minimumFreeze) continue;
            if (load + Heat(row) > budget) continue;
            if (coordinator.ApplyBatchHeat(id, heat, scheduler, now)) { load += Heat(row); frozenAt.Remove(id); changed = true; }
        }
        return changed;
    }

    private void MarkFrozen(string id, DateTimeOffset now) => frozenAt.TryAdd(id, now);
    private const double HeatTolerance = 1e-9; // kWh; numerical tolerance only
    private static double Heat(AttemptRecord a) => a.Attempt.Reservation.RecoverableHeatKwhPerHour;
    private static bool Pausable(AttemptRecord a) => a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowPause;
    private static JobPriority Priority(AttemptRecord a) =>
        new(a.Attempt.Request.Deadline?.DueAtUtc.UtcTicks ?? long.MaxValue, a.Attempt.Request.Id);

    private void Propose(OriginJob job) => transport.Send(Id, job.Target!, new PeerProposal(Id, job.Generation,
        Identity(job.Request, job.ProfileId), job.Request, job.ProfileId, job.InventoryRevision, job.HourStartUtc, job.Heat!));

    public void Receive(string from, PeerMessage message)
    {
        if (!peers.Contains(from)) throw new InvalidDataException("Unknown peer.");
        switch (message)
        {
            case PeerOffer offer when offer.PeerId == from&&!IsPeerWithdrawn(from):
                if (!offers.TryGetValue(from, out var old) || offer.Sequence > old.Sequence)
                    offers[from] = WorkloadJson.Copy(offer);
                break;
            case PeerProposal proposal when proposal.Origin == from:
                Accept(from, proposal);
                break;
            case PeerReply reply:
                var job = journal.Jobs.SingleOrDefault(j => j.Request.Id == reply.JobId);
                if (job is null || job.Status=="cancelled" || job.Target != from || job.Generation != reply.Generation
                    || Identity(job.Request, job.ProfileId) != reply.Identity) break;
                if (reply.Status == "rejected") { plannedAgainst.Remove(job.Request.Id); Put(job with { Target = null, Status = "waiting" }); }
                else if (reply.Status == "accepted")
                {
                    if (offers.TryGetValue(from, out var seen) && plannedAgainst.TryGetValue(job.Request.Id, out long planned))
                        plannedAgainst[job.Request.Id] = Math.Max(planned, seen.Sequence);
                    Put(job with { Status = "accepted" });
                }
                break;
        }
    }

    private void Accept(string origin, PeerProposal proposal)
    {
        if (proposal.Generation <= 0 || proposal.Request.OriginSiteId != origin
            || proposal.Identity != Identity(proposal.Request, proposal.ProfileId)) throw new InvalidDataException("Invalid proposal identity.");
        if(journal.CancelledWorkloads.Contains(proposal.Request.Id,StringComparer.Ordinal)){
            transport.Send(Id,origin,new PeerReply(proposal.Request.Id,proposal.Generation,proposal.Identity,"rejected"));return;
        }
        var old = journal.Outcomes.SingleOrDefault(o => o.Origin == origin && o.JobId == proposal.Request.Id && o.Generation == proposal.Generation);
        if (old is not null)
        {
            if (old.Identity != proposal.Identity) throw new InvalidDataException("Conflicting proposal replay.");
            if (old.Status == "rejected") { Reply(origin, old); return; }
        }
        string status;
        try
        {
            coordinator.Reconcile(runtimeNodes);
            coordinator.EnforceDeadlinePolicies();
            if (old is not null) { Reply(origin, old); return; }
            var prior = coordinator.Attempts().SingleOrDefault(a => a.Attempt.Request.Id == proposal.Request.Id);
            if (prior is not null)
            {
                if (Identity(prior.Attempt.Request, prior.Attempt.Profile.Id) != proposal.Identity)
                    throw new InvalidDataException("Existing execution has different requirements.");
                status = "accepted"; // Expired old offer does not erase an existing/uncertain attempt.
            }
            else if (journal.OperatorHold || !profiles.TryGetValue(proposal.ProfileId, out var profile)) status = "rejected";
            else
            {
                var result = coordinator.TryStartAtSite(proposal.Request, runtimeNodes, profile, clock.GetUtcNow(),
                    Id, proposal.Heat, proposal.InventoryRevision, proposal.HourStartUtc);
                status = result is null ? "rejected" : "accepted";
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or InvalidDataException)
        {
            // An uncertain call cannot authorize replanning elsewhere. Retry/reconcile this target.
            transport.Send(Id, origin, new PeerReply(proposal.Request.Id, proposal.Generation, proposal.Identity, "unknown"));
            return;
        }
        var outcome = new ProposalOutcome(origin, proposal.Request.Id, proposal.Generation, proposal.Identity, status);
        journal = journal with { Outcomes = [.. journal.Outcomes, outcome] };
        Save(); // Rejection tombstones precede replies: delayed duplicates cannot start later.
        Reply(origin, outcome);
        Publish();
    }

    private void Reply(string origin, ProposalOutcome result) => transport.Send(Id, origin,
        new PeerReply(result.JobId, result.Generation, result.Identity, result.Status));
    private static string Identity(WorkloadRequest request, string profile) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(profile + "\n" + WorkloadJson.Identity(request))));
    private void Put(OriginJob job)
    {
        journal = journal with { Jobs = journal.Jobs.Select(j => j.Request.Id == job.Request.Id ? job : j).ToArray() };
        Save();
    }
    private void Save()
    {
        string temporary = journalPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, journal, WorkloadJson.Options); stream.Flush(true); }
        JournalReplace.Move(temporary, journalPath);
    }
    public void Dispose() => coordinator.Dispose();
}
