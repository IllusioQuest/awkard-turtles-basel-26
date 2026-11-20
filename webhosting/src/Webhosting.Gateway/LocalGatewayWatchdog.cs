using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;

// Runs beside the gateway worker inside the same Linux container. It cannot detect a
// frozen container, because that freezes this process too. The peer must handle that case.
internal static class LocalGatewayWatchdog
{
    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("The container watchdog requires Linux.");
        var settings = configuration.GetSection("Gateway").Get<GatewaySettings>()!;
        var options = configuration.GetSection("Gateway:Watchdog").Get<WatchdogOptions>()!;
        if (options.FailureThreshold < 1 || options.IntervalSeconds <= 0 || options.TimeoutSeconds <= 0 ||
            !double.IsFinite(options.IntervalSeconds) || !double.IsFinite(options.TimeoutSeconds))
            throw new InvalidOperationException("Watchdog intervals and failure threshold must be positive.");

        using var shutdown = new CancellationTokenSource();
        using var termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM,
            context => { context.Cancel = true; shutdown.Cancel(); });
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
        using var worker = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, UseShellExecute = false }
        };
        worker.StartInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        worker.StartInfo.ArgumentList.Add("--gateway-worker");
        // Environment and appsettings are inherited; the container has no command-line overrides.
        var active = 0;
        worker.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            Console.WriteLine(e.Data);
            if (e.Data == $"GATEWAY_ACTIVE {settings.InstanceId}") Interlocked.Exchange(ref active, 1);
        };
        worker.Start();
        worker.BeginOutputReadLine();
        Console.WriteLine($"GATEWAY_WORKER_PID {worker.Id}");
        using var handler = new HttpClientHandler { UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };
        var failures = 0;
        try
        {
            while (!worker.HasExited && !shutdown.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), shutdown.Token);
                // Standby has no listener of its own. Probing the shared URL there would probe its peer.
                if (Volatile.Read(ref active) == 0) continue;
                try
                {
                    var reply = await client.GetFromJsonAsync<GatewayIdentity>(
                        $"http://{settings.ListenAddress}:{settings.Port}/_platform/gateway", shutdown.Token);
                    if (reply?.GatewayId != settings.InstanceId) throw new HttpRequestException("Wrong gateway identity.");
                    failures = 0;
                }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
                catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
                {
                    if (++failures < options.FailureThreshold) continue;
                    Console.WriteLine($"GATEWAY_WATCHDOG_FAILED {settings.InstanceId}: {failures} failed probes; terminating worker.");
                    worker.Kill(entireProcessTree: true);
                    await worker.WaitForExitAsync();
                    return 1; // Let the already-running peer take over; do not race it by restarting locally.
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        finally
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync();
        }
        return shutdown.IsCancellationRequested ? 0 : worker.ExitCode;
    }

    private sealed record GatewayIdentity(string GatewayId);
    private sealed record WatchdogOptions(double IntervalSeconds, double TimeoutSeconds, int FailureThreshold);
}
