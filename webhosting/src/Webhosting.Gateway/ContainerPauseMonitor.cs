using System.Net.Sockets;
using System.Text.Json;

// Trusted local lab only: the mounted outer Engine socket grants broad host authority.
internal static class ContainerPauseMonitor
{
    public static async Task<int> RunAsync(IConfiguration config)
    {
        var project = config["Monitor:Project"] ?? throw new InvalidOperationException("Monitor project required");
        var services = config.GetSection("Monitor:Services").Get<string[]>() ?? [];
        var threshold = config.GetValue<int>("Monitor:FailureThreshold");
        var interval = config.GetValue<double>("Monitor:IntervalSeconds");
        var timeout = config.GetValue<double>("Monitor:TimeoutSeconds");
        if (services.Length == 0 || threshold < 1 || !double.IsFinite(interval) || interval <= 0 ||
            !double.IsFinite(timeout) || timeout <= 0) throw new InvalidOperationException("Invalid monitor settings");
        using var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try { await socket.ConnectAsync(new UnixDomainSocketEndPoint("/var/run/docker.sock"), token); }
                catch { socket.Dispose(); throw; }
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        using var engine = new HttpClient(handler) { BaseAddress = new Uri("http://docker/"), Timeout = TimeSpan.FromSeconds(timeout) };
        using var probe = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(timeout) };
        var url = $"http://{config["Gateway:ListenAddress"]}:{config["Gateway:Port"]}/_platform/gateway";
        var failures = 0;
        string? lastActive = null;
        async Task<bool> Healthy()
        {
            try
            {
                using var response = await probe.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var identity = json.RootElement.GetProperty("gatewayId").GetString();
                if (!services.Contains(identity)) return false;
                lastActive = identity;
                failures = 0;
                return true;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException) { return false; }
        }
        bool Allowed(JsonElement container)
        {
            var labels = container.GetProperty("Config").GetProperty("Labels");
            return labels.TryGetProperty("com.docker.compose.project", out var p) && p.GetString() == project &&
                labels.TryGetProperty("com.docker.compose.service", out var s) && services.Contains(s.GetString()) &&
                (lastActive is null || lastActive == s.GetString()) &&
                container.GetProperty("State").GetProperty("Paused").GetBoolean();
        }
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval));
            try
            {
                if (await Healthy() || ++failures < threshold) continue;
                using var list = JsonDocument.Parse(await engine.GetStringAsync("containers/json"));
                foreach (var item in list.RootElement.EnumerateArray())
                {
                    var id = item.GetProperty("Id").GetString();
                    if (id is null || id.Length != 64 || !id.All(Uri.IsHexDigit)) continue;
                    using var inspected = JsonDocument.Parse(await engine.GetStringAsync($"containers/{id}/json"));
                    if (!Allowed(inspected.RootElement) || await Healthy()) continue;
                    using var rechecked = JsonDocument.Parse(await engine.GetStringAsync($"containers/{id}/json"));
                    if (!Allowed(rechecked.RootElement)) continue;
                    using var stopped = await engine.PostAsync($"containers/{id}/stop?t=2", null);
                    stopped.EnsureSuccessStatusCode();
                    Console.WriteLine($"MONITOR_STOPPED_PAUSED {id}");
                    failures = 0;
                    break;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            { Console.WriteLine($"MONITOR_RETRY {e.GetType().Name}: {e.Message}"); }
        }
    }
}
