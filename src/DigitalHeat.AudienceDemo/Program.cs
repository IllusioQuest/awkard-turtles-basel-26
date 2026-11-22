using System.Collections.Concurrent;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;

var stateDir = Path.GetFullPath(Environment.GetEnvironmentVariable("DEMO_STATE_DIR") ?? "data/external/audience-demo");
var image = Environment.GetEnvironmentVariable("DEMO_IMAGE") ?? "digitalheat/apertus-audience:local";
var operatorToken = Environment.GetEnvironmentVariable("DEMO_OPERATOR_TOKEN") ?? throw new InvalidDataException("DEMO_OPERATOR_TOKEN required");
if (operatorToken.Length < 32) throw new InvalidDataException("Operator token too short");
var port = int.Parse(Environment.GetEnvironmentVariable("DEMO_PORT") ?? "18970");
Directory.CreateDirectory(stateDir);
var configPath = Environment.GetEnvironmentVariable("DEMO_CONFIG") ?? "config/audience-apertus-demo.json";
using var config = JsonDocument.Parse(File.ReadAllText(configPath));
var heat = config.RootElement.GetProperty("worker_demand_kwh_per_hour").EnumerateArray().Select(v => v.GetDouble()).ToArray();
var referenceHeat = config.RootElement.GetProperty("reference_recoverable_heat_kwh_per_hour").GetDouble();
var workerRamMib = config.RootElement.GetProperty("worker_ram_mib").GetInt64();
if (heat.Length != 4 || heat.Any(v => !double.IsFinite(v) || v < referenceHeat) || referenceHeat <= 0 || workerRamMib < 1024)
    throw new InvalidDataException("Invalid four-worker audience demo configuration.");
var source = config.RootElement.GetProperty("worker_demand_source").GetString() + " "
    + config.RootElement.GetProperty("reference_heat_source").GetString() + " Not measured laptop heat.";
var names = new[] { "Home", "Hotel", "Pool", "Research" };
var colors = new[] { "#f18b6d", "#e3b95b", "#5dc4c5", "#9886e6" };
// Illustrative hourly demand at four virtual sites. Every site can use one 0.8 kWh/h worker.
var jobs = new Dictionary<string, DemoJob>(StringComparer.Ordinal);
string? lastError = null;
var lastVisitor = new ConcurrentDictionary<string, DateTimeOffset>();
var gate = new object();
var docker = new DockerCommands();
var runtime = new DockerWorkloadRuntime(docker);
var runtimeNodes = names.Select((_, i) => new RuntimeNode($"worker-{i+1}", "desktop-linux", "none", [], [])).ToArray();
// Short audience questions are independent jobs. The hourly heat-budget retention used for
// annual accounting would exhaust a virtual site's full-hour budget after its first answer.
using var owner = new WorkloadCoordinator("audience-apertus-v1", stateDir, runtime);
long revision = 0;
WorkloadInventory Inventory()
{
    var now = DateTimeOffset.UtcNow;
    var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
    return new(++revision, hour, hour.AddHours(1), source,
        names.Select((_, i) => new WorkloadSite($"site-{i+1}", "CH", new HeatSignal(heat[i], 0, 0, 1, referenceHeat), 0)).ToArray(),
        [new("one-laptop", new(4, 4 * workerRamMib, 4), [])],
        names.Select((_, i) => new ComputeNode($"worker-{i+1}", $"site-{i+1}", "one-laptop", true, true,
            new(1, workerRamMib, 1), ["apertus-cpu"], [], [], [])).ToArray(), [])
        { ResourceObservedAtUtc = now, ResourceValidUntilUtc = now.AddMinutes(2) };
}
owner.Update(Inventory(), DateTimeOffset.UtcNow);
owner.Reconcile(runtimeNodes);
var recordPath = Path.Combine(stateDir, "requests.json");
if (File.Exists(recordPath))
    foreach (var j in JsonSerializer.Deserialize<DemoJob[]>(File.ReadAllText(recordPath)) ?? []) jobs.Add(j.Id, j);
void Save()
{
    var temp = recordPath + ".tmp";
    File.WriteAllText(temp, JsonSerializer.Serialize(jobs.Values.ToArray()));
    File.Move(temp, recordPath, true);
}
void Tick()
{
    lock (gate)
    {
        owner.Update(Inventory(), DateTimeOffset.UtcNow);
        owner.Reconcile(runtimeNodes);
        var scheduler = new HeatFirstScheduler(0);
        foreach (var active in owner.Attempts().Where(a => a.Phase is "running" or "paused"))
        {
            var index = Array.FindIndex(runtimeNodes, n => n.NodeId == active.Attempt.Reservation.NodeId);
            if (index >= 0 && (active.Phase == "running" && heat[index] == 0
                || active.Phase == "paused" && heat[index] >= referenceHeat))
                owner.ApplyBatchHeat(active.Attempt.Request.Id, new HeatSignal(heat[index], 0, 0, 1, referenceHeat), scheduler, DateTimeOffset.UtcNow);
        }
        foreach (var j in jobs.Values.Where(j => j.State is "queued" or "running" or "paused").ToArray())
        {
            var attempt = owner.Attempts().SingleOrDefault(a => a.Attempt.Request.Id == j.Id);
            if (attempt is null && j.State == "queued")
            {
                var req = new WorkloadRequest(j.Id, "batch", "site-1", new("country-allowlist", "Audience public-text demo: Switzerland only", ["CH"]),
                    new(1, workerRamMib, 1), source, referenceHeat, source, ["apertus-cpu"], null, null, null, [], new(true, true, false));
                var profile = new ContainerProfile("apertus-" + j.Id, image, "65534", [j.Question], null, [], [], []);
                attempt = owner.TryStart(req, runtimeNodes, profile, DateTimeOffset.UtcNow);
            }
            if (attempt is null) continue;
            var nodeIndex = Array.FindIndex(runtimeNodes, n => n.NodeId == attempt.Attempt.Reservation.NodeId);
            var nextState = attempt.Phase;
            if (nextState == "terminal")
            {
                var name = DockerWorkloadRuntime.Name(attempt.Attempt);
                string output = docker.Run(["--context", "desktop-linux", "logs", name]);
                j.Answer = output.Length > 6000 ? output[..6000] : output;
                nextState = owner.Observe(j.Id).ExitCode == 0 ? "completed" : "failed";
                owner.StopAndRelease(j.Id);
            }
            j.State = nextState;
            j.Node = nodeIndex < 0 ? null : nodeIndex + 1;
        }
        Save();
        lastError = null;
    }
}
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 4096);
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    // These two endpoints are deliberately public. Lovable hosts only the visitor UI;
    // the model and job owner remain on the laptop behind the temporary tunnel.
    if (ctx.Request.Path.StartsWithSegments("/api/jobs"))
    {
        ctx.Response.Headers.AccessControlAllowOrigin = "*";
        ctx.Response.Headers.AccessControlAllowMethods = "GET, POST, OPTIONS";
        ctx.Response.Headers.AccessControlAllowHeaders = "Content-Type, X-Visitor-Id";
        if (ctx.Request.Method == "OPTIONS") { ctx.Response.StatusCode = 204; return; }
    }
    await next();
});
app.MapGet("/healthz", () => Results.Ok(new { state = "ready" }));
app.MapPost("/api/jobs", (QuestionInput input, HttpContext ctx) =>
{
    string question = input.Question?.Trim() ?? "";
    if (question.Length is < 3 or > 240 || question.Any(char.IsControl)) return Results.BadRequest(new { error = "Use 3–240 printable characters." });
    string ip = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var rawVisitorId = ctx.Request.Headers["X-Visitor-Id"].FirstOrDefault();
    string visitorKey = Guid.TryParse(rawVisitorId, out var visitorId) ? $"browser:{visitorId:N}" : $"ip:{ip}";
    var now = DateTimeOffset.UtcNow;
    if (lastVisitor.TryGetValue(visitorKey, out var previous) && now - previous < TimeSpan.FromSeconds(10)) return Results.StatusCode(429);
    lock (gate)
    {
        if (jobs.Values.Count(j => j.State is "queued" or "running" or "paused") >= 20) return Results.StatusCode(503);
        lastVisitor[visitorKey] = now;
        var job = new DemoJob(Guid.NewGuid().ToString("N"), question, "queued", now);
        jobs.Add(job.Id, job); Save();
        return Results.Accepted($"/api/jobs/{job.Id}", new { id = job.Id });
    }
});
app.MapGet("/api/jobs/{id}", (string id) => { lock (gate) return jobs.TryGetValue(id, out var j) ? Results.Ok(j) : Results.NotFound(); });
app.MapGet("/api/status", () => { lock (gate) return Results.Ok(new { error = lastError, workers = names.Select((n,i) => new { id = i+1, name = n, color = colors[i], demand_kwh_per_hour = heat[i], useful_heat_kwh_per_hour = jobs.Values.Any(j => j.Node == i+1 && j.State == "running") ? referenceHeat : 0.0, heat_source = source }).ToArray(),
    jobs = jobs.Values.OrderByDescending(j => j.SubmittedAtUtc).Take(30).Select(j => new { j.Id, j.State, j.Node, j.SubmittedAtUtc, question = j.Question.Length > 64 ? j.Question[..64] + "…" : j.Question }).ToArray() }); });
app.MapPost("/api/operator/heat", (HeatInput input, HttpContext ctx) =>
{
    if (ctx.Request.Headers["X-Operator-Token"] != operatorToken) return Results.Unauthorized();
    if (input.Node is < 1 or > 4 || !double.IsFinite(input.DemandKwhPerHour)
        || input.DemandKwhPerHour != 0 && input.DemandKwhPerHour < referenceHeat
        || input.DemandKwhPerHour > 2 * referenceHeat) return Results.BadRequest();
    lock (gate) { heat[input.Node-1] = input.DemandKwhPerHour; Tick(); }
    return Results.Ok();
});
app.MapPost("/api/operator/shutdown", (HttpContext ctx) =>
{
    if (ctx.Request.Headers["X-Operator-Token"] != operatorToken) return Results.Unauthorized();
    lock (gate)
    {
        owner.Reconcile(runtimeNodes);
        foreach (var attempt in owner.Attempts().Where(a => a.Phase is "created" or "running" or "paused" or "terminal"))
        {
            owner.StopAndRelease(attempt.Attempt.Request.Id);
            if (jobs.TryGetValue(attempt.Attempt.Request.Id, out var job) && job.State is not ("completed" or "failed"))
                job.State = "failed";
        }
        Save();
    }
    _ = Task.Run(async () => { await Task.Delay(200); await app.StopAsync(); });
    return Results.Ok(new { stopped = true });
});
app.MapGet("/operator", () => Results.Content(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "operator.html")), "text/html"));
app.MapGet("/ask", () => Results.Content(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ask.html")), "text/html"));
var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
_ = Task.Run(async () => { while (await timer.WaitForNextTickAsync()) try { Tick(); } catch (Exception e) { lock (gate) lastError = e.Message; Console.Error.WriteLine("Audience tick: " + e.Message); } });
app.Run();

record QuestionInput(string? Question);
record HeatInput(int Node, double DemandKwhPerHour);
sealed class DemoJob(string id, string question, string state, DateTimeOffset submittedAtUtc)
{
    public string Id { get; set; } = id;
    public string Question { get; set; } = question;
    public string State { get; set; } = state;
    public DateTimeOffset SubmittedAtUtc { get; set; } = submittedAtUtc;
    public int? Node { get; set; }
    public string? Answer { get; set; }
}
