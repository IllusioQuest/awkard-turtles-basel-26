using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Transforms;

var bootstrap = WebApplication.CreateBuilder(args);
if (!args.Contains("--gateway-worker") && bootstrap.Configuration.GetValue<bool>("Gateway:Watchdog:Enabled"))
    return await LocalGatewayWatchdog.RunAsync(bootstrap.Configuration);

// The Linux host-network demo elects an ingress owner by exclusive TCP bind.
// This covers process/container crashes, not hung processes retaining their socket.
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
using var terminate = OperatingSystem.IsLinux()
    ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); })
    : null;

while (!shutdown.IsCancellationRequested)
{
    var builder = WebApplication.CreateBuilder(args);
    var settings = builder.Configuration.GetSection("Gateway").Get<GatewaySettings>()
        ?? throw new InvalidOperationException("Gateway configuration is required.");
    if (settings.Port is < 1 or > 65535 || settings.BindRetrySeconds <= 0 ||
        !double.IsFinite(settings.BindRetrySeconds) || string.IsNullOrWhiteSpace(settings.InstanceId))
        throw new InvalidOperationException("Gateway identity, port and retry interval must be valid.");
    builder.WebHost.UseUrls($"http://{settings.ListenAddress}:{settings.Port}");
    builder.Services.Configure<TransportFailureRateHealthPolicyOptions>(
        builder.Configuration.GetSection("PassiveHealthPolicy"));
    builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms(transforms => transforms.AddResponseHeader("X-Webhosting-Gateway", settings.InstanceId, append: false));
    var app = builder.Build();
    app.MapGet("/_platform/gateway", () => new { gatewayId = settings.InstanceId, state = "active", runtime = RuntimeInformation.OSDescription });
    app.MapGet("/_platform/health", () => Results.Ok());
    app.MapReverseProxy();
    try
    {
        await app.StartAsync(shutdown.Token);
        Console.WriteLine($"GATEWAY_ACTIVE {settings.InstanceId}");
        await app.WaitForShutdownAsync(shutdown.Token);
        break;
    }
    catch (Exception error) when (HasAddressConflict(error) && !shutdown.IsCancellationRequested)
    {
        Console.WriteLine($"GATEWAY_STANDBY {settings.InstanceId}: port owned; retry in {settings.BindRetrySeconds} seconds.");
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
    // Release server/service resources before retrying, including after a failed bind.
    finally { await app.DisposeAsync(); }
    try { await Task.Delay(TimeSpan.FromSeconds(settings.BindRetrySeconds), shutdown.Token); }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
}
return 0;

static bool HasAddressConflict(Exception error) => error is AddressInUseException ||
    (error.InnerException is not null && HasAddressConflict(error.InnerException));

internal sealed record GatewaySettings
{
    public string InstanceId { get; init; } = "";
    public string ListenAddress { get; init; } = "";
    public int Port { get; init; }
    public double BindRetrySeconds { get; init; }
}
