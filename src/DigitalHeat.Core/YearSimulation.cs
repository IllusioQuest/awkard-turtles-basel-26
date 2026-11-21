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
    string MissedDeadlines, string JobDemandAssumption, string ProfitabilityNote,
    JobSummary? Jobs = null);

public sealed record YearResult(IReadOnlyList<HourResult> Hours, YearSummary Summary);

public static class YearSimulation
{
    public static YearResult Run(IReadOnlyList<DemandHour> demand, ReferenceParameters p,
        IScheduler scheduler, IExecutor executor, JobScenario? jobScenario = null)
    {
        if (demand.Count == 0) throw new ArgumentException("Empty demand.", nameof(demand));
        jobScenario?.Validate();
        int hoursPerDay = checked((int)p.HoursPerDay.Value);
        if (jobScenario?.Mode == "finite" && demand.Count % hoursPerDay != 0)
            throw new ArgumentException("Finite job runs require complete local days.", nameof(demand));
        var rows = new List<HourResult>(demand.Count);
        double tankKwh = 0;
        double count = p.ServerCount.Value;
        double perServerHeatKwh = p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * p.HoursPerStep.Value;
        JobQueueExecutor? queue = jobScenario?.Mode == "finite"
            ? new JobQueueExecutor(jobScenario, executor, hoursPerDay, demand.Count, count,
                p.HoursPerStep.Value) : null;
        IExecutor effectiveExecutor = queue ?? executor;
        IScheduler effectiveScheduler = jobScenario is null ? scheduler
            : new ExcessHeatPolicyScheduler(scheduler, jobScenario.ExcessHeatPolicy);
        for (int step = 0; step < demand.Count; step++)
        {
            var hour = demand[step];
            queue?.BeginHour(step);
            var signal = new HeatSignal(hour.TotalHeatKwh, tankKwh, p.TankCapacityKwh.Value,
                count, perServerHeatKwh);
            var applied = SchedulerController.Apply(signal, effectiveScheduler, effectiveExecutor);
            double actual = applied.ActualLoadPerServer;
            double produced = actual * count * perServerHeatKwh;
            var balance = HeatBalance.Step(produced, hour.TotalHeatKwh, tankKwh, p.TankCapacityKwh.Value);
            tankKwh = balance.TankStoredKwh;
            rows.Add(new HourResult(hour.LocalTime, hour.TotalHeatKwh, hour.SpaceHeatKwh,
                hour.HotWaterKwh, applied.TargetLoadPerServer, actual, produced, balance.DirectUsedKwh,
                balance.TankChargeKwh, balance.TankDischargeKwh, tankKwh, balance.DumpedHeatKwh,
                balance.BackupHeatKwh,
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
        JobSummary? jobs = queue?.Finish();
        string missedDeadlines = jobs is null ? "not modelled yet"
            : jobs.MissedDeadlinePercent is double percent
                ? $"{percent:R}% of jobs due within the simulated year"
                : "undefined (no jobs due within the simulated year)";
        string jobDemandAssumption = jobScenario is null
            ? "Unlimited immediately available compute work is assumed for the load target; no job arrivals or deadlines are modelled."
            : $"{jobScenario.Mode} jobs; excess heat policy: {jobScenario.ExcessHeatPolicy}; source: {jobScenario.Source}";
        string profitabilityNote = "Case break-even covers electricity minus used-heat value only. Hardware, integration and operations are excluded; this is not a full-cost profitability claim."
            + (jobs is null ? "" : " Finite-job computing income assumes all achieved server-hours are billable, including work on jobs that later miss deadlines; penalties are not modelled.");
        var summary = new YearSummary((int)count, demand.Count(x => x.HeatingDay) / (int)p.HoursPerDay.Value,
            rows.Count, space, hot, total, used, backupTotal, rows.Sum(x => x.DumpedHeatKwh),
            serverHours, serverHours / (count * rows.Count * p.HoursPerStep.Value), used / total,
            electricCost, heatValue, income, breakEven, missedDeadlines, jobDemandAssumption,
            profitabilityNote,
            jobs);
        return new YearResult(rows, summary);
    }
}
