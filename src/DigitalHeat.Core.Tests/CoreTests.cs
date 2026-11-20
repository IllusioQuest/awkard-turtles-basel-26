using System.Globalization;
using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class CoreTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static ReferenceParameters Parameters() => ReferenceParameters.Load(
        Path.Combine(Root(), "config", "reference-house.json"));

    private static IReadOnlyList<DemandHour> Demand()
    {
        var lines = File.ReadAllLines(Path.Combine(Root(), "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
        int header = Array.FindIndex(lines, x => x.StartsWith("time,", StringComparison.Ordinal));
        var weather = lines.Skip(header + 1).Where(x => x.Length > 0).Select(x =>
        {
            var columns = x.Split(',');
            return new WeatherHour(columns[0], double.Parse(columns[1], CultureInfo.InvariantCulture));
        }).ToArray();
        return HeatDemand.Build(weather, Parameters());
    }

    [Fact]
    public void ReferenceWeatherHasCaseHeatingDaysAndSpaceHeat()
    {
        var demand = Demand();
        Assert.Equal(206, demand.Count(x => x.HeatingDay) / (int)Parameters().HoursPerDay.Value);
        Assert.InRange(demand.Sum(x => x.SpaceHeatKwh), 26850, 26852);
    }

    [Fact]
    public void OneServerProducesCaseRecoverableDailyHeat()
    {
        var p = Parameters();
        Assert.Equal(19.2, p.ServerElectricKw.Value * p.HeatRecoveryShare.Value
            * p.HoursPerDay.Value, 8);
    }

    [Fact]
    public void HourlyStorageAndHeatBalancesClose()
    {
        var p = Parameters();
        var result = YearSimulation.Run(Demand(), p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor());
        double previousTank = 0;
        foreach (var h in result.Hours)
        {
            Assert.InRange(h.TankStoredKwh, -1e-9, p.TankCapacityKwh.Value + 1e-9);
            Assert.Equal(h.ProducedHeatKwh, h.DirectUsedKwh + h.TankChargeKwh + h.DumpedHeatKwh, 8);
            Assert.Equal(h.DemandKwh, h.DirectUsedKwh + h.TankDischargeKwh + h.BackupHeatKwh, 8);
            Assert.Equal(h.TankStoredKwh, previousTank + h.TankChargeKwh - h.TankDischargeKwh, 8);
            previousTank = h.TankStoredKwh;
        }
    }

    [Fact]
    public void FourServerReferenceMatchesExistingYearExplorer()
    {
        var p = Parameters();
        var result = YearSimulation.Run(Demand(), p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor());
        var s = result.Summary;
        Assert.Equal(4, s.ServerCount);
        Assert.InRange(s.Coverage, 0.5795, 0.5805);
        Assert.InRange(s.DumpedHeatKwh, -1e-8, 1e-8);
        Assert.InRange(s.ServerHours, 22660.5, 22661.5);
        Assert.InRange(s.BreakEvenChfPerServerHour!.Value, 0.2255, 0.2265);
    }

    [Fact]
    public void SchedulerHonoursMinimumLoadAndExecutorCanReportActualLoad()
    {
        var scheduler = new GreedyHeatScheduler(0.25);
        var signal = new HeatSignal(0, 35, 35, 4, 0.8);
        Assert.Equal(0.25, scheduler.Decide(signal).TargetLoadPerServer);
        var applied = SchedulerController.Apply(signal, scheduler, new LimitedExecutor());
        Assert.Equal(0.25, applied.TargetLoadPerServer);
        Assert.Equal(0.1, applied.ActualLoadPerServer);
    }

    [Fact]
    public void CaseEconomicsUseElectricityAndActuallyUsedHeat()
    {
        var p = Parameters();
        var result = YearSimulation.Run(Demand(), p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor());
        var s = result.Summary;
        Assert.Equal(s.ServerHours * p.ServerElectricKw.Value * p.ElectricityPriceChfPerKwh.Value,
            s.ElectricityCostChf, 6);
        Assert.Equal(s.ServerHeatUsedKwh * p.ElectricityPriceChfPerKwh.Value / p.HeatPumpEfficiency.Value,
            s.HeatValueChf, 6);
        Assert.Equal((s.ElectricityCostChf - s.HeatValueChf) / s.ServerHours,
            s.BreakEvenChfPerServerHour!.Value, 8);
    }

    private sealed class LimitedExecutor : IExecutor
    {
        public double ApplyTargetLoad(double targetLoadPerServer) => Math.Min(targetLoadPerServer, 0.1);
    }
}
