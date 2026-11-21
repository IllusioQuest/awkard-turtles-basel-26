using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;

try
{
    if (args.Length == 2 && args[0] == "render-node") { await RenderNodeHost.Run(args[1]); return 0; }
    if (args.Length == 2 && args[0] == "mesh-node") { await MeshNodeHost.Run(args[1]); return 0; }
    if (args.Length == 3 && args[0] == "mesh-demo")
    {
        Console.WriteLine(JsonSerializer.Serialize(PeerMeshDemo.Run(args[1], args[2]), WorkloadJson.Options));
        return 0;
    }
    if (args.Length == 3 && args[0] == "publish-feedback")
    {
        var report = JsonSerializer.Deserialize<NodeFeedback>(File.ReadAllText(args[2]), WorkloadJson.Options)
            ?? throw new InvalidDataException("Empty node report.");
        NodeFeedbackInbox.Publish(args[1], report);
        Console.WriteLine("Published atomic node update; this does not itself authorize a workload start.");
        return 0;
    }
    if (args.Length >= 3 && args[0] == "example")
    {
        string localHour = args.Length >= 4 ? args[3] : "2025-07-15T18:00";
        var example = ExampleConfiguration.Create(Path.GetFullPath(args[1]), localHour, args.Contains("--unoccupied-simulation"));
        if (File.Exists(args[2])) throw new IOException("Example output already exists; choose a new file.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
        File.WriteAllText(args[2], JsonSerializer.Serialize(example, WorkloadJson.Options));
        Console.WriteLine($"Wrote historical PLAN example. UTC plan time: {example.Inventory.IntervalStartUtc:O}. This is not fresh runtime inventory.");
        return 0;
    }
    if (args.Length < 2) throw new ArgumentException("Usage: WorkloadRun plan <config> [UTC-time] | status/start/pause/resume/heat/stop <config> <state-dir> [workload-id] | watch-feedback <config> <state-dir> <inbox-dir> <policy.json> | publish-feedback <inbox-dir> <report.json> | backup/restore-empty <config> <state-dir> <id> <storage-id> <backup-volume> <archive.tar.gz> <local-tool-image> [new-volume]");
    string command = args[0];
    var config = WorkloadConfiguration.Load(args[1]);
    if (command == "set-deadline")
    {
        if (args.Length != 6) throw new ArgumentException("set-deadline <config> <new-output> <workload-id> <profile-id> <declared-work-units>");
        if (File.Exists(args[2])) throw new IOException("Output exists; no submission deadline will be silently replaced.");
        var policy = LiveDeadlineProfiles.Load(Path.Combine(FindRoot(), "config/live-job-deadlines.json"));
        var profile = policy.Profiles.Single(p => p.Id == args[4]);
        var target = config.Workloads.Single(w => w.Request.Id == args[3]);
        if (target.Request.Kind != "batch" || target.Request.Deadline is not null) throw new InvalidOperationException("Only a new untimed batch request may be stamped. Never extend a prior submission.");
        double units = double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
        var stamped = target.Request with { Deadline = policy.For(profile.Id, profile.JobId, units, DateTimeOffset.UtcNow) };
        var result = config with { Workloads = config.Workloads.Select(w => w.Request.Id == stamped.Id ? w with { Request = stamped } : w).ToArray() };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
        File.WriteAllText(args[2], JsonSerializer.Serialize(result, WorkloadJson.Options));
        Console.WriteLine(JsonSerializer.Serialize(new { workload_id = stamped.Id, stamped.Deadline, warning = "Experimental target; input identity/bounds are operator declarations, not validated private-file contents. Queue/startup time counts now." }, WorkloadJson.Options));
        return 0;
    }
    if (command == "plan")
    {
        var now = args.Length == 3 ? DateTimeOffset.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : DateTimeOffset.UtcNow;
        var placement = new WorkloadPlacement(); placement.Update(config.Inventory, now);
        var plans = config.Workloads.Select(w => new { workload_id = w.Request.Id, reservation = placement.TryReserve(w.Request, now) }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "plan_only_no_docker", config.Inventory.Source, plans }, WorkloadJson.Options));
        return 0;
    }
    if (args.Length < 3) throw new ArgumentException("A persistent state directory is required.");
    if (command is not ("status" or "start" or "pause" or "resume" or "heat" or "stop" or "backup" or "restore-empty" or "watch-deadlines" or "watch-feedback")) throw new ArgumentException("Unknown command.");
    var docker = new DockerCommands();
    bool retainedHeat = command == "watch-feedback";
    string journalPath = Path.Combine(args[2], "journal.json");
    if (File.Exists(journalPath)) retainedHeat |= JsonSerializer.Deserialize<CoordinatorState>(File.ReadAllText(journalPath), WorkloadJson.Options)?.RetainHourlyHeat == true;
    if (retainedHeat && command is "start" or "resume" or "heat" or "watch-deadlines")
        throw new InvalidOperationException("This fleet uses node feedback. Use watch-feedback for admission/resume; status, pause, stop and cold backup remain available after stopping its owner.");
    using var coordinator = new WorkloadCoordinator(config.FleetId, args[2], new DockerWorkloadRuntime(docker), retainHourlyHeat: retainedHeat);
    if (command == "watch-feedback")
    {
        if (args.Length != 5) throw new ArgumentException("watch-feedback <config> <state-dir> <inbox-dir> <feedback-policy.json>");
        var policy = NodeFeedbackPolicy.Load(args[4]);
        var policyIdentity = WorkloadJson.Identity(policy);
        var inbox = new NodeFeedbackInbox(args[3], args[2], config.FleetId, policy, config.Inventory);
        var owner = new NodeFeedbackOwner(coordinator, config, inbox, policy);
        using var wake = new NodeFeedbackWake(args[3]);
        using var configWatch = new FileSystemWatcher(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, Path.GetFileName(args[1]));
        configWatch.Changed += (_, _) => wake.Notify(); configWatch.Created += (_, _) => wake.Notify();
        configWatch.Renamed += (_, _) => wake.Notify(); configWatch.Error += (_, _) => wake.Notify();
        configWatch.EnableRaisingEvents = true;
        using var cancelled = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancelled.Cancel(); };
        Console.CancelKeyPress += cancel;
        string? previous = null;
        try
        {
            while (!cancelled.IsCancellationRequested)
            {
                var errors = owner.Tick(() =>
                {
                    if (WorkloadJson.Identity(NodeFeedbackPolicy.Load(args[4])) != policyIdentity)
                        throw new InvalidDataException("Feedback policy changed; restart explicitly to change authority/limits.");
                    return WorkloadConfiguration.Load(args[1]);
                });
                string json = JsonSerializer.Serialize(new { mode = "event_driven_node_feedback", errors,
                    attempts = coordinator.Attempts() }, WorkloadJson.Options);
                if (json != previous)
                {
                    Console.WriteLine(json); previous = json;
                }
                await wake.WaitAsync(TimeSpan.FromSeconds(policy.FallbackIntervalSeconds), cancelled.Token);
            }
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
        finally { Console.CancelKeyPress -= cancel; }
        Console.Error.WriteLine("Feedback owner stopped; jobs remain owned. Feedback/deadline enforcement requires this owner running.");
        return 130;
    }
    if (command == "watch-deadlines")
    {
        if (config.Workloads.Length == 0 || config.Workloads.Any(w => w.Request.Kind != "batch" || w.Request.Deadline is null))
            throw new InvalidDataException("Deadline watch accepts only a finite manifest of explicitly deadline-bound batch jobs; no interactive workloads.");
        foreach (var prior in coordinator.Attempts())
        {
            var manifestEntry = config.Workloads.SingleOrDefault(w => w.Request.Id == prior.Attempt.Request.Id);
            if (manifestEntry is null || WorkloadJson.Identity(manifestEntry.Request) != WorkloadJson.Identity(prior.Attempt.Request)
                || WorkloadJson.Identity(manifestEntry.Profile) != WorkloadJson.Identity(prior.Attempt.Profile))
                throw new InvalidDataException("Submission manifest differs from the durable journal; no mutation authorized.");
        }
        var policy = LiveDeadlineProfiles.Load(Path.Combine(FindRoot(), "config/live-job-deadlines.json"));
        if (config.Workloads.Length > policy.MaxQueuedJobs) throw new InvalidDataException("Submission manifest exceeds the configured owner-loop bound.");
        string manifestIdentity = WorkloadJson.Identity(config.Workloads);
        string nodesIdentity = WorkloadJson.Identity(config.RuntimeNodes);
        var inventory = config.Inventory;
        string inventoryIdentity = WorkloadJson.Identity(inventory);
        coordinator.Update(config.Inventory, DateTimeOffset.UtcNow);
        using var cancelled = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancelled.Cancel(); };
        Console.CancelKeyPress += cancel;
        string? previous = null;
        try
        {
            while (!cancelled.IsCancellationRequested)
            {
                // The operator may refresh telemetry in place, but never rewrite accepted deadlines/specs.
                var latest = WorkloadConfiguration.Load(args[1]);
                if (latest.FleetId != config.FleetId || WorkloadJson.Identity(latest.Workloads) != manifestIdentity
                    || WorkloadJson.Identity(latest.RuntimeNodes) != nodesIdentity || latest.Inventory.Revision < inventory.Revision)
                    throw new InvalidDataException("Accepted manifest/node identities or inventory revision changed; refusing mutations.");
                string nextInventoryIdentity = WorkloadJson.Identity(latest.Inventory);
                if (latest.Inventory.Revision == inventory.Revision && nextInventoryIdentity != inventoryIdentity)
                    throw new InvalidDataException("Telemetry changed without an advancing inventory revision.");
                if (latest.Inventory.Revision > inventory.Revision)
                {
                    coordinator.Update(latest.Inventory, DateTimeOffset.UtcNow);
                    inventory = latest.Inventory; inventoryIdentity = nextInventoryIdentity;
                }
                coordinator.Reconcile(config.RuntimeNodes);
                coordinator.EnforceDeadlines();
                foreach (var queuedEntry in config.Workloads.OrderBy(w => w.Request.Deadline!.DueAtUtc).ThenBy(w => w.Request.Id, StringComparer.Ordinal))
                {
                    if (coordinator.Attempts().Any(a => a.Attempt.Request.Id == queuedEntry.Request.Id)) continue;
                    if (WorkloadPlacement.DeadlineOpen(queuedEntry.Request, DateTimeOffset.UtcNow))
                        coordinator.TryStart(queuedEntry.Request, config.RuntimeNodes, queuedEntry.Profile, DateTimeOffset.UtcNow);
                }
                coordinator.EnforceDeadlines();
                foreach (var record in coordinator.Attempts().Where(r => r.Phase == "terminal" && r.Deadline is not null && r.Attempt.Request.Lifecycle.AllowStop))
                    coordinator.StopAndRelease(record.Attempt.Request.Id);
                var attempts = coordinator.Attempts();
                var rows = config.Workloads.Select(w =>
                {
                    var a = attempts.SingleOrDefault(a => a.Attempt.Request.Id == w.Request.Id);
                    return new { workload_id = w.Request.Id, deadline = w.Request.Deadline, phase = a?.Phase ?? "waiting",
                        outcome = a?.Deadline, waiting_expired = a is null && DateTimeOffset.UtcNow >= w.Request.Deadline!.DueAtUtc };
                }).ToArray();
                string json = JsonSerializer.Serialize(rows, WorkloadJson.Options);
                if (json != previous)
                {
                    File.WriteAllText(Path.Combine(args[2], "deadline-status.json.tmp"), json);
                    File.Move(Path.Combine(args[2], "deadline-status.json.tmp"), Path.Combine(args[2], "deadline-status.json"), true);
                    Console.WriteLine(json); previous = json;
                }
                if (rows.All(r => r.waiting_expired || r.phase == "released" || r.outcome is not null))
                    return rows.Any(r => r.waiting_expired || r.outcome?.Status != "on_time") ? 4 : 0;
                await Task.Delay(TimeSpan.FromSeconds(policy.PollIntervalSeconds), cancelled.Token);
            }
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
        finally { Console.CancelKeyPress -= cancel; }
        Console.Error.WriteLine("Deadline owner stopped. Jobs were not cancelled by this interruption; timely enforcement requires the owner loop running.");
        return 130;
    }
    var nowUtc = DateTimeOffset.UtcNow;
    // Observation/cleanup remains available when a resource snapshot expires; admission/resume does not.
    if (command is "start" or "resume" or "heat") coordinator.Update(config.Inventory, nowUtc);
    coordinator.Reconcile(config.RuntimeNodes);
    if (command == "status")
    {
        var rows = coordinator.Attempts().Select(a => new { workload_id = a.Attempt.Request.Id, a.Phase, deadline = a.Attempt.Request.Deadline, deadline_outcome = a.Deadline,
            node_id = a.Attempt.Reservation.NodeId, observation = a.Phase == "released" ? null : coordinator.Observe(a.Attempt.Request.Id) });
        Console.WriteLine(JsonSerializer.Serialize(rows, WorkloadJson.Options));
        return 0;
    }
    if (args.Length < 4) throw new ArgumentException("A workload ID is required.");
    string id = args[3];
    var definition = config.Workloads.Single(w => w.Request.Id == id);
    switch (command)
    {
        case "start":
            var record = coordinator.TryStart(definition.Request, config.RuntimeNodes, definition.Profile, nowUtc);
            if (record is null) { Console.WriteLine("Blocked: no eligible node fits current heat, compute, locality, latency and storage constraints."); return 3; }
            Console.WriteLine(JsonSerializer.Serialize(new { workload_id = id, record.Phase, record.Attempt.Reservation,
                observation = record.Phase == "released" ? null : coordinator.Observe(id) }, WorkloadJson.Options));
            break;
        case "pause": coordinator.PauseBatch(id); Console.WriteLine("Batch paused; CPU/model heat released, RAM/GPU/slot retained."); break;
        case "resume":
            if (!coordinator.ResumeBatch(id, nowUtc)) { Console.WriteLine("Resume blocked by current heat/resource inventory."); return 3; }
            Console.WriteLine("Batch resumed after re-admission."); break;
        case "heat":
            var allocation = coordinator.Allocations().Single(c => c.Request.Id == id);
            var signal = config.Inventory.Sites.Single(s => s.Id == allocation.Reservation.SiteId).Heat;
            if (!coordinator.ApplyBatchHeat(id, signal, new HeatFirstScheduler(0), nowUtc)) return 3;
            Console.WriteLine("Applied shared HeatFirstScheduler decision to the owned batch executor."); break;
        case "stop": coordinator.StopAndRelease(id); Console.WriteLine("Confirmed termination/removal; claim released. Local volumes preserved."); break;
        case "backup":
        case "restore-empty":
            if (args.Length != (command == "backup" ? 8 : 9)) throw new ArgumentException("Expected storage-id backup-volume archive.tar.gz local-tool-image [new-volume].");
            var released = coordinator.Attempts().Single(r => r.Attempt.Request.Id == id);
            var service = new LocalVolumeBackup(docker);
            if (command == "backup") service.Backup(released, args[4], args[5], args[6], args[7]);
            else service.RestoreEmpty(released, args[4], args[5], args[6], args[7], args[8]);
            Console.WriteLine("Cold local-volume operation completed; restore requires explicit future binding/admission."); break;
    }
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }

static string FindRoot()
{
    for (DirectoryInfo? d = new(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config/live-job-deadlines.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository containing config/live-job-deadlines.json.");
}
return 0;
