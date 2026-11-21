using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

public sealed record AttemptRecord(RuntimeAttempt Attempt, string Phase);
public sealed record CoordinatorState(string FleetId, AttemptRecord[] Attempts);

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
    public bool Reconciled { get { lock (gate) return reconciled; } }

    public WorkloadCoordinator(string fleetId, string stateDirectory, IWorkloadRuntime runtime)
    {
        if (string.IsNullOrWhiteSpace(fleetId) || !System.Text.RegularExpressions.Regex.IsMatch(fleetId, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$"))
            throw new ArgumentException("Invalid fleet identity.");
        this.fleetId = fleetId; this.runtime = runtime;
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
                foreach (var record in state.Attempts)
                {
                    DockerWorkloadRuntime.Validate(record.Attempt);
                    if (record.Phase is not ("reserved" or "creating" or "created" or "starting" or "running" or "paused" or "pausing" or "resuming" or "stopping" or "terminal" or "removing" or "released"))
                        throw new InvalidDataException("Unknown journal phase.");
                }
                records.AddRange(state.Attempts);
                placement.Restore(records.Where(r => r.Phase != "released").Select(r => new AllocationView(r.Attempt.Request, r.Attempt.Reservation, r.Phase == "paused")));
            }
        }
        catch { fleetLock.Dispose(); throw; }
    }

    public void Update(WorkloadInventory inventory, DateTimeOffset nowUtc) { lock (gate) { placement.Update(inventory, nowUtc); currentInventory = WorkloadJson.Copy(inventory); } }
    public AllocationView[] Allocations() { lock (gate) return placement.Allocations(); }
    public AttemptRecord[] Attempts() { lock (gate) return WorkloadJson.Copy(records.ToArray()); }

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
        }
    }

    public AttemptRecord? TryStart(WorkloadRequest request, IReadOnlyList<RuntimeNode> nodes, ContainerProfile profile,
        DateTimeOffset nowUtc, bool pauseBatchFirst = true)
    {
        lock (gate)
        {
            RequireReconciled();
            var prior = records.SingleOrDefault(r => r.Attempt.Request.Id == request.Id);
            if (prior is not null)
            {
                if (WorkloadJson.Identity(prior.Attempt.Request) != WorkloadJson.Identity(request) || WorkloadJson.Identity(prior.Attempt.Profile) != WorkloadJson.Identity(profile))
                    throw new InvalidDataException("Attempt retry changed requirements/profile.");
                if (prior.Phase == "created")
                {
                    if (!placement.CanStartHeld(request.Id, nowUtc)) return null;
                    try
                    {
                        if (runtime.Observe(prior.Attempt).State != "created") throw new InvalidOperationException("Created attempt changed during recovery.");
                        SetPhase(prior.Attempt, "starting"); runtime.Start(prior.Attempt);
                        if (runtime.Observe(prior.Attempt).State != "running") throw new InvalidOperationException("Recovered start unconfirmed.");
                        SetPhase(prior.Attempt, "running");
                        return WorkloadJson.Copy(records.Single(r => r.Attempt.AttemptId == prior.Attempt.AttemptId));
                    }
                    catch { reconciled = false; throw; }
                }
                return WorkloadJson.Copy(prior); // Never reissue an uncertain start or reuse a terminated identity.
            }
            Preflight(request, nodes, profile);
            var reservation = placement.TryReserve(request, nowUtc);
            if (reservation is null && request.Kind == "interactive" && pauseBatchFirst && placement.CanAdmitAfterBatchPause(request, nowUtc))
            {
                foreach (var batch in records.Where(r => r.Phase == "running" && r.Attempt.Request.Kind == "batch" && r.Attempt.Request.Lifecycle.AllowPause).ToArray())
                {
                    PauseBatch(batch.Attempt.Request.Id);
                    reservation = placement.TryReserve(request, nowUtc);
                    if (reservation is not null) break;
                }
            }
            if (reservation is null) return null;
            RuntimeAttempt attempt;
            try
            {
                attempt = new(fleetId, Guid.NewGuid().ToString("N"), WorkloadJson.Copy(request), reservation,
                    WorkloadJson.Copy(nodes.Single(n => n.NodeId == reservation.NodeId)), WorkloadJson.Copy(profile));
                DockerWorkloadRuntime.Validate(attempt);
            }
            catch { placement.Release(request.Id); throw; } // No Docker mutation or journal intent yet.
            records.Add(new(attempt, "reserved"));
            try
            {
                Save();
                SetPhase(attempt, "creating"); runtime.Create(attempt); SetPhase(attempt, "created");
                SetPhase(attempt, "starting"); runtime.Start(attempt);
                var observed = runtime.Observe(attempt);
                if (observed.State != "running") throw new InvalidOperationException("Start not confirmed running; claim retained.");
                SetPhase(attempt, "running");
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
                placement.ObservePaused(workloadId); SetPhase(a, "paused");
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
            if (!placement.TryClaimResume(workloadId, nowUtc)) return false;
            try
            {
                SetPhase(a, "resuming"); lastAppliedBatchLoad = runtime.ApplyBatchLoad(a, 1);
                if (runtime.Observe(a).State != "running") throw new InvalidOperationException("Resume not confirmed; full claim retained.");
                SetPhase(a, "running"); return true;
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
                    SetPhase(a, "released"); placement.Release(workloadId); return;
                }
                if (!observed.Terminal)
                {
                    SetPhase(a, "stopping"); runtime.Stop(a); observed = runtime.Observe(a);
                }
                if (!observed.Terminal) throw new InvalidOperationException("Termination unconfirmed; resources remain held.");
                SetPhase(a, "terminal"); SetPhase(a, "removing"); runtime.Remove(a);
                if (runtime.Observe(a).State != "missing") throw new InvalidOperationException("Removal unconfirmed; resources remain held.");
                SetPhase(a, "released"); placement.Release(workloadId);
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
        foreach (var group in nodes.GroupBy(n => n.DockerContext))
            if (inventory.Nodes.Where(n => group.Any(r => r.NodeId == n.Id)).Select(n => n.DomainId).Distinct().Count() > 1)
                throw new InvalidDataException("One Docker context cannot be assigned independent physical resource pools.");
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
        records[index] = new(a, phase); Save();
    }
    private void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CoordinatorState(fleetId, records.ToArray()), WorkloadJson.Options);
        string temporary = journalPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        File.Move(temporary, journalPath, overwrite: true);
    }
    public void Dispose() { lock (gate) { disposed = true; reconciled = false; fleetLock.Dispose(); } }
}
