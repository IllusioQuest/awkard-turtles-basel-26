using System.Diagnostics;
using System.Text.Json;
using DigitalHeat.Orchestration;

const int Warmups = 2;
const int Samples = 21;
string scenario = Path.Combine(args[2], "config/mesh/demo.json");
var values = new List<double>();
int messages = 0;
for (int i = 0; i < Warmups + Samples; i++)
{
    string state = Path.Combine(Path.GetTempPath(), "dh-mesh-timing-" + Guid.NewGuid().ToString("N"));
    long start = Stopwatch.GetTimestamp();
    object result = PeerMeshDemo.Run(scenario, state);
    double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    using var report = JsonDocument.Parse(JsonSerializer.Serialize(result, WorkloadJson.Options));
    messages = report.RootElement.GetProperty("messages").GetInt32();
    if (report.RootElement.GetProperty("executors").EnumerateArray().Sum(p => p.GetProperty("starts").GetInt32()) != 3)
        throw new InvalidOperationException("Protocol fixture did not start exactly three fake jobs.");
    if (i >= Warmups) values.Add(elapsed);
}
values.Sort();
var output = new { generated_utc = DateTimeOffset.UtcNow, source_commit = args[1], samples = Samples, warmups = Warmups,
    peers = 3, jobs = 3, messages, median_ms = values[Samples / 2], p95_ms = values[(int)Math.Ceiling(.95 * Samples) - 1],
    method = "Fresh peer journals per round; includes three peer constructors, direct offer delivery, three proposals, durable intent/outcome/execution journal writes, fake start/ack and disposal. Excludes build, scenario output serialization and temp cleanup. Fixed synthetic config/mesh/demo.json, two warmups, nearest-rank p95.",
    caveat = "In-process deterministic transport and fake executors, not Linux networking or Docker latency. Existing games stay running on a non-dedicated laptop. Per-round disk flushes included; no response SLA or field result. Full mesh initial offers require n*(n-1) deliveries." };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
