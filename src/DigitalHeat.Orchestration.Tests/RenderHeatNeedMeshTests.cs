using Xunit;

namespace DigitalHeat.Orchestration.Tests;

/// <summary>
/// ADR 0069 end to end on the real MeshPeer protocol: one origin and three single-slot GPU peers that
/// publish RenderHeatNeed.DemandKwh as their heat demand, like RenderNodeHost does. Placement is the
/// unchanged heat-first mesh; the test only completes frames and refreshes inventories.
/// </summary>
public class RenderHeatNeedMeshTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dh-need-" + Guid.NewGuid().ToString("N"));
    private readonly Clock clock = new();
    private readonly Bus bus = new();
    private readonly Dictionary<string, CoordinatorTests.Runtime> runtimes = [];
    private readonly Dictionary<string, MeshPeer> peers = [];
    private readonly Dictionary<string, RunHeatNeed?> needs = [];
    private static readonly string[] Gpus = ["g1", "g2", "g3"];
    private static readonly (string Id, double Weight)[] Weights = [("g1", 1.5), ("g2", 1), ("g3", .5)];
    private const double FrameHeatKwh = .8;

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = PlacementTests.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Bus : IPeerMeshTransport
    {
        public readonly Dictionary<string, MeshPeer> Peers = [];
        public void Send(string from, string to, PeerMessage message) { if (Peers.TryGetValue(to, out var p)) p.Receive(from, message); }
    }

    private WorkloadInventory Inventory(string id, long revision)
    {
        var hour = new DateTimeOffset(clock.Now.Year, clock.Now.Month, clock.Now.Day, clock.Now.Hour, 0, 0, TimeSpan.Zero);
        double demand = id == "o" ? 0 // the origin has no GPU work slot in the presentation lab
            : RenderHeatNeed.DemandKwh(needs.GetValueOrDefault(id), peers.TryGetValue(id, out var p) ? p.Attempts() : [], hour, 150);
        return new(revision, hour, hour.AddHours(1), "Synthetic render-need fixture",
            [new(id, "CH", new(demand, 0, 0, 1, FrameHeatKwh), 0)], [new("domain-" + id, new(4, 2048, 1), [])],
            [new("node-" + id, id, "domain-" + id, true, true, new(4, 2048, 1), [], [], [], [])], [])
            { ResourceObservedAtUtc = clock.Now, ResourceValidUntilUtc = clock.Now.AddSeconds(30) };
    }

    public RenderHeatNeedMeshTests()
    {
        long revision = 1;
        foreach (string id in Gpus.Prepend("o"))
        {
            var runtime = runtimes[id] = new();
            var peer = new MeshPeer("need", id, Path.Combine(root, id), Inventory(id, revision++), [CoordinatorTests.Node("node-" + id)],
                new Dictionary<string, ContainerProfile> { [CoordinatorTests.Profile.Id] = CoordinatorTests.Profile },
                Gpus.Prepend("o"), bus, runtime, clock, TimeSpan.FromSeconds(20));
            peers[id] = peer; bus.Peers[id] = peer;
        }
    }
    public void Dispose()
    {
        foreach (var p in peers.Values) p.Dispose();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    private long revision = 100;
    private void Refresh() { foreach (var (id, p) in peers) p.Update(Inventory(id, ++revision)); }

    /// <summary>Registers the weighted need (like RenderSession), submits the frames and runs until all finish.</summary>
    private Dictionary<string, int> Run(string runId, int frames = 48, bool registerNeed = true)
    {
        string prefix = RenderHeatNeed.RunPrefix("o", runId);
        if (registerNeed)
            foreach (var share in RenderHeatNeed.Allocate(frames, Weights)) needs[share.Key] = new(prefix, share.Value, FrameHeatKwh);
        Refresh();
        for (int f = 1; f <= frames; f++)
            peers["o"].Submit(PlacementTests.Request($"{runId}-{f:D3}", "batch") with { OriginSiteId = "o", Lifecycle = new(false, true, false) },
                CoordinatorTests.Profile.Id);
        for (int step = 0; step < 10 * frames; step++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            Refresh();
            peers["o"].Tick();
            // Every running frame finishes; the GPU peer releases it and publishes its new free heat.
            foreach (string g in Gpus)
            {
                foreach (var a in peers[g].Attempts().Where(a => a.Phase == "running"))
                    runtimes[g].States[a.Attempt.AttemptId] = new("exited", "abcdef123456", false) { ExitCode = 0 };
                peers[g].Tick();
            }
            if (peers["o"].Jobs().Where(j => j.Request.Id.StartsWith(prefix, StringComparison.Ordinal)).All(j => j.Status == "accepted")
                && Gpus.All(g => peers[g].Attempts().All(a => a.Phase == "released"))) break;
        }
        return Gpus.ToDictionary(g => g, g => peers[g].Attempts().Count(a => a.Attempt.Request.Id.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact] public void RunsSplitByNeedAndRepeatWithinTheSameHour()
    {
        for (int run = 1; run <= 9; run++) // 9 x 24 = 216 frames on g1, beyond the old 150 kWh / 0.8 = 187 hourly allowance
        {
            var split = Run("run" + run);
            Assert.Equal(new Dictionary<string, int> { ["g1"] = 24, ["g2"] = 16, ["g3"] = 8 }, split);
        }
        Assert.Equal(PlacementTests.Now.Hour, clock.Now.Hour); // all nine runs inside one clock hour
    }

    [Fact] public void WithoutRegisteredNeedTheHourlyAllowanceStillApplies()
    {
        // Fallback (flag off or no registration): configured 150 kWh per GPU, free-share rank spreads evenly.
        var split = Run("plain", registerNeed: false);
        Assert.Equal(48, split.Values.Sum());
        Assert.All(split.Values, n => Assert.InRange(n, 15, 17));
    }
}
