using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Network;

// One policy: reference-house run, or a configured multi-site cohort simulation.
// NetworkRun [root] [--scenario FILE] [--type1-share X] [--phi Y] [--export]
// Optional --start/--hours select a labelled partial-year window. Transfer delay remains an
// eligibility constraint; heat ranks only sites where the work can already execute.
string? rootArg = null, scenarioArg = null;
const int horizon = 1;
int startHour = 0;
int? windowHours = null;
string[] excludeKinds = [];
string? summaryJson = null;
int transferDelay = 0;
double phi = 1.0;
double? type1Share = null;
bool export = false;
string[] strategyIds = ["mesh-heat-first"];
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--exclude-kinds": excludeKinds = args[++i].Split(','); break;
        case "--summary-json": summaryJson = args[++i]; break;
        case "--transfer-delay": transferDelay = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--scenario": scenarioArg = args[++i]; break;
        case "--strategy": strategyIds = args[++i].Split(','); break;
        case "--type1-share": type1Share = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--phi": phi = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--export": export = true; break;
        case "--start": startHour = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--hours": windowHours = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        default:
            if (args[i].StartsWith("--", StringComparison.Ordinal) || rootArg is not null)
                throw new ArgumentException("Usage: NetworkRun [root] " +
                    "| [root] --scenario FILE [--strategy mesh-heat-first] [--type1-share X] [--phi Y] [--export]");
            rootArg = args[i]; break;
    }
}
string root = rootArg is not null ? Path.GetFullPath(rootArg) : FindRepositoryRoot();
if (scenarioArg is not null)
{
    RunScenario(root, Path.GetFullPath(scenarioArg, root), strategyIds, type1Share, phi, horizon, export,
        string.Join(' ', args), startHour, windowHours, excludeKinds,
        summaryJson, transferDelay);
    return;
}
var p = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
var demand = HeatDemand.Build(WeatherCsv.Read(Path.Combine(root, "data", "raw", "open-meteo", "basel-2025-hourly.csv")), p);
// Basel coordinates from the case (docs/case/challenge.md).
var site = NetworkSite.FromReference("basel-reference-house", "Reference house Basel", 47.56, 7.59, "CH", demand, p);
NetworkSite[] sites = [site];

var rows = new List<object>();
void Record(string id, string label, string forecast, SiteSummary s, double seconds)
{
    rows.Add(new { id, label, forecast, horizonHours = horizon,
        coverage = s.Coverage, dumpedHeatKwh = s.DumpedHeatKwh, serverHours = s.ServerHours,
        utilisation = s.Utilisation, backupHeatKwh = s.BackupHeatKwh, finalTankKwh = s.FinalTankKwh,
        caseBreakEvenChfPerServerHour = s.CaseBreakEvenChfPerServerHour, runtimeSeconds = seconds });
    Console.WriteLine($"{id,-4} {s.Coverage,8:P2} {s.DumpedHeatKwh,10:F1} {s.ServerHours,11:F1} {s.Utilisation,8:P2} " +
        $"{s.FinalTankKwh,7:F1} {seconds,7:F1}s  {forecast}");
}
Console.WriteLine("id   coverage  dumped kWh  server-h    util.  tank   runtime  information");
{
    var scheduler = new HeatFirstNetworkScheduler();
    var sw = Stopwatch.StartNew();
    var r = NetworkSimulation.Run(sites, scheduler, new PerfectForesightForecasts(), 1);
    Record(scheduler.Id, scheduler.Label, "current hour only", r.Sites[0].Summary, sw.Elapsed.TotalSeconds);
}
string output = Path.Combine(root, "results", "network");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "reference-house-comparison.json"), JsonSerializer.Serialize(new
{
    note = "Unified heat-first policy; historical comparison modes retired. " +
           "Heat-first decisions use the current signal; forecasts do not change placement.",
    inputs = new { weather = "data/raw/open-meteo/basel-2025-hourly.csv", config = "config/reference-house.json",
        horizonHours = horizon },
    strategies = rows
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {Path.Combine(output, "reference-house-comparison.json")}");

static void RunScenario(string root, string scenarioPath, string[] strategyIds, double? type1Share, double phi,
    int horizon, bool export, string command, int startHour, int? windowHours, string[] excludeKinds,
    string? summaryJson, int transferDelay)
{
    var scenario = NetworkScenarioLoader.Load(scenarioPath, root);
    if (excludeKinds.Length > 0)
    {
        var keep = scenario.Sites.Select((s, i) => (s, i)).Where(t => !excludeKinds.Contains(t.s.Kind)).Select(t => t.i).ToArray();
        scenario = scenario with { Sites = keep.Select(i => scenario.Sites[i]).ToArray(),
            NetworkSites = keep.Select(i => scenario.NetworkSites[i]).ToArray() };
        Console.WriteLine($"EXCLUDED KINDS: {string.Join(',', excludeKinds)} ({keep.Length} sites left).");
    }
    var specs = NetworkScenarioLoader.Type1Specs(scenario, type1Share); // volumes from the full year
    IReadOnlyList<NetworkSite> sites = scenario.NetworkSites;
    if (windowHours is int wh)
    {
        if (startHour < 0 || wh < 1 || startHour + wh > sites[0].Demand.Count)
            throw new ArgumentOutOfRangeException(nameof(windowHours), "--start/--hours outside the year.");
        sites = sites.Select(s => s with { Demand = s.Demand.Skip(startHour).Take(wh).ToList() }).ToList();
        Console.WriteLine($"PARTIAL-YEAR WINDOW: hours {startHour}..{startHour + wh - 1} (not yearly figures).");
    }
    Console.WriteLine($"Scenario {scenario.Id}: {sites.Count} sites, {sites[0].Demand.Count} h, phi={phi:R}, " +
        $"Type 1 jobs/day: {string.Join(", ", specs.Select((s, i) => $"{sites[i].Id}={s?.JobsPerDay.ToString() ?? "-"}"))}");
    Console.WriteLine("strategy   coverage  dumped kWh  server-h   util.   T1 missed %  moved T1 server-h  runtime");
    foreach (var id in strategyIds)
    {
        if (id != "mesh-heat-first") throw new ArgumentException("Retired strategy: " + id + "; use mesh-heat-first.");
        INetworkScheduler scheduler = new HeatFirstNetworkScheduler(transferDelay);
        var ledger = new WorkLedger(sites, specs, sites.Select(_ => phi).ToArray(), sites[0].Demand.Count,
            NetworkScenarioLoader.HoursPerDay);
        var sw = Stopwatch.StartNew();
        // Greedy strategies ignore the forecast; the LP plans on perfect foresight of demand here
        // (labelled), with pending work only (decision 0025 §4).
        IForecastProvider forecast = new PerfectForesightForecasts();
        var result = NetworkSimulation.Run(sites, scheduler, forecast,
            horizon, work: ledger);
        var n = NetworkExport.Network(result);
        var t1 = result.Work!.Type1;
        Console.WriteLine($"{id,-10} {n.Coverage,8:P2} {n.DumpedHeatKwh,11:F1} {n.ServerHours,9:F0} {n.Utilisation,7:P2} " +
            $"{(t1.MissedDeadlinePercent is double m ? m.ToString("F2", CultureInfo.InvariantCulture) : "n/a"),12} " +
            $"{t1.MovedServerHours,18:F1} {sw.Elapsed.TotalSeconds,7:F1}s");
        if (summaryJson is not null)
            File.AppendAllText(Path.GetFullPath(summaryJson, root), JsonSerializer.Serialize(new
            {
                scenario = scenario.Id, strategy = id, forecast = result.ForecastLabel, horizonHours = horizon,
                phi, type1Share, excludeKinds, startHour, windowHours, transferDelayHours = transferDelay, sites = sites.Count,
                coverage = n.Coverage, dumpedHeatKwh = n.DumpedHeatKwh, serverHours = n.ServerHours,
                utilisation = n.Utilisation, type1MissedDeadlinePercent = t1.MissedDeadlinePercent,
                type1Arrived = t1.Arrived, type1Missed = t1.Missed, type1MovedServerHours = t1.MovedServerHours,
                runtimeSeconds = sw.Elapsed.TotalSeconds, command = "NetworkRun " + command
            }) + Environment.NewLine);
        if (export)
            Console.WriteLine("  exported to " + NetworkExport.Write(root, scenario, result,
                new NetworkRunInfo(phi, type1Share, specs, "NetworkRun " + command, transferDelay)));
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository or pass its root as an argument.");
}
