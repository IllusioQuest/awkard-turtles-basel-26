using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>A scenario number: <c>{value, unit, source}</c> or <c>{value, unit, synthetic: true, note}</c>.</summary>
public sealed record ScenarioParameter(double Value, string Unit, string? Source, bool Synthetic, string? Note, JsonElement Raw);

public sealed record ScenarioType1(ScenarioParameter ShareOfHeatLedServerHours, ScenarioParameter DeadlineHours,
    ScenarioParameter ServerHoursPerJob);

public sealed record ScenarioSite(string Id, string Name, string Kind, string Canton, string CountryCode,
    IReadOnlyDictionary<string, ScenarioParameter> Parameters, ScenarioType1? Type1, JobPlacementPolicy Placement,
    JsonElement Demand, JsonElement Raw);

/// <summary>A loaded multi-site scenario (decision 0025 §8) with its sites, Type 1 specs and input files.</summary>
public sealed record NetworkScenario(string Id, string Description, string Timezone, int Year,
    IReadOnlyList<ScenarioSite> Sites, IReadOnlyList<NetworkSite> NetworkSites,
    IReadOnlyList<string> InputFiles, string ScenarioPath);

/// <summary>
/// Loads <c>config/network/*.json</c>. Every number must carry a source or be marked synthetic with a note.
/// Demand models: <c>case-linear</c> (Core HeatDemand.Build, the case formula: no second heat model),
/// <c>file</c> (CSV <c>time,space_heat_kwh,hot_water_kwh,temperature_c</c>, local Europe/Zurich, 24 values/day),
/// <c>constant</c> (annual heat spread evenly over the scenario's hours; the flat shape is synthetic).
/// </summary>
public static class NetworkScenarioLoader
{
    /// <summary>Hourly data, 24 nominal values per local day (AGENTS.md §5.5).</summary>
    public const double HoursPerStep = 1;
    public const int HoursPerDay = 24;

    private static readonly string[] SiteParameters =
    [
        "latitudeDeg", "longitudeDeg", "serverCount", "serverElectricKw", "heatRecoveryShare", "tankCapacityKwh",
        "minimumLoadShare", "electricityPriceChfPerKwh", "heatValueChfPerKwh"
    ];

    public static NetworkScenario Load(string scenarioPath, string repositoryRoot)
    {
        scenarioPath = Path.GetFullPath(scenarioPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(scenarioPath));
        var root = doc.RootElement.Clone();
        string id = Str(root, "id"), timezone = Str(root, "timezone");
        if (timezone != "Europe/Zurich") throw new InvalidDataException("Only Europe/Zurich local hours are supported.");
        int year = root.GetProperty("year").GetInt32();
        var inputs = new List<string> { scenarioPath };
        var sites = new List<ScenarioSite>();
        var networkSites = new List<NetworkSite>();
        var demandCache = new Dictionary<string, IReadOnlyList<DemandHour>>();
        IReadOnlyList<DemandHour>? axis = null;
        var siteElements = root.GetProperty("sites").EnumerateArray().ToArray();
        if (siteElements.Length == 0) throw new InvalidDataException("Scenario has no sites.");
        // Sites with constant demand need the time axis of another site or the calendar year.
        foreach (var pass in new[] { false, true })
            foreach (var s in siteElements)
            {
                bool constant = Str(s.GetProperty("demand"), "model") == "constant";
                if (constant != pass) continue;
                var parameters = SiteParameters.ToDictionary(k => k, k => Parameter(s, k));
                var demand = LoadDemand(s.GetProperty("demand"), repositoryRoot, year, axis, demandCache, inputs);
                axis ??= demand;
                ScenarioType1? type1 = s.TryGetProperty("type1", out var t1)
                    ? new ScenarioType1(Parameter(t1, "shareOfHeatLedServerHours"), Parameter(t1, "deadlineHours"),
                        Parameter(t1, "serverHoursPerJob"))
                    : null;
                var placement = Placement(s.GetProperty("placement"));
                var site = new ScenarioSite(Str(s, "id"), Str(s, "name"), Str(s, "kind"), Str(s, "canton"),
                    Str(s, "countryCode"), parameters, type1, placement, s.GetProperty("demand").Clone(), s.Clone());
                sites.Add(site);
                networkSites.Add(new NetworkSite(site.Id, site.Name, parameters["latitudeDeg"].Value,
                    parameters["longitudeDeg"].Value, site.CountryCode, parameters["serverCount"].Value,
                    parameters["serverElectricKw"].Value,
                    parameters["serverElectricKw"].Value * parameters["heatRecoveryShare"].Value * HoursPerStep,
                    parameters["tankCapacityKwh"].Value, parameters["minimumLoadShare"].Value, HoursPerStep,
                    parameters["electricityPriceChfPerKwh"].Value, parameters["heatValueChfPerKwh"].Value, demand));
            }
        // Restore file order (constant-demand sites were loaded in a second pass).
        var order = siteElements.Select(s => Str(s, "id")).ToList();
        if (order.Distinct(StringComparer.Ordinal).Count() != order.Count) throw new InvalidDataException("Duplicate site ids.");
        var sortedSites = sites.OrderBy(s => order.IndexOf(s.Id)).ToArray();
        var sortedNetwork = networkSites.OrderBy(s => order.IndexOf(s.Id)).ToArray();
        return new NetworkScenario(id, root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            timezone, year, sortedSites, sortedNetwork, inputs.Distinct().ToArray(), scenarioPath);
    }

    /// <summary>
    /// Type 1 jobs per day per site (decision 0025 §6, a labelled scenario): share × the site's yearly
    /// heat-led server-hours under the local greedy rule, spread evenly over the days, ÷ server-hours per job,
    /// rounded to whole jobs. <paramref name="shareOverride"/> replaces every site's share if given.
    /// </summary>
    public static IReadOnlyList<Type1Spec?> Type1Specs(NetworkScenario scenario, double? shareOverride = null)
    {
        int days = scenario.NetworkSites[0].Demand.Count / HoursPerDay;
        return scenario.Sites.Select((s, i) =>
        {
            if (s.Type1 is null) return null;
            double share = shareOverride ?? s.Type1.ShareOfHeatLedServerHours.Value;
            var site = scenario.NetworkSites[i];
            double heatLed = NetworkSimulation.Run([site], new LocalGreedyScheduler(), new PerfectForesightForecasts(), 1)
                .Sites[0].Summary.ServerHours;
            int jobs = (int)Math.Round(share * heatLed / days / s.Type1.ServerHoursPerJob.Value, MidpointRounding.AwayFromZero);
            return (Type1Spec?)new Type1Spec(jobs, s.Type1.ServerHoursPerJob.Value,
                checked((int)s.Type1.DeadlineHours.Value), s.Placement);
        }).ToArray();
    }

    private static IReadOnlyList<DemandHour> LoadDemand(JsonElement demand, string root, int year,
        IReadOnlyList<DemandHour>? axis, Dictionary<string, IReadOnlyList<DemandHour>> cache, List<string> inputs)
    {
        string model = Str(demand, "model");
        switch (model)
        {
            case "case-linear":
            {
                string weather = Full(root, Str(demand, "weatherFile")), parameters = Full(root, Str(demand, "parameters"));
                inputs.Add(weather); inputs.Add(parameters);
                string key = weather + "|" + parameters;
                if (!cache.TryGetValue(key, out var d))
                    cache[key] = d = HeatDemand.Build(WeatherCsv.Read(weather), ReferenceParameters.Load(parameters));
                return d;
            }
            case "file":
            {
                string file = Full(root, Str(demand, "file"));
                inputs.Add(file);
                return ReadDemandCsv(file);
            }
            case "constant":
            {
                var annual = Parameter(demand, "annualHeatKwh");
                var labels = axis?.Select(h => h.LocalTime).ToArray() ?? CalendarAxis(year);
                double perHour = annual.Value / labels.Length;
                return labels.Select(l => new DemandHour(l, double.NaN, false, perHour, 0)).ToArray();
            }
            default: throw new InvalidDataException($"Unknown demand model '{model}'.");
        }
    }

    /// <summary>24 nominal local hours per calendar day, as in the Open-Meteo data (decision 0008).</summary>
    private static string[] CalendarAxis(int year)
    {
        var labels = new List<string>();
        for (var day = new DateTime(year, 1, 1); day.Year == year; day = day.AddDays(1))
            for (int h = 0; h < HoursPerDay; h++)
                labels.Add(day.AddHours(h).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
        return labels.ToArray();
    }

    public static IReadOnlyList<DemandHour> ReadDemandCsv(string path)
    {
        var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var header = lines[0].Split(',');
        int time = Array.IndexOf(header, "time"), space = Array.IndexOf(header, "space_heat_kwh"),
            hot = Array.IndexOf(header, "hot_water_kwh"), temp = Array.IndexOf(header, "temperature_c");
        if (time < 0 || space < 0 || hot < 0) throw new InvalidDataException($"Demand CSV columns missing: {path}");
        return lines.Skip(1).Select(l =>
        {
            var f = l.Split(',');
            double s = double.Parse(f[space], CultureInfo.InvariantCulture);
            double t = temp >= 0 && f[temp].Length > 0 ? double.Parse(f[temp], CultureInfo.InvariantCulture) : double.NaN;
            return new DemandHour(f[time], t, s > 0, s, double.Parse(f[hot], CultureInfo.InvariantCulture));
        }).ToArray();
    }

    private static ScenarioParameter Parameter(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var p)) throw new InvalidDataException($"Missing parameter '{name}'.");
        double value = p.GetProperty("value").GetDouble();
        string unit = Str(p, "unit");
        string? source = p.TryGetProperty("source", out var s) ? s.GetString() : null;
        bool synthetic = p.TryGetProperty("synthetic", out var sy) && sy.GetBoolean();
        string? note = p.TryGetProperty("note", out var n) ? n.GetString() : null;
        bool sourced = !string.IsNullOrWhiteSpace(source) && !source!.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase);
        if (!double.IsFinite(value) || !(sourced || (synthetic && !string.IsNullOrWhiteSpace(note))))
            throw new InvalidDataException($"Parameter '{name}' needs a value and a source, or synthetic: true with a note.");
        return new ScenarioParameter(value, unit, source, synthetic, note, p.Clone());
    }

    private static JobPlacementPolicy Placement(JsonElement p)
    {
        string[]? countries = p.TryGetProperty("allowedCountryCodes", out var c)
            ? c.EnumerateArray().Select(x => x.GetString()!).ToArray() : null;
        var policy = new JobPlacementPolicy(Str(p, "mode"), Str(p, "source"), countries);
        policy.Validate();
        return policy;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.GetString() is { Length: > 0 } s ? s
            : throw new InvalidDataException($"Missing text field '{name}'.");

    private static string Full(string root, string relative) => Path.GetFullPath(Path.Combine(root, relative));
}
