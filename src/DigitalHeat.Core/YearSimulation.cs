namespace DigitalHeat.Core;

public sealed record HourResult(string LocalTime, double DemandKwh, double SpaceHeatKwh,
    double HotWaterKwh, double TargetLoadPerServer, double ActualLoadPerServer,
    double ProducedHeatKwh, double DirectUsedKwh, double TankChargeKwh, double TankDischargeKwh,
    double TankStoredKwh, double DumpedHeatKwh, double BackupHeatKwh, double ServerElectricKwh)
{
    public double UsedServerHeatKwh => DirectUsedKwh + TankDischargeKwh;
}

public sealed record YearSummary(int ServerCount, int HeatingDays, int Hours,
    double SpaceHeatDemandKwh, double HotWaterDemandKwh, double TotalHeatDemandKwh,
    double ServerHeatUsedKwh, double BackupHeatKwh, double DumpedHeatKwh,
    double ServerHours, double Utilisation, double Coverage, double ElectricityCostChf,
    double HeatValueChf, double ComputingIncomeChf, double? BreakEvenChfPerServerHour,
    string MissedDeadlines, string JobDemandAssumption, string ProfitabilityNote);

public sealed record YearResult(IReadOnlyList<HourResult> Hours, YearSummary Summary);

public static class YearSimulation
{
    public static YearResult Run(IReadOnlyList<DemandHour> demand, ReferenceParameters p,
        IScheduler scheduler, IExecutor executor)
    {
        if (demand.Count == 0) throw new ArgumentException("Empty demand.", nameof(demand));
        var rows = new List<HourResult>(demand.Count);
        double tankKwh = 0;
        double count = p.ServerCount.Value;
        double perServerHeatKwh = p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * p.HoursPerStep.Value;
        foreach (var hour in demand)
        {
            var signal = new HeatSignal(hour.TotalHeatKwh, tankKwh, p.TankCapacityKwh.Value,
                count, perServerHeatKwh);
            var applied = SchedulerController.Apply(signal, scheduler, executor);
            double actual = applied.ActualLoadPerServer;
            double produced = actual * count * perServerHeatKwh;
            double direct = Math.Min(produced, hour.TotalHeatKwh);
            double charge = Math.Min(produced - direct, p.TankCapacityKwh.Value - tankKwh);
            double discharge = Math.Min(tankKwh, hour.TotalHeatKwh - direct);
            double dump = produced - direct - charge;
            double backup = hour.TotalHeatKwh - direct - discharge;
            tankKwh += charge - discharge;
            rows.Add(new HourResult(hour.LocalTime, hour.TotalHeatKwh, hour.SpaceHeatKwh,
                hour.HotWaterKwh, applied.TargetLoadPerServer, actual, produced, direct,
                charge, discharge, tankKwh, dump, backup,
                actual * count * p.ServerElectricKw.Value * p.HoursPerStep.Value));
        }
        double space = demand.Sum(x => x.SpaceHeatKwh), hot = demand.Sum(x => x.HotWaterKwh);
        double total = space + hot, backupTotal = rows.Sum(x => x.BackupHeatKwh);
        // Useful heat includes tank discharge, exactly as demand minus backup in the case prototype.
        double used = total - backupTotal;
        double serverHours = rows.Sum(x => x.ActualLoadPerServer * count * p.HoursPerStep.Value);
        double electricCost = rows.Sum(x => x.ServerElectricKwh) * p.ElectricityPriceChfPerKwh.Value;
        double heatValue = used * p.ElectricityPriceChfPerKwh.Value / p.HeatPumpEfficiency.Value;
        double income = serverHours * p.ComputePriceChfPerServerHour.Value;
        double? breakEven = serverHours > 0 ? (electricCost - heatValue) / serverHours : null;
        var summary = new YearSummary((int)count, demand.Count(x => x.HeatingDay) / (int)p.HoursPerDay.Value,
            rows.Count, space, hot, total, used, backupTotal, rows.Sum(x => x.DumpedHeatKwh),
            serverHours, serverHours / (count * rows.Count * p.HoursPerStep.Value), used / total,
            electricCost, heatValue, income, breakEven, "not modelled yet",
            "Unlimited immediately available compute work is assumed for the load target; no job arrivals or deadlines are modelled.",
            "Case break-even covers electricity minus used-heat value only. Hardware, integration and operations are excluded; this is not a full-cost profitability claim.");
        return new YearResult(rows, summary);
    }
}
