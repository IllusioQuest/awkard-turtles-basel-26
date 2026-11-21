using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DigitalHeat.Fleet;

const int Warmups = 1;
const int Samples = 5;
var cases = new List<object>();
foreach (int houses in new[] { 100, 1000 })
{
    var callbacks = new List<double>();
    var totals = new List<double>();
    long events = 0; int jobs = 0;
    for (int sample = -Warmups; sample < Samples; sample++)
    {
        var cfg = Scenario.Load(Path.Combine(args[2], "config/fleet/scenarios/basel-region-100.json"));
        cfg.Run.EndLocal = "2025-01-02T00:00";
        cfg.Logging.HouseTimeseries = "none";
        var spec = JsonNode.Parse(File.ReadAllText(Path.Combine(args[2], "config/fleet/houses/basel-region-100.spec.json")))!;
        spec["count"] = houses;
        cfg.Houses = new() { Generate = JsonDocument.Parse(spec.ToJsonString()).RootElement };
        cfg.Demand = new() { VolumeScale = cfg.JobTypes.ToDictionary(p => Path.GetFileNameWithoutExtension(p)!, _ => houses / 100.0) };
        var measured = new Measured(new HeatFirstFleetScheduler(cfg.Scheduler));
        long start = Stopwatch.GetTimestamp();
        var run = FleetSimulation.Run(cfg, scheduler: measured);
        if (sample < 0) continue;
        totals.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        callbacks.AddRange(measured.Milliseconds);
        events = run.Result.EventCount; jobs = run.Jobs.Count;
    }
    callbacks.Sort(); totals.Sort();
    cases.Add(new { houses, simulated_hours = 24, jobs, events, callback_count = callbacks.Count,
        callback_median_ms = callbacks[callbacks.Count / 2],
        callback_p95_ms = callbacks[(int)Math.Ceiling(.95 * callbacks.Count) - 1],
        callback_total_per_run_ms = callbacks.Sum() / Samples, complete_run_median_ms = totals[Samples / 2] });
}
var report = new { source_commit = args[1], generated_utc = DateTimeOffset.UtcNow, samples = Samples, warmups = Warmups,
    source = "Deterministic Basel-region fixture, first January day; existing house generator/seed, job profiles and physical/economic inputs. Counts 100/1000 and proportional demand scaling are performance scenarios, not measured market demand. Callback timings include wrapper clock overhead; complete runs include input loading and job generation. No Docker/network. Non-dedicated laptop.", cases };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

sealed class Measured(IFleetScheduler inner) : IFleetScheduler
{
    public List<double> Milliseconds { get; } = [];
    public string Id => inner.Id;
    public void Initialize(IFleetView view, IFleetActions actions) => inner.Initialize(view, actions);
    public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions actions)
    {
        long start = Stopwatch.GetTimestamp();
        inner.OnEvents(view, events, actions);
        Milliseconds.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
