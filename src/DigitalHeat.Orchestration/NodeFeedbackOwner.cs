using System.Threading.Channels;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

/// <summary>Notifications are wake hints. State is reread once per tick, never queued without a bound.</summary>
public sealed class NodeFeedbackWake : IDisposable
{
    // Drop duplicate wake hints, not node state: each tick rereads the latest complete reports.
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
    private readonly NodeFeedbackPolicy policy;
    private readonly string manifestIdentity, nodesIdentity;
    private readonly WorkloadDefinition[] admissionOrder;
    private WorkloadInventory inventory;
    private string inventoryIdentity;
    private long revision;

    public NodeFeedbackOwner(WorkloadCoordinator coordinator, WorkloadConfiguration manifest, NodeFeedbackInbox inbox,
        NodeFeedbackPolicy policy, TimeProvider? clock = null)
    {
        if (manifest.Workloads.Length > policy.MaxQueuedJobs) throw new InvalidDataException("Manifest exceeds feedback owner queue bound.");
        this.coordinator = coordinator;
        this.manifest = manifest;
        this.inbox = inbox;
        this.clock = clock ?? TimeProvider.System;
        this.policy = policy;
        inventory = WorkloadJson.Copy(manifest.Inventory);
        inventoryIdentity = WorkloadJson.Identity(inventory);
        manifestIdentity = WorkloadJson.Identity(manifest.Workloads);
        nodesIdentity = WorkloadJson.Identity(manifest.RuntimeNodes);
        // The accepted manifest is immutable. Sort once; do not repeat this on every notification.
        admissionOrder = manifest.Workloads.OrderBy(w => new JobPriority(
            w.Request.Deadline?.DueAtUtc.UtcTicks ?? long.MaxValue, w.Request.Id)).ToArray();
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
        // Keep deadline work before input parsing and between admissions below. These checks are
        // intentional: malformed reports and slow runtime calls must not starve existing deadlines.
        coordinator.Reconcile(manifest.RuntimeNodes);
        coordinator.EnforceDeadlines();
        var errors = new List<string>();
        WorkloadInventory projected;
        try
        {
            projected = RefreshInventory(load(), errors);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException)
        { errors.Add("Admission blocked: " + e.Message); return errors.ToArray(); }

        PauseBatchWithoutHeat(projected);
        ReleaseFinishedBatch();
        AdmitWaitingWork();
        ResumePausedBatch();
        coordinator.EnforceDeadlines();
        return errors.ToArray();
    }

    private WorkloadInventory RefreshInventory(WorkloadConfiguration next, List<string> errors)
    {
        string nextIdentity = WorkloadJson.Identity(next.Inventory);
        if (next.FleetId != manifest.FleetId || WorkloadJson.Identity(next.Workloads) != manifestIdentity
            || WorkloadJson.Identity(next.RuntimeNodes) != nodesIdentity || next.Inventory.Revision < inventory.Revision
            || next.Inventory.IntervalStartUtc < inventory.IntervalStartUtc
            || next.Inventory.Revision == inventory.Revision && nextIdentity != inventoryIdentity)
            throw new InvalidDataException("Accepted jobs/endpoints changed or inventory revision regressed/conflicted.");

        WorkloadPlacement.ValidateInventory(next.Inventory, clock.GetUtcNow());
        policy.Validate(next.Inventory);
        inventory = WorkloadJson.Copy(next.Inventory);
        inventoryIdentity = nextIdentity;
        errors.AddRange(inbox.Read(inventory, clock.GetUtcNow()));
        var projected = inbox.Project(inventory, checked(++revision), clock.GetUtcNow());
        coordinator.Update(projected, clock.GetUtcNow());
        return projected;
    }

    private void PauseBatchWithoutHeat(WorkloadInventory projected)
    {
        // Only the shared scheduler may request a batch pause from heat; games have no pause path.
        var scheduler = new HeatFirstScheduler(0);
        var sitesById = projected.Sites.ToDictionary(s => s.Id, StringComparer.Ordinal);
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "running" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowPause))
        {
            var heat = sitesById[row.Attempt.Reservation.SiteId].Heat;
            if (scheduler.Decide(heat).TargetLoadPerServer == 0)
                coordinator.ApplyBatchHeat(row.Attempt.Request.Id, heat, scheduler, clock.GetUtcNow());
        }
    }

    private void ReleaseFinishedBatch()
    {
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "terminal" && a.Attempt.Request.Kind == "batch" && a.Attempt.Request.Lifecycle.AllowStop))
            coordinator.StopAndRelease(row.Attempt.Request.Id);
    }

    private void AdmitWaitingWork()
    {
        foreach (var entry in admissionOrder)
        {
            // EnforceDeadlines already returns a defensive snapshot. Reuse it instead of cloning
            // the entire attempt history again. Keep this check between potentially slow starts.
            var attempts = coordinator.EnforceDeadlines();
            var prior = attempts.SingleOrDefault(a => a.Attempt.Request.Id == entry.Request.Id);
            if (prior is not null && prior.Phase != "created") continue;
            if (WorkloadPlacement.DeadlineOpen(entry.Request, clock.GetUtcNow()))
                coordinator.TryStart(entry.Request, manifest.RuntimeNodes, entry.Profile, clock.GetUtcNow());
        }
    }

    private void ResumePausedBatch()
    {
        foreach (var row in coordinator.Attempts().Where(a => a.Phase == "paused" && a.Attempt.Request.Kind == "batch"))
        {
            var attempts = coordinator.EnforceDeadlines();
            if (attempts.Single(a => a.Attempt.Request.Id == row.Attempt.Request.Id).Phase != "paused") continue;
            coordinator.ResumeBatch(row.Attempt.Request.Id, clock.GetUtcNow());
        }
    }
}
