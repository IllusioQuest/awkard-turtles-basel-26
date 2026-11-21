using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;

try
{
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
    if (args.Length < 2) throw new ArgumentException("Usage: WorkloadRun plan <config> [UTC-time] | status/start/pause/resume/heat/stop <config> <state-dir> [workload-id] | backup/restore-empty <config> <state-dir> <id> <storage-id> <backup-volume> <archive.tar.gz> <local-tool-image> [new-volume]");
    string command = args[0];
    var config = WorkloadConfiguration.Load(args[1]);
    if (command == "plan")
    {
        var now = args.Length == 3 ? DateTimeOffset.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : DateTimeOffset.UtcNow;
        var placement = new WorkloadPlacement(); placement.Update(config.Inventory, now);
        var plans = config.Workloads.Select(w => new { workload_id = w.Request.Id, reservation = placement.TryReserve(w.Request, now) }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "plan_only_no_docker", config.Inventory.Source, plans }, WorkloadJson.Options));
        return 0;
    }
    if (args.Length < 3) throw new ArgumentException("A persistent state directory is required.");
    if (command is not ("status" or "start" or "pause" or "resume" or "heat" or "stop" or "backup" or "restore-empty")) throw new ArgumentException("Unknown command.");
    var docker = new DockerCommands();
    using var coordinator = new WorkloadCoordinator(config.FleetId, args[2], new DockerWorkloadRuntime(docker));
    var nowUtc = DateTimeOffset.UtcNow;
    // Observation/cleanup remains available when a resource snapshot expires; admission/resume does not.
    if (command is "start" or "resume" or "heat") coordinator.Update(config.Inventory, nowUtc);
    coordinator.Reconcile(config.RuntimeNodes);
    if (command == "status")
    {
        var rows = coordinator.Attempts().Select(a => new { workload_id = a.Attempt.Request.Id, a.Phase,
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
            if (!coordinator.ApplyBatchHeat(id, signal, new GreedyHeatScheduler(0), nowUtc)) return 3;
            Console.WriteLine("Applied shared GreedyHeatScheduler decision to the owned batch executor."); break;
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
return 0;
