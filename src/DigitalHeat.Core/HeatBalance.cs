namespace DigitalHeat.Core;

/// <summary>Result of one hourly heat balance at one site, all values in kWh.</summary>
public sealed record HeatBalanceStep(double DirectUsedKwh, double TankChargeKwh,
    double TankDischargeKwh, double DumpedHeatKwh, double BackupHeatKwh, double TankStoredKwh);

/// <summary>
/// The one hourly heat balance of the model (case: lossless tank, recovered heat serves space heating
/// and hot water). Server heat first meets demand directly, the rest charges the tank up to its
/// capacity, anything beyond is dumped. Unmet demand is drawn from the tank, then from the backup
/// heater. Shared by the year simulation and the network simulation (decision 0024).
/// </summary>
public static class HeatBalance
{
    public static HeatBalanceStep Step(double producedKwh, double demandKwh, double tankStoredKwh,
        double tankCapacityKwh)
    {
        double direct = Math.Min(producedKwh, demandKwh);
        double charge = Math.Min(producedKwh - direct, tankCapacityKwh - tankStoredKwh);
        double discharge = Math.Min(tankStoredKwh, demandKwh - direct);
        double dump = producedKwh - direct - charge;
        double backup = demandKwh - direct - discharge;
        return new HeatBalanceStep(direct, charge, discharge, dump, backup,
            tankStoredKwh + (charge - discharge));
    }
}
