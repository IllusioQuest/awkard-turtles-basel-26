using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Fleet;

namespace DigitalHeat.FleetApi;

/// <summary>Thrown for unknown runs/houses or missing files (HTTP 404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Thrown for an invalid hour range (HTTP 400).</summary>
public sealed class BadRangeException(string message) : Exception(message);

/// <summary>
/// Read-only view of the run folders under <c>data/derived/fleet</c>. Lists runs and loads each run's parts lazily,
/// once. The API only reshapes what the simulation logged (docs/model/fleet-api.md).
/// </summary>
public sealed class RunStore
{
    public const int MaxRangeHours = 744;
    public const int CappedRangeHours = 400;
    private const long MsPerHour = 3_600_000;

    private readonly string _runsDir, _repoRoot;
    private readonly ConcurrentDictionary<string, Lazy<RunMeta>> _meta = new();
    private readonly ConcurrentDictionary<string, Lazy<List<HouseInfo>>> _houses = new();
    private readonly ConcurrentDictionary<string, Lazy<NetworkHour[]>> _network = new();
    private readonly ConcurrentDictionary<string, Lazy<RunSeries>> _series = new();
    private readonly Lazy<Dictionary<string, GridPoint>> _grid;
    private readonly Lazy<(WeatherPoint[] Points, float[][] Temps)> _weather;

    public RunStore(string runsDir, string repoRoot)
    {
        _runsDir = runsDir;
        _repoRoot = repoRoot;
        _grid = new(() => GridWeather.ReadGrid(Path.Combine(_repoRoot, GridWeather.GridPath)));
        _weather = new(LoadWeather);
    }

    private sealed record RunMeta(RunInfo Info, RunCalendar Calendar, string HouseFile, string Dir);

    // ------------------------------------------------------------------ runs

    public IReadOnlyList<RunInfo> Runs()
    {
        if (!Directory.Exists(_runsDir)) return [];
        return Directory.GetDirectories(_runsDir).Select(Path.GetFileName).Where(n => n is not null)
            .Where(n => File.Exists(Path.Combine(_runsDir, n!, "run.json")))
            .OrderBy(n => n, StringComparer.Ordinal).Select(n => Meta(n!).Info).ToList();
    }

    private RunMeta Meta(string id)
    {
        string dir = Path.Combine(_runsDir, id);
        if (id.Length == 0 || id.Contains('/') || id.Contains('\\') || id.StartsWith('.') || !File.Exists(Path.Combine(dir, "run.json")))
            throw new NotFoundException($"Unknown run '{id}'.");
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(dir, "run.json")).Ticks;
        var key = id;
        var lazy = _meta.GetOrAdd(key, _ => new(() => ReadMeta(id, dir)));
        // A rewritten run is picked up when run.json changed.
        if (lazy.Value.Info.Id == id && _stamps.TryGetValue(id, out var old) && old != stamp)
        {
            _meta.TryRemove(key, out _); _houses.TryRemove(id, out _); _network.TryRemove(id, out _); _series.TryRemove(id, out _);
            lazy = _meta.GetOrAdd(key, _ => new(() => ReadMeta(id, dir)));
        }
        _stamps[id] = stamp;
        return lazy.Value;
    }

    private readonly ConcurrentDictionary<string, long> _stamps = new();

    private RunMeta ReadMeta(string id, string dir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "run.json")));
        var root = doc.RootElement;
        var scenario = JsonSerializer.Deserialize<Scenario>(root.GetProperty("scenario").GetRawText(), Scenario.JsonOptions)
            ?? throw new InvalidDataException($"No scenario in {id}/run.json.");
        var calendar = RunCalendar.From(scenario.Run);
        var baseline = ReferenceParameters.Load(Path.Combine(_repoRoot, HouseDemandBuilder.ReferenceHouseConfig));
        double maxKw = scenario.Servers.MaxElectricKw ?? baseline.ServerElectricKw.Value;
        double share = scenario.Servers.HeatRecoveryShare ?? baseline.HeatRecoveryShare.Value;
        string name = root.TryGetProperty("scenario_name", out var n) ? n.GetString() ?? id : id;

        int periodHours = 0;
        if (File.Exists(Path.Combine(dir, "houses-timeseries.csv")))
            periodHours = scenario.Logging.HouseTimeseries switch { "hourly" => 1, "daily" => 24, _ => 0 };

        string housePath = "";
        if (root.TryGetProperty("inputs", out var inp) && inp.TryGetProperty("house_file", out var hf) && hf.TryGetProperty("path", out var p))
            housePath = p.GetString() ?? "";

        string note = "";
        string scenarioFile = Path.Combine(_repoRoot, "config/fleet/scenarios", name + ".json");
        if (File.Exists(scenarioFile))
        {
            using var sd = JsonDocument.Parse(File.ReadAllText(scenarioFile), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (sd.RootElement.TryGetProperty("_note", out var nt)) note = nt.GetString() ?? "";
        }
        var info = new RunInfo(id, name, calendar.StartLocal, root.GetProperty("run_hours").GetInt32(), periodHours,
            root.GetProperty("houses").GetInt32(), root.GetProperty("servers").GetInt32(), maxKw, share, false, note);
        return new RunMeta(info, calendar, housePath, dir);
    }

    // ------------------------------------------------------------------ houses

    public IReadOnlyList<HouseInfo> Houses(string runId)
    {
        var m = Meta(runId);
        return _houses.GetOrAdd(runId, _ => new(() => ReadHouses(m))).Value;
    }

    private static double Num(CsvTable t, string[] r, string col) => t.Num(r, col);

    private static double Finite(double v) => double.IsFinite(v) ? v : 0;

    private List<HouseInfo> ReadHouses(RunMeta m)
    {
        var t = CsvTable.Read(Path.Combine(m.Dir, "houses-summary.csv"));
        var specs = new Dictionary<string, HouseSpec>();
        if (m.HouseFile.Length > 0)
            foreach (var h in HouseFiles.Read(Path.IsPathRooted(m.HouseFile) ? m.HouseFile : Path.Combine(_repoRoot, m.HouseFile)).Houses)
                specs[h.HouseId] = h;
        var grid = _grid.Value;
        var list = new List<HouseInfo>(t.Rows.Count);
        foreach (var r in t.Rows)
        {
            string id = t.Str(r, "house_id"), cell = t.Str(r, "cell_id");
            specs.TryGetValue(id, out var spec);
            grid.TryGetValue(cell, out var gp);
            string beMs = t.Str(r, "break_even_chf_per_server_hour");
            double? be = double.TryParse(beMs, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && double.IsFinite(b) ? b : null;
            var s = new HouseSummary(Num(t, r, "heat_demand_kwh"), Num(t, r, "heat_used_kwh"), Num(t, r, "heat_dumped_kwh"),
                Num(t, r, "heat_backup_kwh"), Num(t, r, "electric_kwh"), Finite(Num(t, r, "coverage")), Finite(Num(t, r, "utilisation")),
                Num(t, r, "income_chf"), Num(t, r, "electricity_cost_chf"), Num(t, r, "heat_value_chf"), Num(t, r, "margin_chf"), be,
                Num(t, r, "late_work_kwh"), Num(t, r, "pauses"));
            list.Add(new HouseInfo(id, gp?.Municipality ?? "", spec?.Canton ?? gp?.Canton ?? "", cell, spec?.LatDeg ?? gp?.LatDeg ?? 0,
                spec?.LonDeg ?? gp?.LonDeg ?? 0, (int)Num(t, r, "servers"), spec?.TankKwh ?? 0, spec?.ElectricityPriceChfPerKwh ?? 0, s));
        }
        return list;
    }

    // ------------------------------------------------------------------ network

    public IReadOnlyList<NetworkHour> Network(string runId)
    {
        var m = Meta(runId);
        return _network.GetOrAdd(runId, _ => new(() => ReadNetwork(m))).Value;
    }

    private NetworkHour[] ReadNetwork(RunMeta m)
    {
        var t = CsvTable.Read(Path.Combine(m.Dir, "network-hourly.csv"));
        double capacityKwh = m.Info.Servers * m.Info.MaxElectricKwPerServer; // × 1 h
        bool hasEnergy = t.Has("electric_kwh");
        var res = new NetworkHour[t.Rows.Count];
        for (int i = 0; i < res.Length; i++)
        {
            var r = t.Rows[i];
            double electric = hasEnergy ? t.Num(r, "electric_kwh") : t.Num(r, "electric_kw_mean");
            double income = t.Has("income_t1_chf") ? t.Num(r, "income_t1_chf") + t.Num(r, "income_t2_chf") : 0;
            res[i] = new NetworkHour((int)t.Num(r, "hour"), t.Num(r, "heat_demand_kwh"), t.Num(r, "heat_produced_kwh"),
                t.Num(r, "heat_used_kwh"), t.Num(r, "heat_dumped_kwh"), t.Num(r, "heat_backup_kwh"), electric,
                capacityKwh > 0 ? electric / capacityKwh : 0, income, t.Has("electricity_chf") ? t.Num(r, "electricity_chf") : 0,
                t.Num(r, "arrivals"), t.Num(r, "completions"), t.Num(r, "rejections"), t.Num(r, "misses"),
                t.Num(r, "queue_length_t1"), t.Num(r, "queue_length_t2"));
        }
        return res;
    }

    // ------------------------------------------------------------------ weather

    public IReadOnlyList<WeatherPoint> WeatherPoints() => _weather.Value.Points;

    private (WeatherPoint[], float[][]) LoadWeather()
    {
        var points = _grid.Value.Values.Select(g => new WeatherPoint(g.PointId, g.LatDeg, g.LonDeg)).ToArray();
        var index = new Dictionary<string, int>();
        for (int i = 0; i < points.Length; i++) index[points[i].Id] = i;
        int n = RunCalendar.DataHours;
        var temps = new float[points.Length][];
        var fill = new int[points.Length];
        // The 435-point archive as floats only (GridWeather.LoadArchive would also keep 3.8 M time strings).
        using var file = File.OpenRead(Path.Combine(_repoRoot, GridWeather.ArchivePath));
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        var names = (reader.ReadLine() ?? throw new InvalidDataException("Empty weather archive.")).Split(',');
        int tcol = Array.IndexOf(names, "temperature_2m");
        if (names[0] != "point_id" || tcol < 0) throw new InvalidDataException("Unexpected weather archive header.");
        string? line, current = null;
        int cur = -1;
        while ((line = reader.ReadLine()) is not null)
        {
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            var id = line.AsSpan(0, comma);
            if (current is null || !id.SequenceEqual(current))
            {
                current = line[..comma];
                cur = index.TryGetValue(current, out var ix) ? ix : -1;
                if (cur >= 0) temps[cur] ??= new float[n];
            }
            if (cur < 0 || fill[cur] >= n) continue;
            var span = line.AsSpan();
            int start = 0, col = 0;
            while (col < tcol) { start += span[start..].IndexOf(',') + 1; col++; }
            int end = span[start..].IndexOf(',');
            var f = end < 0 ? span[start..] : span.Slice(start, end);
            temps[cur][fill[cur]++] = float.Parse(f, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        for (int i = 0; i < points.Length; i++)
            if (temps[i] is null || fill[i] != n) throw new InvalidDataException($"Weather point {points[i].Id} incomplete in archive.");
        return (points, temps);
    }

    public WeatherHours Weather(string runId, int fromH, int toH)
    {
        var m = Meta(runId);
        (fromH, toH) = Range(fromH, toH, m.Info.Hours);
        var temps = _weather.Value.Temps;
        var res = new float[toH - fromH][];
        for (int h = fromH; h < toH; h++)
        {
            var row = new float[temps.Length];
            for (int p = 0; p < temps.Length; p++) row[p] = temps[p][m.Calendar.OffsetHour + h];
            res[h - fromH] = row;
        }
        return new WeatherHours(fromH, res);
    }

    /// <summary>Validates [from, to): to is clamped to the run length, ranges over 744 hours are capped to 400.</summary>
    public static (int From, int To) Range(int from, int to, int runHours)
    {
        if (from < 0 || from >= runHours || to <= from) throw new BadRangeException($"Bad range [{from}, {to}) for a run of {runHours} hours.");
        if (to - from > MaxRangeHours) to = from + CappedRangeHours;
        return (from, Math.Min(to, runHours));
    }

    // ------------------------------------------------------------------ timeseries

    private static readonly string[] Fields =
    [
        "heat_demand_kwh", "heat_produced_kwh", "heat_used_kwh", "heat_dumped_kwh", "heat_backup_kwh", "electric_kwh",
        "tank_end_kwh", "income_t1_chf", "income_t2_chf", "electricity_chf", "late_work_kwh",
        "heat_value_chf", "jobs_on_time", "deadlines_missed" // older runs lack these: stay 0
    ];

    private sealed class RunSeries(int houses, int periods, int periodHours, double[][] data, double[] temp, double[] servers, double maxKw)
    {
        public int Houses = houses, Periods = periods, PeriodHours = periodHours;
        public double[][] Data = data;
        public double[] Temp = temp, Servers = servers;
        public double MaxKw = maxKw;
    }

    private RunSeries Series(string runId)
    {
        var m = Meta(runId);
        if (m.Info.PeriodHours == 0) throw new NotFoundException($"Run '{runId}' has no house timeseries.");
        var houses = Houses(runId);
        return _series.GetOrAdd(runId, _ => new(() => ReadSeries(m, houses))).Value;
    }

    private RunSeries ReadSeries(RunMeta m, IReadOnlyList<HouseInfo> houses)
    {
        int ph = m.Info.PeriodHours, periods = m.Info.Hours / ph, nh = houses.Count;
        var idx = new Dictionary<string, int>();
        for (int i = 0; i < nh; i++) idx[houses[i].Id] = i;
        var data = Fields.Select(_ => new double[nh * periods]).ToArray();

        using var reader = new StreamReader(Path.Combine(m.Dir, "houses-timeseries.csv"), System.Text.Encoding.UTF8, false, 1 << 20);
        var names = (reader.ReadLine() ?? throw new InvalidDataException("Empty houses-timeseries.csv.")).Split(',');
        int startCol = Array.IndexOf(names, "period_start_ms");
        var target = new int[names.Length];
        Array.Fill(target, -1);
        for (int f = 0; f < Fields.Length; f++)
        {
            int c = Array.IndexOf(names, Fields[f]);
            if (c >= 0) target[c] = f; // older runs lack electricity_chf: stays 0
        }
        string? line, lastId = null;
        int lastIdx = -1;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var span = line.AsSpan();
            int comma = span.IndexOf(',');
            var id = span[..comma];
            if (lastId is null || !id.SequenceEqual(lastId))
            {
                lastId = line[..comma];
                lastIdx = idx.TryGetValue(lastId, out var ix) ? ix : -1;
            }
            if (lastIdx < 0) continue;
            int col = 1, pos = comma + 1, period = -1;
            while (pos <= span.Length)
            {
                int e = span[pos..].IndexOf(',');
                var f = e < 0 ? span[pos..] : span.Slice(pos, e);
                if (col == startCol) period = (int)(long.Parse(f, CultureInfo.InvariantCulture) / (MsPerHour * ph));
                else if (target[col] >= 0)
                {
                    if (period < 0 || period >= periods) break;
                    data[target[col]][lastIdx * periods + period] = double.Parse(f, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                if (e < 0) break;
                pos += e + 1; col++;
            }
        }
        return new RunSeries(nh, periods, ph, data, ReadHouseTemps(m, houses, periods, ph), houses.Select(h => (double)h.Servers).ToArray(), m.Info.MaxElectricKwPerServer);
    }

    /// <summary>Mean archive temperature of each house's weather_ref over each period, with the run's start offset.</summary>
    private double[] ReadHouseTemps(RunMeta m, IReadOnlyList<HouseInfo> houses, int periods, int ph)
    {
        var refs = new Dictionary<string, HouseSpec>();
        if (m.HouseFile.Length > 0)
            foreach (var h in HouseFiles.Read(Path.IsPathRooted(m.HouseFile) ? m.HouseFile : Path.Combine(_repoRoot, m.HouseFile)).Houses)
                refs[h.HouseId] = h;
        var temp = new double[houses.Count * periods];
        var csvCache = new Dictionary<string, double[]>();
        var points = _weather.Value.Points;
        var pointIndex = new Dictionary<string, int>();
        for (int i = 0; i < points.Length; i++) pointIndex[points[i].Id] = i;
        for (int h = 0; h < houses.Count; h++)
        {
            if (!refs.TryGetValue(houses[h].Id, out var spec)) continue;
            Func<int, double> at;
            if (WeatherStore.IsCsvRef(spec.WeatherRef))
            {
                if (!csvCache.TryGetValue(spec.WeatherRef, out var arr))
                    csvCache[spec.WeatherRef] = arr = WeatherCsv.Read(Path.Combine(_repoRoot, spec.WeatherRef)).Select(x => x.TemperatureC).ToArray();
                at = i => arr[i];
            }
            else if (pointIndex.TryGetValue(spec.WeatherRef, out var pi)) { var arr = _weather.Value.Temps[pi]; at = i => arr[i]; }
            else throw new InvalidDataException($"House {spec.HouseId}: unknown weather_ref '{spec.WeatherRef}'.");
            for (int p = 0; p < periods; p++)
            {
                double sum = 0;
                for (int k = 0; k < ph; k++) sum += at(m.Calendar.OffsetHour + p * ph + k);
                temp[h * periods + p] = Math.Round(sum / ph, 4); // float archive noise
            }
        }
        return temp;
    }

    private static HousePeriod Period(RunSeries s, int house, int period)
    {
        int i = house * s.Periods + period;
        var d = s.Data;
        double electric = d[5][i];
        double capacity = s.Servers[house] * s.MaxKw * s.PeriodHours;
        return new HousePeriod(period * s.PeriodHours, capacity > 0 ? electric / capacity : 0, s.Temp[i], d[0][i], d[1][i], d[2][i],
            d[3][i], d[4][i], electric, d[6][i], d[7][i] + d[8][i], d[9][i], d[10][i], d[11][i], d[12][i], d[13][i]);
    }

    public IReadOnlyList<HousePeriod> HouseSeries(string runId, string houseId)
    {
        var s = Series(runId);
        var houses = Houses(runId);
        int hi = -1;
        for (int i = 0; i < houses.Count; i++) if (houses[i].Id == houseId) { hi = i; break; }
        if (hi < 0) throw new NotFoundException($"Unknown house '{houseId}' in run '{runId}'.");
        var res = new HousePeriod[s.Periods];
        for (int p = 0; p < s.Periods; p++) res[p] = Period(s, hi, p);
        return res;
    }

    /// <summary>Most recent 100 events in [fromMs, toMs), preserving file order for ties.</summary>
    public JobEventLog JobEvents(string runId, string houseId, long fromMs, long toMs)
    {
        var m = Meta(runId);
        if (Houses(runId).All(h => h.Id != houseId)) throw new NotFoundException($"Unknown house '{houseId}' in run '{runId}'.");
        if (fromMs < 0 || fromMs >= m.Calendar.EndMs || toMs <= fromMs || toMs > m.Calendar.EndMs)
            throw new BadRangeException($"Bad event range [{fromMs}, {toMs}) for run '{runId}'.");
        string path = Path.Combine(m.Dir, "job-events.csv");
        if (!File.Exists(path)) return new JobEventLog(false, false, []);
        const int limit = 100;
        var queue = new Queue<JobEvent>();
        bool truncated = false;
        using var reader = new StreamReader(path);
        var columns = CsvTable.SplitLine(reader.ReadLine() ?? throw new InvalidDataException("Empty job event log."));
        int col(string name)
        {
            int i = Array.IndexOf(columns, name);
            return i >= 0 ? i : throw new InvalidDataException($"Missing job event column '{name}'.");
        }
        int timeCol = col("time_ms"), houseCol = col("house_id"), idCol = col("job_id"), typeCol = col("job_type"),
            chunkCol = col("chunk_id"), kindCol = col("kind");
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var r = CsvTable.SplitLine(line);
            if (r[houseCol] != houseId) continue;
            long ms = long.Parse(r[timeCol], CultureInfo.InvariantCulture);
            if (ms < fromMs || ms >= toMs) continue;
            string chunk = r[chunkCol];
            if (queue.Count == limit) { queue.Dequeue(); truncated = true; }
            queue.Enqueue(new JobEvent(ms, r[idCol], r[typeCol],
                chunk.Length > 0 ? int.Parse(chunk, CultureInfo.InvariantCulture) : null, r[kindCol]));
        }
        // A job with exactly one chunk has whole-job lifecycle events. Keep chunk labels for
        // split or unknown jobs; the summary covers the entire run, not just the selected range.
        var candidates = queue.Select(e => e.JobId).ToHashSet(StringComparer.Ordinal);
        var singleChunk = new HashSet<string>(StringComparer.Ordinal);
        string jobsPath = Path.Combine(m.Dir, "jobs.csv");
        if (candidates.Count > 0 && File.Exists(jobsPath))
        {
            using var jobs = new StreamReader(jobsPath);
            var header = CsvTable.SplitLine(jobs.ReadLine() ?? throw new InvalidDataException("Empty jobs file."));
            int jobId = Array.IndexOf(header, "job_id"), chunks = Array.IndexOf(header, "chunks");
            if (jobId >= 0 && chunks >= 0)
                while ((line = jobs.ReadLine()) is not null)
                {
                    if (line.Length == 0) continue;
                    var row = CsvTable.SplitLine(line);
                    if (candidates.Contains(row[jobId]) && row[chunks] == "1") singleChunk.Add(row[jobId]);
                }
        }
        return new JobEventLog(true, truncated, queue.Select(e => singleChunk.Contains(e.JobId) ? e with { ChunkId = null } : e).ToArray());
    }

    public IReadOnlyList<Snapshot> Snapshots(string runId, int fromH, int toH)
    {
        var m = Meta(runId);
        var s = Series(runId);
        (fromH, toH) = Range(fromH, toH, m.Info.Hours);
        var res = new Snapshot[toH - fromH];
        for (int h = fromH; h < toH; h++)
        {
            int p = h / s.PeriodHours;
            var arr = new HousePeriod[s.Houses];
            for (int i = 0; i < s.Houses; i++) arr[i] = Period(s, i, p);
            res[h - fromH] = new Snapshot(h, p * s.PeriodHours, s.PeriodHours, arr);
        }
        return res;
    }
}
