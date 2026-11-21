using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.Orchestration;

internal static class ExampleConfiguration
{
    public static WorkloadConfiguration Create(string root, string localHour, bool unoccupiedSimulation)
    {
        var scenario = NetworkScenarioLoader.Load(Path.Combine(root, "config/network/switzerland-test.json"), root);
        var selected = scenario.NetworkSites.Where(s => s.Id is "basel-reference-house-case-1" or "basel-hotel-spa-1").ToArray();
        var sites = selected.Select(s => new WorkloadSite(s.Id, s.CountryCode,
            new(s.Demand.Single(h => h.LocalTime == localHour).TotalHeatKwh, 0, s.TankCapacityKwh, s.ServerCount, s.RecoverableKwhPerServerHour), 0)).ToArray();
        // Fixed offset is the committed 2025 dataset convention, not summer civil time; SOURCE.md.
        var hour = new DateTimeOffset(DateTime.ParseExact(localHour, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromHours(1)).ToUniversalTime();
        if (!unoccupiedSimulation) sites[1] = sites[1] with { ExternalCommittedHeatKwh = 0.8 }; // A-018 declared claim, not telemetry.
        const string source = "Illustrative operator quotas, not hardware measurements. Heat demand: committed Swiss 2025 scenario; building sizes synthetic. No physical heat-location claim.";
        var nodes = new[]
        {
            new ComputeNode("home-worker", sites[0].Id, "one-laptop", true, true, new(4, 6144, 2), ["linux-batch"], [], [], []),
            new ComputeNode("spa-worker", sites[1].Id, "one-laptop", true, true, new(6, 10240, 3), ["linux-batch"], [], [], []),
            new ComputeNode("protected-capacity", sites[1].Id, "one-laptop", false, true, new(2, 4096, 1), [], [], [], [])
        };
        var external = new List<ExternalAllocation> { new("existing-session-a", "home-worker", new(2, 3072, 1), null), new("existing-session-b", "spa-worker", new(2, 3072, 1), null) };
        if (!unoccupiedSimulation) external.Add(new("existing-session-c", "protected-capacity", new(2, 4096, 1), null));
        var inventory = new WorkloadInventory(1, hour, hour.AddHours(1), source + (unoccupiedSimulation ? " WHAT-IF: external reservation omitted; plan only." : " External reservation represented as protected."), sites,
            [new("one-laptop", new(8, 12288, 6), [])], nodes, external.ToArray())
            { ResourceObservedAtUtc = hour, ResourceValidUntilUtc = hour.AddHours(1) };
        var runtime = nodes.Select(n => new RuntimeNode(n.Id, "desktop-linux", "bridge", [], [])).ToArray();
        var batch = new WorkloadRequest("batch-demo", "batch", sites[0].Id, new("anywhere", "Owner-authorized local synthetic lab", null),
            new(0.25, 64, 1), source, 0.05, "Synthetic batch heat for admission testing, not measured", ["linux-batch"], null, null, null, [], new(true, true, false));
        return new("digitalheat-workloads", inventory, runtime,
            [new(batch, new("batch", "alpine:3.22", "65534", ["sh", "-c", "while :; do head -c 1048576 /dev/zero | sha256sum >/dev/null; done"], null, [], [], []))]);
    }
}
