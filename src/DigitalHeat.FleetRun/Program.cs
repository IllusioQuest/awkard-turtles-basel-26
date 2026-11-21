// CLI for the fleet simulation (docs/model/fleet-simulation.md §12).
using System.Globalization;
using System.Text;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Fleet;

const string Usage = "usage: FleetRun generate-houses <spec.json> -o <file> | run <scenario.json> [--run-id <id>] [--out <dir>] | post <run-dir> [--hardware <file>]";
var inv = CultureInfo.InvariantCulture;

try
{
    if (args.Length == 0) { Console.Error.WriteLine(Usage); return 2; }
    string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    switch (args[0])
    {
        case "generate-houses" when args.Length >= 2:
            return GenerateHouses(args[1], Opt("-o") ?? throw new ArgumentException("-o <file> is required."));
        case "run" when args.Length >= 2:
            return RunScenario(args[1], Opt("--run-id"), Opt("--out"));
        case "post" when args.Length >= 2:
            return Post(args[1], Opt("--hardware"));
        default:
            Console.Error.WriteLine(Usage); return 2;
    }
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or KeyNotFoundException or InvalidFleetActionException)
{
    Console.Error.WriteLine("error: " + e.Message);
    return 1;
}

int GenerateHouses(string specPath, string outPath)
{
    string root = Scenario.FindRepoRoot(Path.GetDirectoryName(Path.GetFullPath(specPath))!);
    using var doc = JsonDocument.Parse(File.ReadAllText(specPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
    var (houses, resolved) = HouseGenerator.Generate(doc.RootElement, root);
    long seed = resolved.TryGetProperty("seed", out var s) ? s.GetInt64() : HouseGenerator.DefaultSeed;
    HouseFiles.Write(outPath, houses, resolved, seed);
    Console.WriteLine($"{houses.Count} houses -> {outPath}");
    return 0;
}

int RunScenario(string scenarioPath, string? runId, string? outDir)
{
    var scenario = Scenario.Load(scenarioPath);
    string dir = outDir ?? Path.Combine(scenario.Resolve(scenario.Logging.OutDir), runId ?? scenario.Name);
    var run = FleetSimulation.Run(scenario);
    FleetOutput.Write(run, dir);
    var r = run.Result;
    var sp = run.ServerParams;
    var acc = new EnergyAccount();
    foreach (var h in r.Houses) acc.Add(h.Account);
    double elec = r.Houses.Sum(h => h.ElectricityChf), heat = r.Houses.Sum(h => h.HeatValueChf(sp));
    double income = r.Houses.Sum(h => h.IncomeChf), margin = r.Houses.Sum(h => h.MarginChf(sp));
    double? be = acc.ElectricKwh > 0 ? (elec - heat) / (acc.ElectricKwh / sp.MaxElectricKw) : null;
    int done = r.Jobs.Count(j => j.Status == "done"), open = r.Jobs.Count(j => j.Status == "open"),
        rej = r.Jobs.Count(j => j.Status == "rejected_price"), missed = r.Jobs.Count(j => j.DeadlineMissed);
    string F(double x, string f = "F1") => x.ToString(f, inv);
    Console.WriteLine($"scenario {scenario.Name}: {run.Houses.Count} houses, {run.Houses.Sum(h => h.ServerCount)} servers, {run.Calendar.RunHours} h");
    Console.WriteLine($"jobs {r.Jobs.Count}: done {done}, open {open}, rejected {rej}, missed deadlines {missed}");
    Console.WriteLine($"heat kWh: demand {F(acc.DemandKwh, "F0")}, used {F(acc.UsedKwh, "F0")} (coverage {F(acc.DemandKwh > 0 ? acc.UsedKwh / acc.DemandKwh : 0, "P1")}), backup {F(acc.BackupKwh, "F0")}, dumped {F(acc.DumpedKwh, "F0")}");
    Console.WriteLine($"CHF: electricity {F(elec, "F0")}, heat value {F(heat, "F0")}, income {F(income, "F0")}, margin {F(margin, "F0")}");
    Console.WriteLine($"network break-even (electricity - heat value) per server-hour: {(be is { } b ? F(b, "F3") : "n/a")} CHF");
    Console.WriteLine($"wall {F(r.WallTimeS, "F2")} s, {r.EventCount} events, {F(r.WallTimeS > 0 ? r.EventCount / r.WallTimeS : 0, "F0")} events/s, {r.SchedulerCalls} scheduler calls");
    Console.WriteLine("phases s: " + string.Join(", ", run.PhaseSeconds.Select(kv => $"{kv.Key} {F(kv.Value, "F2")}")));
    Console.WriteLine($"output: {dir}");
    return 0;
}

int Post(string runDir, string? hardwarePath)
{
    hardwarePath ??= ScenarioHardwareProfile(runDir)
        ?? throw new ArgumentException("--hardware <file> is required: the run's scenario names no servers.hardware_profile.");
    var hw = HardwareProfile.Load(hardwarePath);
    double purchase = hw.PurchasePriceChfPerServer.Value!.Value;
    double deprec = hw.DepreciationChfPerServerYear;
    double ops = hw.OpsChfPerServerYear.Value ?? throw new InvalidDataException("Hardware profile has no ops cost (TODO(source)).");
    using var runJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "run.json")));
    double runHours = runJson.RootElement.GetProperty("run_hours").GetDouble();
    double factor = 8760.0 / runHours;
    var t = CsvTable.Read(Path.Combine(runDir, "houses-summary.csv"));
    string R(double d) => d.ToString("R", inv);
    var sb = new StringBuilder();
    sb.Append($"# hardware {hw.Id}: purchase {R(purchase)} CHF / lifetime {R(hw.LifetimeYears.Value!.Value)} y + ops {R(ops)} CHF per server-year; ");
    sb.Append($"margin_chf annualised x 8760 / {R(runHours)} run hours = x {R(factor)}; payback_years = servers x purchase / (annual margin - servers x ops), empty if not positive\n");
    sb.Append("house_id,servers,margin_chf,annual_margin_chf,hardware_chf_per_year,net_after_hardware_chf,payback_years\n");
    foreach (var r in t.Rows)
    {
        double servers = t.Num(r, "servers"), margin = t.Num(r, "margin_chf");
        double annual = margin * factor, hwYear = servers * (deprec + ops), net = annual - hwYear;
        double denom = annual - servers * ops;
        string payback = servers > 0 && denom > 0 ? R(servers * purchase / denom) : "";
        sb.Append(string.Join(',', t.Str(r, "house_id"), R(servers), R(margin), R(annual), R(hwYear), R(net), payback)).Append('\n');
    }
    string path = Path.Combine(runDir, "houses-fullcost.csv");
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"{t.Rows.Count} houses -> {path}");
    return 0;
}

// The profile the run's job types were derived from (resolved scenario in run.json), repo-relative.
static string? ScenarioHardwareProfile(string runDir)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "run.json")));
    if (!doc.RootElement.TryGetProperty("scenario", out var sc) || !sc.TryGetProperty("servers", out var sv)
        || !sv.TryGetProperty("hardware_profile", out var hp) || hp.GetString() is not { } rel) return null;
    return Path.IsPathRooted(rel) ? rel : Path.Combine(Scenario.FindRepoRoot(Path.GetFullPath(runDir)), rel);
}
