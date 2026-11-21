using System.Globalization;

namespace DigitalHeat.Core;

/// <summary>Reads an Open-Meteo hourly CSV export (see data/raw/open-meteo/SOURCE.md).</summary>
public static class WeatherCsv
{
    public static List<WeatherHour> Read(string path)
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
}
