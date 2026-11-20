using System.Globalization;
using DigitalHeat.Core;
using DigitalHeat.JobRuntime;

try
{
    var options = ParseOptions(args);
    string root = FindRepositoryRoot();
    var parameters = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
    var job = JobDescriptorLoader.Load(options["--job"]);
    double cpuCores = double.Parse(options["--cpu-cores"], CultureInfo.InvariantCulture);
    double stepSeconds = double.Parse(options["--step-seconds"], CultureInfo.InvariantCulture);
    if (!double.IsFinite(stepSeconds) || stepSeconds <= 0) throw new ArgumentOutOfRangeException("--step-seconds");
    int? forcedStep = options.TryGetValue("--force-pause-step", out string? step)
        ? int.Parse(step, CultureInfo.InvariantCulture) : null;
    string date = options["--date"];
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out _)) throw new ArgumentException("--date must be yyyy-MM-dd");

    var weather = ReadWeather(Path.Combine(root, "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
    var demand = HeatDemand.Build(weather, parameters);
    var reference = YearSimulation.Run(demand, parameters,
        new GreedyHeatScheduler(parameters.MinimumLoadShare.Value), new SimulatedExecutor());
    var selected = reference.Hours.Select((hour, index) => (hour, index))
        .Where(x => x.hour.LocalTime.StartsWith(date, StringComparison.Ordinal)).ToArray();
    if (selected.Length != (int)parameters.HoursPerDay.Value)
        throw new InvalidDataException($"Expected a complete local day for {date}.");
    if (forcedStep is < 0 || forcedStep >= selected.Length)
        throw new ArgumentOutOfRangeException("--force-pause-step");

    var control = new DockerCliContainerControl();
    var executor = new DockerJobExecutor(control, options["--container"], cpuCores,
        job.SupportedControlLevels.Contains("throttle"));
    var scheduler = new GreedyHeatScheduler(parameters.MinimumLoadShare.Value);
    Console.WriteLine($"Replaying {date} for {job.Id}; press P for one forced pause. " +
        "Each hour uses an independent reference tank snapshot; achieved load is Docker CPU use, not measured heat.");
    try
    {
        for (int dayStep = 0; dayStep < selected.Length; dayStep++)
        {
            var (hour, index) = selected[dayStep];
            double tankBeforeKwh = index == 0 ? 0 : reference.Hours[index - 1].TankStoredKwh;
            bool keyPause = !Console.IsInputRedirected && Console.KeyAvailable
                && Console.ReadKey(true).Key == ConsoleKey.P;
            bool forcePause = forcedStep == dayStep || keyPause;
            var signal = new HeatSignal(forcePause ? 0 : hour.DemandKwh,
                forcePause ? parameters.TankCapacityKwh.Value : tankBeforeKwh,
                parameters.TankCapacityKwh.Value, parameters.ServerCount.Value,
                parameters.ServerElectricKw.Value * parameters.HeatRecoveryShare.Value
                    * parameters.HoursPerStep.Value);
            var load = SchedulerController.Apply(signal, scheduler, executor);
            string progress = job.ProgressFile is { Length: > 0 } path && File.Exists(path)
                ? File.ReadAllText(path).Trim() : "unavailable";
            Console.WriteLine($"{hour.LocalTime} demand={hour.DemandKwh:F2} kWh " +
                $"reference_tank_snapshot={tankBeforeKwh:F2} kWh target={load.TargetLoadPerServer:P0} " +
                $"observed_cpu_load={load.ActualLoadPerServer:P0} progress={progress}" +
                (forcePause ? " [forced pause]" : ""));
            Thread.Sleep(TimeSpan.FromSeconds(stepSeconds));
        }
    }
    finally
    {
        executor.Release();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    if (args.Length % 2 != 0) throw new ArgumentException(Usage());
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 0; i < args.Length; i += 2)
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException(Usage());
    foreach (string required in new[] { "--date", "--job", "--container", "--cpu-cores", "--step-seconds" })
        if (!options.ContainsKey(required)) throw new ArgumentException(Usage());
    if (options.Keys.Any(x => x is not ("--date" or "--job" or "--container" or "--cpu-cores"
        or "--step-seconds" or "--force-pause-step"))) throw new ArgumentException(Usage());
    return options;
}

static string Usage() => "Usage: dotnet run --project src/DigitalHeat.LiveRun -- " +
    "--date yyyy-MM-dd --job config/jobs/<job>.json --container <running-linux-container> " +
    "--cpu-cores <allowed-cores> --step-seconds <seconds> [--force-pause-step 0..23]";

static string FindRepositoryRoot()
{
    for (DirectoryInfo? d = new(Environment.CurrentDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
    throw new DirectoryNotFoundException("Run from the repository root.");
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
