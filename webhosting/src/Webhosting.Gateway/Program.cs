using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Model;
using Webhosting.Gateway;

var bootstrap = WebApplication.CreateBuilder(args);
if (bootstrap.Configuration.GetValue<bool>("Monitor:Enabled"))
    return await ContainerPauseMonitor.RunAsync(bootstrap.Configuration);
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
    bool lifecycleEnabled = builder.Configuration.GetValue<bool>("Hosting:Enabled");
    var plans = new HostingPlanReader(builder.Configuration["Hosting:PlanPath"] ?? "/hosting-state/plan.json");
    var admission = new HostingAdmission();
    app.MapGet("/_platform/gateway", () => new { gatewayId = settings.InstanceId, state = "active", runtime = RuntimeInformation.OSDescription });
    app.MapGet("/_platform/health", () => Results.Ok());
    if (lifecycleEnabled)
    {
        app.MapGet("/_platform/hosting", () => admission.Snapshot(plans.Read()));
        app.MapReverseProxy(pipeline =>
        {
            pipeline.Use(async (context, next) =>
            {
                var feature = context.GetReverseProxyFeature();
                var plan = plans.Read();
                if (plan is null) { context.Response.StatusCode = 503; return; }
                // Mandatory before selection, even with one destination. No affinity/retry bypass.
                var available = feature.AvailableDestinations.Where(d => plan.Nodes.Any(n =>
                    n.Id == d.DestinationId && n.Component == feature.Cluster.Config.ClusterId
                    && string.Equals(n.Address, d.Model.Config.Address, StringComparison.Ordinal)))
                    .ToDictionary(d => d.DestinationId, StringComparer.Ordinal);
                using var lease = admission.Admit(plan, feature.Cluster.Config.ClusterId,
                    available.Keys.ToHashSet(StringComparer.Ordinal), DateTimeOffset.UtcNow, out var selected);
                if (selected is null) { context.Response.StatusCode = 503; return; }
                feature.AvailableDestinations = available[selected.Id];
                await next();
            });
            pipeline.UsePassiveHealthChecks();
        });
    }
    else app.MapReverseProxy();
    using var lifecycleStop = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
    DockerHostingControl? control = null;
    Task? reconciliation = null;
    try
    {
        await app.StartAsync(shutdown.Token);
        Console.WriteLine($"GATEWAY_ACTIVE {settings.InstanceId}");
        if (lifecycleEnabled)
        {
            double interval = builder.Configuration.GetValue<double>("Hosting:IntervalSeconds");
            double timeout = builder.Configuration.GetValue<double>("Hosting:ControlTimeoutSeconds");
            double drain = builder.Configuration.GetValue<double>("Hosting:DrainTimeoutSeconds");
            if (!new[] { interval, timeout, drain }.All(x => double.IsFinite(x) && x > 0))
                throw new InvalidOperationException("Positive hosting timing configuration required");
            control = new DockerHostingControl(builder.Configuration["Hosting:Project"]
                ?? throw new InvalidOperationException("Hosting project required"), TimeSpan.FromSeconds(timeout));
            reconciliation = new HostingLifecycle(plans, admission, control, TimeSpan.FromSeconds(interval),
                TimeSpan.FromSeconds(drain)).RunAsync(lifecycleStop.Token);
        }
        await app.WaitForShutdownAsync(shutdown.Token);
        break;
    }
    catch (Exception error) when (HasAddressConflict(error) && !shutdown.IsCancellationRequested)
    {
        Console.WriteLine($"GATEWAY_STANDBY {settings.InstanceId}: port owned; retry in {settings.BindRetrySeconds} seconds.");
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
    // Release server/service resources before retrying, including after a failed bind.
    finally
    {
        lifecycleStop.Cancel();
        if (reconciliation is not null) await reconciliation;
        control?.Dispose();
        await app.DisposeAsync();
    }
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
