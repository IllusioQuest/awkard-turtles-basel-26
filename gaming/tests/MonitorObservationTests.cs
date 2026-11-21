using System.Net;
using System.Text;
using System.Text.Json;
using DigitalHeat.Gaming;
using DigitalHeat.Gaming.Monitor;
using Xunit;

public class MonitorObservationTests
{
    private static MonitorSetup Setup()
    {
        var root = MonitorSetup.FindRepositoryRoot();
        return MonitorSetup.Load(root, Path.Combine(root, "gaming/deploy/heat-monitor.json"));
    }

    private sealed class Commands : IDockerCommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public string Run(IReadOnlyList<string> arguments)
        {
            Calls.Add(arguments.ToArray());
            if (arguments.Contains("stats"))
                return "{\"ID\":\"bbbbbbbbbbbb\",\"CPUPerc\":\"12.5%\",\"MemUsage\":\"512MiB / 4GiB\"}\n" +
                    "{\"ID\":\"cccccccccccc\",\"CPUPerc\":\"25.0%\",\"MemUsage\":\"1GiB / 4GiB\"}";
            if (arguments.Contains("inspect")) return arguments[^1] == "aaaaaaaaaaaa" ? "true true" : "true false";
            if (arguments.Contains("exec")) return "403";
            return arguments[^1].EndsWith("session-a") ? "aaaaaaaaaaaa" : arguments[^1].EndsWith("session-b") ? "bbbbbbbbbbbb" : "cccccccccccc";
        }
    }

    [Fact]
    public void PausedContainerIsNotAdmittedAndStatsAreBatchedPerEngine()
    {
        var commands = new Commands();
        var observations = new DockerProbe(Setup(), commands).Probe();
        Assert.False(observations["session-a"].ContainerRunning);
        Assert.Null(observations["session-a"].CpuPercent);
        Assert.Equal("25.0%", observations["session-c"].CpuPercent);
        Assert.Equal("1GiB / 4GiB", observations["session-c"].MemoryUsage);
        Assert.Single(commands.Calls, c => c.Contains("stats"));
        Assert.DoesNotContain(commands.Calls, c => c.Contains("exec") && c.Contains("aaaaaaaaaaaa"));
    }

    private sealed class SourceHandler(bool fail = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            string json = request.Method == HttpMethod.Post
                ? "{\"data\":{\"observations\":[{\"operator\":\"1\",\"operatorLabel\":\"Fixture\",\"municipalityLabel\":\"Basel\",\"total\":33.25}]}}"
                : JsonSerializer.Serialize(new { hourly = new { time = Enumerable.Range(0, 24).Select(h => $"2026-01-15T{h:00}:00"), temperature_2m = Enumerable.Repeat(0.0, 24) } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task CurrentWeatherKeepsHistoricalBuildingDemandAndLabelsAnnualTariffProxy()
    {
        var setup = Setup();
        using var http = new HttpClient(new SourceHandler());
        var result = await new LiveSources(setup, http).FetchAsync(new(2026, 1, 15, 17, 30, 0, TimeSpan.Zero));
        Assert.Empty(result.Warnings);
        Assert.Equal("2026-01-15T18:00", result.Hour.LocalLabel);
        var home = result.Hour.Sites["basel-reference-house-case-1"];
        Assert.Equal(0.0, home.TemperatureC);
        Assert.Contains("case formula", home.DemandSource);
        Assert.Equal(0.3325, home.ElectricityPriceChfPerKwh, 9);
        Assert.Contains("annual tariff", home.PriceSource);
        var hotel = result.Hour.Sites["basel-hotel-spa-1"];
        var replay = setup.ReplayHour(setup.IndexOf("2025-01-15T18:00"));
        Assert.Equal(replay.Sites["basel-hotel-spa-1"].Demand, hotel.Demand);
        Assert.Contains("not current demand", hotel.DemandSource);
        Assert.Contains("H4 proxy", hotel.PriceSource);
    }

    [Fact]
    public async Task OfflineSourcesPreserveReplayNumbersAndExposeFallback()
    {
        var setup = Setup();
        using var http = new HttpClient(new SourceHandler(fail: true));
        var result = await new LiveSources(setup, http).FetchAsync(new(2026, 1, 15, 17, 30, 0, TimeSpan.Zero));
        Assert.NotEmpty(result.Warnings);
        var replay = setup.ReplayHour(setup.IndexOf("2025-01-15T18:00"));
        foreach (var (id, site) in result.Hour.Sites)
        {
            Assert.Equal(replay.Sites[id].Demand, site.Demand);
            Assert.Equal(replay.Sites[id].ElectricityPriceChfPerKwh, site.ElectricityPriceChfPerKwh);
            Assert.Contains("fallback", site.PriceSource);
        }
    }
}
