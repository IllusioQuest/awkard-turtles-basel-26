using System.Globalization;
using System.IO.Compression;
using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>Hourly archive temperature of a point plus the local time label of each row.</summary>
public sealed record ArchiveSeries(string[] TimeLocal, double[] TemperatureC);

/// <summary>Forecast columns of a point (data/raw/swiss-grid/SOURCE.md): lead 0 and previous_day1..3, °C.</summary>
public sealed record ForecastSeries(double[] Lead0C, double[] PreviousDay1C, double[] PreviousDay2C, double[] PreviousDay3C)
{
    public double[] PreviousDay(int lead) => lead switch
    {
        1 => PreviousDay1C, 2 => PreviousDay2C, 3 => PreviousDay3C,
        _ => throw new ArgumentOutOfRangeException(nameof(lead), "Forecast lead must be 1..3 days.")
    };
}

public sealed record GridPoint(string PointId, double LatDeg, double LonDeg, int BfsMunicipalityNr,
    string Municipality, string Canton);

/// <summary>Streams the T-51 gz files and keeps only the requested points (§8).</summary>
public static class GridWeather
{
    public const string ArchivePath = "data/raw/swiss-grid/weather-2025-hourly.csv.gz";
    public const string ForecastPath = "data/raw/swiss-grid/forecast-2025-hourly-lead0-3.csv.gz";
    public const string GridPath = "data/raw/swiss-grid/grid-10km.csv";

    /// <summary>
    /// Reads the archive temperature of the given points. The files list each point as one contiguous
    /// block, so reading stops after the last requested block.
    /// </summary>
    public static Dictionary<string, ArchiveSeries> LoadArchive(string gzPath, IReadOnlyCollection<string> pointIds)
    {
        var times = pointIds.ToDictionary(p => p, _ => new List<string>());
        var temps = pointIds.ToDictionary(p => p, _ => new List<double>());
        ReadBlocks(gzPath, pointIds, "time_local", ["temperature_2m"], (id, f) =>
        {
            times[id].Add(f[0]);
            temps[id].Add(double.Parse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture));
        });
        var result = new Dictionary<string, ArchiveSeries>();
        foreach (var id in pointIds)
        {
            Require(times[id].Count, id, gzPath);
            result[id] = new ArchiveSeries(times[id].ToArray(), temps[id].ToArray());
        }
        return result;
    }

    /// <summary>Reads lead 0 and the three previous-day forecast columns of the given points.</summary>
    public static Dictionary<string, ForecastSeries> LoadForecast(string gzPath, IReadOnlyCollection<string> pointIds)
    {
        var cols = new[] { "temperature_2m", "temperature_2m_previous_day1", "temperature_2m_previous_day2", "temperature_2m_previous_day3" };
        var data = pointIds.ToDictionary(p => p, _ => cols.Select(_ => new List<double>()).ToArray());
        ReadBlocks(gzPath, pointIds, "time_local", cols, (id, f) =>
        {
            for (int i = 0; i < cols.Length; i++)
                data[id][i].Add(double.Parse(f[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture));
        });
        var result = new Dictionary<string, ForecastSeries>();
        foreach (var id in pointIds)
        {
            Require(data[id][0].Count, id, gzPath);
            result[id] = new ForecastSeries(data[id][0].ToArray(), data[id][1].ToArray(), data[id][2].ToArray(), data[id][3].ToArray());
        }
        return result;
    }

    private static void Require(int rows, string id, string path)
    {
        if (rows != RunCalendar.DataHours)
            throw new InvalidDataException($"Point '{id}' has {rows} rows in {path}, expected {RunCalendar.DataHours}.");
    }

    /// <summary>
    /// Calls <paramref name="onRow"/> with (point id, [time_local, wanted columns...]) for each row of a requested
    /// point. Stops once all requested points have been read and the next block started.
    /// </summary>
    private static void ReadBlocks(string gzPath, IReadOnlyCollection<string> pointIds, string timeColumn,
        string[] wanted, Action<string, string[]> onRow)
    {
        if (pointIds.Count == 0) return;
        var requested = new HashSet<string>(pointIds);
        var finished = new HashSet<string>();
        using var file = File.OpenRead(gzPath);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        var header = reader.ReadLine() ?? throw new InvalidDataException($"Empty {gzPath}.");
        var names = header.Split(',');
        int tcol = Array.IndexOf(names, timeColumn);
        var cols = wanted.Select(w => Array.IndexOf(names, w)).ToArray();
        if (names[0] != "point_id" || tcol < 0 || cols.Any(c => c < 0))
            throw new InvalidDataException($"Unexpected header in {gzPath}: {header}");
        string? current = null;
        var buffer = new string[cols.Length + 1];
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string id = line[..comma];
            if (id != current)
            {
                if (current is not null && requested.Contains(current)) finished.Add(current);
                if (finished.Count == requested.Count) return;
                if (finished.Contains(id)) throw new InvalidDataException($"Point '{id}' is not contiguous in {gzPath}.");
                current = id;
            }
            if (!requested.Contains(id)) continue;
            var f = line.Split(',');
            buffer[0] = f[tcol];
            for (int i = 0; i < cols.Length; i++) buffer[i + 1] = f[cols[i]];
            onRow(id, buffer);
        }
        if (current is not null && requested.Contains(current)) finished.Add(current);
        var missing = requested.Where(r => !finished.Contains(r)).ToList();
        if (missing.Count > 0) throw new InvalidDataException($"Points not found in {gzPath}: {string.Join(", ", missing)}.");
    }

    /// <summary>Reads data/raw/swiss-grid/grid-10km.csv (point id → metadata).</summary>
    public static Dictionary<string, GridPoint> ReadGrid(string csvPath)
    {
        var t = CsvTable.Read(csvPath);
        var result = new Dictionary<string, GridPoint>();
        foreach (var r in t.Rows)
        {
            string bfs = t.Str(r, "bfs_municipality_nr");
            result[t.Str(r, "point_id")] = new GridPoint(t.Str(r, "point_id"), t.Num(r, "lat_deg"), t.Num(r, "lon_deg"),
                bfs.Length == 0 ? -1 : int.Parse(bfs, CultureInfo.InvariantCulture), t.Str(r, "municipality"), t.Str(r, "canton"));
        }
        return result;
    }
}

/// <summary>
/// Resolves WeatherRef values (grid point id, or a repo-relative Open-Meteo ".csv") and forecast cells,
/// loading each file once.
/// </summary>
public sealed class WeatherStore(string repoRoot)
{
    private readonly Dictionary<string, ArchiveSeries> _actual = new();
    private readonly Dictionary<string, ForecastSeries> _forecast = new();
    public string RepoRoot { get; } = repoRoot;

    public static bool IsCsvRef(string weatherRef) => weatherRef.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>Loads the missing series for the given references and forecast cells in one pass per file.</summary>
    public void Preload(IEnumerable<string> weatherRefs, IEnumerable<string> forecastCells)
    {
        var refs = weatherRefs.Distinct().Where(r => !_actual.ContainsKey(r)).ToList();
        foreach (var csv in refs.Where(IsCsvRef))
        {
            var rows = WeatherCsv.Read(Path.Combine(RepoRoot, csv));
            if (rows.Count != RunCalendar.DataHours)
                throw new InvalidDataException($"{csv} has {rows.Count} rows, expected {RunCalendar.DataHours}.");
            _actual[csv] = new ArchiveSeries(rows.Select(r => r.LocalTime).ToArray(), rows.Select(r => r.TemperatureC).ToArray());
        }
        var grid = refs.Where(r => !IsCsvRef(r)).ToList();
        foreach (var kv in GridWeather.LoadArchive(Path.Combine(RepoRoot, GridWeather.ArchivePath), grid)) _actual[kv.Key] = kv.Value;
        var cells = forecastCells.Distinct().Where(c => !_forecast.ContainsKey(c)).ToList();
        foreach (var kv in GridWeather.LoadForecast(Path.Combine(RepoRoot, GridWeather.ForecastPath), cells)) _forecast[kv.Key] = kv.Value;
    }

    public ArchiveSeries Actual(string weatherRef) => _actual.TryGetValue(weatherRef, out var s)
        ? s : throw new KeyNotFoundException($"Weather '{weatherRef}' not preloaded.");

    public ForecastSeries Forecast(string cellId) => _forecast.TryGetValue(cellId, out var s)
        ? s : throw new KeyNotFoundException($"Forecast cell '{cellId}' not preloaded.");
}
