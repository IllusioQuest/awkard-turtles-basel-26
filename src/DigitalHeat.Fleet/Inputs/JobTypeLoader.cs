using System.Globalization;
using System.Text.Json;

namespace DigitalHeat.Fleet;

/// <summary>Loads job types from config/fleet/job-types/&lt;id&gt;.json (§5.1, §6). Every number is {value, unit, source}.</summary>
public static class JobTypeLoader
{
    /// <summary>Server-hours are defined at 1 kW (case rack server, §7: billed hours = kWh ÷ 1 kW).</summary>
    public const double ReferenceServerKw = 1.0;
    public const double DaysPerWeek = 7;
    /// <summary>Weather and demand year 2025 is not a leap year (RunCalendar.DataHours ÷ 24).</summary>
    public const double DaysPerYear = 365;

    public static IReadOnlyList<JobType> LoadAll(Scenario scenario)
    {
        var types = new List<JobType>();
        for (int i = 0; i < scenario.JobTypes.Count; i++)
        {
            double? over = null;
            var path = scenario.Resolve(scenario.JobTypes[i]);
            var id = Path.GetFileNameWithoutExtension(path);
            if (scenario.Demand.VolumeScale is { } vs && vs.TryGetValue(id, out var v)) over = v;
            types.Add(Load(path, i, scenario.RepoRoot, over));
        }
        return types;
    }

    public static JobType Load(string path, int index, string repoRoot, double? scenarioVolumeScale = null)
    {
        string raw = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var e = doc.RootElement;
        string id = InputJson.Prop(e, "id", path).GetString() ?? "";
        string where = $"{id} ({Path.GetFileName(path)})";
        string cls = InputJson.Prop(e, "class", where).GetString() ?? "";
        var jobClass = cls switch { "type1" => JobClass.Type1, "type2" => JobClass.Type2, _ => throw new InvalidDataException($"{where}: class must be type1 or type2.") };

        var energy = InputJson.Quantity(InputJson.Prop(e, "energy_kwh_per_work_unit", where), where + ".energy_kwh_per_work_unit");
        if (energy.Value <= 0) throw new InvalidDataException($"{where}: energy per work unit must be > 0.");
        var ctx = new DistributionContext(repoRoot, 1.0 / (energy.Value * FleetTime.SecondsPerHour / ReferenceServerKw));

        var size = Distribution.Parse(InputJson.Prop(e, "size", where), ctx, where + ".size");
        var batch = Distribution.Parse(InputJson.Prop(e, "batch_size", where), ctx, where + ".batch_size");
        var deadline = Distribution.Parse(InputJson.Prop(e, "deadline_s", where), ctx, where + ".deadline_s");

        double Q(string name) => InputJson.Quantity(InputJson.Prop(e, name, where), $"{where}.{name}").Value;
        int maxChunks = checked((int)Q("max_chunks"));
        double minChunk = Q("min_chunk_work_units");
        if (maxChunks < 1 || minChunk <= 0) throw new InvalidDataException($"{where}: max_chunks >= 1 and min_chunk_work_units > 0 required.");

        var lc = InputJson.Prop(e, "lifecycle", where);
        InputJson.NonEmptySource(lc, where + ".lifecycle");
        double minLoad = InputJson.Quantity(InputJson.Prop(lc, "min_load", where), where + ".min_load").Value;
        if (minLoad is < 0 or > 1) throw new InvalidDataException($"{where}: min_load must be in [0, 1].");
        var lifecycle = new JobLifecycle(
            InputJson.Prop(lc, "allow_pause", where).GetBoolean(), minLoad,
            InputJson.Prop(lc, "checkpointable", where).GetBoolean(),
            InputJson.Quantity(InputJson.Prop(lc, "resume_overhead_s", where), where + ".resume_overhead_s").Value,
            InputJson.Quantity(InputJson.Prop(lc, "checkpoint_overhead_s", where), where + ".checkpoint_overhead_s").Value);

        var pl = InputJson.Prop(e, "placement_policy", where);
        string mode = InputJson.Prop(pl, "mode", where).GetString() ?? "";
        if (mode is not ("anywhere" or "origin_site" or "country_allowlist")) throw new InvalidDataException($"{where}: unknown placement mode '{mode}'.");
        string[]? countries = pl.TryGetProperty("countries", out var c) ? c.EnumerateArray().Select(x => x.GetString()!).ToArray() : null;
        if (mode == "country_allowlist" && (countries is null || countries.Length == 0)) throw new InvalidDataException($"{where}: country_allowlist needs countries.");
        var placement = new FleetPlacementPolicy(mode, countries, InputJson.NonEmptySource(pl, where + ".placement_policy"));

        double? maxRtt = e.TryGetProperty("max_rtt_ms", out var rtt) ? InputJson.Quantity(rtt, where + ".max_rtt_ms").Value : null;

        var origin = InputJson.Prop(e, "origin_mix", where);
        var share = InputJson.Quantity(InputJson.Prop(origin, "client_site_share", where), where + ".client_site_share");
        if (share.Value is < 0 or > 1) throw new InvalidDataException($"{where}: client_site_share must be in [0, 1].");
        string? clients = origin.TryGetProperty("clients_file", out var cf) && cf.ValueKind == JsonValueKind.String ? cf.GetString() : null;
        if (share.Value > 0 && clients is null) throw new InvalidDataException($"{where}: client_site_share > 0 needs clients_file.");

        var pr = InputJson.Prop(e, "price", where);
        string pkind = InputJson.Prop(pr, "kind", where).GetString() ?? "";
        var price = pkind switch
        {
            "fixed" => new JobPrice("fixed", InputJson.Quantity(InputJson.Prop(pr, "chf_per_work_unit", where), where + ".price")),
            "dynamic" => new JobPrice("dynamic", null),
            _ => throw new InvalidDataException($"{where}: price kind must be fixed or dynamic.")
        };
        if (pkind == "dynamic") InputJson.NonEmptySource(pr, where + ".price");
        if ((jobClass == JobClass.Type1) != (pkind == "fixed")) throw new InvalidDataException($"{where}: type1 needs a fixed price, type2 a dynamic one.");

        return new JobType
        {
            Id = id, Index = index, Class = jobClass,
            WorkUnit = InputJson.Prop(e, "work_unit", where).GetString() ?? "",
            EnergyKwhPerWorkUnit = energy, Size = size, BatchSize = batch, DeadlineS = deadline,
            MaxChunks = maxChunks, MinChunkWorkUnits = minChunk, StartOverheadS = Q("start_overhead_s"),
            Lifecycle = lifecycle, InputMbPerWorkUnit = Q("input_mb_per_work_unit"), StateMb = Q("state_mb"),
            Placement = placement, MaxRttMs = maxRtt,
            Volume = ResolveVolume(InputJson.Prop(e, "volume", where), where, size.Mean, batch.Mean, energy.Value, scenarioVolumeScale),
            ArrivalShape = LoadShape(InputJson.Prop(e, "arrival_shape", where), repoRoot, where),
            OriginMix = new OriginMix(share.Value, clients), Price = price,
            RawJson = raw, SourcePath = Path.GetFullPath(path)
        };
    }

    /// <summary>Resolves a §6 volume to jobs per day: requests/day ÷ mean batch size × volume_scale × scenario override.</summary>
    public static JobVolume ResolveVolume(JsonElement v, string where, double meanSize, double meanBatch, double energyKwhPerUnit,
        double? scenarioVolumeScale)
    {
        string kind = InputJson.Prop(v, "kind", where).GetString() ?? "";
        var scaleQ = InputJson.Quantity(InputJson.Prop(v, "volume_scale", where), where + ".volume_scale");
        double scale = scaleQ.Value * (scenarioVolumeScale ?? 1);
        Quantity Q(string n) => InputJson.Quantity(InputJson.Prop(v, n, where), $"{where}.volume.{n}");
        string F(double x) => x.ToString("R", CultureInfo.InvariantCulture);
        double requestsPerDay; string derivation; string source;
        switch (kind)
        {
            case "requests_per_day":
            {
                var r = Q("requests_per_day");
                requestsPerDay = r.Value;
                derivation = $"{F(r.Value)} requests/day";
                source = r.Source;
                break;
            }
            case "tokens_per_week":
            {
                var t = Q("tokens_per_week"); var sw = Q("swiss_share"); var mk = Q("market_share");
                requestsPerDay = t.Value * sw.Value * mk.Value / DaysPerWeek / meanSize;
                derivation = $"{F(t.Value)} tokens/week x {F(sw.Value)} swiss_share x {F(mk.Value)} market_share / {F(DaysPerWeek)} days / {F(meanSize)} mean tokens per request = {F(requestsPerDay)} requests/day";
                source = $"tokens_per_week: {t.Source}; swiss_share: {sw.Source}; market_share: {mk.Source}";
                break;
            }
            case "server_hours_per_year":
            {
                var s = Q("server_hours_per_year");
                double serverHoursPerRequest = meanSize * energyKwhPerUnit / ReferenceServerKw;
                requestsPerDay = s.Value / DaysPerYear / serverHoursPerRequest;
                derivation = $"{F(s.Value)} server-hours/year / {F(DaysPerYear)} days / {F(serverHoursPerRequest)} server-hours per request (mean size {F(meanSize)} x {F(energyKwhPerUnit)} kWh per unit / {F(ReferenceServerKw)} kW) = {F(requestsPerDay)} requests/day";
                source = s.Source;
                break;
            }
            default: throw new InvalidDataException($"{where}: unknown volume kind '{kind}'.");
        }
        if (meanBatch < 1) throw new InvalidDataException($"{where}: mean batch size must be >= 1.");
        double jobsPerDay = requestsPerDay / meanBatch * scale;
        derivation += $"; / {F(meanBatch)} mean batch size x {F(scaleQ.Value)} volume_scale" +
            (scenarioVolumeScale is { } o ? $" x {F(o)} scenario override" : "") + $" = {F(jobsPerDay)} jobs/day";
        return new JobVolume(jobsPerDay, derivation, source + "; volume_scale: " + scaleQ.Source);
    }

    /// <summary>168 hour-of-week values (Monday 00:00 = 0) from a CSV column, normalised to mean 1.</summary>
    public static ArrivalShape LoadShape(JsonElement s, string repoRoot, string where)
    {
        string file = InputJson.Prop(s, "file", where).GetString()!;
        string column = InputJson.Prop(s, "column", where).GetString()!;
        string indexColumn = InputJson.Prop(s, "index_column", where).GetString()!;
        InputJson.NonEmptySource(s, where + ".arrival_shape");
        var table = CsvTable.Read(Path.Combine(repoRoot, file));
        var filter = s.TryGetProperty("filter", out var f)
            ? f.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!) : new Dictionary<string, string>();
        var values = new double[RunCalendar.HoursPerWeek];
        var seen = new bool[RunCalendar.HoursPerWeek];
        foreach (var r in table.Rows.Where(r => filter.All(kv => table.Str(r, kv.Key) == kv.Value)))
        {
            int i = int.Parse(table.Str(r, indexColumn), CultureInfo.InvariantCulture);
            if (i < 0 || i >= RunCalendar.HoursPerWeek || seen[i]) throw new InvalidDataException($"{where}: bad or duplicate hour of week {i} in {file}.");
            values[i] = table.Num(r, column);
            seen[i] = true;
        }
        if (seen.Any(x => !x)) throw new InvalidDataException($"{where}: {file} does not hold all {RunCalendar.HoursPerWeek} hours of the week for the filter.");
        double mean = values.Average();
        if (mean <= 0 || values.Any(x => x < 0)) throw new InvalidDataException($"{where}: arrival shape must be non-negative with positive mean.");
        return new ArrivalShape(values.Select(x => x / mean).ToArray(), file, column);
    }
}
