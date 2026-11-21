using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalHeat.Core;

/// <summary>Finite supply is an explicit scenario. Unlimited supply preserves the case comparison.</summary>
public sealed record JobScenario(string Mode, string ExcessHeatPolicy, string Source,
    IReadOnlyList<JobTypeDemand> JobTypes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FillerJobDemand? Filler = null)
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
        if (Mode is not ("unlimited" or "finite" or "mixed")
            || ExcessHeatPolicy is not ("wait" or "run-and-dump")
            || string.IsNullOrWhiteSpace(Source)
            || Source.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)
            || JobTypes is null)
            throw new InvalidDataException("Invalid job scenario mode, policy or source.");
        if (Mode == "unlimited" && (JobTypes.Count != 0 || Filler is not null))
            throw new InvalidDataException("Unlimited supply cannot also list job types or a filler.");
        if (Mode == "finite" && Filler is not null)
            throw new InvalidDataException("Finite supply cannot also list a filler.");
        if (Mode == "mixed" && ExcessHeatPolicy != "wait")
            throw new InvalidDataException("Mixed filler jobs must wait for useful heat demand.");
        if ((Mode is "finite" or "mixed") && (JobTypes.Count == 0
            || JobTypes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != JobTypes.Count))
            throw new InvalidDataException("Finite supply needs uniquely named job types.");
        if (Mode == "mixed" && (Filler is null || string.IsNullOrWhiteSpace(Filler.Id)
            || string.IsNullOrWhiteSpace(Filler.WorkUnit)
            || JobTypes.Any(x => x.Id == Filler.Id)
            || !Valid(Filler.PriceChfPerServerHour, "CHF/server-hour")
            || !Valid(Filler.SoldCapacityShare, "fraction", allowZero: true)
            || Filler.SoldCapacityShare.Value > 1))
            throw new InvalidDataException("Mixed supply needs one sourced filler with a sold-capacity share.");
        foreach (var type in JobTypes)
        {
            if (string.IsNullOrWhiteSpace(type.Id) || string.IsNullOrWhiteSpace(type.WorkUnit)
                || !type.Interruptible
                || !Valid(type.ArrivalsPerDay, "jobs/day", allowZero: true, integer: true)
                || !Valid(type.WorkUnitsPerJob, "work units/job")
                || !Valid(type.WorkUnitsPerServerHour, "work units/server-hour")
                || !Valid(type.DeadlineHours, "hours")
                || type.ArrivalsPerDay.Value > int.MaxValue
                || !double.IsFinite(type.WorkUnitsPerJob.Value / type.WorkUnitsPerServerHour.Value)
                || (Mode == "mixed" && !Valid(type.PriceChfPerServerHour, "CHF/server-hour"))
                || (type.PriceChfPerServerHour is not null
                    && !Valid(type.PriceChfPerServerHour, "CHF/server-hour")))
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
    Parameter WorkUnitsPerServerHour, Parameter DeadlineHours,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Parameter? PriceChfPerServerHour = null);

public sealed record FillerJobDemand(string Id, string WorkUnit, Parameter PriceChfPerServerHour,
    Parameter SoldCapacityShare);

public sealed record JobTypeSummary(string Id, string WorkUnit, int Arrived, int DueWithinHorizon,
    int Completed, int Missed, int Backlog, double WorkUnitsProcessed,
    double BacklogWorkUnits, double ServerHours, double? ComputingIncomeChf,
    double? MissedDeadlinePercent);

public sealed record FillerJobSummary(string Id, string WorkUnit, double SoldCapacityShare,
    double ServerHours, double ComputingIncomeChf);

public sealed record JobSummary(int Arrived, int DueWithinHorizon, int Completed, int Missed,
    int Backlog, double? MissedDeadlinePercent, IReadOnlyList<JobTypeSummary> ByType,
    FillerJobSummary? Filler = null);
