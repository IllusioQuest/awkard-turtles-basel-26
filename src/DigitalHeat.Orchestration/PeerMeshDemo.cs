using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

public sealed record MeshDemoNode(string Id, double DemandKwh, double PlayerRttMs);
public sealed record MeshDemoJob(string Id, string Origin, string Kind, double? MaxPlayerRttMs);
public sealed record MeshDemoSpec(string Source, DateTimeOffset IntervalStartUtc, double CpuCoresPerPeer,
    long RamMibPerPeer, int SlotsPerPeer, double JobCpuCores, long JobRamMib, double JobHeatKwhPerHour,
    double OfferLifetimeSeconds, MeshDemoNode[] Nodes, MeshDemoJob[] Jobs);

/// <summary>Executable protocol example. Fake runtime only: never opens Docker or runs job commands.</summary>
public static class PeerMeshDemo
{
    public static object Run(string scenarioPath, string stateDirectory)
    {
        var spec = JsonSerializer.Deserialize<MeshDemoSpec>(File.ReadAllText(scenarioPath), WorkloadJson.Options)
            ?? throw new InvalidDataException("Empty mesh demo.");
        var clock = new DemoClock(spec.IntervalStartUtc);
        var bus = new DemoBus();
        var peers = new List<MeshPeer>();
        var runtimes = new Dictionary<string, DemoRuntime>();
        try
        {
            foreach (var node in spec.Nodes)
            {
                var runtime = new DemoRuntime(); runtimes[node.Id] = runtime;
                var resources = new ResourceAmount(spec.CpuCoresPerPeer, spec.RamMibPerPeer, spec.SlotsPerPeer);
                var inventory = new WorkloadInventory(1, clock.GetUtcNow(), clock.GetUtcNow().AddHours(1), spec.Source,
                    [new(node.Id, "CH", new(node.DemandKwh, 0, 0, 1, spec.JobHeatKwhPerHour), 0)],
                    [new(node.Id + "-physical", resources, [])],
                    [new(node.Id + "-worker", node.Id, node.Id + "-physical", true, true, resources, [], [], [], [new("player", node.PlayerRttMs)])], [])
                    { ResourceObservedAtUtc = clock.GetUtcNow(), ResourceValidUntilUtc = clock.GetUtcNow().AddMinutes(1) };
                var profile = new ContainerProfile("demo", "simulation-only", "65534", ["simulation-only"], null, [], [], ["true"]);
                var peer = new MeshPeer("mesh-demo", node.Id, Path.Combine(stateDirectory, node.Id), inventory,
                    [new(node.Id + "-worker", node.Id + "-engine", "none", [], [])],
                    new Dictionary<string, ContainerProfile> { [profile.Id] = profile }, spec.Nodes.Select(n => n.Id), bus,
                    runtime, clock, TimeSpan.FromSeconds(spec.OfferLifetimeSeconds));
                peers.Add(peer); bus.Peers[node.Id] = peer;
            }
            foreach (var peer in peers) peer.Publish();
            foreach (var job in spec.Jobs)
            {
                var request = new WorkloadRequest(job.Id, job.Kind, job.Origin, new("anywhere", spec.Source, null),
                    new(spec.JobCpuCores, spec.JobRamMib, 1), spec.Source, spec.JobHeatKwhPerHour, spec.Source,
                    [], null, null, null, job.MaxPlayerRttMs is {} max ? [new("player", max)] : [], new(job.Kind == "batch", true, false));
                bus.Peers[job.Origin].Submit(request, "demo");
                bus.Peers[job.Origin].Tick();
            }
            return new { mode = "deterministic_peer_mesh_fake_executors", spec.Source, messages = bus.Messages,
                origins = peers.Select(p => new { peer_id = p.Id, jobs = p.Jobs().Select(j => new { job_id = j.Request.Id, j.Target, j.Status }) }).ToArray(),
                executors = peers.Select(p => new { peer_id = p.Id, starts = runtimes[p.Id].Starts, attempts = p.Attempts().Select(a => new { job_id = a.Attempt.Request.Id, a.Phase }) }).ToArray() };
        }
        finally { foreach (var peer in peers) peer.Dispose(); }
    }

    private sealed class DemoClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class DemoBus : IPeerMeshTransport
    {
        public Dictionary<string, MeshPeer> Peers { get; } = [];
        public int Messages { get; private set; }
        public void Send(string from, string to, PeerMessage message) { Messages++; Peers[to].Receive(from, message); }
    }
    private sealed class DemoRuntime : IWorkloadRuntime
    {
        private readonly Dictionary<string, ContainerObservation> states = [];
        public int Starts { get; private set; }
        public IReadOnlyList<string> Discover(string fleetId, IReadOnlyList<RuntimeNode> nodes) => states.Keys.ToArray();
        public ContainerObservation Observe(RuntimeAttempt a) => states.GetValueOrDefault(a.AttemptId) ?? new("missing", null, false);
        public void Create(RuntimeAttempt a) => states[a.AttemptId] = new("created", a.AttemptId, false);
        public void Start(RuntimeAttempt a) { Starts++; states[a.AttemptId] = new("running", a.AttemptId, true); }
        public void Stop(RuntimeAttempt a) => states[a.AttemptId] = new("exited", a.AttemptId, false);
        public void Remove(RuntimeAttempt a) => states.Remove(a.AttemptId);
        public double ApplyBatchLoad(RuntimeAttempt a, double targetLoad) { states[a.AttemptId] = new(targetLoad == 0 ? "paused" : "running", a.AttemptId, targetLoad > 0); return targetLoad; }
    }
}
