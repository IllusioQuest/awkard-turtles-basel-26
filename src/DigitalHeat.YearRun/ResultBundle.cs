using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.YearRun;

/// <summary>Versioned file adapter; all calculated values come directly from YearResult.</summary>
public static class ResultBundle
{
    public const int SchemaVersion = 1;
    public const string SiteId = "reference-house";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static void Write(string output, string root, string scenarioPath, YearResult result,
        ReferenceParameters parameters, JobScenario scenario)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Export directory must be empty; choose a new run directory.");
        string scenarioRelative = RelativeInput(root, scenarioPath);
        string[] inputPaths = ["config/reference-house.json", scenarioRelative,
            "data/raw/open-meteo/basel-2025-hourly.csv", "data/raw/elcom/iwb-basel-h4-2026.json"];
        var inputs = inputPaths.Distinct(StringComparer.Ordinal).Select(path => new
        {
            Path = path,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))
                .ToLowerInvariant()
        }).ToArray();
        string? revision = Git(root, "rev-parse", "HEAD");
        string? status = Git(root, "status", "--porcelain", "--untracked-files=normal");
        var run = new
        {
            SchemaVersion, Timezone = "Europe/Zurich",
            TimeConvention = "24 nominal local hours per day; timestamps have no UTC offset",
            StepHours = parameters.HoursPerStep.Value,
            GitCommit = revision, GitDirty = status is null ? (bool?)null : status.Length != 0,
            CodeProvenance = revision is null || status is null ? "unknown"
                : status.Length != 0 ? "dirty_not_reproducible_from_commit" : "clean_commit",
            Seed = (int?)null, Randomness = "none",
            Command = new[] { "dotnet", "run", "--project", "src/DigitalHeat.YearRun", "--",
                ".", scenarioRelative, "--output", "results/replay" },
            CommandWorkingDirectory = "repository_root",
            Inputs = inputs, FlowsStatus = "not_modelled"
        };
        Directory.CreateDirectory(Path.Combine(output, "sites", SiteId));
        WriteJson(Path.Combine(output, "summary.json"), new
        {
            SchemaVersion,
            Sites = new[] { new { SiteId, HourlyPath = $"sites/{SiteId}/hourly.csv", Summary = SummaryV1(result.Summary) } }
        });
        WriteJson(Path.Combine(output, "parameters.json"), new
        {
            SchemaVersion, SiteId, ReferenceParameters = parameters, JobScenario = scenario
        });
        WriteJson(Path.Combine(output, "run.json"), run);
        WriteHourly(Path.Combine(output, "sites", SiteId, "hourly.csv"), result.Hours);
        if (scenario.Mode == "mixed")
        {
            var jobs = result.Summary.Jobs ?? throw new InvalidDataException("Mixed run has no job summary.");
            WriteJson(Path.Combine(output, "jobs.json"), new
            {
                SchemaVersion, SiteId, Mode = scenario.Mode, ScenarioPath = scenarioRelative,
                TotalServerHours = result.Summary.ServerHours,
                TotalComputingIncomeChf = result.Summary.ComputingIncomeChf,
                ByType = jobs.ByType.Select(type => new
                {
                    type.Id, type.WorkUnit, type.Arrived, type.DueWithinHorizon,
                    type.Completed, type.Missed, type.Backlog, type.WorkUnitsProcessed,
                    type.BacklogWorkUnits, type.ServerHours, type.ComputingIncomeChf,
                    type.MissedDeadlinePercent
                }),
                Filler = jobs.Filler
            });
        }
        File.WriteAllText(Path.Combine(output, "flows.csv"),
            "local_time,job_id,from_site_id,to_site_id,work_units,work_unit\n", new UTF8Encoding(false));
    }

    public static void WriteHourly(string path, IReadOnlyList<HourResult> hours)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        writer.WriteLine("local_time,heat_demand_kwh,space_heat_kwh,hot_water_kwh,target_load_per_server,actual_load_per_server,produced_heat_kwh,direct_used_kwh,tank_charge_kwh,tank_discharge_kwh,tank_stored_kwh,dumped_heat_kwh,backup_heat_kwh,server_electric_kwh");
        foreach (var h in hours)
        {
            var values = new[] { h.DemandKwh, h.SpaceHeatKwh, h.HotWaterKwh, h.TargetLoadPerServer,
                h.ActualLoadPerServer, h.ProducedHeatKwh, h.DirectUsedKwh, h.TankChargeKwh,
                h.TankDischargeKwh, h.TankStoredKwh, h.DumpedHeatKwh, h.BackupHeatKwh, h.ServerElectricKwh };
            writer.WriteLine(Csv(h.LocalTime) + "," + string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        }
    }

    private static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    // Explicit v1 projection prevents new core metrics silently changing the export contract.
    private static object SummaryV1(YearSummary s) => new
    {
        s.ServerCount, s.HeatingDays, s.Hours, s.SpaceHeatDemandKwh, s.HotWaterDemandKwh,
        s.TotalHeatDemandKwh, s.ServerHeatUsedKwh, s.BackupHeatKwh, s.DumpedHeatKwh,
        s.ServerHours, s.Utilisation, s.Coverage, s.ElectricityCostChf, s.HeatValueChf,
        s.ComputingIncomeChf, s.BreakEvenChfPerServerHour, s.MissedDeadlines,
        s.JobDemandAssumption, s.ProfitabilityNote,
        Jobs = s.Jobs is null ? null : new
        {
            s.Jobs.Arrived, s.Jobs.DueWithinHorizon, s.Jobs.Completed, s.Jobs.Missed,
            s.Jobs.Backlog, s.Jobs.MissedDeadlinePercent,
            ByType = s.Jobs.ByType.Select(j => new { j.Id, j.WorkUnit, j.Arrived,
                j.DueWithinHorizon, j.Completed, j.Missed, j.Backlog,
                j.WorkUnitsProcessed, j.BacklogWorkUnits })
        }
    };

    private static void WriteJson(string path, object value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, JsonOptions) + "\n", new UTF8Encoding(false));

    private static string RelativeInput(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
            throw new ArgumentException("Versioned exports require scenario inputs inside the repository.");
        return relative;
    }

    private static string? Git(string root, params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = root, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return null;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000)) { process.Kill(entireProcessTree: true); return null; }
            Task.WaitAll(stdout, stderr);
            return process.ExitCode == 0 ? stdout.Result.Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}
