using System.Globalization;

namespace DigitalHeat.Fleet;

/// <summary>Demand generation before the run (§6): Poisson arrivals shaped by hour of week, one RNG stream per job type.</summary>
public static class DemandGenerator
{
    public const string OriginAnywhere = "anywhere";
    public const string OriginClientSite = "client_site";

    public static List<JobRequest> Generate(IReadOnlyList<JobType> types, RunCalendar calendar, long runSeed, string repoRoot)
    {
        var all = new List<JobRequest>();
        foreach (var type in types) all.AddRange(GenerateType(type, calendar, runSeed, repoRoot));
        // JobKey is zero-padded, so ordinal order is sequence order within a type.
        all.Sort((a, b) =>
        {
            int c = a.ArrivalMs.CompareTo(b.ArrivalMs);
            if (c != 0) return c;
            c = a.TypeIndex.CompareTo(b.TypeIndex);
            return c != 0 ? c : string.CompareOrdinal(a.JobKey, b.JobKey);
        });
        return all;
    }

    /// <summary>Jobs of one type; depends only on the type, the calendar and runSeed + type.Index.</summary>
    public static List<JobRequest> GenerateType(JobType type, RunCalendar calendar, long runSeed, string repoRoot)
    {
        var rng = new Rng(runSeed + type.Index);
        ClientsFile? clients = type.OriginMix.ClientSiteShare > 0
            ? ClientsFile.Read(Path.Combine(repoRoot, type.OriginMix.ClientsFile!)) : null;
        double jobsPerHourBase = type.Volume.JobsPerDay / RunCalendar.HoursPerDay;
        var jobs = new List<JobRequest>();
        for (int h = 0; h < calendar.RunHours; h++)
        {
            double lambda = jobsPerHourBase * type.ArrivalShape.HourOfWeek[(calendar.StartHourOfWeek + h) % RunCalendar.HoursPerWeek];
            int n = Poisson.Sample(rng, lambda);
            for (int j = 0; j < n; j++)
            {
                long arrival = h * FleetTime.MsPerHour + (long)Math.Floor(rng.NextDouble() * FleetTime.MsPerHour);
                int batch = Math.Max(1, (int)Math.Round(type.BatchSize.Sample(rng), MidpointRounding.AwayFromZero));
                double units = 0;
                for (int b = 0; b < batch; b++) units += type.Size.Sample(rng);
                long deadline = arrival + (long)Math.Round(type.DeadlineS.Sample(rng) * FleetTime.MsPerSecond, MidpointRounding.AwayFromZero);
                string kind = OriginAnywhere; double? lat = null, lon = null; string? clientId = null, canton = null;
                if (clients is not null && rng.NextDouble() < type.OriginMix.ClientSiteShare)
                {
                    var c = clients.Draw(rng);
                    kind = OriginClientSite; lat = c.LatDeg; lon = c.LonDeg; clientId = c.ClientId; canton = c.Canton;
                }
                jobs.Add(new JobRequest("", type.Id, type.Index, arrival, deadline, units, batch, kind, lat, lon, clientId, canton));
            }
        }
        // Sequence numbers follow arrival order within the type (stable for equal arrivals).
        var ordered = jobs.Select((j, i) => (j, i)).OrderBy(x => x.j.ArrivalMs).ThenBy(x => x.i).Select(x => x.j).ToList();
        for (int i = 0; i < ordered.Count; i++)
            ordered[i] = ordered[i] with { JobKey = $"{type.Id}-{(i + 1).ToString("D7", CultureInfo.InvariantCulture)}" };
        return ordered;
    }
}

/// <summary>jobs-input.csv (§11): job_id, type, type_index, arrival_ms, deadline_ms, work_units, batch_size, origin_kind,
/// origin_lat, origin_lon, client_id, origin_canton. Doubles use the round-trip "R" format, so a read-back is exact.</summary>
public static class JobsInputCsv
{
    public static readonly string[] Columns =
    [
        "job_id", "type", "type_index", "arrival_ms", "deadline_ms", "work_units", "batch_size", "origin_kind",
        "origin_lat", "origin_lon", "client_id", "origin_canton"
    ];

    private static string R(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string I(long d) => d.ToString(CultureInfo.InvariantCulture);

    public static void Write(string path, IEnumerable<JobRequest> jobs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)) { NewLine = "\n" };
        w.WriteLine(string.Join(',', Columns));
        foreach (var j in jobs)
            w.WriteLine(string.Join(',', j.JobKey, j.TypeId, I(j.TypeIndex), I(j.ArrivalMs), I(j.DeadlineMs), R(j.WorkUnits),
                I(j.BatchSize), j.OriginKind, j.OriginLatDeg is { } la ? R(la) : "", j.OriginLonDeg is { } lo ? R(lo) : "",
                j.ClientId ?? "", j.OriginCanton ?? ""));
    }

    public static List<JobRequest> Read(string path)
    {
        var t = CsvTable.Read(path);
        string? S(string[] r, string c) => t.Str(r, c).Length == 0 ? null : t.Str(r, c);
        double? D(string[] r, string c) => S(r, c) is { } s ? double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) : null;
        return t.Rows.Select(r => new JobRequest(t.Str(r, "job_id"), t.Str(r, "type"),
            int.Parse(t.Str(r, "type_index"), CultureInfo.InvariantCulture), long.Parse(t.Str(r, "arrival_ms"), CultureInfo.InvariantCulture),
            long.Parse(t.Str(r, "deadline_ms"), CultureInfo.InvariantCulture), t.Num(r, "work_units"),
            int.Parse(t.Str(r, "batch_size"), CultureInfo.InvariantCulture), t.Str(r, "origin_kind"),
            D(r, "origin_lat"), D(r, "origin_lon"), S(r, "client_id"), S(r, "origin_canton"))).ToList();
    }
}
