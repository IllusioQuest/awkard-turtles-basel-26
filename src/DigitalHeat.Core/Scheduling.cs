namespace DigitalHeat.Core;

/// <summary>The same hourly heat signal is passed to the year simulator and a live job adapter.</summary>
public sealed record HeatSignal(double DemandKwh, double TankStoredKwh, double TankCapacityKwh,
    double ServerCount, double RecoverableKwhPerServerAtFullLoad);

public sealed record SchedulerDecision(double TargetLoadPerServer);

public interface IScheduler
{
    SchedulerDecision Decide(HeatSignal heatSignal);
}

/// <summary>A runtime applies a load request and reports the achieved 0..1 load.</summary>
public interface IExecutor
{
    double ApplyTargetLoad(double targetLoadPerServer);
}

public sealed class SimulatedExecutor : IExecutor
{
    public double ApplyTargetLoad(double targetLoadPerServer) => targetLoadPerServer;
}

public sealed record AppliedLoad(double TargetLoadPerServer, double ActualLoadPerServer);

/// <summary>Shared decision-and-actuation path for annual and live executors.</summary>
public static class SchedulerController
{
    public static AppliedLoad Apply(HeatSignal signal, IScheduler scheduler, IExecutor executor)
    {
        double target = scheduler.Decide(signal).TargetLoadPerServer;
        if (!double.IsFinite(target) || target is < 0 or > 1)
            throw new InvalidOperationException("Scheduler returned an invalid load.");
        double actual = executor.ApplyTargetLoad(target);
        if (!double.IsFinite(actual) || actual is < 0 or > 1)
            throw new InvalidOperationException("Executor returned an invalid load.");
        return new AppliedLoad(target, actual);
    }
}

/// <summary>Shared useful-heat load policy. A configured mandatory minimum is explicit and may
/// produce dumped heat; new-job admission independently requires useful heat capacity.</summary>
public sealed class HeatFirstScheduler(double minimumLoadShare) : IScheduler
{
    public SchedulerDecision Decide(HeatSignal signal)
    {
        if (signal.ServerCount <= 0 || signal.RecoverableKwhPerServerAtFullLoad <= 0
            || signal.DemandKwh < 0 || signal.TankCapacityKwh < 0
            || signal.TankStoredKwh < 0 || signal.TankStoredKwh > signal.TankCapacityKwh
            || minimumLoadShare is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(signal));
        double desiredTotalLoad = HeatCapacity.RemainingKwh(signal)
            / signal.RecoverableKwhPerServerAtFullLoad;
        double totalLoad = Math.Clamp(desiredTotalLoad, minimumLoadShare * signal.ServerCount,
            signal.ServerCount);
        return new SchedulerDecision(totalLoad / signal.ServerCount);
    }
}
