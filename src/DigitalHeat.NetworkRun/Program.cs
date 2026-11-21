using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.Network.Lp;

// Two modes.
// 1) Strategy comparison on the reference house (decision 0024, step 1), the default:
//    NetworkRun [repository-root] [--horizon HOURS] [--rho RHO] [--sigma24 S1,S2,...] [--seed N]
//    Writes results/network/reference-house-comparison.json (generated, git-ignored).
// 2) Scenario run with Type 1 work and filler (decision 0025):
//    NetworkRun [root] --scenario config/network/<file>.json [--strategy B1,B2,LP] [--type1-share X]
//               [--phi Y] [--horizon H] [--export] [--start HOUR --hours N]
//    --start/--hours run a partial year (probe or experiment window). Type 1 volumes are still derived
//    from the full year; results are labelled as a partial-year window, not yearly figures.
//    --export writes results/network/<scenario>/<strategy>/ (map export contract, decision 0025 §7).
string? rootArg = null, scenarioArg = null;
int horizon = 48, seed = 20251121, startHour = 0;
int? windowHours = null;
double rho = 0.8, phi = 1.0;
double? type1Share = null;
bool export = false;
string[] strategyIds = ["B1", "B2"];
double[] sigmas = [0.05, 0.10, 0.20];
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--horizon": horizon = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--rho": rho = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--sigma24":
            sigmas = args[++i].Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray(); break;
        case "--scenario": scenarioArg = args[++i]; break;
        case "--strategy": strategyIds = args[++i].Split(','); break;
        case "--type1-share": type1Share = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--phi": phi = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--export": export = true; break;
        case "--start": startHour = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--hours": windowHours = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        default:
            if (args[i].StartsWith("--", StringComparison.Ordinal) || rootArg is not null)
                throw new ArgumentException("Usage: NetworkRun [root] [--horizon H] [--rho R] [--sigma24 a,b] [--seed N] " +
                    "| [root] --scenario FILE [--strategy B1,B2,LP] [--type1-share X] [--phi Y] [--export]");
            rootArg = args[i]; break;
    }
}
string root = rootArg is not null ? Path.GetFullPath(rootArg) : FindRepositoryRoot();
if (scenarioArg is not null)
{
    RunScenario(root, Path.GetFullPath(scenarioArg, root), strategyIds, type1Share, phi, horizon, export,
        string.Join(' ', args), startHour, windowHours);
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
    rows.Add(new { id, label, forecast, horizonHours = id.StartsWith("LP") ? horizon : (int?)null,
        coverage = s.Coverage, dumpedHeatKwh = s.DumpedHeatKwh, serverHours = s.ServerHours,
        utilisation = s.Utilisation, backupHeatKwh = s.BackupHeatKwh, finalTankKwh = s.FinalTankKwh,
        caseBreakEvenChfPerServerHour = s.CaseBreakEvenChfPerServerHour, runtimeSeconds = seconds });
    Console.WriteLine($"{id,-4} {s.Coverage,8:P2} {s.DumpedHeatKwh,10:F1} {s.ServerHours,11:F1} {s.Utilisation,8:P2} " +
        $"{s.FinalTankKwh,7:F1} {seconds,7:F1}s  {forecast}");
}
Console.WriteLine("id   coverage  dumped kWh  server-h    util.  tank   runtime  information");
foreach (INetworkScheduler scheduler in new INetworkScheduler[] { new AlwaysOnScheduler(), new LocalGreedyScheduler() })
{
    var sw = Stopwatch.StartNew();
    var r = NetworkSimulation.Run(sites, scheduler, new PerfectForesightForecasts(), 1);
    Record(scheduler.Id, scheduler.Label, "current hour only", r.Sites[0].Summary, sw.Elapsed.TotalSeconds);
}
var forecasts = new List<IForecastProvider> { new PerfectForesightForecasts() };
forecasts.AddRange(sigmas.Select(s => (IForecastProvider)new Ar1ErrorForecasts(s, rho, seed)));
foreach (var f in forecasts)
{
    var sw = Stopwatch.StartNew();
    var r = NetworkSimulation.Run(sites, new RollingHorizonLpScheduler(), f, horizon);
    Record("LP", r.SchedulerLabel, f.Label, r.Sites[0].Summary, sw.Elapsed.TotalSeconds);
}
{
    var sw = Stopwatch.StartNew();
    var ub = FullYearUpperBound.Solve(sites)[0];
    double total = demand.Sum(h => h.TotalHeatKwh);
    rows.Add(new { id = "UB", label = "full-year LP, upper bound only", forecast = "perfect foresight (whole year)",
        coverage = ub.UsedHeatKwh / total, dumpedHeatKwh = ub.DumpedHeatKwh, serverHours = ub.ServerHours,
        utilisation = ub.ServerHours / (site.ServerCount * demand.Count * site.HoursPerStep),
        finalTankKwh = ub.FinalTankKwh, runtimeSeconds = sw.Elapsed.TotalSeconds });
    Console.WriteLine($"UB   {ub.UsedHeatKwh / total,8:P2} {ub.DumpedHeatKwh,10:F1} {ub.ServerHours,11:F1} " +
        $"{ub.ServerHours / (site.ServerCount * demand.Count * site.HoursPerStep),8:P2} {ub.FinalTankKwh,7:F1} " +
        $"{sw.Elapsed.TotalSeconds,7:F1}s  perfect foresight (whole year, bound only)");
}
string output = Path.Combine(root, "results", "network");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "reference-house-comparison.json"), JsonSerializer.Serialize(new
{
    note = "Decision 0024 step 1. LP with perfect foresight and UB are upper bounds, not realistic results. " +
           "Forecast-error parameters (sigma24, rho) are TODO(source) and shown as a sweep.",
    inputs = new { weather = "data/raw/open-meteo/basel-2025-hourly.csv", config = "config/reference-house.json",
        horizonHours = horizon, rho, sigma24 = sigmas, seed },
    strategies = rows
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {Path.Combine(output, "reference-house-comparison.json")}");

static void RunScenario(string root, string scenarioPath, string[] strategyIds, double? type1Share, double phi,
    int horizon, bool export, string command, int startHour, int? windowHours)
{
    var scenario = NetworkScenarioLoader.Load(scenarioPath, root);
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
    Console.WriteLine("strategy coverage  dumped kWh  server-h   util.   T1 missed %  moved T1 server-h  runtime");
    foreach (var id in strategyIds)
    {
        INetworkScheduler scheduler = id switch
        {
            "B1" => new LocalGreedyJobsScheduler(),
            "B2" => new NetworkGreedyJobsScheduler(),
            "LP" => new RollingHorizonWorkLpScheduler(),
            _ => throw new ArgumentException($"Unknown strategy {id} (B1, B2, LP).")
        };
        var ledger = new WorkLedger(sites, specs, sites.Select(_ => phi).ToArray(), sites[0].Demand.Count,
            NetworkScenarioLoader.HoursPerDay);
        var sw = Stopwatch.StartNew();
        // Greedy strategies ignore the forecast; the LP plans on perfect foresight of demand here
        // (labelled), with pending work only (decision 0025 §4).
        var result = NetworkSimulation.Run(sites, scheduler, new PerfectForesightForecasts(),
            id == "LP" ? horizon : 1, work: ledger);
        var n = NetworkExport.Network(result);
        var t1 = result.Work!.Type1;
        Console.WriteLine($"{id,-8} {n.Coverage,8:P2} {n.DumpedHeatKwh,11:F1} {n.ServerHours,9:F0} {n.Utilisation,7:P2} " +
            $"{(t1.MissedDeadlinePercent is double m ? m.ToString("F2", CultureInfo.InvariantCulture) : "n/a"),12} " +
            $"{t1.MovedServerHours,18:F1} {sw.Elapsed.TotalSeconds,7:F1}s");
        if (export)
            Console.WriteLine("  exported to " + NetworkExport.Write(root, scenario, result,
                new NetworkRunInfo(phi, type1Share, specs, "NetworkRun " + command)));
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository or pass its root as an argument.");
}
