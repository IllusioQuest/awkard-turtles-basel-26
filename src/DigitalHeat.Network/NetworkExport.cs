using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DigitalHeat.Network;

public sealed record NetworkTotals(double TotalHeatDemandKwh, double ServerHeatUsedKwh, double Coverage,
    double DumpedHeatKwh, double ServerHours, double Utilisation, double MovedServerHours, double? MissedDeadlinePercent);

/// <summary>Run settings written to <c>run.json</c> (decision 0025 §7).</summary>
public sealed record NetworkRunInfo(double FillerAvailability, double? Type1ShareOverride,
    IReadOnlyList<Type1Spec?> Type1Specs, string Command);

/// <summary>
/// Writes the map export contract of decision 0025 §7 into <c>results/network/&lt;scenario&gt;/&lt;strategy&gt;/</c>:
/// run.json, sites.json, site-hours.json, flows.json, summary.json. Times are local Europe/Zurich hours.
/// </summary>
public static class NetworkExport
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public static string Write(string repositoryRoot, NetworkScenario scenario, NetworkResult result, NetworkRunInfo info)
    {
        string dir = Path.Combine(repositoryRoot, "results", "network", scenario.Id, result.SchedulerId);
        Directory.CreateDirectory(dir);
        string Rel(string path) => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

        var run = new JsonObject
        {
            ["scenarioId"] = scenario.Id,
            ["scenarioFile"] = Rel(scenario.ScenarioPath),
            ["gitCommit"] = GitCommit(repositoryRoot),
            ["strategy"] = result.SchedulerId,
            ["strategyLabel"] = result.SchedulerLabel,
            ["forecast"] = result.ForecastLabel,
            ["horizonHours"] = result.HorizonHours,
            ["fillerAvailabilityPhi"] = info.FillerAvailability,
            ["type1ShareOverride"] = info.Type1ShareOverride,
            ["type1JobsPerDay"] = new JsonObject(scenario.Sites.Select((s, i) =>
                KeyValuePair.Create(s.Id, (JsonNode?)(info.Type1Specs[i]?.JobsPerDay)))),
            ["timezone"] = scenario.Timezone,
            ["command"] = info.Command,
            ["inputs"] = new JsonArray(scenario.InputFiles.Select(f => (JsonNode)new JsonObject
                { ["path"] = Rel(f), ["sha256"] = Sha256(f) }).ToArray()),
            ["notes"] = "Type 1 volume, transfer costs and synthetic site values are scenarios, not findings " +
                        "(decision 0025). Moves are simulated flows only."
        };
        File.WriteAllText(Path.Combine(dir, "run.json"), run.ToJsonString(Indented));

        var sites = new JsonArray(scenario.Sites.Select((s, i) =>
        {
            var o = new JsonObject
            {
                ["id"] = s.Id, ["name"] = s.Name, ["kind"] = s.Kind, ["canton"] = s.Canton, ["countryCode"] = s.CountryCode
            };
            foreach (var (name, p) in s.Parameters) o[name] = JsonNode.Parse(p.Raw.GetRawText());
            o["demandModel"] = JsonNode.Parse(s.Demand.GetRawText());
            if (info.Type1Specs[i] is { } spec)
                o["type1JobsPerDay"] = new JsonObject
                {
                    ["value"] = spec.JobsPerDay, ["unit"] = "jobs/day", ["synthetic"] = true,
                    ["note"] = "Scenario (decision 0025 §6): share of the site's heat-led server-hours ÷ days ÷ server-hours per job."
                };
            return (JsonNode)o;
        }).ToArray());
        File.WriteAllText(Path.Combine(dir, "sites.json"), sites.ToJsonString(Indented));

        var w = result.Work;
        var bySite = new JsonObject();
        for (int i = 0; i < result.Sites.Count; i++)
        {
            var h = result.Sites[i].Hours;
            double[] Zero() => new double[h.Count];
            bySite[result.Sites[i].Site.Id] = new JsonObject
            {
                ["demandKwh"] = Arr(h.Select(x => x.DemandKwh)),
                ["usedKwh"] = Arr(h.Select(x => x.UsedServerHeatKwh)),
                ["dumpedKwh"] = Arr(h.Select(x => x.DumpedHeatKwh)),
                ["backupKwh"] = Arr(h.Select(x => x.BackupHeatKwh)),
                ["tankKwh"] = Arr(h.Select(x => x.TankStoredKwh)),
                ["loadShare"] = Arr(h.Select(x => x.ActualLoadPerServer)),
                ["type1ServerHours"] = Arr(w?.Type1ServerHoursBySite[i] ?? Zero()),
                ["type2ServerHours"] = Arr(w?.Type2ServerHoursBySite[i] ?? Zero()),
                ["missedServerHours"] = Arr(w?.MissedServerHoursBySite[i] ?? Zero())
            };
        }
        var siteHours = new JsonObject
        {
            ["strategy"] = result.SchedulerId,
            ["timezone"] = scenario.Timezone,
            ["localTimes"] = new JsonArray(result.Sites[0].Hours.Select(x => (JsonNode)x.LocalTime).ToArray()),
            ["sites"] = bySite
        };
        File.WriteAllText(Path.Combine(dir, "site-hours.json"), siteHours.ToJsonString(Compact));

        var ids = scenario.NetworkSites.Select(s => s.Id).ToArray();
        var flows = (w?.Flows ?? [])
            .GroupBy(f => (f.Hour, f.FromSite, f.ToSite, f.Type))
            .OrderBy(g => g.Key.Hour).ThenBy(g => g.Key.FromSite).ThenBy(g => g.Key.ToSite)
            .Select(g => (JsonNode)new JsonArray(g.Key.Hour, ids[g.Key.FromSite], ids[g.Key.ToSite], g.Key.Type,
                Math.Round(g.Sum(f => f.ServerHours), 3)))
            .ToArray();
        File.WriteAllText(Path.Combine(dir, "flows.json"), new JsonObject
        {
            ["columns"] = new JsonArray("hour", "fromSiteId", "toSiteId", "type", "serverHours"),
            ["rows"] = new JsonArray(flows)
        }.ToJsonString(Compact));

        File.WriteAllText(Path.Combine(dir, "summary.json"), JsonSerializer.Serialize(new
        {
            strategy = result.SchedulerId,
            network = Network(result),
            type1 = w?.Type1,
            sites = result.Sites.Select((s, i) => new
            {
                s.Summary,
                type1ServerHours = w?.Type1ServerHoursBySite[i].Sum(),
                type2ServerHours = w?.Type2ServerHoursBySite[i].Sum(),
                missedServerHours = w?.MissedServerHoursBySite[i].Sum()
            })
        }, Indented));
        return dir;
    }

    /// <summary>Network totals: same definitions as the per-site summary, summed over sites.</summary>
    public static NetworkTotals Network(NetworkResult r)
    {
        double demand = r.Sites.Sum(s => s.Summary.TotalHeatDemandKwh), used = r.Sites.Sum(s => s.Summary.ServerHeatUsedKwh);
        double serverHours = r.Sites.Sum(s => s.Summary.ServerHours);
        double installed = r.Sites.Sum(s => s.Site.ServerCount * s.Hours.Count * s.Site.HoursPerStep);
        return new NetworkTotals(demand, used, demand > 0 ? used / demand : 0,
            r.Sites.Sum(s => s.Summary.DumpedHeatKwh), serverHours, installed > 0 ? serverHours / installed : 0,
            r.Work?.Type1.MovedServerHours ?? 0, r.Work?.Type1.MissedDeadlinePercent);
    }

    private static JsonArray Arr(IEnumerable<double> values) =>
        new(values.Select(v => (JsonNode)Math.Round(v, 3)).ToArray());

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string? GitCommit(string root)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false });
            string? sha = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit();
            return string.IsNullOrEmpty(sha) ? null : sha;
        }
        catch (Exception) { return null; }
    }
}
