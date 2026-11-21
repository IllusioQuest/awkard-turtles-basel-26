using System.Net;
using System.Text;
using System.Text.Json;
using DigitalHeat.Gaming;
using DigitalHeat.Gaming.Monitor;
using Xunit;

public class DockerEngineObservationTests
{
    private static readonly DockerObservationOptions Policy = new(4, 1_048_576, 60, "1.41", "1.51");
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 17, 0, 0, TimeSpan.Zero);
    private static MonitorSetup Setup()
    {
        string root = MonitorSetup.FindRepositoryRoot(); return MonitorSetup.Load(root, Path.Combine(root, "gaming/deploy/heat-monitor.json"));
    }
    private static JsonDocument Stats(ulong own, ulong system, int seconds, bool v1 = false) => JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        read = Now.AddSeconds(seconds), cpu_stats = new { cpu_usage = new { total_usage = own }, system_cpu_usage = system, online_cpus = 4 },
        memory_stats = new { usage = 4096, limit = 8192, stats = v1 ? new Dictionary<string, int> { ["total_inactive_file"] = 1024 } : new() { ["inactive_file"] = 1024 } }
    }));
    [Fact] public void CpuRequiresConsecutiveValidCountersAndMemoryMatchesLinuxCacheAccounting()
    {
        using var first = Stats(100, 1000, 0); var one = DockerStats.Read(first.RootElement, null, Now);
        Assert.Null(one.CpuPercent); Assert.Null(one.CpuSampleIntervalSeconds); Assert.Equal("3KiB / 8KiB", one.MemoryUsage);
        using var second = Stats(300, 2000, 2, v1: true); var two = DockerStats.Read(second.RootElement, one.Next, Now);
        Assert.Equal("80.00%", two.CpuPercent); Assert.Equal(2, two.CpuSampleIntervalSeconds); Assert.Equal(one.MemoryUsage, two.MemoryUsage);
        using var idle = Stats(300, 3000, 4); Assert.Equal("0.00%", DockerStats.Read(idle.RootElement, two.Next, Now).CpuPercent);
        using var reset = Stats(1, 10, 6); Assert.Null(DockerStats.Read(reset.RootElement, two.Next, Now).CpuPercent);
        using var incomplete = JsonDocument.Parse("{\"cpu_stats\":{},\"memory_stats\":{\"usage\":\"invalid\"}}");
        var missing = DockerStats.Read(incomplete.RootElement, two.Next, Now); Assert.Null(missing.CpuPercent); Assert.Null(missing.MemoryUsage);
    }
    [Theory]
    [InlineData("npipe:////./pipe/dockerDesktopLinuxEngine", true)]
    [InlineData("npipe://remote/pipe/docker", false)]
    [InlineData("unix:///var/run/docker.sock", true)]
    [InlineData("unix://remote/var/run/docker.sock", false)]
    [InlineData("tcp://127.0.0.1:2375", false)]
    [InlineData("https://example.com", false)]
    [InlineData("ssh://example.com", false)]
    public void NativeTransportAcceptsOnlyExplicitLocalEndpoints(string endpoint, bool expected) => Assert.Equal(expected, DockerReadOnlyApi.IsLocalEndpoint(endpoint));

    private sealed class Commands(string endpoint = "npipe:////./pipe/test-docker") : IDockerCommandRunner
    {
        public int Resolutions;
        public List<string[]> Calls { get; } = [];
        public string Run(IReadOnlyList<string> args)
        {
            Calls.Add(args.ToArray());
            if (args[2] == "context") { Resolutions++; return JsonSerializer.Serialize(new { Host = endpoint, SkipTLSVerify = false }); }
            if (args[2] == "inspect") return "true false";
            if (args[2] == "top") return "COMMAND\n/opt/Xonotic/xonotic-linux64-sdl";
            if (args[2] == "stats") return "";
            return "aaaaaaaaaaaa";
        }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Time = Now;
        public override DateTimeOffset GetUtcNow() => Time;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Path)> Calls = [];
        public bool FailList, Redirect, Block;
        public string? LargeBody;
        public bool PauseA, FailStatsA;
        private readonly Dictionary<string, int> samples = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            lock (Calls) Calls.Add((request.Method, request.RequestUri!.PathAndQuery));
            if (Block) await Task.Delay(Timeout.Infinite, token);
            if (Redirect) return new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://untrusted.example/") } };
            if (LargeBody is not null) return Response(LargeBody);
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/version") return Response("{\"ApiVersion\":\"1.54\",\"MinAPIVersion\":\"1.44\"}");
            if (path.EndsWith("/containers/json"))
            {
                if (FailList) throw new HttpRequestException("SECRET_RAW_DIAGNOSTIC");
                var data = new[] { 'a', 'b', 'c' }.Select(c => new { Id = new string(c, 64), State = c == 'a' && PauseA ? "paused" : "running",
                    Labels = new Dictionary<string, string> { ["com.docker.compose.project"] = c == 'c' ? "digitalheat-dozen" : "digitalheat-gaming", ["com.docker.compose.service"] = "session-" + c } });
                return Response(JsonSerializer.Serialize(data));
            }
            if (path.EndsWith("/top")) return Response("{\"Titles\":[\"COMMAND\"],\"Processes\":[[\"SECRET_PROCESS_ARGUMENT /opt/Xonotic/xonotic-linux64-sdl\"]]}");
            string id = path.Split('/')[3]; int sample;
            if (FailStatsA && id[0] == 'a') return new(HttpStatusCode.ServiceUnavailable);
            lock (samples) { sample = samples.GetValueOrDefault(id) + 1; samples[id] = sample; }
            using var stats = Stats((ulong)sample * 100, (ulong)sample * 1000, sample * 2);
            return Response(stats.RootElement.GetRawText());
        }
        private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
    [Fact] public async Task NativeProbeReusesConnectionPreservesPausedAndUnknownCpuAndDoesNotLeakArguments()
    {
        var setup = Setup(); var commands = new Commands(); var handler = new Handler { PauseA = true }; var clock = new Clock(); int creations = 0;
        using var source = new DockerEngineObservationSource(setup, Policy, TimeSpan.FromSeconds(5), _ => commands,
            _ => { creations++; return new HttpClient(handler, disposeHandler: false); }, clock);
        var first = await source.ProbeAsync(); Assert.False(first["session-a"].ContainerRunning);
        Assert.Null(first["session-c"].CpuPercent); Assert.True(first["session-c"].ApplicationRunning);
        clock.Time = clock.Time.AddSeconds(6);
        var second = await source.ProbeAsync(); Assert.Equal("40.00%", second["session-c"].CpuPercent);
        Assert.Equal(1, creations); Assert.Equal(1, commands.Resolutions); Assert.Equal("local_engine_api", second["session-c"].ObservationSource);
        Assert.All(handler.Calls, c => Assert.Equal(HttpMethod.Get, c.Method));
        Assert.Contains(handler.Calls, c => c.Path.StartsWith("/v1.51/containers/", StringComparison.Ordinal));
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(second)); Assert.DoesNotContain(commands.Calls, c => c.Contains("exec"));
        Assert.Contains(handler.Calls, c => c.Path.EndsWith("/top?ps_args=-eo%20pid,args", StringComparison.Ordinal));
    }
    [Fact] public async Task FailedStatsDoNotDisposeConnectionDuringSiblingReads()
    {
        var commands = new Commands(); var handler = new Handler { FailStatsA = true };
        using var source = new DockerEngineObservationSource(Setup(), Policy, TimeSpan.FromSeconds(5), _ => commands,
            _ => new HttpClient(handler, disposeHandler: false));
        var first = await source.ProbeAsync();
        Assert.Null(first["session-a"].MemoryUsage);
        Assert.Equal("3KiB / 8KiB", first["session-b"].MemoryUsage);
        handler.FailStatsA = false;
        var second = await source.ProbeAsync();
        Assert.Equal(2, commands.Resolutions);
        Assert.Equal("3KiB / 8KiB", second["session-a"].MemoryUsage);
    }
    [Fact] public async Task PausingDropsOldCpuBaselineBeforeResume()
    {
        var commands = new Commands(); var handler = new Handler();
        using var source = new DockerEngineObservationSource(Setup(), Policy, TimeSpan.FromSeconds(5), _ => commands,
            _ => new HttpClient(handler, disposeHandler: false));
        await source.ProbeAsync(); handler.PauseA = true;
        await source.ProbeAsync(); handler.PauseA = false;
        var resumed = await source.ProbeAsync(); Assert.Null(resumed["session-a"].CpuPercent);
        Assert.Equal("40.00%", resumed["session-b"].CpuPercent);
    }
    [Fact] public async Task UnsupportedContextUsesOnlyScopedCliFallbackAndNoNativeConnection()
    {
        var commands = new Commands("ssh://trusted-operator-context");
        using var source = new DockerEngineObservationSource(Setup(), Policy, TimeSpan.FromSeconds(5), _ => commands,
            _ => throw new Exception("Native client must not handle remote transports"));
        var observed = await source.ProbeAsync(); Assert.Equal("docker_cli_fallback", observed["session-c"].ObservationSource);
        Assert.Contains(commands.Calls, c => c.Contains("top")); Assert.DoesNotContain(commands.Calls, c => c.Contains("exec"));
    }
    [Fact] public async Task FailureIsSanitizedAndNextRefreshResolvesEndpointAgain()
    {
        var commands = new Commands(); var broken = new Handler { FailList = true }; int count = 0;
        using var source = new DockerEngineObservationSource(Setup(), Policy, TimeSpan.FromSeconds(5), _ => commands,
            _ => new HttpClient(count++ == 0 ? broken : new Handler(), disposeHandler: false));
        var first = await source.ProbeAsync(); Assert.Contains("docker error:", first["session-c"].Detail);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(first));
        var second = await source.ProbeAsync(); Assert.True(second["session-c"].ContainerRunning); Assert.Equal(2, commands.Resolutions);
    }
    [Fact] public async Task FixedRoutesRejectBadIdsRedirectsAndOversizedBodies()
    {
        var handler = new Handler(); using var reads = new SemaphoreSlim(1);
        using var api = new DockerReadOnlyApi(new HttpClient(handler, false), Policy, reads, TimeSpan.FromSeconds(5));
        await api.Initialize(default);
        await Assert.ThrowsAsync<DockerReadException>(async () => await api.Stats("../untrusted", default));
        handler.Redirect = true; await Assert.ThrowsAsync<DockerReadException>(() => api.Containers(default));
        handler.Redirect = false; handler.LargeBody = new string('x', Policy.MaxResponseBytes + 1);
        await Assert.ThrowsAsync<DockerReadException>(() => api.Containers(default));
        Assert.DoesNotContain(handler.Calls, c => c.Path.Contains("untrusted"));
    }
    [Fact] public async Task CancellationPropagatesThroughPendingHttpRead()
    {
        var handler = new Handler { Block = true }; using var reads = new SemaphoreSlim(1);
        using var api = new DockerReadOnlyApi(new HttpClient(handler, false), Policy, reads, TimeSpan.FromSeconds(10));
        using var cancel = new CancellationTokenSource(); var pending = api.Initialize(cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
