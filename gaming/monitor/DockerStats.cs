using System.Globalization;
using System.Text.Json;

namespace DigitalHeat.Gaming.Monitor;

public sealed record DockerObservationOptions(int MaxConcurrentReads, int MaxResponseBytes, int EndpointCacheSeconds,
    string MinApiVersion, string MaxApiVersion)
{
    public void Validate()
    {
        if (MaxConcurrentReads <= 0 || MaxResponseBytes <= 0 || EndpointCacheSeconds <= 0
            || !Version.TryParse(MinApiVersion, out var min) || !Version.TryParse(MaxApiVersion, out var max) || min > max)
            throw new InvalidDataException("Invalid local Docker observation policy.");
    }
}

internal sealed record DockerCpuSample(ulong UsageNs, ulong SystemNs, int CpuCount, DateTimeOffset AtUtc);
internal sealed record DockerParsedStats(string? CpuPercent, string? MemoryUsage, DockerCpuSample? Next,
    DateTimeOffset SampledAtUtc, double? CpuSampleIntervalSeconds);

internal static class DockerStats
{
    private const double Percent = 100;
    private const double BinaryUnit = 1024;
    private static readonly string[] MemoryUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
    private static JsonElement? At(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out root)) return null;
        }
        return root;
    }
    private static ulong? Number(JsonElement root, params string[] keys) => At(root, keys) is {} value && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number) ? number : null;
    private static string Bytes(ulong value)
    {
        double n = value; int unit = 0;
        while (n >= BinaryUnit && unit + 1 < MemoryUnits.Length) { n /= BinaryUnit; unit++; }
        return n.ToString("0.###", CultureInfo.InvariantCulture) + MemoryUnits[unit];
    }
    public static DockerParsedStats Read(JsonElement root, DockerCpuSample? previous, DateTimeOffset receivedAtUtc)
    {
        var at = At(root, "read") is {} read && read.ValueKind == JsonValueKind.String && read.TryGetDateTimeOffset(out var stamp)
            ? stamp.ToUniversalTime() : receivedAtUtc;
        ulong? cpu = Number(root, "cpu_stats", "cpu_usage", "total_usage"), system = Number(root, "cpu_stats", "system_cpu_usage");
        ulong count = Number(root, "cpu_stats", "online_cpus") ?? 0;
        if (count == 0 && At(root, "cpu_stats", "cpu_usage", "percpu_usage") is {} per && per.ValueKind == JsonValueKind.Array)
            count = (ulong)per.GetArrayLength();
        DockerCpuSample? next = cpu is {} own && system is {} total && count > 0 && count <= int.MaxValue ? new(own, total, (int)count, at) : null;
        string? percentage = null; double? interval = null;
        if (next is {} n && previous is {} p && n.UsageNs >= p.UsageNs && n.SystemNs > p.SystemNs && n.AtUtc > p.AtUtc)
        {
            double value = (double)(n.UsageNs - p.UsageNs) / (n.SystemNs - p.SystemNs) * n.CpuCount * Percent;
            if (double.IsFinite(value)) { percentage = value.ToString("0.00", CultureInfo.InvariantCulture) + "%"; interval = (n.AtUtc - p.AtUtc).TotalSeconds; }
        }
        string? memory = null;
        if (Number(root, "memory_stats", "usage") is {} usage && Number(root, "memory_stats", "limit") is {} limit && limit > 0)
        {
            ulong cache = Number(root, "memory_stats", "stats", "total_inactive_file") ?? Number(root, "memory_stats", "stats", "inactive_file") ?? 0;
            // Same Linux inactive-file subtraction as Docker CLI; invalid larger cache is not subtracted.
            memory = Bytes(cache < usage ? usage - cache : usage) + " / " + Bytes(limit);
        }
        return new(percentage, memory, next, at, interval);
    }
}
