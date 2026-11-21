using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.JobRuntime;

var root = FindRoot();
var reference = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
var config = JsonSerializer.Deserialize<DemoConfig>(File.ReadAllText(Path.Combine(root,
    "config", "live-demo-four-node.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Empty four-node demo config.");
config.Validate();
if (config.ReferenceServerElectricKw.Value != reference.ServerElectricKw.Value
    || config.HeatRecoveryShare.Value != reference.HeatRecoveryShare.Value
    || config.MinimumLoadShare.Value != reference.MinimumLoadShare.Value)
    throw new InvalidDataException("The live demo's reference values must match config/reference-house.json.");
var docker = new DockerCliContainerControl();
var nodes = config.Nodes.Select(n => new LiveNode(n, config, docker,
    new GreedyHeatScheduler(config.MinimumLoadShare.Value), root)).ToArray();
double initialTargetC = (config.TargetTemperatureMinC.Value + config.TargetTemperatureMaxC.Value) / 2;
foreach (var node in nodes) node.SetTarget(initialTargetC);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot(Path.Combine(root, "src", "DigitalHeat.LiveGrid", "wwwroot"));
builder.WebHost.UseUrls("http://127.0.0.1:5087");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/state", () => Results.Json(new
{
    title = "One laptop · four independent worker nodes",
    mapping = "Illustrative target temperature is translated to a reference heat signal; it is not a room sensor.",
    nodes = nodes.Select(n => n.ReadState())
}));
app.MapPost("/api/nodes/{id}/target", (string id, TargetRequest request) =>
{
    var node = nodes.SingleOrDefault(n => n.Config.Id == id);
    if (node is null) return Results.NotFound();
    if (!double.IsFinite(request.TemperatureC)
        || request.TemperatureC < config.TargetTemperatureMinC.Value
        || request.TemperatureC > config.TargetTemperatureMaxC.Value)
        return Results.BadRequest(new { error = "Target temperature is outside the demo slider range." });
    try { return Results.Json(node.SetTarget(request.TemperatureC)); }
    catch (Exception error) { return Results.Problem(error.Message, statusCode: 503); }
});
using var observationStop = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() => observationStop.Cancel());
_ = Task.Run(async () =>
{
    while (!observationStop.IsCancellationRequested)
    {
        try
        {
            var cpu = docker.ReadCpuPercents(nodes.Select(n => n.Config.Container).ToArray());
            foreach (var node in nodes) node.ObserveCpu(cpu[node.Config.Container]);
        }
        catch (Exception error)
        {
            foreach (var node in nodes) node.SetObservationError(error.Message);
        }
        try { await Task.Delay(TimeSpan.FromSeconds(2), observationStop.Token); }
        catch (OperationCanceledException) { break; }
    }
}, observationStop.Token);
app.Run();

static string FindRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Start LiveGrid from the repository root.");
}

sealed record TargetRequest(double TemperatureC);
sealed record DemoNodeConfig(string Id, string Container, int Position);
sealed record DemoConfig(Parameter MinimumLoadShare, Parameter AllowedCpuCoresPerContainer,
    Parameter ReferenceServerElectricKw, Parameter HeatRecoveryShare, Parameter TargetTemperatureMinC,
    Parameter TargetTemperatureMaxC, DemoNodeConfig[] Nodes)
{
    public void Validate()
    {
        foreach (var p in new[] { MinimumLoadShare, AllowedCpuCoresPerContainer, ReferenceServerElectricKw,
                     HeatRecoveryShare, TargetTemperatureMinC, TargetTemperatureMaxC })
            if (!double.IsFinite(p.Value) || string.IsNullOrWhiteSpace(p.Unit) || string.IsNullOrWhiteSpace(p.Source))
                throw new InvalidDataException("Every demo parameter needs a finite value, unit and source.");
        if (Nodes.Length != 4 || Nodes.Select(n => n.Id).Distinct().Count() != 4
            || Nodes.Select(n => n.Container).Distinct().Count() != 4
            || MinimumLoadShare.Value is < 0 or > 1 || AllowedCpuCoresPerContainer.Value <= 0
            || ReferenceServerElectricKw.Value <= 0 || HeatRecoveryShare.Value is <= 0 or > 1
            || TargetTemperatureMaxC.Value <= TargetTemperatureMinC.Value)
            throw new InvalidDataException("Invalid four-node demo configuration.");
    }
}

sealed class LiveNode
{
    private readonly DemoConfig demo;
    private readonly DockerJobExecutor executor;
    private readonly IScheduler scheduler;
    private readonly string workRoot;
    private double targetTemperatureC;
    private double targetLoad;
    private double actualLoad;
    private string? error;

    public DemoNodeConfig Config { get; }

    public LiveNode(DemoNodeConfig config, DemoConfig demo, IContainerControl docker,
        IScheduler scheduler, string workRoot)
    {
        Config = config; this.demo = demo; this.scheduler = scheduler; this.workRoot = workRoot;
        executor = new DockerJobExecutor(docker, config.Container,
            demo.AllowedCpuCoresPerContainer.Value, supportsThrottle: true);
        targetTemperatureC = (demo.TargetTemperatureMinC.Value + demo.TargetTemperatureMaxC.Value) / 2;
    }

    public object SetTarget(double temperatureC)
    {
        lock (this)
        {
            targetTemperatureC = temperatureC;
            double span = demo.TargetTemperatureMaxC.Value - demo.TargetTemperatureMinC.Value;
            double heatShare = (temperatureC - demo.TargetTemperatureMinC.Value) / span;
            double demandKwh = heatShare * demo.ReferenceServerElectricKw.Value * demo.HeatRecoveryShare.Value;
            var signal = new HeatSignal(demandKwh, 0, 0, 1,
                demo.ReferenceServerElectricKw.Value * demo.HeatRecoveryShare.Value);
            try
            {
                var applied = SchedulerController.Apply(signal, scheduler, executor);
                targetLoad = applied.TargetLoadPerServer;
                actualLoad = applied.ActualLoadPerServer;
                error = null;
            }
            catch (Exception ex) { error = ex.Message; throw; }
            return ReadStateUnsafe();
        }
    }

    public object ReadState()
    {
        lock (this) return ReadStateUnsafe();
    }

    public void ObserveCpu(double cpuPercent)
    {
        lock (this)
        {
            actualLoad = Math.Clamp(cpuPercent / (100 * demo.AllowedCpuCoresPerContainer.Value), 0, 1);
            error = null;
        }
    }

    public void SetObservationError(string message)
    {
        lock (this) error = message;
    }

    private object ReadStateUnsafe()
    {
        string logPath = Path.Combine(workRoot, "data", "external", "research-sim", "live-demo-work", "out", $"{Config.Id}.csv");
        int done = File.Exists(logPath) ? Math.Max(0, File.ReadAllLines(logPath).Length - 1) : 0;
        return new { id = Config.Id, position = Config.Position, targetTemperatureC,
            targetLoad, actualLoad, completedTasks = done, error };
    }
}
