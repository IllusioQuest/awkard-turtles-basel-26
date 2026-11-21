using System.Text.Json;

namespace DigitalHeat.Core;

/// <summary>Finite supply is an explicit scenario. Unlimited supply preserves the case comparison.</summary>
public sealed record JobScenario(string Mode, string ExcessHeatPolicy, string Source,
    IReadOnlyList<JobTypeDemand> JobTypes)
{
    public static JobScenario Load(string path)
    {
        var scenario = JsonSerializer.Deserialize<JobScenario>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty job scenario.");
        scenario.Validate();
        return scenario;
    }

    public void Validate()
    {
        if (Mode is not ("unlimited" or "finite")
            || ExcessHeatPolicy is not ("wait" or "run-and-dump")
            || string.IsNullOrWhiteSpace(Source)
            || Source.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)
            || JobTypes is null)
            throw new InvalidDataException("Invalid job scenario mode, policy or source.");
        if (Mode == "unlimited" && JobTypes.Count != 0)
            throw new InvalidDataException("Unlimited supply cannot also list finite job types.");
        if (Mode == "finite" && (JobTypes.Count == 0
            || JobTypes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != JobTypes.Count))
            throw new InvalidDataException("Finite supply needs uniquely named job types.");
        foreach (var type in JobTypes)
        {
            if (string.IsNullOrWhiteSpace(type.Id) || string.IsNullOrWhiteSpace(type.WorkUnit)
                || !type.Interruptible
                || !Valid(type.ArrivalsPerDay, "jobs/day", allowZero: true, integer: true)
                || !Valid(type.WorkUnitsPerJob, "work units/job")
                || !Valid(type.WorkUnitsPerServerHour, "work units/server-hour")
                || !Valid(type.DeadlineHours, "hours")
                || type.ArrivalsPerDay.Value > int.MaxValue
                || !double.IsFinite(type.WorkUnitsPerJob.Value / type.WorkUnitsPerServerHour.Value))
                throw new InvalidDataException($"Invalid or unsupported finite job type: {type.Id}.");
        }
    }

    private static bool Valid(Parameter? value, string unit, bool allowZero = false, bool integer = false)
        => value is not null && double.IsFinite(value.Value)
           && (allowZero ? value.Value >= 0 : value.Value > 0)
           && (!integer || value.Value % 1 == 0)
           && value.Unit == unit && !string.IsNullOrWhiteSpace(value.Source)
           && !value.Source.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase);
}

public sealed record JobTypeDemand(string Id, string WorkUnit, bool Interruptible,
    Parameter ArrivalsPerDay, Parameter WorkUnitsPerJob,
    Parameter WorkUnitsPerServerHour, Parameter DeadlineHours);

public sealed record JobTypeSummary(string Id, string WorkUnit, int Arrived, int DueWithinHorizon,
    int Completed, int Missed, int Backlog, double WorkUnitsProcessed,
    double BacklogWorkUnits);

public sealed record JobSummary(int Arrived, int DueWithinHorizon, int Completed, int Missed,
    int Backlog, double? MissedDeadlinePercent, IReadOnlyList<JobTypeSummary> ByType);
