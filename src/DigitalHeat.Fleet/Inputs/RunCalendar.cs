using System.Globalization;

namespace DigitalHeat.Fleet;

/// <summary>
/// Maps the run window (local Europe/Zurich times of the scenario's run section) to rows of the
/// hourly 2025 series (§3: 24 rows per local day, 8,760 rows, as the case CSV and the T-51 files).
/// </summary>
public sealed class RunCalendar
{
    /// <summary>Year of the weather data (data/raw/swiss-grid/SOURCE.md: 2025-01-01 to 2025-12-31).</summary>
    public const int DataYear = 2025;
    public const int HoursPerDay = 24;
    public const int HoursPerWeek = 7 * HoursPerDay;
    public const string LocalTimeFormat = "yyyy-MM-dd'T'HH:mm";

    private static readonly DateTime DataStart = new(DataYear, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Rows in the data series: (days of 2025) × 24 = 8,760.</summary>
    public static int DataHours => (new DateTime(DataYear + 1, 1, 1) - DataStart).Days * HoursPerDay;

    /// <summary>Row of the data series at run hour 0.</summary>
    public int OffsetHour { get; }
    public int RunHours { get; }
    public long EndMs => RunHours * FleetTime.MsPerHour;
    /// <summary>Hour of week (Monday 00:00 = 0) of run hour 0.</summary>
    public int StartHourOfWeek { get; }
    /// <summary>Local time string "yyyy-MM-ddTHH:mm" of run hour 0.</summary>
    public string StartLocal { get; }

    public RunCalendar(int offsetHour, int runHours)
    {
        if (offsetHour < 0 || runHours <= 0 || offsetHour + runHours > DataHours)
            throw new ArgumentException($"Run window [{offsetHour}, {offsetHour + runHours}) lies outside the data [0, {DataHours}).");
        OffsetHour = offsetHour;
        RunHours = runHours;
        var start = DataStart.AddHours(offsetHour);
        StartLocal = start.ToString(LocalTimeFormat, CultureInfo.InvariantCulture);
        StartHourOfWeek = ((int)start.DayOfWeek + 6) % 7 * HoursPerDay + start.Hour;
    }

    public static RunCalendar From(RunSection run)
    {
        int s = ParseOffset(run.StartLocal, "start_local");
        int e = ParseOffset(run.EndLocal, "end_local");
        if (e <= s) throw new ArgumentException($"end_local {run.EndLocal} must be after start_local {run.StartLocal}.");
        return new RunCalendar(s, e - s);
    }

    /// <summary>Hours since 2025-01-01 00:00 local (days × 24 + hour; DST is ignored by the data's 24-row days).</summary>
    public static int ParseOffset(string local, string what)
    {
        if (!DateTime.TryParseExact(local, LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            || t.Minute != 0)
            throw new ArgumentException($"{what} '{local}' must be 'yyyy-MM-ddTHH:00'.");
        return checked((int)(t.Date - DataStart).TotalDays * HoursPerDay + t.Hour);
    }
}
