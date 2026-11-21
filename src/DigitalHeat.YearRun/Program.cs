using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Network;
using DigitalHeat.YearRun;

var positional = new List<string>();
string? bundlePath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--output" && bundlePath is null && i + 1 < args.Length)
        bundlePath = Path.GetFullPath(args[++i]);
    else if (args[i].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException("Usage: YearRun [repository-root] [job-scenario.json] [--output DIRECTORY]");
    else positional.Add(args[i]);
}
if (positional.Count > 2) throw new ArgumentException("Too many positional arguments.");
string root = positional.Count > 0 ? Path.GetFullPath(positional[0]) : FindRepositoryRoot();
string scenarioPath = positional.Count > 1 ? Path.GetFullPath(positional[1], root)
    : Path.Combine(root, "config", "job-scenarios", "unlimited.json");
var parameters = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
var jobScenario = JobScenario.Load(scenarioPath);
var weather = WeatherCsv.Read(Path.Combine(root, "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
VerifyElectricitySnapshot(Path.Combine(root, "data", "raw", "elcom", "iwb-basel-h4-2026.json"), parameters);
var demand = HeatDemand.Build(weather, parameters);
IScheduler scheduler = jobScenario.Mode == "unlimited"
    ? new SingleSiteUnlimitedNetworkScheduler(
        NetworkScenarioLoader.Load(Path.Combine(root, "config", "network", "reference-only.json"), root)
            .NetworkSites.Single(),
        new HeatFirstNetworkScheduler())
    : new HeatFirstScheduler(parameters.MinimumLoadShare.Value);
var result = YearSimulation.Run(demand, parameters, scheduler, new SimulatedExecutor(), jobScenario);
string output = Path.Combine(root, "results", "reference-house");
if (bundlePath is not null)
{
    output = bundlePath;
    ResultBundle.Write(output, root, scenarioPath, result, parameters, jobScenario);
}
else
{
Directory.CreateDirectory(output);
ResultBundle.WriteHourly(Path.Combine(output, "hourly.csv"), result.Hours);
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
{
    result.Summary,
    Parameters = parameters,
    JobScenario = jobScenario,
    Inputs = new
    {
        Weather = "data/raw/open-meteo/basel-2025-hourly.csv",
        Electricity = "data/raw/elcom/iwb-basel-h4-2026.json",
        Config = "config/reference-house.json",
        NetworkConfig = jobScenario.Mode == "unlimited" ? "config/network/reference-only.json" : null,
        JobScenario = Path.GetRelativePath(root, scenarioPath)
    }
}, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"Wrote {result.Hours.Count} hours to {output}");
Console.WriteLine($"Coverage {result.Summary.Coverage:P1}; server-hours {result.Summary.ServerHours:F0}; " +
    $"case break-even {result.Summary.BreakEvenChfPerServerHour?.ToString("F3") ?? "undefined"} CHF/server-hour");

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository or pass its root as an argument.");
}

static void VerifyElectricitySnapshot(string path, ReferenceParameters p)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var observations = doc.RootElement.GetProperty("response").GetProperty("data").GetProperty("observations");
    var basel = observations.EnumerateArray().Single(x => x.GetProperty("municipality").GetString() == "2701"
        && x.GetProperty("category").GetString() == "H4" && x.GetProperty("period").GetString() == "2026");
    double publishedChfPerKwh = basel.GetProperty("total").GetDouble() / 100.0;
    if (Math.Abs(publishedChfPerKwh - p.ElectricityPriceChfPerKwh.Value) > 1e-9)
        throw new InvalidDataException("Config electricity price differs from ElCom snapshot.");
}
