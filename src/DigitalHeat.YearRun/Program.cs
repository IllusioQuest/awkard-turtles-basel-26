using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;

if (args.Length > 2) throw new ArgumentException("Usage: YearRun [repository-root] [job-scenario.json]");
string root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRepositoryRoot();
string scenarioPath = args.Length > 1 ? Path.GetFullPath(args[1], root)
    : Path.Combine(root, "config", "job-scenarios", "unlimited.json");
var parameters = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
var jobScenario = JobScenario.Load(scenarioPath);
var weather = ReadWeather(Path.Combine(root, "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
VerifyElectricitySnapshot(Path.Combine(root, "data", "raw", "elcom", "iwb-basel-h4-2026.json"), parameters);
var demand = HeatDemand.Build(weather, parameters);
var result = YearSimulation.Run(demand, parameters,
    new GreedyHeatScheduler(parameters.MinimumLoadShare.Value), new SimulatedExecutor(), jobScenario);
string output = Path.Combine(root, "results", "reference-house");
Directory.CreateDirectory(output);
WriteHourly(Path.Combine(output, "hourly.csv"), result.Hours);
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
        JobScenario = Path.GetRelativePath(root, scenarioPath)
    }
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {result.Hours.Count} hours to {output}");
Console.WriteLine($"Coverage {result.Summary.Coverage:P1}; server-hours {result.Summary.ServerHours:F0}; " +
    $"case break-even {result.Summary.BreakEvenChfPerServerHour?.ToString("F3") ?? "undefined"} CHF/server-hour");

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository or pass its root as an argument.");
}

static List<WeatherHour> ReadWeather(string path)
{
    using var reader = new StreamReader(path);
    string? line;
    do { line = reader.ReadLine(); }
    while (line is not null && !line.StartsWith("time,", StringComparison.Ordinal));
    if (line is null) throw new InvalidDataException("Missing weather CSV header.");
    var columns = line.Split(',');
    int time = Array.IndexOf(columns, "time");
    int temperature = Array.FindIndex(columns, x => x.StartsWith("temperature_2m", StringComparison.Ordinal));
    if (time < 0 || temperature < 0) throw new InvalidDataException("Missing weather columns.");
    var rows = new List<WeatherHour>();
    while ((line = reader.ReadLine()) is not null)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var fields = line.Split(',');
        rows.Add(new WeatherHour(fields[time], double.Parse(fields[temperature], CultureInfo.InvariantCulture)));
    }
    return rows;
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

static void WriteHourly(string path, IReadOnlyList<HourResult> hours)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine("local_time,heat_demand_kwh,space_heat_kwh,hot_water_kwh,target_load_per_server,actual_load_per_server,produced_heat_kwh,direct_used_kwh,tank_charge_kwh,tank_discharge_kwh,tank_stored_kwh,dumped_heat_kwh,backup_heat_kwh,server_electric_kwh");
    foreach (var h in hours)
    {
        var values = new[] { h.DemandKwh, h.SpaceHeatKwh, h.HotWaterKwh, h.TargetLoadPerServer,
            h.ActualLoadPerServer, h.ProducedHeatKwh, h.DirectUsedKwh, h.TankChargeKwh,
            h.TankDischargeKwh, h.TankStoredKwh, h.DumpedHeatKwh, h.BackupHeatKwh, h.ServerElectricKwh };
        writer.WriteLine(h.LocalTime + "," + string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
    }
}
