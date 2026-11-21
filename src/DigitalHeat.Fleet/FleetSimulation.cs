using System.Diagnostics;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>A finished run: the engine result plus the resolved inputs needed for the output files (§11).</summary>
public sealed class FleetRun
{
    public required Scenario Scenario { get; init; }
    public required FleetRunResult Result { get; init; }
    public required RunCalendar Calendar { get; init; }
    public required ServerParams ServerParams { get; init; }
    public required IReadOnlyList<EngineHouse> Houses { get; init; }
    public required IReadOnlyList<JobType> JobTypes { get; init; }
    public required IReadOnlyList<JobRequest> Jobs { get; init; }
    public required string SchedulerId { get; init; }
    public required string PricingId { get; init; }
    public required string ForecastKind { get; init; }
    public required string ServerPolicy { get; init; }
    /// <summary>Absolute path of the house file, null when the houses were generated in process.</summary>
    public string? HouseFilePath { get; init; }
    /// <summary>Absolute path of the jobs-input.csv when demand.from_file was used.</summary>
    public string? DemandFilePath { get; init; }
    public long Seed => Scenario.Run.Seed;
    /// <summary>Wall time in seconds per phase (load houses, weather and demand, job generation, engine); not deterministic.</summary>
    public required IReadOnlyDictionary<string, double> PhaseSeconds { get; init; }
}

/// <summary>Library entry point (§4.4): builds all inputs from a scenario and runs the engine. No global state, no console output.</summary>
public static class FleetSimulation
{
    public static FleetRun Run(Scenario scenario, IServerCountPolicy? servers = null, IFleetScheduler? scheduler = null,
        IPricing? pricing = null)
    {
        var phases = new Dictionary<string, double>();
        var sw = Stopwatch.StartNew();
        void Lap(string name) { phases[name] = sw.Elapsed.TotalSeconds; sw.Restart(); }

        string root = scenario.RepoRoot;
        var baseline = ReferenceParameters.Load(Path.Combine(root, HouseDemandBuilder.ReferenceHouseConfig));
        var sp = new ServerParams(
            scenario.Servers.MaxElectricKw ?? baseline.ServerElectricKw.Value,
            scenario.Servers.HeatRecoveryShare ?? baseline.HeatRecoveryShare.Value,
            baseline.HeatPumpEfficiency.Value);
        var calendar = RunCalendar.From(scenario.Run);

        IReadOnlyList<HouseSpec> specs;
        string? housePath = null;
        if (scenario.Houses.File is { } hf)
        {
            housePath = Path.GetFullPath(scenario.Resolve(hf));
            specs = HouseFiles.Read(housePath).Houses;
        }
        else if (scenario.Houses.Generate is { } gen)
            specs = HouseGenerator.Generate(gen, root).Houses;
        else throw new InvalidDataException("Scenario needs houses.file or houses.generate.");
        Lap("load_houses");

        var weather = new WeatherStore(root);
        weather.Preload(specs.Select(h => h.WeatherRef), scenario.Forecast.Kind == "archived" ? specs.Select(h => h.CellId) : []);
        var builder = new HouseDemandBuilder(root, weather, baseline);
        var demands = builder.BuildAll(specs, calendar);
        var policy = servers ?? ServerCountPolicies.FromConfig(scenario.Servers, sp);
        var houses = new List<EngineHouse>(specs.Count);
        for (int i = 0; i < specs.Count; i++)
            houses.Add(new EngineHouse(specs[i], policy.ServerCount(specs[i], demands[i].Summary), demands[i]));
        var forecast = ForecastProviders.Create(scenario.Forecast.Kind, specs, calendar, demands, weather, baseline);
        Lap("weather_and_demand");

        var types = JobTypeLoader.LoadAll(scenario);
        string? demandPath = null;
        List<JobRequest> jobs;
        if (scenario.Demand.FromFile is { } df)
        {
            demandPath = Path.GetFullPath(scenario.Resolve(df));
            jobs = JobsInputCsv.Read(demandPath);
        }
        else jobs = DemandGenerator.Generate(types, calendar, scenario.Run.Seed, root);
        Lap("jobs");

        var sched = scheduler ?? (scenario.Scheduler.Id == "mesh-heat-first"
            ? new HeatFirstFleetScheduler(scenario.Scheduler)
            : throw new InvalidDataException($"Unknown scheduler.id '{scenario.Scheduler.Id}'."));
        var price = pricing ?? PricingFactory.FromConfig(scenario.Pricing, scenario.Run.Seed);
        var inputs = new EngineInputs
        {
            EndMs = calendar.EndMs, StartHourOfWeek = calendar.StartHourOfWeek,
            Mode = scenario.Run.Mode switch
            {
                "event" => RunMode.Event, "tick" => RunMode.Tick,
                var m => throw new InvalidDataException($"Unknown run.mode '{m}' (event | tick).")
            },
            TickMs = scenario.Run.TickMs, ServerParams = sp, Houses = houses, JobTypes = types, Jobs = jobs,
            Scheduler = sched, Pricing = price, Forecast = forecast,
            CaptureJobEvents = scenario.Logging.JobEvents,
            Timeseries = scenario.Logging.HouseTimeseries switch
            {
                "none" => TimeseriesMode.None, "daily" => TimeseriesMode.Daily, "hourly" => TimeseriesMode.Hourly,
                "events" => TimeseriesMode.Events,
                var m => throw new InvalidDataException($"Unknown logging.house_timeseries '{m}'.")
            },
        };
        Lap("setup");
        var result = new FleetEngine(inputs).Run();
        Lap("engine");

        return new FleetRun
        {
            Scenario = scenario, Result = result, Calendar = calendar, ServerParams = sp, Houses = houses,
            JobTypes = types, Jobs = jobs, SchedulerId = sched.Id, PricingId = price.Id, ForecastKind = forecast.Kind,
            ServerPolicy = servers is null ? scenario.Servers.Policy : "custom", HouseFilePath = housePath,
            DemandFilePath = demandPath, PhaseSeconds = phases,
        };
    }
}
