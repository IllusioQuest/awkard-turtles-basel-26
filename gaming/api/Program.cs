using System.Net;
using System.Text.Json;
using DigitalHeat.Gaming.Monitor;

var builder = WebApplication.CreateBuilder(args);
string root = Environment.GetEnvironmentVariable("DIGITALHEAT_ROOT") ?? MonitorSetup.FindRepositoryRoot();
var setup = MonitorSetup.Load(root, Path.Combine(root, "gaming/deploy/heat-monitor.json"));
var policy = JsonSerializer.Deserialize<ServicePolicy>(File.ReadAllText(Path.Combine(root, "gaming/deploy/monitor-api.json")), MonitorInputs.JsonOptions)
    ?? throw new InvalidDataException("Missing API policy");
policy.Validate();
builder.WebHost.ConfigureKestrel(k =>
{
    k.Listen(IPAddress.Loopback, policy.Port);
    k.Limits.MaxRequestBodySize = policy.MaxRequestBytes;
});
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
var app = builder.Build();
using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("DigitalHeat-monitor/1.0");
using var gate = new SemaphoreSlim(1, 1);
IReadOnlyDictionary<string, DockerNodeState> observations = new Dictionary<string, DockerNodeState>();
DateTimeOffset? observedAt = null;
app.Use(async (context, next) =>
{
    var host = context.Request.Host;
    bool loopbackHost = host.Host is "localhost" or "127.0.0.1" && host.Port == policy.Port;
    string origin = context.Request.Headers.Origin.ToString();
    bool trustedOrigin = string.IsNullOrEmpty(origin) || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme == "http" && uri.Host is "localhost" or "127.0.0.1"
        && (uri.Port == policy.Port || uri.Port == policy.FrontendPort) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment));
    if (!loopbackHost || !trustedOrigin)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.MapGet("/api/monitor/config", () => Results.Ok(new
{
    schema_version = MonitorInputs.CurrentSchemaVersion, default_inputs = MonitorInputs.Default(setup.Config),
    available_hours = setup.Axis, winter_hour_local = setup.Config.WinterJumpLocal,
    summer_hour_local = setup.Config.SummerJumpLocal, max_reservations = MonitorInputs.MaximumReservations,
    observation_cache_seconds = policy.ObservationCacheSeconds
}));
app.MapPost("/api/monitor/snapshot", async (HttpContext context) =>
{
    if (!context.Request.HasJsonContentType()) return Results.BadRequest(new { error = "Content-Type must be application/json." });
    MonitorInputs? inputs;
    try
    {
        inputs = await context.Request.ReadFromJsonAsync<MonitorInputs>(MonitorInputs.JsonOptions, context.RequestAborted);
        if (inputs is null) throw new ArgumentException("Missing inputs.");
        inputs.Validate(setup.Axis);
    }
    catch (Exception e) when (e is JsonException or ArgumentException or BadHttpRequestException)
    { return Results.BadRequest(new { error = "Invalid monitoring inputs: " + e.Message }); }
    if (!await gate.WaitAsync(0, context.RequestAborted))
        return Results.Json(new { error = "A snapshot is already being prepared. Retry shortly." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    deadline.CancelAfter(TimeSpan.FromSeconds(policy.SnapshotTimeoutSeconds));
    try
    {
        var warnings = new List<string>();
        HourInputs hour;
        if (inputs.Mode == "live")
        {
            var live = await new LiveSources(setup, http).FetchAsync(DateTimeOffset.UtcNow, deadline.Token);
            hour = live.Hour;
            warnings.AddRange(live.Warnings);
        }
        else hour = setup.ReplayHour(setup.IndexOf(inputs.SelectedHourLocal!));
        if (inputs.ObserveDocker && (observedAt is null || DateTimeOffset.UtcNow - observedAt.Value >= TimeSpan.FromSeconds(policy.ObservationCacheSeconds)))
        {
            observations = await Task.Run(() => new DockerProbe(setup, new ReadOnlyDockerCommands(policy, deadline.Token)).Probe(), deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            observedAt = DateTimeOffset.UtcNow;
        }
        return Results.Ok(MonitorSnapshotBuilder.Build(setup, inputs, hour, observations, observedAt, DateTimeOffset.UtcNow, warnings));
    }
    catch (OperationCanceledException) { return Results.Json(new { error = "Snapshot deadline exceeded or request cancelled; no model result substituted." }, statusCode: 504); }
    catch (Exception e)
    {
        app.Logger.LogWarning(e, "Monitor snapshot failed");
        return Results.Json(new { error = "Snapshot unavailable. Check the local API log; no simulated observations substituted." }, statusCode: 503);
    }
    finally { gate.Release(); }
});
app.Run();

public sealed record ServicePolicy(int Port, int FrontendPort, int ObservationCacheSeconds,
    int SnapshotTimeoutSeconds, int DockerCommandTimeoutSeconds, long MaxRequestBytes, string Source)
{
    public void Validate()
    {
        if (Port is < 1024 or > 65535 || FrontendPort is < 1024 or > 65535 || ObservationCacheSeconds <= 0 ||
            SnapshotTimeoutSeconds <= 0 || DockerCommandTimeoutSeconds <= 0 || MaxRequestBytes <= 0 || string.IsNullOrWhiteSpace(Source))
            throw new InvalidDataException("Invalid local API policy");
    }
}
