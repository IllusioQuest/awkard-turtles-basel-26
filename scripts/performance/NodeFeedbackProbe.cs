using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;

const int WarmupCalls = 5;
const int Samples = 31;
const int QueuedJobs = 32;
const int MaxHeldJobs = 16;
var now = new DateTimeOffset(2025, 7, 15, 17, 0, 0, TimeSpan.Zero);
var rows = new List<object>();
foreach (int count in new[] { 4, 32, 128 })
foreach (bool occupied in new[] { false, true })
{
    // No Docker calls: independent physical domains and all numbers here are synthetic fixtures.
    string root = Path.Combine(Path.GetTempPath(), "dh-feedback-probe-" + Guid.NewGuid().ToString("N"));
    var sites = Enumerable.Range(0, count).Select(i => new WorkloadSite($"s{i}", "CH", new(10, 0, 35, 1, .8), 0)).ToArray();
    var domains = Enumerable.Range(0, count).Select(i => new ResourceDomain($"d{i}", new(1, 128, 1), [])).ToArray();
    var nodes = Enumerable.Range(0, count).Select(i => new ComputeNode($"n{i}", $"s{i}", $"d{i}", true, true,
        new(1, 128, 1), [], [], [], [])).ToArray();
    var inventory = new WorkloadInventory(1, now, now.AddHours(1), "Synthetic diagnostic, not measured capacity or heat",
        sites, domains, nodes, []) { ResourceObservedAtUtc = now, ResourceValidUntilUtc = now.AddMinutes(1) };
    var runtimes = nodes.Select(n => new RuntimeNode(n.Id, $"fixture-context-{n.Id}", "none", [], [])).ToArray();
    var profile = new ContainerProfile("fixture", "local-fixture-image", "65534", ["sleep", "300"], null, [], [], ["true"]) { ShmMib = 16 };
    WorkloadRequest Request(string id, string kind, string? pin, string[] capabilities) => new(id, kind, sites[0].Id,
        new("anywhere", "Synthetic diagnostic authorization", null), new(1, 64, 1), "Synthetic resource fixture",
        .8, "Synthetic heat fixture, not measured game heat", capabilities, null, pin, null, [], new(kind == "batch", true, false));
    var held = occupied ? Enumerable.Range(0, Math.Min(count, MaxHeldJobs)).Select(i =>
        new WorkloadDefinition(Request($"held{i}", "interactive", nodes[i].Id, []), profile)).ToArray() : [];
    var queued = occupied ? Enumerable.Range(0, QueuedJobs).Select(i =>
        new WorkloadDefinition(Request($"waiting{i}", "batch", null, ["unavailable-fixture-capability"]), profile)).ToArray() : [];
    var manifest = new WorkloadConfiguration("probe-" + Guid.NewGuid().ToString("N"), inventory, runtimes, [.. held, .. queued]);
    var policy = new NodeFeedbackPolicy(1, 1, 30, 65536, MaxHeldJobs + QueuedJobs, "Synthetic diagnostic policy",
        nodes.Select(n => new HeatReporter(n.SiteId, n.Id)).ToArray());
    var runtime = new FakeRuntime();
    var clock = new FrozenClock(now);
    using (var coordinator = new WorkloadCoordinator(manifest.FleetId, Path.Combine(root, "state"), runtime, clock, retainHourlyHeat: true))
    {
        coordinator.Update(inventory, now); coordinator.Reconcile(runtimes);
        foreach (var entry in held) coordinator.TryStart(entry.Request, runtimes, entry.Profile, now);
        string directory = Path.Combine(root, "inbox");
        foreach (var node in nodes) NodeFeedbackInbox.Publish(directory, new(node.Id, 1, now, now.AddSeconds(30), now,
            true, sites.Single(s => s.Id == node.SiteId).Heat, "Synthetic fixture", "hourly_baseline"));
        var inbox = new NodeFeedbackInbox(directory, Path.Combine(root, "state"), manifest.FleetId, policy, inventory);
        var owner = new NodeFeedbackOwner(coordinator, manifest, inbox, policy, clock);
        void Tick()
        {
            if (owner.Tick(() => manifest).Length != 0 || runtime.Creates != held.Length)
                throw new InvalidOperationException("Diagnostic changed behavior or admitted a blocked workload.");
        }
        for (int i = 0; i < WarmupCalls; i++) Tick();
        var elapsed = new double[Samples]; var allocated = new long[Samples];
        for (int i = 0; i < Samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
            Tick();
            elapsed[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated[i] = GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
        Array.Sort(elapsed); Array.Sort(allocated);
        rows.Add(new { nodes = count, held_jobs = held.Length, waiting_jobs = queued.Length,
            median_ms = elapsed[Samples / 2], p95_ms = elapsed[(int)Math.Ceiling(.95 * Samples) - 1],
            median_allocated_bytes = allocated[Samples / 2] });
    }
    // Keep the scoped temp fixture for traceability; never delete any runtime/game files.
}
var report = new { generated_utc = DateTimeOffset.UtcNow, source_commit = args[1], runtime = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription, logical_processors = Environment.ProcessorCount, warmup_calls = WarmupCalls, samples = Samples,
    method = "Warm synchronous NodeFeedbackOwner.Tick, including real temporary-file reads/validation, fake runtime reconciliation and blocked queue admission. Initial publication/journal writes, constructor, build, fixture setup and JIT warmup excluded. Median and nearest-rank p95; current-thread managed allocations.",
    caveat = "Synthetic fixed-clock fixture on a non-dedicated Windows laptop; no Docker/network/graphics/stream latency or event-delivery SLA. Existing game containers stay running. Steady duplicate reports only: new-report fsync and new-job runtime/journal startup are not timed. Same numeric fixtures for both source revisions.",
    measurements = rows };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

sealed class FrozenClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
sealed class FakeRuntime : IWorkloadRuntime
{
    private readonly Dictionary<string, ContainerObservation> states = [];
    public int Creates { get; private set; }
    public IReadOnlyList<string> Discover(string fleetId, IReadOnlyList<RuntimeNode> nodes) => states.Keys.ToArray();
    public ContainerObservation Observe(RuntimeAttempt a) => states.GetValueOrDefault(a.AttemptId) ?? new("missing", null, false);
    public void Create(RuntimeAttempt a) { Creates++; states[a.AttemptId] = new("created", "abcdef123456", false); }
    public void Start(RuntimeAttempt a) => states[a.AttemptId] = new("running", "abcdef123456", true);
    public void Stop(RuntimeAttempt a) => throw new InvalidOperationException("No stop expected in fixture.");
    public void Remove(RuntimeAttempt a) => throw new InvalidOperationException("No remove expected in fixture.");
    public double ApplyBatchLoad(RuntimeAttempt a, double targetLoad) => throw new InvalidOperationException("No pause expected in fixture.");
}
