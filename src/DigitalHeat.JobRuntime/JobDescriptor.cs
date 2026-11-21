using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalHeat.Core;

namespace DigitalHeat.JobRuntime;

/// <summary>Metadata supplied by a job owner; measurements remain optional until observed.</summary>
public sealed record JobDescriptor(
    string Id,
    string? ContainerImage,
    string[]? Command,
    bool Interruptible,
    string[] SupportedControlLevels,
    MeasuredQuantity? DeadlineHours,
    string WorkUnit,
    MeasuredQuantity? RuntimeSecondsPerWorkUnit,
    MeasuredQuantity? EnergyKwhPerWorkUnit,
    string? ProgressFile,
    JobPlacementPolicy PlacementPolicy,
    string ServiceMode = "batch");

public sealed record MeasuredQuantity(double Value, string Unit, string Source);

public static class JobDescriptorLoader
{
    public static JobDescriptor Load(string path)
    {
        var job = JsonSerializer.Deserialize<JobDescriptor>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Empty job descriptor.");
        if (string.IsNullOrWhiteSpace(job.Id) || (string.IsNullOrWhiteSpace(job.ContainerImage)
            && (job.Command is null || job.Command.Length == 0))
            || job.SupportedControlLevels is null
            || job.SupportedControlLevels.Distinct(StringComparer.Ordinal).Count() != job.SupportedControlLevels.Length
            || job.SupportedControlLevels.Any(x => x is not ("freeze" or "throttle" or "checkpoint"))
            || string.IsNullOrWhiteSpace(job.WorkUnit)
            || !Valid(job.DeadlineHours, "hours", positive: true)
            || !Valid(job.RuntimeSecondsPerWorkUnit, "s/work unit")
            || !Valid(job.EnergyKwhPerWorkUnit, "kWh/work unit")
            || job.PlacementPolicy is null)
            throw new InvalidDataException("Invalid job descriptor fields or measurement source.");
        job.PlacementPolicy.Validate();
        if (job.ServiceMode == "batch") RequireBatchControl(job);
        else if (job.ServiceMode != "always-on" || job.Interruptible
            || job.SupportedControlLevels.Length != 0 || job.DeadlineHours is not null)
            throw new InvalidDataException("Always-on requires non-interruptible service, no batch controls and no deadline; unknown modes are rejected.");
        return job;
    }

    public static void RequireBatchControl(JobDescriptor job)
    {
        if (job.ServiceMode != "batch" || !job.Interruptible || !job.SupportedControlLevels.Contains("freeze"))
            throw new InvalidDataException("Live heat control requires an interruptible batch job with freeze support; always-on uses the hosting lifecycle.");
    }

    private static bool Valid(MeasuredQuantity? item, string unit, bool positive = false) => item is null ||
        (double.IsFinite(item.Value) && (positive ? item.Value > 0 : item.Value >= 0) && item.Unit == unit
         && !string.IsNullOrWhiteSpace(item.Source));
}
