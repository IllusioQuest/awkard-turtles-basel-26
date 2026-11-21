using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.Network.Lp;

// Strategy comparison on the reference house (decision 0024, step 1).
// Usage: NetworkRun [repository-root] [--horizon HOURS] [--rho RHO] [--sigma24 S1,S2,...] [--seed N]
// Writes results/network/reference-house-comparison.json (generated, git-ignored).
string? rootArg = null;
int horizon = 48, seed = 20251121;
double rho = 0.8;
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
        default:
            if (args[i].StartsWith("--", StringComparison.Ordinal) || rootArg is not null)
                throw new ArgumentException("Usage: NetworkRun [root] [--horizon H] [--rho R] [--sigma24 a,b] [--seed N]");
            rootArg = args[i]; break;
    }
}
string root = rootArg is not null ? Path.GetFullPath(rootArg) : FindRepositoryRoot();
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

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository or pass its root as an argument.");
}
