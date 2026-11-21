using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.JobRuntime;
using DigitalHeat.Network;

// Live demo (decision 0042, ticket T-54): the network scheduler moves real Ising tasks between Docker nodes.
// One line from the repository root (starts the containers, opens the browser, stops them on Ctrl+C):
//   dotnet run --project src/DigitalHeat.LiveGrid -- --up
// Options: --config config/live-demo-four-node.json  --port 5087  --no-browser
var root = FindRoot();
bool up = args.Contains("--up");
string configPath = Path.Combine(root, Arg("--config") ?? Path.Combine("config", "live-demo-four-node.json"));
if (up)
{
    // Containers with the demo's names left over from another checkout would block `compose up`.
    RemoveStale(JsonDocument.Parse(File.ReadAllText(configPath)).RootElement.GetProperty("nodes")
        .EnumerateArray().Select(n => n.GetProperty("container").GetString()!).ToArray());
    Compose("up", "-d", "--build");
}
int port = int.Parse(Arg("--port") ?? "5087");
if (Arg("--replay") is { } replayArgument)
{
    string replayPath = Path.GetFullPath(Path.IsPathRooted(replayArgument)
        ? replayArgument : Path.Combine(root, replayArgument));
    JsonElement[] frames = File.ReadLines(replayPath)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    if (frames.Length == 0) throw new InvalidDataException($"Replay file has no frames: {replayPath}");

    long frameIndex = 0;
    var replayBuilder = WebApplication.CreateBuilder(args);
    replayBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
    replayBuilder.WebHost.UseWebRoot(Path.Combine(root, "src", "DigitalHeat.LiveGrid", "wwwroot"));
    replayBuilder.WebHost.UseUrls($"http://127.0.0.1:{port}");
    var replayApp = replayBuilder.Build();
    replayApp.UseDefaultFiles();
    replayApp.UseStaticFiles();
    replayApp.MapGet("/api/state", () =>
    {
        long next = Interlocked.Increment(ref frameIndex) - 1;
        return Results.Json(frames[(int)(next % frames.Length)]);
    });
    // Keep the unchanged page usable during playback; controls are intentionally inert.
    replayApp.MapPost("/api/{**path}", () => Results.NoContent());
    Console.WriteLine($"Replaying {frames.Length} frames from {replayPath} on http://127.0.0.1:{port}");
    await replayApp.RunAsync();
    return;
}
var reference = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
var config = JsonSerializer.Deserialize<DemoConfig>(File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Empty live demo config.");
config.Validate();
if (config.ReferenceServerElectricKw.Value != reference.ServerElectricKw.Value
    || config.HeatRecoveryShare.Value != reference.HeatRecoveryShare.Value)
    throw new InvalidDataException("The live demo's reference values must match config/reference-house.json.");

var cal = new LiveCalibration(config.ReferenceServerElectricKw.Value, config.HeatRecoveryShare.Value,
    config.ServerHoursPerTask.Value, (int)config.ForecastHorizonSteps.Value,
    reference.ElectricityPriceChfPerKwh.Value, reference.ElectricityPriceChfPerKwh.Value / reference.HeatPumpEfficiency.Value,
    (int)config.ServersPerNode.Value);
var docker = new CachedDockerControl(new DockerCliContainerControl());
double cores = config.AllowedCpuCoresPerContainer.Value;
var executors = config.Nodes.Select(n => (IExecutor)new DockerJobExecutor(docker, n.Container, cores, supportsThrottle: true)).ToArray();
string work = Path.Combine(root, "data", "external", "research-sim", "live-demo-work");
var coordinator = new LiveCoordinator(config, cal, work, executors)
{
    ActualLoad = container => docker.Load(container, cores),
};
string[] containers = config.Nodes.Select(n => n.Container).ToArray();
// Start paused (no work yet), so a worker cannot claim a task before the first scheduler decision.
foreach (var e in executors) e.ApplyTargetLoad(0);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseWebRoot(Path.Combine(root, "src", "DigitalHeat.LiveGrid", "wwwroot"));
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/state", (long? since) => Results.Json(coordinator.Read(since ?? 0)));
app.MapPost("/api/nodes/{id}/target", (string id, TargetRequest request) =>
{
    if (!double.IsFinite(request.TemperatureC)
        || request.TemperatureC < config.TargetTemperatureMinC.Value
        || request.TemperatureC > config.TargetTemperatureMaxC.Value)
        return Results.BadRequest(new { error = "Target temperature is outside the demo slider range." });
    return coordinator.SetTarget(id, request.TemperatureC) ? Results.NoContent() : Results.NotFound();
});
app.MapPost("/api/nodes/{id}/spawn", (string id) => coordinator.Spawn(id) is { } job ? Results.Json(job) : Results.NotFound());
app.MapPost("/api/autospawn", (ToggleRequest r) => { coordinator.SetAutoSpawn(r.On); return Results.NoContent(); });
app.MapPost("/api/scheduler", (SchedulerRequest r) =>
{
    try { coordinator.SetScheduler(r.Id); return Results.NoContent(); }
    catch (InvalidDataException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/reset", () => { coordinator.Reset(); return Results.NoContent(); });

using var stop = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() => stop.Cancel());
_ = Task.Run(async () =>   // the scheduler clock
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.StepSeconds.Value));
    while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
    {
        try { coordinator.Step(); }
        catch (Exception e) { Console.Error.WriteLine($"step failed: {e.Message}"); }
    }
});
_ = Task.Run(async () =>   // batched CPU observation, never on the request path
{
    while (!stop.IsCancellationRequested)
    {
        try { docker.Observe(containers); } catch (Exception) { /* shown as missing reading */ }
        try { await Task.Delay(TimeSpan.FromSeconds(1), stop.Token); } catch (OperationCanceledException) { break; }
    }
});
Console.WriteLine($"Live demo on http://127.0.0.1:{port}  ({config.Nodes.Length} nodes, scheduler {config.Scheduler}, "
    + $"1 step = {config.StepSeconds.Value} s)");
if (up)
{
    app.Lifetime.ApplicationStopping.Register(() => Compose("stop", "-t", "1"));
    if (!args.Contains("--no-browser")) app.Lifetime.ApplicationStarted.Register(() => OpenBrowser($"http://127.0.0.1:{port}"));
}
app.Run();

void Compose(params string[] verb)
{
    var psi = new System.Diagnostics.ProcessStartInfo("docker") { WorkingDirectory = root };
    foreach (var a in new[] { "compose", "-f", "compose.live-demo.yml" }.Concat(verb)) psi.ArgumentList.Add(a);
    using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("docker not found");
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"docker compose {string.Join(' ', verb)} failed (is Docker running?)");
}

void RemoveStale(string[] names)
{
    var psi = new System.Diagnostics.ProcessStartInfo("docker") { RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var a in new[] { "rm", "-f" }.Concat(names)) psi.ArgumentList.Add(a);
    using var p = System.Diagnostics.Process.Start(psi);
    p?.WaitForExit();   // "No such container" is fine
}

static void OpenBrowser(string url)
{
    try
    {
        if (OperatingSystem.IsLinux()) System.Diagnostics.Process.Start("xdg-open", url);
        else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception) { Console.WriteLine($"Open {url} in a browser."); }
}

string? Arg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string FindRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Start LiveGrid from the repository root.");
}

sealed record TargetRequest(double TemperatureC);
sealed record ToggleRequest(bool On);
sealed record SchedulerRequest(string Id);
sealed record DemoNodeConfig(string Id, string Label, string Container, int Position);
sealed record DemoConfig(Parameter MinimumLoadShare, Parameter AllowedCpuCoresPerContainer,
    Parameter ReferenceServerElectricKw, Parameter HeatRecoveryShare, Parameter TargetTemperatureMinC,
    Parameter TargetTemperatureMaxC, Parameter StepSeconds, Parameter ServerHoursPerTask, Parameter TaskLatticeSize,
    Parameter TaskSweeps, Parameter TasksPerJob, Parameter DeadlineSteps, Parameter ForecastHorizonSteps,
    Parameter AutoSpawnEverySteps, Parameter Seed, Parameter ServersPerNode, string Scheduler, DemoNodeConfig[] Nodes)
{
    public void Validate()
    {
        foreach (var p in new[] { MinimumLoadShare, AllowedCpuCoresPerContainer, ReferenceServerElectricKw,
                     HeatRecoveryShare, TargetTemperatureMinC, TargetTemperatureMaxC, StepSeconds, ServerHoursPerTask,
                     TaskLatticeSize, TaskSweeps, TasksPerJob, DeadlineSteps, ForecastHorizonSteps, AutoSpawnEverySteps, Seed, ServersPerNode })
            if (p is null || !double.IsFinite(p.Value) || string.IsNullOrWhiteSpace(p.Unit) || string.IsNullOrWhiteSpace(p.Source))
                throw new InvalidDataException("Every demo parameter needs a finite value, unit and source.");
        if (Nodes.Length is < 2 or > 16 || Nodes.Select(n => n.Id).Distinct().Count() != Nodes.Length
            || Nodes.Select(n => n.Container).Distinct().Count() != Nodes.Length
            || MinimumLoadShare.Value != 0 || AllowedCpuCoresPerContainer.Value <= 0
            || TargetTemperatureMaxC.Value <= TargetTemperatureMinC.Value || StepSeconds.Value < 0.5
            || ServerHoursPerTask.Value <= 0 || TasksPerJob.Value < 1 || DeadlineSteps.Value < 1
            || ForecastHorizonSteps.Value < 1 || AutoSpawnEverySteps.Value < 1 || ServersPerNode.Value is < 1 or > 8)
            throw new InvalidDataException("Invalid live demo configuration (the live sites assume minimumLoadShare 0).");
        LiveCoordinator.CreateScheduler(Scheduler);
    }
}
