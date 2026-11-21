using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// Type 1 demand of one origin site (decision 0025 §1): identical jobs arriving each local day at 00:00.
/// The job count is a labelled scenario value (decision 0025 §6), not a sourced arrival rate.
/// </summary>
public sealed record Type1Spec(int JobsPerDay, double ServerHoursPerJob, int DeadlineHours,
    JobPlacementPolicy Policy);

/// <summary>The jobs of one type that arrived at one origin site on one day.</summary>
public sealed class Type1Cohort
{
    public required int Id { get; init; }
    public required int OriginSite { get; init; }
    public required int ArrivalHour { get; init; }
    /// <summary>Exclusive deadline boundary: work must finish before this hour starts.</summary>
    public required int DueHour { get; init; }
    public required int Jobs { get; init; }
    public required double ServerHoursPerJob { get; init; }
    /// <summary>Per site: may this cohort run there (JobPlacement.IsEligible, decision 0013)?</summary>
    public required IReadOnlyList<bool> EligibleSites { get; init; }
    public double RemainingServerHours { get; internal set; }
    public double TotalServerHours => Jobs * ServerHoursPerJob;

    /// <summary>
    /// A cohort observed from outside a simulation (live demo, decision 0042): remaining work is what is
    /// still on disk, not what a <see cref="WorkLedger"/> tracked.
    /// </summary>
    public static Type1Cohort Observed(int id, int originSite, int arrivalHour, int dueHour, int jobs,
        double serverHoursPerJob, IReadOnlyList<bool> eligibleSites, double remainingServerHours)
    {
        if (jobs < 1 || !double.IsFinite(serverHoursPerJob) || serverHoursPerJob <= 0 || dueHour <= arrivalHour
            || originSite < 0 || originSite >= eligibleSites.Count || !double.IsFinite(remainingServerHours)
            || remainingServerHours < 0 || remainingServerHours > jobs * serverHoursPerJob + 1e-9)
            throw new ArgumentOutOfRangeException(nameof(remainingServerHours), "Invalid observed cohort.");
        return new Type1Cohort
        {
            Id = id, OriginSite = originSite, ArrivalHour = arrivalHour, DueHour = dueHour, Jobs = jobs,
            ServerHoursPerJob = serverHoursPerJob, EligibleSites = eligibleSites,
            RemainingServerHours = remainingServerHours,
        };
    }
}

/// <summary>Server-hours of one cohort planned at one site in the current hour.</summary>
public sealed record WorkAllocation(int CohortId, int SiteIndex, double ServerHours);

/// <summary>Type 1 work executed away from its origin (a simulated move, decision 0025 §3).</summary>
public sealed record WorkFlow(int Hour, int FromSite, int ToSite, string Type, double ServerHours);

/// <summary>What strategies see of the work: pending cohorts (earliest deadline first) and filler.</summary>
public sealed record WorkView(IReadOnlyList<Type1Cohort> Pending, IReadOnlyList<double> FillerAvailableServerHours);

public sealed record Type1JobSummary(int Arrived, int DueWithinHorizon, int Completed, int Missed, int Backlog,
    double? MissedDeadlinePercent, double ArrivedServerHours, double MissedServerHours, double MovedServerHours);

public sealed record WorkResult(
    IReadOnlyList<double[]> Type1ServerHoursBySite,
    IReadOnlyList<double[]> Type2ServerHoursBySite,
    IReadOnlyList<double[]> MissedServerHoursBySite,
    IReadOnlyList<WorkFlow> Flows,
    Type1JobSummary Type1);

/// <summary>
/// Network-side work ledger (decision 0025 §2). Shaped like Core's JobQueueExecutor: strategies cap
/// their target at the available work, the executor applies one load per site, and the ledger
/// attributes the achieved server-hours in a fixed order: Type 1 earliest deadline first, filler last.
/// So a shortfall hits filler first, then the Type 1 work with the latest deadline. Remaining Type 1
/// work is dropped at its due hour (as Core's Expire).
/// </summary>
public sealed class WorkLedger
{
    /// <summary>Residual server-hours below this count as done (LP and floating-point noise, decision 0025 §5).</summary>
    public const double MissedEpsilonServerHours = 1e-6;

    private readonly IReadOnlyList<NetworkSite> sites;
    private readonly IReadOnlyList<Type1Spec?> specs;
    private readonly IReadOnlyList<double> fillerAvailableServerHours;
    private readonly int hours;
    private readonly int hoursPerDay;
    private readonly bool[][] eligibility;
    private readonly List<Type1Cohort> pending = [];
    private readonly double[][] type1, type2, missedByHour;
    private readonly List<WorkFlow> flows = [];
    private int nextId, arrived, dueWithinHorizon, missedJobs, completedJobs;
    private double arrivedServerHours, missedServerHours, movedServerHours;
    private int currentHour = -1;
    private bool finished;

    /// <param name="fillerAvailability">φ per site: share of the site's capacity that filler work can use (1 = unlimited).</param>
    public WorkLedger(IReadOnlyList<NetworkSite> sites, IReadOnlyList<Type1Spec?> specs,
        IReadOnlyList<double> fillerAvailability, int hours, int hoursPerDay)
    {
        if (specs.Count != sites.Count || fillerAvailability.Count != sites.Count)
            throw new ArgumentException("One Type 1 spec and one filler availability per site.");
        if (hours <= 0 || hoursPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(hours));
        if (fillerAvailability.Any(f => !double.IsFinite(f) || f is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(fillerAvailability));
        foreach (var s in specs.OfType<Type1Spec>())
            if (s.JobsPerDay < 0 || !(s.ServerHoursPerJob > 0) || s.DeadlineHours <= 0)
                throw new ArgumentOutOfRangeException(nameof(specs));
        this.sites = sites; this.specs = specs; this.hours = hours; this.hoursPerDay = hoursPerDay;
        fillerAvailableServerHours = sites.Select((s, i) => fillerAvailability[i] * s.ServerCount * s.HoursPerStep).ToArray();
        eligibility = specs.Select((spec, origin) => sites.Select(target => spec is not null && JobPlacement.IsEligible(
            spec.Policy, new JobPlacementContext(sites[origin].Id, target.Id, target.CountryCode))).ToArray()).ToArray();
        type1 = sites.Select(_ => new double[hours]).ToArray();
        type2 = sites.Select(_ => new double[hours]).ToArray();
        missedByHour = sites.Select(_ => new double[hours]).ToArray();
    }

    public WorkView View() => new(pending, fillerAvailableServerHours);

    /// <summary>Expire due work, then add the day's arrivals at local 00:00 (hour index multiple of hoursPerDay).</summary>
    public void BeginHour(int hour)
    {
        if (hour != currentHour + 1 || hour >= hours || finished)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hours must be run once, in order.");
        currentHour = hour;
        Expire(hour);
        if (hour % hoursPerDay != 0) return;
        for (int origin = 0; origin < sites.Count; origin++)
        {
            if (specs[origin] is not { JobsPerDay: > 0 } spec) continue;
            int due = checked(hour + spec.DeadlineHours);
            var cohort = new Type1Cohort
            {
                Id = nextId++, OriginSite = origin, ArrivalHour = hour, DueHour = due, Jobs = spec.JobsPerDay,
                ServerHoursPerJob = spec.ServerHoursPerJob, EligibleSites = eligibility[origin],
                RemainingServerHours = spec.JobsPerDay * spec.ServerHoursPerJob
            };
            arrived += cohort.Jobs;
            arrivedServerHours += cohort.TotalServerHours;
            if (due <= hours) dueWithinHorizon += cohort.Jobs;
            pending.Add(cohort);
        }
        pending.Sort((a, b) => a.DueHour != b.DueHour ? a.DueHour.CompareTo(b.DueHour) : a.Id.CompareTo(b.Id));
    }

    /// <summary>
    /// Attributes a site's achieved server-hours: its Type 1 allocations in earliest-deadline order, then
    /// filler up to the planned filler. Returns (type 1, type 2) server-hours actually credited.
    /// </summary>
    public (double Type1ServerHours, double Type2ServerHours) Apply(int site, IEnumerable<WorkAllocation> allocations,
        double plannedFillerServerHours, double achievedServerHours)
    {
        if (currentHour < 0) throw new InvalidOperationException("BeginHour must precede Apply.");
        double remaining = achievedServerHours, t1 = 0;
        var byId = pending.ToDictionary(c => c.Id);
        foreach (var a in allocations.Where(a => a.SiteIndex == site)
                     .OrderBy(a => byId[a.CohortId].DueHour).ThenBy(a => a.CohortId))
        {
            var cohort = byId[a.CohortId];
            if (!cohort.EligibleSites[site])
                throw new InvalidOperationException($"Cohort {cohort.Id} is not eligible at site {sites[site].Id}.");
            double used = Math.Min(Math.Min(a.ServerHours, cohort.RemainingServerHours), remaining);
            if (used <= 0) continue;
            cohort.RemainingServerHours -= used;
            remaining -= used;
            t1 += used;
            if (cohort.OriginSite != site)
            {
                flows.Add(new WorkFlow(currentHour, cohort.OriginSite, site, "type1", used));
                movedServerHours += used;
            }
        }
        double t2 = Math.Max(0, Math.Min(plannedFillerServerHours, remaining));
        type1[site][currentHour] += t1;
        type2[site][currentHour] += t2;
        foreach (var done in pending.Where(c => c.RemainingServerHours <= 0)) completedJobs += done.Jobs;
        pending.RemoveAll(c => c.RemainingServerHours <= 0);
        return (t1, t2);
    }

    public WorkResult Finish()
    {
        if (currentHour != hours - 1 || finished) throw new InvalidOperationException("The full horizon has not run.");
        finished = true;
        Expire(hours);
        int backlog = 0;
        foreach (var c in pending)
        {
            int open = OpenJobs(c);
            backlog += open;
            completedJobs += c.Jobs - open;
        }
        var summary = new Type1JobSummary(arrived, dueWithinHorizon, completedJobs, missedJobs, backlog,
            dueWithinHorizon == 0 ? null : 100.0 * missedJobs / dueWithinHorizon,
            arrivedServerHours, missedServerHours, movedServerHours);
        return new WorkResult(type1, type2, missedByHour, flows, summary);
    }

    /// <summary>Jobs not finished: ⌈(remaining − ε) ÷ per job⌉, floored at 0 (decision 0025 §5).</summary>
    private static int OpenJobs(Type1Cohort c) =>
        Math.Max(0, (int)Math.Ceiling((c.RemainingServerHours - MissedEpsilonServerHours) / c.ServerHoursPerJob));

    private void Expire(int hour)
    {
        foreach (var c in pending.Where(c => c.DueHour <= hour))
        {
            int open = OpenJobs(c);
            missedJobs += open;
            completedJobs += c.Jobs - open;
            if (open > 0)
            {
                missedServerHours += c.RemainingServerHours;
                missedByHour[c.OriginSite][Math.Min(hour, hours - 1)] += c.RemainingServerHours;
            }
        }
        pending.RemoveAll(c => c.DueHour <= hour);
    }
}
