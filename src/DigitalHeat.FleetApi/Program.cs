using DigitalHeat.Fleet;
using DigitalHeat.FleetApi;

string repoRoot = Scenario.FindRepoRoot(AppContext.BaseDirectory);
string runsDir = Path.Combine(repoRoot, "data/derived/fleet");
int port = 5080;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--runs") runsDir = Path.GetFullPath(args[i + 1]);
    else if (args[i] == "--port") port = int.Parse(args[i + 1]);
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();
var store = new RunStore(runsDir, repoRoot);

IResult Guard(Func<object> f)
{
    try { return Results.Ok(f()); }
    catch (NotFoundException e) { return Results.Text(e.Message, statusCode: 404); }
    catch (BadRangeException e) { return Results.Text(e.Message, statusCode: 400); }
}

app.MapGet("/api/runs", () => Guard(() => store.Runs()));
app.MapGet("/api/runs/{id}/houses", (string id) => Guard(() => store.Houses(id)));
app.MapGet("/api/runs/{id}/network", (string id) => Guard(() => store.Network(id)));
app.MapGet("/api/runs/{id}/snapshots", (string id, int? from, int? to) =>
    from is null || to is null ? Results.Text("from and to are required.", statusCode: 400) : Guard(() => store.Snapshots(id, from.Value, to.Value)));
app.MapGet("/api/runs/{id}/houses/{houseId}/series", (string id, string houseId) => Guard(() => store.HouseSeries(id, houseId)));
app.MapGet("/api/live-household", () =>
{
    string path = Path.Combine(repoRoot, "data", "external", "live-household", "current.json");
    if (!File.Exists(path)) return Results.NotFound();
    try { return Results.Content(File.ReadAllText(path), "application/json"); }
    catch (IOException) { return Results.StatusCode(503); }
});
// T-78 voice demo: the phone page posts one recording (or typed text); DigitalHeat.VoiceDemo picks it
// up from the inbox. Optional shared token (env VOICE_DEMO_TOKEN) when the page is exposed via a tunnel.
app.MapPost("/api/voice-demo/requests", async (HttpRequest request) =>
{
    string? token = Environment.GetEnvironmentVariable("VOICE_DEMO_TOKEN");
    if (!string.IsNullOrEmpty(token) && request.Headers["X-Voice-Token"] != token) return Results.StatusCode(401);
    string type = (request.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
    string? file = type switch
    {
        "text/plain" => "text.txt", "audio/webm" => "audio.webm", "audio/ogg" => "audio.ogg", "audio/mp4" => "audio.m4a",
        "audio/mpeg" => "audio.mp3", "audio/wav" or "audio/x-wav" or "audio/wave" => "audio.wav", _ => null
    };
    if (file is null) return Results.StatusCode(415);
    const long maxBytes = 20 * 1024 * 1024; // about 20 minutes of compressed speech; far above a spoken request
    string id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8];
    string dir = Path.Combine(repoRoot, "data", "external", "voice-demo", "inbox", id);
    Directory.CreateDirectory(dir);
    using (var output = File.Create(Path.Combine(dir, file)))
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await request.Body.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > maxBytes) { output.Close(); Directory.Delete(dir, true); return Results.StatusCode(413); }
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        if (total == 0) { output.Close(); Directory.Delete(dir, true); return Results.BadRequest(); }
    }
    File.WriteAllText(Path.Combine(dir, "ready"), "");
    return Results.Ok(new { id });
});
app.MapGet("/api/runs/{id}/houses/{houseId}/job-events", (string id, string houseId, long? fromMs, long? toMs) =>
    fromMs is null || toMs is null ? Results.Text("fromMs and toMs are required.", statusCode: 400) : Guard(() => store.JobEvents(id, houseId, fromMs.Value, toMs.Value)));
app.MapGet("/api/weather/points", () => Guard(() => store.WeatherPoints()));
app.MapGet("/api/runs/{id}/weather", (string id, int? from, int? to) =>
    from is null || to is null ? Results.Text("from and to are required.", statusCode: 400) : Guard(() => store.Weather(id, from.Value, to.Value)));

app.Run();
