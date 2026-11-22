using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

public sealed record AttemptRecord(RuntimeAttempt Attempt, string Phase)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset[]? CommittedHeatIntervalsUtc { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DeadlineOutcome? Deadline { get; init; }
}
/// <summary>Detached heat-only read snapshot; does not authorize placement or replace ownership checks.</summary>
public sealed record HeatCommitment(string WorkloadId,double HeatKwhPerHour,DateTimeOffset[] HourStartsUtc,bool Finished);

public sealed record CoordinatorState(string FleetId, AttemptRecord[] Attempts)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? RetainHourlyHeat { get; init; }
}

/// <summary>
/// Single-host/operator coordinator. A stable per-fleet OS file lock prevents two local instances,
/// including different journal paths. The flushed journal precedes every uncertain Docker mutation.
/// This is not cross-host fencing or an HA control plane.
/// </summary>
public sealed class WorkloadCoordinator : IDisposable
{
    private readonly object gate = new();
    private readonly IWorkloadRuntime runtime;
    private readonly string fleetId, journalPath;
    private readonly FileStream fleetLock;
    private readonly List<AttemptRecord> records = [];
    private readonly WorkloadPlacement placement = new();
    private WorkloadInventory? currentInventory;
    private bool reconciled;
    private bool disposed;
    private double lastAppliedBatchLoad;
    private readonly TimeProvider clock;
    private readonly bool retainHourlyHeat;
    private long budgetRevision;
    public bool Reconciled { get { lock (gate) return reconciled; } }

    public WorkloadCoordinator(string fleetId, string stateDirectory, IWorkloadRuntime runtime, TimeProvider? timeProvider = null, bool retainHourlyHeat = false)
    {
        if (string.IsNullOrWhiteSpace(fleetId) || !System.Text.RegularExpressions.Regex.IsMatch(fleetId, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$"))
            throw new ArgumentException("Invalid fleet identity.");
        this.fleetId = fleetId; this.runtime = runtime; clock = timeProvider ?? TimeProvider.System; this.retainHourlyHeat = retainHourlyHeat;
        var lockRoot = Path.Combine(Path.GetTempPath(), "digitalheat-coordinator-locks");
        Directory.CreateDirectory(lockRoot);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fleetId))).ToLowerInvariant();
        fleetLock = new FileStream(Path.Combine(lockRoot, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Directory.CreateDirectory(stateDirectory);
            journalPath = Path.GetFullPath(Path.Combine(stateDirectory, "journal.json"));
            if (File.Exists(journalPath))
            {
                var state = JsonSerializer.Deserialize<CoordinatorState>(File.ReadAllText(journalPath), WorkloadJson.Options)
                    ?? throw new InvalidDataException("Empty coordinator journal.");
                if (state.FleetId != fleetId || state.Attempts is null || state.Attempts.Any(r => r is null || r.Attempt is null || r.Attempt.FleetId != fleetId)
                    || state.Attempts.Select(r => r.Attempt.Request.Id).Distinct().Count() != state.Attempts.Length
                    || state.Attempts.Select(r => r.Attempt.AttemptId).Distinct().Count() != state.Attempts.Length)
                    throw new InvalidDataException("Invalid coordinator journal ownership.");
                if ((state.RetainHourlyHeat ?? false) != retainHourlyHeat)
                    throw new InvalidDataException("Cannot change hourly heat accounting mode on an existing journal.");
                foreach (var record in state.Attempts)
                {
                    runtime.ValidateAttempt(record.Attempt);
                    if (retainHourlyHeat && (record.CommittedHeatIntervalsUtc is null || record.CommittedHeatIntervalsUtc.Length == 0
                        || record.CommittedHeatIntervalsUtc.Any(t => t.Offset != TimeSpan.Zero || t.Ticks % TimeSpan.TicksPerHour != 0)
                        || record.CommittedHeatIntervalsUtc.Distinct().Count() != record.CommittedHeatIntervalsUtc.Length))
                        throw new InvalidDataException("Invalid durable hourly heat commitments.");
                    if (record.Deadline is {} outcome && (record.Attempt.Request.Deadline is null
                        || outcome.Status is not ("on_time" or "missed" or "failed" or "unknown") || outcome.ObservedAtUtc.Offset != TimeSpan.Zero))
                        throw new InvalidDataException("Invalid persisted deadline outcome.");
                    if (record.Phase is not ("reserved" or "creating" or "created" or "starting" or "running" or "paused" or "pausing" or "resuming" or "stopping" or "terminal" or "removing" or "released"))
                        throw new InvalidDataException("Unknown journal phase.");
                }
                records.AddRange(state.Attempts);
                placement.Restore(records.Where(r => r.Phase != "released").Select(r => new AllocationView(r.Attempt.Request, r.Attempt.Reservation, r.Phase == "paused")));
            }
        }
        catch { fleetLock.Dispose(); throw; }
    }

    public void Update(WorkloadInventory inventory, DateTimeOffset nowUtc)
    {
        lock (gate)
        {
            if (!retainHourlyHeat) { placement.Update(inventory, nowUtc); currentInventory = WorkloadJson.Copy(inventory); return; }
            // Copy caller-owned arrays before validation; do not build a throwaway placement index.
            var copy = WorkloadJson.Copy(inventory);
            WorkloadPlacement.ValidateInventory(copy, nowUtc);
            if (currentInventory is {} old && (copy.Revision <= old.Revision || copy.IntervalStartUtc < old.IntervalStartUtc))
                throw new InvalidDataException("Cannot regress feedback inventory revision/hour.");
            currentInventory = copy;
            foreach (var claim in placement.Allocations().Where(a => !a.Paused)) CommitHeat(claim.Request.Id);
            RefreshHeatBudget(nowUtc);
        }
    }

    private void CommitHeat(string id)
    {
        if (!retainHourlyHeat || currentInventory is null) return;
        int i = records.FindIndex(r => r.Attempt.Request.Id == id);
        var intervals = records[i].CommittedHeatIntervalsUtc ?? [];
        if (intervals.Contains(currentInventory.IntervalStartUtc)) return;
        records[i] = records[i] with { CommittedHeatIntervalsUtc = [.. intervals, currentInventory.IntervalStartUtc] };
        Save(); // The full-hour commitment precedes create/start/resume, even if those fail.
    }

    private void RefreshHeatBudget(DateTimeOffset now, string? resuming = null)
    {
        if (!retainHourlyHeat || currentInventory is not {} basis || now < basis.IntervalStartUtc || now >= basis.IntervalEndUtc) return;
        budgetRevision = checked(Math.Max(budgetRevision, basis.Revision) + 1);
        placement.Update(basis with { Revision = budgetRevision, Sites = BudgetSites(resuming) }, now);
    }

    private WorkloadSite[] BudgetSites(string? resuming = null, bool previewBatchPause = false)
    {
        var basis = currentInventory!;
        // Group once instead of scanning the entire history for every site. GroupBy preserves
        // per-site record order: changing summation order can change boundary admission decisions.
        var committedBySite = records.Where(r => r.CommittedHeatIntervalsUtc?.Contains(basis.IntervalStartUtc) == true)
            .GroupBy(r => r.Attempt.Reservation.SiteId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Attempt.Reservation.RecoverableHeatKwhPerHour), StringComparer.Ordinal);
        var heldBySite = placement.Allocations().Where(a => (!a.Paused || a.Request.Id == resuming)
                && !(previewBatchPause && a.Request.Kind == "batch" && a.Request.Lifecycle.AllowPause))
            .GroupBy(a => a.Reservation.SiteId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Request.RecoverableHeatKwhPerHour), StringComparer.Ordinal);
        return basis.Sites.Select(site =>
        {
            // Active reservations are already subtracted by WorkloadPlacement. Do not count them twice.
            double retainedHeat = Math.Max(0, committedBySite.GetValueOrDefault(site.Id) - heldBySite.GetValueOrDefault(site.Id));
            return site with { ExternalCommittedHeatKwh = site.ExternalCommittedHeatKwh + retainedHeat };
        }).ToArray();
    }

    private bool CanAdmitAfterBatchPause(WorkloadRequest request, DateTimeOffset now)
    {
        if (!retainHourlyHeat) return placement.CanAdmitAfterBatchPause(request, now);
        // Use the same placement code, but retain the batch heat in the pause preview too.
        var preview = new WorkloadPlacement();
        preview.Restore(placement.Allocations());
        preview.Update(currentInventory! with { Sites = BudgetSites(previewBatchPause: true) }, now);
        return preview.CanAdmitAfterBatchPause(request, now);
    }
    public AllocationView[] Allocations() { lock (gate) return placement.Allocations(); }
    public AttemptRecord[] Attempts() { lock (gate) return WorkloadJson.Copy(records.ToArray()); }
    // Routine scans need every uncertain/live phase, but not released history. Ownership and heat use full records.
    public AttemptRecord[] ActiveAttempts()
    { lock(gate)return WorkloadJson.Copy(records.Where(r=>r.Phase!="released").ToArray()); }

    // Copy only heat fields for frequent monitoring; full ownership reconciliation stays unchanged.
    public HeatCommitment[] HeatCommitments()
    {
        lock(gate)return records.Select(r=>new HeatCommitment(r.Attempt.Request.Id,
            r.Attempt.Reservation.RecoverableHeatKwhPerHour,r.CommittedHeatIntervalsUtc?.ToArray()??[],
            r.Phase is "released" or "terminal")).ToArray();
    }
    public double CommittedHeatKwh(DateTimeOffset hourStartUtc)
    {
        lock(gate){
            // Preserve record order and every phase: retained heat outlives job completion.
            double total=0;
            foreach(var r in records)if(r.CommittedHeatIntervalsUtc?.Contains(hourStartUtc)==true)
                total+=r.Attempt.Reservation.RecoverableHeatKwhPerHour;
            return total;
        }
    }


    public void Reconcile(IReadOnlyList<RuntimeNode> configuredNodes)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            reconciled = false;
            var allNodes = configuredNodes.Concat(records.Select(r => r.Attempt.Node)).Distinct().ToArray();
            var discovered = runtime.Discover(fleetId, allNodes);
            if (discovered.Any(id => records.All(r => r.Attempt.AttemptId != id)))
                throw new InvalidOperationException("Unknown owned container exists. Recover its journal; no new admissions allowed.");
            foreach (var record in records.ToArray())
            {
                var observed = runtime.Observe(record.Attempt);
                if (record.Phase == "released")
                {
                    if (observed.State != "missing") throw new InvalidOperationException("Released attempt reappeared; operator reconciliation required.");
                    continue;
                }
                CaptureDeadlineEvidence(record.Attempt, observed);
                if (observed.State == "missing")
                {
                    if (record.Phase is "reserved" or "removing")
                    {
                        SetPhase(record.Attempt, "released"); placement.Release(record.Attempt.Request.Id);
                        continue;
                    }
                    throw new InvalidOperationException("Previously dispatched attempt is missing/uncertain. Claim retained; no automatic relaunch.");
                }
                if (observed.State == "paused")
                {
                    placement.ObservePaused(record.Attempt.Request.Id); SetPhase(record.Attempt, "paused");
                }
                else if (observed.Terminal) SetPhase(record.Attempt, "terminal");
                else if (observed.State == "running")
                {
                    // A formerly paused process may have been externally resumed. Conservatively restore
                    // full CPU/heat ownership before allowing another admission, even if now over capacity.
                    RestoreRunningClaim(record.Attempt.Request.Id);
                    SetPhase(record.Attempt, "running");
                }
                else if (observed.State == "created" && record.Phase is "creating" or "created") SetPhase(record.Attempt, "created");
                else throw new InvalidOperationException("Uncertain container transition; ownership retained until operator reconciliation.");
            }
            reconciled = true;
            RefreshHeatBudget(clock.GetUtcNow());
        }
    }

    public AttemptRecord? TryStart(WorkloadRequest request, IReadOnlyList<RuntimeNode> nodes, ContainerProfile profile,
        DateTimeOffset nowUtc, bool pauseBatchFirst = true)
        => TryStartCore(request, nodes, profile, nowUtc, pauseBatchFirst, null);

    /// <summary>Start only at the network-selected site and inventory hour; never reroute to another site.</summary>
    public AttemptRecord? TryStartAtSite(WorkloadRequest request, IReadOnlyList<RuntimeNode> nodes, ContainerProfile profile,
        DateTimeOffset nowUtc, string selectedSiteId, HeatSignal expectedHeatSignal,
        long expectedInventoryRevision, DateTimeOffset expectedHourStartUtc)
    {
        if (string.IsNullOrWhiteSpace(selectedSiteId)) throw new ArgumentException("A selected site is required.", nameof(selectedSiteId));
        ArgumentNullException.ThrowIfNull(expectedHeatSignal);
        return TryStartCore(request, nodes, profile, nowUtc, false, selectedSiteId, expectedInventoryRevision, expectedHourStartUtc, expectedHeatSignal);
    }

    private AttemptRecord? TryStartCore(WorkloadRequest request, IReadOnlyList<RuntimeNode> nodes, ContainerProfile profile,
        DateTimeOffset nowUtc, bool pauseBatchFirst, string? selectedSiteId, long? expectedInventoryRevision = null,
        DateTimeOffset? expectedHourStartUtc = null, HeatSignal? expectedHeatSignal = null)
    {
        lock (gate)
        {
            RequireReconciled();
            WorkloadPlacement.ValidateRequest(request);
            // Plans bind the externally supplied inventory. Retained-heat accounting installs a
            // private budget revision; admit against that current budget without confusing the two.
            long? admissionRevision = expectedInventoryRevision;
            if (expectedInventoryRevision is not null)
            {
                if (currentInventory is not {} raw || raw.Revision != expectedInventoryRevision
                    || raw.IntervalStartUtc != expectedHourStartUtc || nowUtc < raw.IntervalStartUtc || nowUtc >= raw.IntervalEndUtc
                    || raw.Sites.SingleOrDefault(s => s.Id == selectedSiteId)?.Heat != expectedHeatSignal) return null;
                admissionRevision = retainHourlyHeat ? budgetRevision : expectedInventoryRevision;
                if (!placement.MatchesSnapshot(selectedSiteId!, expectedHeatSignal!, admissionRevision.Value, expectedHourStartUtc!.Value, DecisionTime(request, nowUtc))) return null;
            }
            var prior = records.SingleOrDefault(r => r.Attempt.Request.Id == request.Id);
            if (prior is not null)
            {
                if (WorkloadJson.Identity(prior.Attempt.Request) != WorkloadJson.Identity(request) || WorkloadJson.Identity(prior.Attempt.Profile) != WorkloadJson.Identity(profile))
                    throw new InvalidDataException("Attempt retry changed requirements/profile.");
                if (selectedSiteId is not null && prior.Attempt.Reservation.SiteId != selectedSiteId)
                    throw new InvalidOperationException("Existing attempt is at a different site than the network decision.");
                if (prior.Phase == "created")
                {
                    if (!CanDispatch(request)) { EnforceDeadlines(); return CurrentRecord(request.Id); }
                    if (!placement.CanStartHeld(request.Id, DecisionTime(request, nowUtc))) return null;
                    try
                    {
                        if (runtime.Observe(prior.Attempt).State != "created") throw new InvalidOperationException("Created attempt changed during recovery.");
                        SetPhase(prior.Attempt, "starting");
                        if (!CanDispatch(request)) { SetPhase(prior.Attempt, "created"); EnforceDeadlines(); return CurrentRecord(request.Id); }
                        runtime.Start(prior.Attempt);
                        var observed = runtime.Observe(prior.Attempt);
                        if (observed.State != "running" && !(request.Kind == "batch" && observed.Terminal)) throw new InvalidOperationException("Recovered start unconfirmed.");
                        SetPhase(prior.Attempt, observed.Terminal ? "terminal" : "running");
                        CaptureDeadlineEvidence(prior.Attempt, observed);
                        EnforceDeadlines();
                        return WorkloadJson.Copy(records.Single(r => r.Attempt.AttemptId == prior.Attempt.AttemptId));
                    }
                    catch { reconciled = false; throw; }
                }
                return WorkloadJson.Copy(prior); // Never reissue an uncertain start or reuse a terminated identity.
            }
            if (!CanDispatch(request)) return null;
            Preflight(request, nodes, profile);
            if (!CanDispatch(request)) return null;
            var reservation = selectedSiteId is null
                ? placement.TryReserve(request, DecisionTime(request, nowUtc))
                : placement.TryReserveAtSite(request, selectedSiteId, expectedHeatSignal!, admissionRevision!.Value,
                    expectedHourStartUtc!.Value, DecisionTime(request, nowUtc));
            if (reservation is null && request.Kind == "interactive" && pauseBatchFirst && CanAdmitAfterBatchPause(request, DecisionTime(request, nowUtc)))
            {
                foreach (var batch in records.Where(r => r.Phase == "running" && r.Attempt.Request.Kind == "batch" && r.Attempt.Request.Lifecycle.AllowPause).ToArray())
                {
                    if (!CanDispatch(request)) break;
                    PauseBatch(batch.Attempt.Request.Id);
                    reservation = placement.TryReserve(request, DecisionTime(request, nowUtc));
                    if (reservation is not null) break;
                }
            }
            if (reservation is null) return null;
            RuntimeAttempt attempt;
            try
            {
                attempt = new(fleetId, Guid.NewGuid().ToString("N"), WorkloadJson.Copy(request), reservation,
                    WorkloadJson.Copy(nodes.Single(n => n.NodeId == reservation.NodeId)), WorkloadJson.Copy(profile));
                runtime.ValidateAttempt(attempt);
            }
            catch { placement.Release(request.Id); throw; } // No Docker mutation or journal intent yet.
            records.Add(new(attempt, "reserved"));
            try
            {
                CommitHeat(request.Id);
                Save();
                SetPhase(attempt, "creating");
                if (!CanDispatch(request))
                {
                    MarkDeadline(attempt, new("missed", clock.GetUtcNow(), "Expired before create dispatch; no container launched."));
                    SetPhase(attempt, "released"); placement.Release(request.Id); RefreshHeatBudget(clock.GetUtcNow()); return CurrentRecord(request.Id);
                }
                runtime.Create(attempt); SetPhase(attempt, "created");
                if (!CanDispatch(request)) { EnforceDeadlines(); return CurrentRecord(request.Id); }
                if (!placement.CanStartHeld(request.Id, DecisionTime(request, nowUtc))) return CurrentRecord(request.Id);
                SetPhase(attempt, "starting");
                if (!CanDispatch(request)) { SetPhase(attempt, "created"); EnforceDeadlines(); return CurrentRecord(request.Id); }
                runtime.Start(attempt);
                var observed = runtime.Observe(attempt);
                if (observed.State != "running" && !(request.Kind == "batch" && observed.Terminal)) throw new InvalidOperationException("Start not confirmed running; claim retained.");
                SetPhase(attempt, observed.Terminal ? "terminal" : "running");
                CaptureDeadlineEvidence(attempt, observed);
                EnforceDeadlines();
                return WorkloadJson.Copy(records.Single(r => r.Attempt.AttemptId == attempt.AttemptId));
            }
            catch { reconciled = false; throw; }
        }
    }

    public ContainerObservation Observe(string workloadId)
    {
        lock (gate) return runtime.Observe(Find(workloadId));
    }

    public void PauseBatch(string workloadId)
    {
        lock (gate)
        {
            RequireReconciled(); var a = Find(workloadId);
            if (a.Request.Kind != "batch" || !a.Request.Lifecycle.AllowPause) throw new InvalidOperationException("Cannot pause protected workload.");
            try
            {
                SetPhase(a, "pausing"); lastAppliedBatchLoad = runtime.ApplyBatchLoad(a, 0);
                if (runtime.Observe(a).State != "paused") throw new InvalidOperationException("Pause not confirmed; full claim retained.");
                placement.ObservePaused(workloadId); SetPhase(a, "paused"); RefreshHeatBudget(clock.GetUtcNow());
            }
            catch { reconciled = false; throw; }
        }
    }

    public bool ResumeBatch(string workloadId, DateTimeOffset nowUtc)
    {
        lock (gate)
        {
            RequireReconciled(); var a = Find(workloadId);
            if (a.Request.Kind != "batch" || !a.Request.Lifecycle.AllowPause) throw new InvalidOperationException("Cannot resume non-batch workload.");
            if (!CanDispatch(a.Request)) { EnforceDeadlines(); return false; }
            RefreshHeatBudget(DecisionTime(a.Request, nowUtc), workloadId);
            if (!placement.TryClaimResume(workloadId, DecisionTime(a.Request, nowUtc))) { RefreshHeatBudget(DecisionTime(a.Request, nowUtc)); return false; }
            try
            {
                CommitHeat(workloadId);
                SetPhase(a, "resuming");
                if (!CanDispatch(a.Request)) { placement.ObservePaused(workloadId); SetPhase(a, "paused"); RefreshHeatBudget(clock.GetUtcNow()); EnforceDeadlines(); return false; }
                lastAppliedBatchLoad = runtime.ApplyBatchLoad(a, 1);
                if (runtime.Observe(a).State != "running") throw new InvalidOperationException("Resume not confirmed; full claim retained.");
                SetPhase(a, "running"); RefreshHeatBudget(clock.GetUtcNow()); EnforceDeadlines(); return CurrentRecord(workloadId).Phase == "running";
            }
            catch { reconciled = false; throw; }
        }
    }

    /// <summary>Use the shared heat-load decision for batch work; active games have no IExecutor pause path.</summary>
    public bool ApplyBatchHeat(string id, HeatSignal signal, IScheduler scheduler, DateTimeOffset nowUtc)
    {
        lock (gate)
        {
            var executor = new HeatControlledBatch(this, id, nowUtc);
            SchedulerController.Apply(signal, scheduler, executor);
            return executor.Succeeded;
        }
    }

    private sealed class HeatControlledBatch(WorkloadCoordinator owner, string id, DateTimeOffset now) : IExecutor
    {
        public bool Succeeded { get; private set; }
        public double ApplyTargetLoad(double targetLoadPerServer)
        {
            if (targetLoadPerServer == 0) { owner.PauseBatch(id); Succeeded = true; }
            else Succeeded = owner.ResumeBatch(id, now);
            return Succeeded ? owner.lastAppliedBatchLoad : 0;
        }
    }

    public void StopAndRelease(string workloadId)
    {
        lock (gate)
        {
            RequireReconciled(); var a = Find(workloadId);
            if (!a.Request.Lifecycle.AllowStop) throw new InvalidOperationException("Owner did not authorize stopping this workload.");
            try
            {
                var observed = runtime.Observe(a);
                if (observed.State == "created")
                {
                    SetPhase(a, "removing"); runtime.Remove(a);
                    if (runtime.Observe(a).State != "missing") throw new InvalidOperationException("Unstarted container removal unconfirmed.");
                    SetPhase(a, "released"); placement.Release(workloadId); RefreshHeatBudget(clock.GetUtcNow()); return;
                }
                if (!observed.Terminal)
                {
                    SetPhase(a, "stopping"); runtime.Stop(a); observed = runtime.Observe(a);
                }
                if (!observed.Terminal) throw new InvalidOperationException("Termination unconfirmed; resources remain held.");
                SetPhase(a, "terminal"); SetPhase(a, "removing"); runtime.Remove(a);
                if (runtime.Observe(a).State != "missing") throw new InvalidOperationException("Removal unconfirmed; resources remain held.");
                SetPhase(a, "released"); placement.Release(workloadId); RefreshHeatBudget(clock.GetUtcNow());
            }
            catch { reconciled = false; throw; }
        }
    }

    private void RestoreRunningClaim(string id)
    {
        // Rebuild only the ownership ledger, not inventory or physics; restore conservatively.
        placement.ObserveRunning(id);
    }
    private void Preflight(WorkloadRequest request, IReadOnlyList<RuntimeNode> nodes, ContainerProfile profile)
    {
        WorkloadPlacement.ValidateRequest(request);
        var inventory = currentInventory ?? throw new InvalidOperationException("Update inventory before admission.");
        WorkloadConfiguration.ValidateRuntimeDomains(inventory, nodes);
        foreach (var node in inventory.Nodes.Where(n => n.Ready && n.NetworkEligible &&
            (request.PinnedNodeId is null || request.PinnedNodeId == n.Id) && (request.StorageId is null || n.StorageIds.Contains(request.StorageId)) &&
            !request.RequiredCapabilities.Except(n.Capabilities).Any()))
        {
            var runtimeNode = nodes.SingleOrDefault(n => n.NodeId == node.Id) ?? throw new InvalidDataException("Runtime node mapping missing.");
            string?[] devices = request.GpuCapability is {} capability
                ? inventory.Domains.Single(d => d.Id == node.DomainId).Gpus.Where(g => g.Capability == capability && node.GpuDeviceIds.Contains(g.Id)
                    && (request.MinGpuVramMib is null || g.VramMib >= request.MinGpuVramMib)).Select(g => (string?)g.Id).ToArray()
                : [null];
            foreach (var gpu in devices)
            {
                var proposal = new RuntimeAttempt(fleetId, new string('0', 32), request,
                    new(request.Id, node.SiteId, node.Id, node.DomainId, gpu, request.RecoverableHeatKwhPerHour, 0), runtimeNode, profile);
                runtime.Preflight(proposal);
            }
        }
    }
    private RuntimeAttempt Find(string id) => records.SingleOrDefault(r => r.Attempt.Request.Id == id && r.Phase != "released")?.Attempt
        ?? throw new ArgumentException("No owned active attempt with that identity.");
    private void RequireReconciled() { ObjectDisposedException.ThrowIf(disposed, this); if (!reconciled) throw new InvalidOperationException("Reconcile ownership before new mutations/admissions."); }
    private void SetPhase(RuntimeAttempt a, string phase)
    {
        int index = records.FindIndex(r => r.Attempt.AttemptId == a.AttemptId);
        if (records[index].Phase == phase) return;
        records[index] = records[index] with { Phase = phase }; Save();
    }
    private AttemptRecord CurrentRecord(string id) => records.Single(r => r.Attempt.Request.Id == id);
    private bool CanDispatch(WorkloadRequest request)
    {
        var now = clock.GetUtcNow();
        return WorkloadPlacement.DeadlineOpen(request, now) && (!retainHourlyHeat || currentInventory is {} i
            && now >= i.IntervalStartUtc && now < i.IntervalEndUtc
            && i.ResourceObservedAtUtc is {} observed && now >= observed && i.ResourceValidUntilUtc is {} until && now < until);
    }
    private DateTimeOffset DecisionTime(WorkloadRequest request, DateTimeOffset fallback) => !retainHourlyHeat && request.Deadline is null ? fallback : clock.GetUtcNow();

    private void MarkDeadline(RuntimeAttempt a, DeadlineOutcome result)
    {
        int index = records.FindIndex(r => r.Attempt.AttemptId == a.AttemptId);
        records[index] = records[index] with { Deadline = result };
        Save(); // Evidence/intent precedes any cleanup or stop mutation.
    }
    private void CaptureDeadlineEvidence(RuntimeAttempt a, ContainerObservation observed)
    {
        if (a.Request.Deadline is not {} d || CurrentRecord(a.Request.Id).Deadline is not null) return;
        var now = clock.GetUtcNow();
        if (observed.Terminal)
        {
            string status = observed.ExitCode is null ? "unknown" : observed.ExitCode != 0 ? "failed" : now < d.DueAtUtc ? "on_time" : "unknown";
            MarkDeadline(a, new(status, now, status == "on_time" ? "Successful terminal state observed before deadline."
                : status == "failed" ? "Observed nonzero process exit; not successful completion."
                : "Successful completion time/exit is not established before deadline; remote wall-clock finish is informational only."));
        }
        else if (now >= d.DueAtUtc)
            MarkDeadline(a, new("missed", now, "Deadline reached without confirmed successful completion."));
    }

    /// <summary>Explicit owner tick; expiry alone never releases resources. Caller must keep polling.</summary>
    public AttemptRecord[] EnforceDeadlines()
    { lock(gate){EnforceDeadlinesCore();return WorkloadJson.Copy(records.ToArray());} }

    /// <summary>Same deadline actions without a discarded history snapshot; never skips enforcement.</summary>
    public void EnforceDeadlinePolicies()
    { lock(gate)EnforceDeadlinesCore(); }

    // Caller holds gate through observations, persisted intent, stop and (when requested) snapshot.
    private void EnforceDeadlinesCore()
    {
        RequireReconciled();
        foreach (var record in records.Where(r => r.Attempt.Request.Deadline is not null && r.Phase != "released").ToArray())
        {
            var a = record.Attempt;
            CaptureDeadlineEvidence(a, runtime.Observe(a));
            var result = CurrentRecord(a.Request.Id).Deadline;
            if (result?.Status != "missed" || !a.Request.Deadline!.StopWhenOverdue || !a.Request.Lifecycle.AllowStop) continue;
            if (!result.StopIntentPersisted) MarkDeadline(a, result with { StopIntentPersisted = true });
            StopAndRelease(a.Request.Id);
        }
    }
    private void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CoordinatorState(fleetId, records.ToArray())
            { RetainHourlyHeat = retainHourlyHeat ? true : null }, WorkloadJson.Options);
        string temporary = journalPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        JournalReplace.Move(temporary, journalPath);
    }
    public void Dispose() { lock (gate) { disposed = true; reconciled = false; fleetLock.Dispose(); } }
}
