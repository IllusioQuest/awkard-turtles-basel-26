namespace DigitalHeat.Core;

/// <summary>Finite interruptible work, served earliest deadline first; lower availability reduces achieved load.</summary>
public sealed class JobQueueExecutor : IExecutor
{
    private const double LoadTolerance = 1e-9; // Floating-point comparison only, not a scenario parameter.
    private sealed class PendingJob(int typeIndex, int dueStep, double remainingServerHours)
    {
        public int TypeIndex { get; } = typeIndex;
        public int DueStep { get; } = dueStep;
        public double RemainingServerHours { get; set; } = remainingServerHours;
    }

    private readonly JobScenario scenario;
    private readonly IExecutor inner;
    private readonly int hoursPerDay;
    private readonly int horizonSteps;
    private readonly double serverCount;
    private readonly double hoursPerStep;
    private readonly List<PendingJob> pending = [];
    private readonly int[] arrived;
    private readonly int[] dueWithinHorizon;
    private readonly int[] completed;
    private readonly int[] missed;
    private readonly double[] workUnitsProcessed;
    private readonly double[] serverHoursByType;
    private double fillerServerHours;
    private bool hourStarted;
    private int nextStep;

    public JobQueueExecutor(JobScenario scenario, IExecutor inner, int hoursPerDay,
        int horizonSteps, double serverCount, double hoursPerStep)
    {
        scenario.Validate();
        if (scenario.Mode is not ("finite" or "mixed") || hoursPerDay <= 0 || horizonSteps <= 0
            || !double.IsFinite(serverCount) || serverCount <= 0
            || !double.IsFinite(hoursPerStep) || hoursPerStep <= 0)
            throw new ArgumentException("Invalid finite queue dimensions.");
        foreach (var type in scenario.JobTypes)
            if (type.DeadlineHours.Value / hoursPerStep % 1 != 0)
                throw new InvalidDataException($"Deadline for {type.Id} must align with hourly steps.");
        this.scenario = scenario;
        this.inner = inner;
        this.hoursPerDay = hoursPerDay;
        this.horizonSteps = horizonSteps;
        this.serverCount = serverCount;
        this.hoursPerStep = hoursPerStep;
        int count = scenario.JobTypes.Count;
        arrived = new int[count]; dueWithinHorizon = new int[count];
        completed = new int[count]; missed = new int[count];
        workUnitsProcessed = new double[count];
        serverHoursByType = new double[count];
    }

    /// <summary>Arrivals occur at local 00:00; dueStep is the exclusive deadline boundary.</summary>
    public void BeginHour(int step)
    {
        if (hourStarted || step != nextStep || step >= horizonSteps)
            throw new ArgumentOutOfRangeException(nameof(step), "Hours must be executed once in order.");
        Expire(step);
        if (step % hoursPerDay == 0)
            for (int typeIndex = 0; typeIndex < scenario.JobTypes.Count; typeIndex++)
            {
                var type = scenario.JobTypes[typeIndex];
                int dueStep = checked(step + (int)(type.DeadlineHours.Value / hoursPerStep));
                int count = checked((int)type.ArrivalsPerDay.Value);
                arrived[typeIndex] = checked(arrived[typeIndex] + count);
                if (dueStep <= horizonSteps)
                    dueWithinHorizon[typeIndex] = checked(dueWithinHorizon[typeIndex] + count);
                double requiredServerHours = type.WorkUnitsPerJob.Value / type.WorkUnitsPerServerHour.Value;
                for (int i = 0; i < count; i++) pending.Add(new PendingJob(typeIndex, dueStep, requiredServerHours));
            }
        hourStarted = true;
    }

    public double ApplyTargetLoad(double targetLoadPerServer)
    {
        if (!hourStarted) throw new InvalidOperationException("BeginHour must precede execution.");
        double capacityServerHours = serverCount * hoursPerStep;
        double availableServerHours = pending.Sum(x => x.RemainingServerHours);
        double fillerCapacityServerHours = scenario.Mode == "mixed"
            ? capacityServerHours * scenario.Filler!.SoldCapacityShare.Value : 0;
        double cappedTarget = Math.Min(targetLoadPerServer,
            (availableServerHours + fillerCapacityServerHours) / capacityServerHours);
        double achieved = inner.ApplyTargetLoad(cappedTarget);
        if (!double.IsFinite(achieved) || achieved < 0 || achieved > cappedTarget + LoadTolerance)
            throw new InvalidOperationException("Executor achieved more load than available job work.");
        double remainingToApply = achieved * capacityServerHours;
        foreach (var job in pending.OrderBy(x => x.DueStep))
        {
            if (remainingToApply <= 0) break;
            double used = Math.Min(job.RemainingServerHours, remainingToApply);
            job.RemainingServerHours -= used;
            remainingToApply -= used;
            serverHoursByType[job.TypeIndex] += used;
            workUnitsProcessed[job.TypeIndex] += used * scenario.JobTypes[job.TypeIndex].WorkUnitsPerServerHour.Value;
            if (job.RemainingServerHours == 0) completed[job.TypeIndex]++;
        }
        if (remainingToApply > LoadTolerance)
        {
            if (scenario.Mode != "mixed" || remainingToApply > fillerCapacityServerHours + LoadTolerance)
                throw new InvalidOperationException("Executed work exceeds sold filler capacity.");
            fillerServerHours += remainingToApply;
        }
        pending.RemoveAll(x => x.RemainingServerHours == 0);
        hourStarted = false;
        nextStep++;
        return achieved;
    }

    public JobSummary Finish()
    {
        if (hourStarted || nextStep != horizonSteps)
            throw new InvalidOperationException("The complete horizon has not been executed.");
        Expire(horizonSteps);
        var byType = scenario.JobTypes.Select((type, index) => new JobTypeSummary(type.Id,
            type.WorkUnit, arrived[index], dueWithinHorizon[index], completed[index], missed[index],
            pending.Count(x => x.TypeIndex == index), workUnitsProcessed[index],
            pending.Where(x => x.TypeIndex == index).Sum(x => x.RemainingServerHours)
                * type.WorkUnitsPerServerHour.Value, serverHoursByType[index],
            type.PriceChfPerServerHour is null ? null
                : serverHoursByType[index] * type.PriceChfPerServerHour.Value,
            dueWithinHorizon[index] == 0 ? null
                : 100.0 * missed[index] / dueWithinHorizon[index])).ToArray();
        FillerJobSummary? filler = scenario.Filler is null ? null : new FillerJobSummary(
            scenario.Filler.Id, scenario.Filler.WorkUnit, scenario.Filler.SoldCapacityShare.Value,
            fillerServerHours, fillerServerHours * scenario.Filler.PriceChfPerServerHour.Value);
        int due = dueWithinHorizon.Sum(), missedCount = missed.Sum();
        return new JobSummary(arrived.Sum(), due, completed.Sum(), missedCount, pending.Count,
            due == 0 ? null : 100.0 * missedCount / due, byType, filler);
    }

    private void Expire(int step)
    {
        foreach (var job in pending.Where(x => x.DueStep <= step)) missed[job.TypeIndex]++;
        pending.RemoveAll(x => x.DueStep <= step);
    }
}

/// <summary>Optional alternative to heat-led waiting: execute available work and dump surplus heat.</summary>
public sealed class ExcessHeatPolicyScheduler(IScheduler heatLed, string policy) : IScheduler
{
    public SchedulerDecision Decide(HeatSignal signal)
    {
        var heatLedDecision = heatLed.Decide(signal);
        return policy switch
        {
            "wait" => heatLedDecision,
            "run-and-dump" => new SchedulerDecision(1),
            _ => throw new InvalidDataException("Unknown excess-heat policy.")
        };
    }
}
