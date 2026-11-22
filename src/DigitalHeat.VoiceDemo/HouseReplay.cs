using DigitalHeat.Core;

namespace DigitalHeat.VoiceDemo;

/// <summary>Inputs of the live summer-house replay.</summary>
/// <param name="DemandKwhByLocalHour">24 hourly heat demands (space heat + hot water), kWh per hour.</param>
public sealed record HouseReplaySettings(
    IReadOnlyList<double> DemandKwhByLocalHour,
    double StartLocalHour,
    double TankCapacityKwh,
    double TankStartKwh,
    double ServerCount,
    double RecoverableKwhPerServerAtFullLoad);

/// <summary>
/// Modelled reference house for the live demo. The case's lossless tank (Core <see cref="HeatBalance"/>)
/// is integrated in short sub-steps so it follows the real container. Two heat sources:
/// the request, with its heat measured live, and the house's other servers. Those servers run
/// simulated background work, scheduled heat-first on whatever useful heat the request leaves:
/// enough to cover the rest of this hour's demand and to refill the tank by the end of the hour,
/// capped at the servers' capacity. The request has priority. Nothing is switched off by hand.
/// </summary>
public sealed class HouseReplay(HouseReplaySettings s)
{
    public double LocalHour { get; private set; } = s.StartLocalHour;
    public double TankKwh { get; private set; } = s.TankStartKwh;
    public double DumpedKwh { get; private set; }
    public double BackupKwh { get; private set; }
    public double RequestHeatKwh { get; private set; }
    public double FillerHeatKwhPerHour { get; private set; }

    public double DemandKwhPerHour => s.DemandKwhByLocalHour[Math.Min(s.DemandKwhByLocalHour.Count - 1, (int)Math.Floor(LocalHour + 1e-9))];
    private double ServerCapacityKwhPerHour => s.ServerCount * s.RecoverableKwhPerServerAtFullLoad;

    /// <summary>Case-model hourly signal for the request at the current tank level (kWh per hour).</summary>
    public HeatSignal Signal() => new(DemandKwhPerHour, TankKwh, s.TankCapacityKwh, s.ServerCount, s.RecoverableKwhPerServerAtFullLoad);

    /// <summary>Advance by <paramref name="hours"/>, with the request producing <paramref name="requestHeatKwhPerHour"/>
    /// (0 while frozen). Splits at hour boundaries; the background re-plans every sub-step.</summary>
    public void Advance(double hours, double requestHeatKwhPerHour, double maxStepHours = 1.0 / 60)
    {
        if (!double.IsFinite(hours) || hours < 0 || requestHeatKwhPerHour < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        while (hours > 1e-12)
        {
            double hourEnd = Math.Floor(LocalHour + 1e-9) + 1;
            double dt = Math.Min(Math.Min(hours, hourEnd - LocalHour), maxStepHours);
            double left = hourEnd - LocalHour;
            // Heat-first background: demand rate plus the tank room spread over the rest of the hour,
            // minus what the request delivers.
            double room = s.TankCapacityKwh - TankKwh;
            FillerHeatKwhPerHour = Math.Clamp(DemandKwhPerHour + room / left - requestHeatKwhPerHour, 0, ServerCapacityKwhPerHour);
            double produced = (FillerHeatKwhPerHour + requestHeatKwhPerHour) * dt;
            var step = HeatBalance.Step(produced, DemandKwhPerHour * dt, TankKwh, s.TankCapacityKwh);
            TankKwh = step.TankStoredKwh;
            DumpedKwh += step.DumpedHeatKwh;
            BackupKwh += step.BackupHeatKwh;
            RequestHeatKwh += requestHeatKwhPerHour * dt;
            LocalHour += dt;
            hours -= dt;
        }
    }

    /// <summary>Hourly demand from a tapping profile: each tapping's share of Qref, scaled to the daily
    /// hot water, binned into its local hour (case: hourly steps), plus the hour's space heat.</summary>
    public static double[] Demand(IReadOnlyList<(int Hour, double QtapKwh)> tappings, double qrefKwh,
        double hotWaterKwhPerDay, IReadOnlyList<double> spaceHeatKwhByHour)
    {
        var demand = spaceHeatKwhByHour.ToArray();
        foreach (var (hour, q) in tappings) demand[hour] += q / qrefKwh * hotWaterKwhPerDay;
        return demand;
    }
}
