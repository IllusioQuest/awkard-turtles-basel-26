using System.Threading.Channels;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

/// <summary>Notifications are wake hints. State is reread once per tick, never queued without a bound.</summary>
public sealed class NodeFeedbackWake : IDisposable
{
    private readonly Channel<bool> signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly FileSystemWatcher watcher;
    public NodeFeedbackWake(string directory)
    {
        Directory.CreateDirectory(directory);
        watcher = new FileSystemWatcher(directory, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Changed += (_, _) => Notify(); watcher.Created += (_, _) => Notify();
        watcher.Deleted += (_, _) => Notify(); watcher.Renamed += (_, _) => Notify();
        watcher.Error += (_, _) => Notify(); // Overflow triggers a full bounded rescan too.
        watcher.EnableRaisingEvents = true;
    }
    public void Notify() => signal.Writer.TryWrite(true);
    public async Task WaitAsync(TimeSpan fallback, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(fallback);
        try { await signal.Reader.ReadAsync(timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
    }
    public void Dispose() { watcher.Dispose(); signal.Writer.TryComplete(); }
}

/// <summary>One fleet lock/owner. Notifications never directly mutate Docker or bypass admission.</summary>
public sealed class NodeFeedbackOwner
{
    private readonly WorkloadCoordinator coordinator;
    private readonly WorkloadConfiguration manifest;
    private readonly NodeFeedbackInbox inbox;
    private readonly TimeProvider clock;
    private readonly string manifestIdentity, nodesIdentity;
    private WorkloadInventory inventory;
    private long revision;

    public NodeFeedbackOwner(WorkloadCoordinator coordinator, WorkloadConfiguration manifest, NodeFeedbackInbox inbox,
        NodeFeedbackPolicy policy, TimeProvider? clock = null)
    {
        if (manifest.Workloads.Length > policy.MaxQueuedJobs) throw new InvalidDataException("Manifest exceeds feedback owner queue bound.");
        this.coordinator = coordinator; this.manifest = manifest; this.inbox = inbox; this.clock = clock ?? TimeProvider.System;
        inventory = WorkloadJson.Copy(manifest.Inventory);
        manifestIdentity = WorkloadJson.Identity(manifest.Workloads); nodesIdentity = WorkloadJson.Identity(manifest.RuntimeNodes);
        foreach (var prior in coordinator.Attempts())
        {
            var entry = manifest.Workloads.SingleOrDefault(w => w.Request.Id == prior.Attempt.Request.Id);
            if (entry is null || WorkloadJson.Identity(entry.Request) != WorkloadJson.Identity(prior.Attempt.Request)
                || WorkloadJson.Identity(entry.Profile) != WorkloadJson.Identity(prior.Attempt.Profile))
                throw new InvalidDataException("Manifest differs from durable ownership; no mutations authorized.");
        }
    }

    public string[] Tick(Func<WorkloadConfiguration> load)
    {
        // Report/config rejection must not starve reconciliation and deadline enforcement.
        coordinator.Reconcile(manifest.RuntimeNodes);
        coordinator.EnforceDeadlines();
        var errors = new List<string>();
        WorkloadInventory projected;
        try
        {
            var next = load();
            if (next.FleetId != manifest.FleetId || WorkloadJson.Identity(next.Workloads) != manifestIdentity
                || WorkloadJson.Identity(next.RuntimeNodes) != nodesIdentity || next.Inventory.Revision < inventory.Revision
                || next.Inventory.IntervalStartUtc < inventory.IntervalStartUtc
                || next.Inventory.Revision == inventory.Revision && WorkloadJson.Identity(next.Inventory) != WorkloadJson.Identity(inventory))
                throw new InvalidDataException("Accepted jobs/endpoints changed or inventory revision regressed/conflicted.");
            new WorkloadPlacement().Update(next.Inventory, clock.GetUtcNow());
            inventory = WorkloadJson.Copy(next.Inventory);
            errors.AddRange(inbox.Read(inventory, clock.GetUtcNow()));
            projected = inbox.Project(inventory, checked(++revision), clock.GetUtcNow());
            coordinator.Update(projected, clock.GetUtcNow());
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
        { errors.Add("Admission blocked: " + e.Message); return errors.ToArray(); }

        // Only the shared scheduler may request a batch pause from heat; games have no pause path.
        var scheduler = new GreedyHeatScheduler(0);
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "running" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowPause))
        {
            var heat = projected.Sites.Single(s => s.Id == row.Attempt.Reservation.SiteId).Heat;
            if (scheduler.Decide(heat).TargetLoadPerServer == 0)
                coordinator.ApplyBatchHeat(row.Attempt.Request.Id, heat, scheduler, clock.GetUtcNow());
        }
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "terminal" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowStop))
            coordinator.StopAndRelease(row.Attempt.Request.Id);
        foreach (var entry in manifest.Workloads.OrderBy(w => w.Request.Kind == "interactive" ? 0 : 1)
            .ThenBy(w => w.Request.Deadline?.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(w => w.Request.Id, StringComparer.Ordinal))
        {
            coordinator.EnforceDeadlines();
            var prior = coordinator.Attempts().SingleOrDefault(a => a.Attempt.Request.Id == entry.Request.Id);
            if (prior is not null && prior.Phase != "created") continue;
            if (WorkloadPlacement.DeadlineOpen(entry.Request, clock.GetUtcNow()))
                coordinator.TryStart(entry.Request, manifest.RuntimeNodes, entry.Profile, clock.GetUtcNow());
        }
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "paused" && a.Attempt.Request.Kind == "batch"))
        {
            coordinator.EnforceDeadlines();
            if (coordinator.Attempts().Single(a => a.Attempt.Request.Id == row.Attempt.Request.Id).Phase != "paused") continue;
            coordinator.ResumeBatch(row.Attempt.Request.Id, clock.GetUtcNow());
        }
        coordinator.EnforceDeadlines();
        return errors.ToArray();
    }
}
