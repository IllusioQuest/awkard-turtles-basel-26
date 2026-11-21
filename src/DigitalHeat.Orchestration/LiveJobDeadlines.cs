using System.Text.Json;

namespace DigitalHeat.Orchestration;

public sealed record LiveDeadlineProfile(string Id, string JobId, string WorkUnit, double MaxWorkUnits, double DeadlineSeconds, string Evidence);
public sealed record LiveDeadlineProfiles(int SchemaVersion, double PollIntervalSeconds, int MaxQueuedJobs, string Source, LiveDeadlineProfile[] Profiles)
{
    public static LiveDeadlineProfiles Load(string path)
    {
        var c = JsonSerializer.Deserialize<LiveDeadlineProfiles>(File.ReadAllText(path), WorkloadJson.Options)
            ?? throw new InvalidDataException("Missing deadline profiles.");
        if (c.SchemaVersion != 1 || !double.IsFinite(c.PollIntervalSeconds) || c.PollIntervalSeconds <= 0 || c.MaxQueuedJobs <= 0 || string.IsNullOrWhiteSpace(c.Source)
            || c.Profiles is null || c.Profiles.Any(p => p is null) || c.Profiles.Select(p => p.Id).Distinct().Count() != c.Profiles.Length
            || c.Profiles.Any(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.JobId) || string.IsNullOrWhiteSpace(p.WorkUnit)
                || !double.IsFinite(p.MaxWorkUnits) || p.MaxWorkUnits <= 0 || !double.IsFinite(p.DeadlineSeconds) || p.DeadlineSeconds <= 0 || string.IsNullOrWhiteSpace(p.Evidence)))
            throw new InvalidDataException("Invalid sourced deadline profile policy.");
        return c;
    }

    public JobDeadline For(string profileId, string jobId, double declaredWorkUnits, DateTimeOffset submittedAtUtc)
    {
        var p = Profiles.SingleOrDefault(p => p.Id == profileId) ?? throw new ArgumentException("Unknown deadline profile.");
        if (p.JobId != jobId || !double.IsFinite(declaredWorkUnits) || declaredWorkUnits <= 0 || declaredWorkUnits > p.MaxWorkUnits
            || submittedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Job identity/input bound does not match this deadline profile. Larger or unbounded jobs need an explicit budget.");
        return new(submittedAtUtc, submittedAtUtc.AddSeconds(p.DeadlineSeconds),
            $"{Source} Profile {p.Id}; operator declares {declaredWorkUnits:R} {p.WorkUnit}. {p.Evidence}", true);
    }
}

/// <summary>Outcome is independent of container phase, so cleanup/recovery never turns a stop into success.</summary>
public sealed record DeadlineOutcome(string Status, DateTimeOffset ObservedAtUtc, string Reason, bool StopIntentPersisted = false);
