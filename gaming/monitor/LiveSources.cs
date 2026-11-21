using System.Globalization;
using System.Text;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>Result of a live fetch: the hour (always complete) plus warnings for every fallback used.</summary>
public sealed record LiveHourResult(HourInputs Hour, IReadOnlyList<string> Warnings);

/// <summary>
/// Live mode: today's hourly temperature per site location from the Open-Meteo forecast API and the
/// current ElCom household price per site municipality. Any failure falls back to the committed
/// replay values for that site, with a warning. Network access is optional (demo must run offline).
/// </summary>
public sealed class LiveSources(MonitorSetup setup, HttpClient http)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10); // Lab UI timeout, not a service level.

    public static TimeZoneInfo Zurich()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
    }

    public async Task<LiveHourResult> FetchAsync(DateTimeOffset nowUtc, CancellationToken cancel = default)
    {
        var warnings = new List<string>();
        var startUtc = new DateTimeOffset(nowUtc.UtcDateTime.Ticks - nowUtc.UtcDateTime.Ticks % TimeSpan.TicksPerHour, TimeSpan.Zero);
        var local = TimeZoneInfo.ConvertTime(startUtc, Zurich());
        string label = local.ToString(MonitorConfig.LocalHourFormat, CultureInfo.InvariantCulture);
        int replayIndex = setup.SameCalendarHourIndex(label);
        var replay = setup.ReplayHour(replayIndex);

        var prices = new Dictionary<string, (double Chf, string Label)>(StringComparer.Ordinal);
        foreach (var id in setup.Config.Nodes.Select(n => n.ElcomMunicipality.Id).Distinct())
        {
            try { prices[id] = await ElcomPriceAsync(id, local.Year, cancel); }
            catch (Exception e) when (e is not OperationCanceledException || !cancel.IsCancellationRequested)
            { warnings.Add($"ElCom {id}: {Short(e)}; using committed price."); }
        }

        var sites = new Dictionary<string, SiteHour>(StringComparer.Ordinal);
        foreach (var (siteId, site) in setup.NetworkSites)
        {
            var node = setup.Config.Nodes.First(n => n.SiteId == siteId);
            var fallback = replay.Sites[siteId];
            double? liveTemperature = null;
            DemandHour demand = fallback.Demand;
            string demandSource = $"historical model {replay.LocalLabel} (not current demand)";
            try
            {
                var weather = await TodayTemperatureAsync(site.LatitudeDeg, site.LongitudeDeg, cancel);
                int hourIndex = weather.FindIndex(w => w.LocalTime == label);
                if (hourIndex < 0) throw new InvalidDataException($"forecast has no hour {label}");
                liveTemperature = weather[hourIndex].TemperatureC;
                if (node.LiveDemand == MonitorConfig.CaseFormula)
                {
                    // The case formula needs the whole local day (heating day = daily mean < threshold).
                    demand = HeatDemand.Build(weather, setup.Reference)[hourIndex];
                    demandSource = "live Open-Meteo + case formula";
                }
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancel.IsCancellationRequested)
            { warnings.Add($"Open-Meteo {siteId}: {Short(e)}; using replay."); }

            double price = fallback.ElectricityPriceChfPerKwh, heatValue = fallback.HeatValueChfPerKwh;
            string priceSource = fallback.PriceSource + " (fallback)";
            if (prices.TryGetValue(node.ElcomMunicipality.Id, out var p))
            {
                price = p.Chf;
                heatValue = p.Chf / setup.Reference.HeatPumpEfficiency.Value; // case: price ÷ heat-pump efficiency
                priceSource = p.Label + " (annual tariff, not spot price; H4 proxy for non-household sites)";
            }
            sites[siteId] = new SiteHour(demand, demandSource, liveTemperature, price, heatValue, priceSource,
                liveTemperature is null ? "Current forecast unavailable; no temperature substituted" : "Current Open-Meteo hourly forecast; not a local sensor");
        }
        return new LiveHourResult(new HourInputs("live", label, startUtc, sites), warnings);
    }

    /// <summary>Open-Meteo forecast, today in Europe/Zurich, hourly temperature_2m (same variable as the case).</summary>
    private async Task<List<WeatherHour>> TodayTemperatureAsync(double latitude, double longitude, CancellationToken cancel)
    {
        string url = setup.Config.OpenMeteoForecastEndpoint
            + "?latitude=" + latitude.ToString(CultureInfo.InvariantCulture)
            + "&longitude=" + longitude.ToString(CultureInfo.InvariantCulture)
            + "&hourly=temperature_2m&timezone=Europe%2FZurich&forecast_days=1&format=json";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);
        using var response = await http.GetAsync(url, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var hourly = doc.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time").EnumerateArray().Select(t => t.GetString()!).ToArray();
        var temps = hourly.GetProperty("temperature_2m").EnumerateArray()
            .Select(t => t.ValueKind == JsonValueKind.Number ? t.GetDouble() : double.NaN).ToArray();
        int hoursPerDay = checked((int)setup.Reference.HoursPerDay.Value);
        if (times.Length != hoursPerDay || temps.Length != hoursPerDay || temps.Any(t => !double.IsFinite(t)))
            throw new InvalidDataException($"expected {hoursPerDay} finite hourly values (DST day?)");
        return times.Zip(temps, (t, v) => new WeatherHour(t, v)).ToList();
    }

    /// <summary>
    /// ElCom GraphQL (same endpoint and price component as scripts/fetch_data.py), filtered by
    /// municipality instead of operator so the price follows the site's location. If several grid
    /// operators serve the municipality, the lowest operator number is used and the count is shown.
    /// </summary>
    private async Task<(double Chf, string Label)> ElcomPriceAsync(string municipality, int year, CancellationToken cancel)
    {
        string category = setup.Config.ElcomCategory;
        string query = "{ observations(locale: \"de\", filters: {period: [\"" + year + "\"], municipality: [\"" + municipality
            + "\"], category: [\"" + category + "\"]}) { ... on OperatorObservation { operator operatorLabel municipalityLabel "
            + "total: value(priceComponent: total) } } }";
        string payload = JsonSerializer.Serialize(new { query });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(setup.Config.ElcomEndpoint, content, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new InvalidDataException("API error: " + errors.ToString()[..Math.Min(120, errors.ToString().Length)]);
        var rows = doc.RootElement.GetProperty("data").GetProperty("observations").EnumerateArray()
            .Where(o => o.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number)
            .Select(o => (Operator: o.GetProperty("operator").GetString() ?? "", Label: o.GetProperty("operatorLabel").GetString() ?? "",
                Place: o.TryGetProperty("municipalityLabel", out var m) ? m.GetString() ?? municipality : municipality,
                RpPerKwh: o.GetProperty("total").GetDouble()))
            .OrderBy(o => int.TryParse(o.Operator, out var n) ? n : int.MaxValue).ToArray();
        if (rows.Length == 0) throw new InvalidDataException($"no {category} price for {year}");
        var first = rows[0];
        const double RappenPerFranc = 100;
        string label = $"ElCom {year} {category} {first.Place}, {first.Label}" + (rows.Length > 1 ? $" (1 of {rows.Length} operators)" : "");
        return (first.RpPerKwh / RappenPerFranc, label);
    }

    private static string Short(Exception e) => e is OperationCanceledException ? "timeout" : e.Message.Split('\n')[0];
}

/// <summary>Docker state of one node, probed with exact compose labels (as DockerDesktopRuntime does).</summary>
public sealed record DockerNodeState(bool ContainerRunning, bool? ApplicationRunning, string Detail,
    string? CpuPercent = null, string? MemoryUsage = null);

/// <summary>Read-only Docker probe: <c>docker ps</c> and <c>pgrep</c>. Never starts, stops or pauses anything.</summary>
public sealed class DockerProbe(MonitorSetup setup, IDockerCommandRunner docker)
{
    public IReadOnlyDictionary<string, DockerNodeState> Probe()
    {
        var app = setup.Profiles.Applications.Single(a => a.Id == setup.Config.ApplicationId);
        var result = new Dictionary<string, DockerNodeState>(StringComparer.Ordinal);
        var containers = new Dictionary<string, (string Context, string Container)>();
        foreach (var node in setup.Config.Nodes)
        {
            var profile = setup.Profiles.Nodes.Single(p => p.Id == node.NodeId);
            try
            {
                var ids = docker.Run(["--context", profile.DockerContext, "ps", "-q",
                        "--filter", $"label=com.docker.compose.project={profile.ComposeProject}",
                        "--filter", $"label=com.docker.compose.service={profile.ComposeService}"])
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (ids.Length != 1) { result[node.NodeId] = new(false, null, ids.Length == 0 ? "not running" : "ambiguous"); continue; }
                if (!System.Text.RegularExpressions.Regex.IsMatch(ids[0], "^[a-f0-9]{12,64}$"))
                    throw new InvalidDataException("Malformed container identity.");
                var state = docker.Run(["--context", profile.DockerContext, "inspect", "--format", "{{.State.Running}} {{.State.Paused}}", ids[0]]).Trim();
                if (state != "true false") { result[node.NodeId] = new(false, null, "not available (stopped or paused)"); continue; }
                bool? appRunning = null;
                string processDetail = "";
                if (!setup.IncompatibleNodes.ContainsKey(node.NodeId))
                {
                    // The API runner maps pgrep's clean no-match result to empty output; errors remain unknown.
                    try { appRunning = docker.Run(["--context", profile.DockerContext, "exec", ids[0], "pgrep", "-f", app.Executable]).Trim().Length > 0; }
                    catch (Exception) { processDetail = "; application probe unavailable"; }
                }
                result[node.NodeId] = new(true, appRunning, "running " + ids[0][..Math.Min(12, ids[0].Length)] + processDetail);
                containers[node.NodeId] = (profile.DockerContext, ids[0]);
            }
            catch (Exception e) { result[node.NodeId] = new(false, null, "docker error: " + e.Message.Split('\n')[0]); }
        }
        // One stats snapshot per engine; CPU/RAM are observations, never converted to watts or heat.
        foreach (var context in containers.Values.Select(v => v.Context).Distinct())
        {
            try
            {
                var ids = containers.Values.Where(v => v.Context == context).Select(v => v.Container).ToArray();
                var output = docker.Run(new[] { "--context", context, "stats", "--no-stream", "--format", "{{json .}}" }.Concat(ids).ToArray());
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    using var document = JsonDocument.Parse(line);
                    var row = document.RootElement;
                    string id = row.GetProperty("ID").GetString() ?? "";
                    if (id.Length == 0) continue;
                    var node = containers.SingleOrDefault(c => c.Value.Context == context && c.Value.Container.StartsWith(id, StringComparison.Ordinal));
                    if (node.Key is not null)
                        result[node.Key] = result[node.Key] with { CpuPercent = row.GetProperty("CPUPerc").GetString(), MemoryUsage = row.GetProperty("MemUsage").GetString() };
                }
            }
            catch (Exception)
            {
                foreach (var node in containers.Where(c => c.Value.Context == context))
                    result[node.Key] = result[node.Key] with { Detail = result[node.Key].Detail + "; resource stats unavailable" };
            }
        }
        return result;
    }
}
