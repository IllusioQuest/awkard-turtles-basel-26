namespace DigitalHeat.Core;

/// <summary>Useful heat that can be consumed or stored in the signal's hourly interval.</summary>
public static class HeatCapacity
{
    public static double RemainingKwh(HeatSignal signal, double committedRecoverableHeatKwh = 0)
    {
        ArgumentNullException.ThrowIfNull(signal);
        double[] values = [signal.DemandKwh, signal.TankStoredKwh, signal.TankCapacityKwh,
            signal.ServerCount, signal.RecoverableKwhPerServerAtFullLoad, committedRecoverableHeatKwh];
        if (values.Any(x => !double.IsFinite(x) || x < 0)
            || signal.ServerCount == 0 || signal.RecoverableKwhPerServerAtFullLoad == 0
            || signal.TankStoredKwh > signal.TankCapacityKwh)
            throw new ArgumentOutOfRangeException(nameof(signal));
        // Same numerator as GreedyHeatScheduler; case's lossless hourly storage model.
        double capacity = signal.DemandKwh + signal.TankCapacityKwh - signal.TankStoredKwh;
        if (!double.IsFinite(capacity)) throw new ArgumentOutOfRangeException(nameof(signal));
        return Math.Max(0, capacity - committedRecoverableHeatKwh);
    }
}
