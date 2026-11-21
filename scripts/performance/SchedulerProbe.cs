// Diagnostic synthetic fixtures only. Calls production code; no Docker, network or lifecycle mutations.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.Orchestration;

var output = args[0];
var sourceCommit = args[1];
const int Samples = 101;
const int WarmupCalls = 20;
const int CoreBatch = 10000;
const int OfferBatch = 10;
const double MicrosecondsPerSecond = 1_000_000;
int[] sizes = [3, 32, 128, 512, 1024];
var now = new DateTimeOffset(2025, 7, 15, 17, 0, 0, TimeSpan.Zero);
var authorization = new JobPlacementPolicy("country-allowlist", "Synthetic performance fixture; not a deployment authorization", ["CH"]);
var rows = new List<Measurement>();
double consumed = 0;

void Measure(string name, int nodes, int claims, string topology, Action action, Action? cleanup = null, int batch = 1)
{
    for (int i = 0; i < WarmupCalls; i++) { for (int k = 0; k < batch; k++) action(); cleanup?.Invoke(); }
    var times = new double[Samples];
    var allocations = new double[Samples];
    for (int i = 0; i < Samples; i++)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int k = 0; k < batch; k++) action();
        long end = Stopwatch.GetTimestamp();
        allocations[i] = (GC.GetAllocatedBytesForCurrentThread() - bytes) / (double)batch;
        times[i] = (end - start) * MicrosecondsPerSecond / Stopwatch.Frequency / batch;
        cleanup?.Invoke();
    }
    Array.Sort(times); Array.Sort(allocations);
    double Percentile(double p) => times[(int)Math.Ceiling(Samples * p) - 1];
    var row = new Measurement(name, nodes, claims, topology, batch, Percentile(.50), Percentile(.95), Percentile(.99), allocations[Samples / 2]);
    rows.Add(row);
    Console.Error.WriteLine($"{name} n={nodes} held={claims} {topology}: median {row.MedianUs:F3} us, p95 {row.P95Us:F3} us, {row.MedianAllocatedBytesPerOperation:F0} B/op");
}
WorkloadRequest Request(string id) => new(id, "interactive", "site-00000", authorization,
    new(1, 1024, 1), "Synthetic diagnostic fixture", .8, "Synthetic diagnostic fixture, not measured power/heat",
    ["game"], null, null, null, [], new(false, true, false));
WorkloadInventory Inventory(int count, bool sharedDomain)
{
    var sites = Enumerable.Range(0, count).Select(i => new WorkloadSite($"site-{i:D5}", "CH", new(10, 0, 35, 1, .8), 0)).ToArray();
    var domains = Enumerable.Range(0, sharedDomain ? 1 : count).Select(i => new ResourceDomain($"domain-{i:D5}",
        new(8 * (sharedDomain ? count : 1), 32768L * (sharedDomain ? count : 1), 8 * (sharedDomain ? count : 1)), [])).ToArray();
    var nodes = Enumerable.Range(0, count).Select(i => new ComputeNode($"node-{i:D5}", sites[i].Id, domains[sharedDomain ? 0 : i].Id,
        true, true, new(8, 32768, 8), ["game"], [], [], [])).ToArray();
    return new(1, now, now.AddHours(1), "Synthetic scaling fixture; one node per heat site", sites, domains, nodes, [])
    { ResourceObservedAtUtc = now, ResourceValidUntilUtc = now.AddMinutes(1) };
}

var greedy = new GreedyHeatScheduler(0);
var signals = Enumerable.Range(0, 16).Select(i => new HeatSignal(i, 5, 35, 4, .8)).ToArray();
int signalIndex = 0;
Measure("core_greedy_decide", 1, 0, "single-site", () => consumed += greedy.Decide(signals[signalIndex++ % signals.Length]).TargetLoadPerServer, batch: CoreBatch);
var request = Request("probe");
Measure("request_json_defensive_copy", 0, 0, "request-only", () => consumed += WorkloadJson.Copy(request).Resources.CpuCores, batch: 10);

foreach (int count in sizes)
{
    var offers = Enumerable.Range(0, count).Select(i => new FederatedCapacityOffer($"site-{i:D5}", "CH", "fixture-v1", now,
        now.AddMinutes(1), new(10 + i % 16, 0, 35, 1, .8), 0)).ToArray();
    Measure("federated_best_offer", count, 0, "one-offer-per-site", () =>
    {
        if (FederatedOfferSelection.BestEligible(offers, authorization, "site-00000", "fixture-v1", now, TimeSpan.Zero) is null)
            throw new Exception("Fixture unexpectedly rejected");
    }, batch: OfferBatch);
    foreach (bool shared in new[] { false, true })
    foreach (int claimCount in new[] { 0, Math.Min(100, count) })
    {
        var inventory = Inventory(count, shared);
        var placement = new WorkloadPlacement();
        placement.Update(inventory, now);
        if (claimCount > 0)
        {
            placement.Restore(Enumerable.Range(0, claimCount).Select(i =>
            {
                var node = inventory.Nodes[i]; var r = Request($"held-{i}");
                return new AllocationView(r, new(r.Id, node.SiteId, node.Id, node.DomainId, null, r.RecoverableHeatKwhPerHour, 45), false);
            }));
        }
        string topology = shared ? "shared-domain" : "independent-domains";
        Measure("reserve", count, claimCount, topology, () =>
        {
            var result = placement.TryReserve(request, now) ?? throw new Exception("Fixture unexpectedly full");
            consumed += result.AvailableHeatKwhBeforeReservation;
        }, () => { if (!placement.Release(request.Id)) throw new Exception("Fixture release failed"); });
        // Also check that pinned requests still pay the current candidate traversal cost.
        if (claimCount == 0)
        {
            var pinned = request with { PinnedNodeId = inventory.Nodes[^1].Id };
            Measure("reserve_pinned_last_node", count, claimCount, topology,
                () => { if (placement.TryReserve(pinned, now) is null) throw new Exception("Pinned fixture rejected"); },
                () => placement.Release(pinned.Id));
        }
        long revision = 1;
        Measure("inventory_update", count, claimCount, topology, () => placement.Update(inventory with { Revision = ++revision }, now));
        if (!shared && claimCount == 0)
            Measure("inventory_json_copy", count, 0, topology, () => consumed += WorkloadJson.Copy(inventory).Nodes.Length);
    }
}
var report = new
{
    generated_utc = DateTimeOffset.UtcNow, source_commit = sourceCommit,
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    process_architecture = RuntimeInformation.ProcessArchitecture.ToString(), logical_processors = Environment.ProcessorCount,
    stopwatch_frequency_hz = Stopwatch.Frequency, samples = Samples, warmup_calls_per_case = WarmupCalls,
    method = "Nearest-rank percentiles of elapsed microseconds per operation; batch>1 percentiles describe batch means. GC allocation is current-thread managed allocation. Cleanup/release, fixture construction and initial Update/Restore excluded from reservation timers. Warm steady-state calls only. No parallel admission or Docker measured.",
    fixture = "Synthetic one-node-per-heat-site; 8 CPU/32768 MiB/8 slots per node, additive shared-domain capacity, 10 kWh need + 35 kWh empty tank, .8 kWh/h per request; all illustrative diagnostic values, not hardware/heat measurements. Unique domains vs one shared physical domain. Up to 100 existing claims, one per node. No GPU, latency or external-allocation fixtures.",
    caveat = "Non-dedicated Windows laptop; existing Docker desktops, games and web/API left running. Includes OS/GC interference, no machine-idle or performance-SLA claim. Does not time app startup, journal writes, network simulation/LP solving, network transit or failover.",
    consumed_checksum = consumed, measurements = rows
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));

record Measurement(string Operation, int Nodes, int HeldClaims, string Topology, int OperationsPerSample,
    double MedianUs, double P95Us, double P99Us, double MedianAllocatedBytesPerOperation);
