using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DigitalHeat.Fleet;

/// <summary>
/// Writes the output files of a run (§11). Invariant culture, doubles in round-trip "R" format, fixed row
/// order, "\n" line endings: the same scenario gives byte-identical files except the timing and git fields of run.json.
/// </summary>
public static class FleetOutput
{
    /// <summary>§15, verbatim, copied into run.json.</summary>
    public static readonly string[] KnownSimplifications =
    [
        "Servers are abstract 1 kW GPU servers with one load value; energy per work unit is from GPU benchmarks (research simulation: CPU placeholder) and independent of load.",
        "No outages, no network or bandwidth model (transfer is the scheduler's task).",
        "Hot water is flat; heat demand uses the case's linear formula; the tank is lossless.",
        "Arrivals are Poisson (real LLM traffic is burstier, BurstGPT CV 2.2 per minute).",
        "Job sizes are known when a job arrives.",
        "Pricing is a scenario model and may not represent actual customer contracts; this is a simulation, not a live execution.",
    ];

    // Seed offsets that MockLoadPricing uses (Policies/Pricing.cs).

    private static string R(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string R(double? d) => d is { } v ? R(v) : "";
    private static string I(long d) => d.ToString(CultureInfo.InvariantCulture);
    private static string B(bool b) => b ? "true" : "false";

    public static void Write(FleetRun run, string dir)
    {
        Directory.CreateDirectory(dir);
        var res = run.Result;
        var sp = run.ServerParams;

        JobsInputCsv.Write(Path.Combine(dir, "jobs-input.csv"), run.Jobs);
        WriteLines(Path.Combine(dir, "jobs.csv"), JobsCsv(run));
        string eventsPath = Path.Combine(dir, "job-events.csv");
        if (run.Scenario.Logging.JobEvents) WriteLines(eventsPath, JobEventsCsv(run));
        else if (File.Exists(eventsPath)) File.Delete(eventsPath);
        WriteLines(Path.Combine(dir, "houses-summary.csv"), HousesCsv(run));
        WriteLines(Path.Combine(dir, "network-hourly.csv"), NetworkCsv(run));
        string tsPath = Path.Combine(dir, "houses-timeseries.csv");
        if (res.HouseTimeseries.Count > 0) WriteLines(tsPath, TimeseriesCsv(run));
        else if (File.Exists(tsPath)) File.Delete(tsPath);

        File.WriteAllText(Path.Combine(dir, "run.json"), RunJson(run, dir).ToJsonString(Pretty).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private static void WriteLines(string path, IEnumerable<string> lines)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        foreach (var l in lines) w.WriteLine(l);
    }

    // ---------------------------------------------------------------- account columns (shared by summary and timeseries)

    private static readonly (string Name, Func<EnergyAccount, double> Get)[] AccountColumns =
    [
        ("space_heat_demand_kwh", a => a.SpaceHeatKwh), ("hot_water_demand_kwh", a => a.HotWaterKwh),
        ("heat_demand_kwh", a => a.DemandKwh),
        ("electric_overhead_kwh", a => a.ElectricOverheadKwh), ("electric_t1_kwh", a => a.ElectricT1Kwh),
        ("electric_t2_kwh", a => a.ElectricT2Kwh), ("electric_kwh", a => a.ElectricKwh),
        ("heat_produced_kwh", a => a.ProducedKwh), ("heat_direct_kwh", a => a.DirectKwh),
        ("tank_charge_kwh", a => a.ChargeKwh), ("tank_discharge_kwh", a => a.DischargeKwh),
        ("heat_used_kwh", a => a.UsedKwh), ("heat_dumped_kwh", a => a.DumpedKwh), ("heat_backup_kwh", a => a.BackupKwh),
        ("units_late", a => a.UnitsLate), ("late_work_kwh", a => a.LateWorkKwh), ("pauses", a => a.Pauses),
        ("jobs_on_time", a => a.JobsOnTime), ("deadlines_missed", a => a.DeadlinesMissed),
        ("income_t1_chf", a => a.IncomeT1Chf), ("income_t2_chf", a => a.IncomeT2Chf),
        ("electricity_chf", a => a.ElectricityChf),
    ];

    private static IEnumerable<string> AccountValues(EnergyAccount a) => AccountColumns.Select(c => R(c.Get(a)));

    // houses-summary has its own electricity_cost_chf column, so the account's electricity_chf is left out there.
    private static readonly (string Name, Func<EnergyAccount, double> Get)[] SummaryAccountColumns =
        AccountColumns.Where(c => c.Name != "electricity_chf").ToArray();

    // ---------------------------------------------------------------- jobs

    private static IEnumerable<string> JobEventsCsv(FleetRun run)
    {
        yield return "time_ms,house_id,job_id,job_type,chunk_id,kind";
        foreach (var e in run.Result.JobEvents)
            yield return string.Join(',', I(e.TimeMs), CsvField(e.HouseId), CsvField(e.JobId), CsvField(e.JobType),
                e.ChunkId is { } chunk ? I(chunk) : "", CsvField(e.Kind));
    }

    private static string CsvField(string value)
    {
        if (value.Contains('\n') || value.Contains('\r')) throw new InvalidDataException("Job event identifiers cannot contain line breaks.");
        return value.Contains(',') || value.Contains('"') ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    }

    private static IEnumerable<string> JobsCsv(FleetRun run)
    {
        yield return string.Join(',', JobsInputCsv.Columns.Concat(
        [
            "status", "quote_chf", "accepted", "first_start_ms", "finish_ms", "deadline_missed", "lateness_s", "chunks",
            "pauses", "stops", "moves", "houses_used", "units_done", "units_late", "income_chf", "energy_kwh"
        ]));
        foreach (var j in run.Result.Jobs)
        {
            var q = j.Request;
            yield return string.Join(',', q.JobKey, q.TypeId, I(q.TypeIndex), I(q.ArrivalMs), I(q.DeadlineMs), R(q.WorkUnits),
                I(q.BatchSize), q.OriginKind, R(q.OriginLatDeg), R(q.OriginLonDeg), q.ClientId ?? "", q.OriginCanton ?? "",
                j.Status, R(j.QuoteChf), B(j.Accepted), j.FirstStartMs is { } f ? I(f) : "", j.FinishMs is { } e ? I(e) : "",
                B(j.DeadlineMissed), R(j.LatenessS), I(j.Chunks), I(j.Pauses), I(j.Stops), I(j.Moves), I(j.HousesUsed),
                R(j.UnitsDone), R(j.UnitsLate), R(j.IncomeChf), R(j.EnergyKwh));
        }
    }

    // ---------------------------------------------------------------- houses

    private static IEnumerable<string> HousesCsv(FleetRun run)
    {
        var sp = run.ServerParams;
        yield return string.Join(',', new[] { "house_id", "cell_id", "servers", "heating_days" }
            .Concat(SummaryAccountColumns.Select(c => c.Name))
            .Concat(["coverage", "tank_final_kwh", "utilisation", "busy_share", "server_hours_t1", "server_hours_t2",
                "electricity_cost_chf", "heat_value_chf", "income_chf", "margin_chf", "break_even_chf_per_server_hour"]));
        foreach (var h in run.Result.Houses)
        {
            var a = h.Account;
            yield return string.Join(',', new[] { h.Spec.HouseId, h.Spec.CellId, I(h.ServerCount), I(h.HeatingDays) }
                .Concat(SummaryAccountColumns.Select(c => R(c.Get(a))))
                .Concat([R(h.Coverage), R(h.TankFinalKwh), R(h.Utilisation), R(h.BusyShare),
                    R(a.ElectricT1Kwh / sp.MaxElectricKw), R(a.ElectricT2Kwh / sp.MaxElectricKw),
                    R(h.ElectricityChf), R(h.HeatValueChf(sp)), R(h.IncomeChf), R(h.MarginChf(sp)),
                    R(h.BreakEvenChfPerServerHour(sp))]));
        }
    }

    private static IEnumerable<string> TimeseriesCsv(FleetRun run)
    {
        yield return string.Join(',', new[] { "house_id", "period_start_ms", "period_end_ms" }
            .Concat(AccountColumns.Select(c => c.Name)).Concat(["tank_end_kwh", "heat_value_chf"]));
        var sp = run.ServerParams;
        foreach (var r in run.Result.HouseTimeseries)
        {
            var spec = run.Houses[r.HouseIndex].Spec;
            // Same formula as HouseResult.HeatValueChf: used heat × house price ÷ heat-pump efficiency (case §10).
            double heatValue = r.Account.UsedKwh * spec.ElectricityPriceChfPerKwh / sp.HeatPumpEfficiency;
            yield return string.Join(',', new[] { spec.HouseId, I(r.StartMs), I(r.EndMs) }
                .Concat(AccountValues(r.Account)).Concat([R(r.TankEndKwh), R(heatValue)]));
        }
    }

    // ---------------------------------------------------------------- network

    private static IEnumerable<string> NetworkCsv(FleetRun run)
    {
        yield return string.Join(',', "hour", "heat_demand_kwh", "heat_produced_kwh", "heat_direct_kwh", "tank_charge_kwh",
            "tank_discharge_kwh", "heat_used_kwh", "heat_dumped_kwh", "heat_backup_kwh", "electric_kw_mean", "running_chunks",
            "queue_length_t1", "queue_length_t2", "remaining_kwh_t1", "remaining_kwh_t2", "arrivals", "rejections",
            "completions", "misses", "mean_t2_quote_chf", "electric_kwh", "income_t1_chf", "income_t2_chf", "electricity_chf");
        foreach (var n in run.Result.NetworkHours)
        {
            var a = n.Account;
            yield return string.Join(',', I(n.Hour), R(a.DemandKwh), R(a.ProducedKwh), R(a.DirectKwh), R(a.ChargeKwh),
                R(a.DischargeKwh), R(a.UsedKwh), R(a.DumpedKwh), R(a.BackupKwh), R(n.ElectricKwMean), I(n.RunningChunks),
                I(n.QueueLengthT1), I(n.QueueLengthT2), R(n.RemainingKwhT1), R(n.RemainingKwhT2), I(n.Arrivals),
                I(n.Rejections), I(n.Completions), I(n.Misses), R(n.MeanT2QuoteChf), R(a.ElectricKwh), R(a.IncomeT1Chf),
                R(a.IncomeT2Chf), R(a.ElectricityChf));
        }
    }

    // ---------------------------------------------------------------- run.json

    private static JsonObject RunJson(FleetRun run, string dir)
    {
        var res = run.Result;
        var sc = run.Scenario;
        var seeds = new JsonObject();
        foreach (var t in run.JobTypes) seeds[t.Id] = sc.Run.Seed + t.Index;
        seeds["pricing_accept"] = sc.Run.Seed + MockLoadPricing.AcceptSeedOffset;
        seeds["pricing_reference_noise"] = sc.Run.Seed + MockLoadPricing.RefSeedOffset;

        var inputs = new JsonObject();
        if (run.HouseFilePath is { } hp) inputs["house_file"] = FileEntry(sc, hp);
        else inputs["house_file"] = null;
        var jt = new JsonObject();
        foreach (var t in run.JobTypes) jt[t.Id] = FileEntry(sc, t.SourcePath);
        inputs["job_type_files"] = jt;
        inputs["jobs_input_csv"] = new JsonObject
        {
            ["path"] = "jobs-input.csv",
            ["sha256"] = Sha256(Path.Combine(dir, "jobs-input.csv")),
            ["from_demand_file"] = run.DemandFilePath is { } dp ? Relative(sc, dp) : null,
        };

        var volumes = new JsonObject();
        foreach (var t in run.JobTypes)
            volumes[t.Id] = new JsonObject
            {
                ["jobs_per_day"] = t.Volume.JobsPerDay, ["derivation"] = t.Volume.Derivation, ["source"] = t.Volume.Source,
            };

        var timing = new JsonObject();
        foreach (var kv in run.PhaseSeconds) timing[kv.Key + "_s"] = kv.Value;

        return new JsonObject
        {
            ["scenario"] = JsonNode.Parse(sc.ToJson()),
            ["scenario_name"] = sc.Name,
            ["scheduler"] = run.SchedulerId,
            ["pricing"] = run.PricingId,
            ["forecast"] = run.ForecastKind,
            ["server_policy"] = run.ServerPolicy,
            ["run_seed"] = sc.Run.Seed,
            ["stream_seeds"] = seeds,
            ["git_commit"] = GitCommit(sc.RepoRoot),
            ["start_local"] = run.Calendar.StartLocal,
            ["end_local"] = EndLocal(run.Calendar),
            ["run_hours"] = run.Calendar.RunHours,
            ["houses"] = run.Houses.Count,
            ["servers"] = run.Houses.Sum(h => h.ServerCount),
            ["jobs"] = run.Jobs.Count,
            ["event_count"] = res.EventCount,
            ["scheduler_calls"] = res.SchedulerCalls,
            ["wall_time_s"] = res.WallTimeS,
            ["events_per_second"] = res.WallTimeS > 0 ? res.EventCount / res.WallTimeS : 0,
            ["phase_wall_time"] = timing,
            ["inputs"] = inputs,
            ["job_type_volumes"] = volumes,
            ["known_simplifications"] = new JsonArray(KnownSimplifications.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
        };
    }

    private static string EndLocal(RunCalendar c) =>
        new DateTime(RunCalendar.DataYear, 1, 1).AddHours(c.OffsetHour + c.RunHours)
            .ToString(RunCalendar.LocalTimeFormat, CultureInfo.InvariantCulture);

    private static string Relative(Scenario sc, string path) =>
        Path.GetRelativePath(sc.RepoRoot, path).Replace('\\', '/');

    private static JsonObject FileEntry(Scenario sc, string path) =>
        new() { ["path"] = Relative(sc, path), ["sha256"] = Sha256(path) };

    private static string Sha256(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    private static string GitCommit(string repoRoot)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = repoRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 && o.Length > 0 ? o : "unknown";
        }
        catch { return "unknown"; }
    }
}
