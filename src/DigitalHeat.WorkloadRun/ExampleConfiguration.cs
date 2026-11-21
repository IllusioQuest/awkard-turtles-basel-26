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
            new ComputeNode("spa-worker", sites[1].Id, "one-laptop", true, true, new(6, 10240, 3), ["linux-batch", "xonotic"], ["intel-dxg"], ["client-home"], []),
            new ComputeNode("protected-game", sites[1].Id, "one-laptop", false, true, new(2, 4096, 1), [], ["intel-dxg"], [], [])
        };
        var external = new List<ExternalAllocation> { new("existing-session-a", "home-worker", new(2, 3072, 1), null), new("existing-session-b", "spa-worker", new(2, 3072, 1), null) };
        if (!unoccupiedSimulation) external.Add(new("existing-session-c", "protected-game", new(2, 4096, 1), "intel-dxg"));
        var inventory = new WorkloadInventory(1, hour, hour.AddHours(1), source + (unoccupiedSimulation ? " WHAT-IF: current game omitted; plan only." : " Current game represented as external/protected."), sites,
            [new("one-laptop", new(8, 12288, 6), [new("intel-dxg", "opengl")])], nodes, external.ToArray())
            { ResourceObservedAtUtc = hour, ResourceValidUntilUtc = hour.AddHours(1) };
        var gpu = new GpuBinding("intel-dxg", null, ["/dev/dxg"], ["/usr/lib/wsl/lib:/usr/lib/wsl/lib", "/usr/lib/wsl/drivers:/usr/lib/wsl/drivers"]);
        var runtime = nodes.Select(n => new RuntimeNode(n.Id, "desktop-linux", "bridge", n.GpuDeviceIds.Length == 0 ? [] : [gpu],
            n.StorageIds.Length == 0 ? [] : [new("client-home", "digitalheat-workload-xonotic-home", "/home/ubuntu")])).ToArray();
        var batch = new WorkloadRequest("batch-demo", "batch", sites[0].Id, new("anywhere", "Owner-authorized local synthetic lab", null),
            new(0.25, 64, 1), source, 0.05, "Synthetic batch heat for admission testing, not measured", ["linux-batch"], null, null, null, [], new(true, true, false));
        var game = new WorkloadRequest("xonotic-demo", "interactive", sites[0].Id, new("anywhere", "Owner-authorized local synthetic lab", null),
            new(2, 4096, 1), "Existing Node C Compose quotas, declared budget not measured use", 0.8,
            "A-018: one case 1 kW server x 80%; not measured Xonotic heat", ["xonotic"], "opengl", "spa-worker", "client-home", [], new(false, true, false));
        var gameStartup = "cd /opt/Xonotic || exit 1; export DISPLAY=:20 XDG_RUNTIME_DIR=/tmp/runtime-ubuntu PULSE_SERVER=unix:/tmp/runtime-ubuntu/pulse/native; " +
            "i=0; until test -S /tmp/.X11-unix/X20; do i=$((i+1)); test $i -lt 60 || exit 1; sleep 1; done; " +
            "exec flock -n /tmp/digitalheat-desktop.lock flock -n /tmp/digitalheat-xonotic.lock /opt/Xonotic/xonotic-linux64-sdl -window +vid_width 1280 +vid_height 720 +vid_fullscreen 0 +cl_maxfps 30 >> /home/ubuntu/xonotic-workload.log 2>&1";
        var gameProbe = "for p in /proc/[0-9]*/exe; do test \"$(readlink \"$p\")\" = /opt/Xonotic/xonotic-linux64-sdl && exit 0; done; exit 1";
        return new("digitalheat-workloads", inventory, runtime,
            [new(batch, new("batch", "alpine:3.22", "65534", ["sh", "-c", "while :; do head -c 1048576 /dev/zero | sha256sum >/dev/null; done"], null, [], [], [])),
             new(game, new("xonotic", "digitalheat-xonotic:experiment", "ubuntu", [], Path.GetFullPath(Path.Combine(root, "gaming/TestResults/workload-xonotic.env")),
                 ["127.0.0.1:18884:8080"], ["sh", "-c", gameStartup], ["sh", "-c", gameProbe]) { ShmMib = 1024 })]);
    }
}
