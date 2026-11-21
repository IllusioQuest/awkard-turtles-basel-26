using System.Globalization;
using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class JobQueueTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static ReferenceParameters Parameters() => ReferenceParameters.Load(
        Path.Combine(Root(), "config", "reference-house.json"));

    // Synthetic test inputs exercise the queue; they are not market demand or scored results.
    private static Parameter TestValue(double value, string unit) => new(value, unit, "synthetic test fixture");

    private static JobTypeDemand Type(string id, double arrivals, double workUnits,
        double throughput, double deadlineHours) => new(id, "batch", true,
            TestValue(arrivals, "jobs/day"), TestValue(workUnits, "work units/job"),
            TestValue(throughput, "work units/server-hour"), TestValue(deadlineHours, "hours"));

    [Fact]
    public void HeatFreeDayMissesJobsAndEveryHourStillBalances()
    {
        var p = Parameters();
        var scenario = new JobScenario("finite", "wait", "synthetic test fixture",
            [Type("batch", 10, 4, 1, 12)]);
        var demand = Enumerable.Range(0, 48).Select(hour => new DemandHour(
            $"2025-07-{hour / 24 + 1:00}T{hour % 24:00}:00", 25, false, 0, 0)).ToArray();
        var result = YearSimulation.Run(demand, p, new GreedyHeatScheduler(0),
            new SimulatedExecutor(), scenario);
        var jobs = Assert.IsType<JobSummary>(result.Summary.Jobs);
        Assert.Equal(20, jobs.Arrived);
        Assert.Equal(20, jobs.DueWithinHorizon);
        Assert.Equal(10, jobs.Completed);
        Assert.Equal(10, jobs.Missed);
        Assert.Equal(50, jobs.MissedDeadlinePercent);
        Assert.Equal(0, jobs.Backlog);
        double tank = 0;
        foreach (var hour in result.Hours)
        {
            Assert.Equal(hour.ProducedHeatKwh,
                hour.DirectUsedKwh + hour.TankChargeKwh + hour.DumpedHeatKwh, 8);
            Assert.Equal(hour.DemandKwh,
                hour.DirectUsedKwh + hour.TankDischargeKwh + hour.BackupHeatKwh, 8);
            Assert.Equal(hour.TankStoredKwh, tank + hour.TankChargeKwh - hour.TankDischargeKwh, 8);
            tank = hour.TankStoredKwh;
        }
    }

    [Fact]
    public void RunAndDumpPolicyCompletesWorkAndRecordsSurplusHeat()
    {
        var p = Parameters();
        var scenario = new JobScenario("finite", "run-and-dump", "synthetic test fixture",
            [Type("batch", 10, 4, 1, 12)]);
        var demand = Enumerable.Range(0, 48).Select(hour => new DemandHour(
            $"2025-07-{hour / 24 + 1:00}T{hour % 24:00}:00", 25, false, 0, 0)).ToArray();
        var result = YearSimulation.Run(demand, p, new GreedyHeatScheduler(0),
            new SimulatedExecutor(), scenario);
        Assert.Equal(20, result.Summary.Jobs!.Completed);
        Assert.Equal(0, result.Summary.Jobs.Missed);
        Assert.True(result.Summary.DumpedHeatKwh > 0);
    }

    [Fact]
    public void QueueServesEarliestDeadlineAndDoesNotInventPostYearMisses()
    {
        var scenario = new JobScenario("finite", "wait", "synthetic test fixture",
            [Type("later", 1, 1, 1, 48), Type("soon", 1, 1, 1, 1)]);
        var queue = new JobQueueExecutor(scenario, new SimulatedExecutor(), 24, 1, 1, 1);
        queue.BeginHour(0);
        Assert.Equal(1, queue.ApplyTargetLoad(1));
        var jobs = queue.Finish();
        Assert.Equal(2, jobs.Arrived);
        Assert.Equal(1, jobs.DueWithinHorizon);
        Assert.Equal(1, jobs.Completed);
        Assert.Equal(0, jobs.Missed);
        Assert.Equal(0, jobs.MissedDeadlinePercent);
        Assert.Equal("soon", jobs.ByType[1].Id);
        Assert.Equal(1, jobs.ByType[1].Completed);
        Assert.Equal(1, jobs.ByType[0].Backlog);
    }

    [Fact]
    public void FiniteScenarioRejectsUnsourcedAndNonInterruptibleInputs()
    {
        var valid = Type("batch", 1, 1, 1, 1);
        Assert.Throws<InvalidDataException>(() => new JobScenario("finite", "wait", "test",
            [valid with { Interruptible = false }]).Validate());
        Assert.Throws<InvalidDataException>(() => new JobScenario("finite", "wait", "test",
            [valid with { ArrivalsPerDay = new Parameter(1, "jobs/day", "TODO(source)") }]).Validate());
    }

    private static JobScenario Mixed(double arrivals, double soldShare) => new("mixed", "wait",
        "synthetic mixed-queue test fixture",
        [Type("type-1", arrivals, 1, 1, 24) with
            { PriceChfPerServerHour = TestValue(.90, "CHF/server-hour") }],
        new FillerJobDemand("type-2", "server-hour", TestValue(.66, "CHF/server-hour"),
            TestValue(soldShare, "fraction")));

    [Fact]
    public void MixedQueueServesFiniteJobsFirstAndNeverDumpsFillerHeat()
    {
        var p = Parameters();
        var demand = Enumerable.Range(0, 24).Select(hour => new DemandHour(
            $"2025-01-01T{hour:00}:00", 0, true, 3.2, 0)).ToArray();
        var result = YearSimulation.Run(demand, p, new GreedyHeatScheduler(0),
            new SimulatedExecutor(), Mixed(1, 1));
        var jobs = Assert.IsType<JobSummary>(result.Summary.Jobs);
        Assert.Equal(1, jobs.ByType[0].Completed);
        Assert.Equal(1, jobs.ByType[0].ServerHours, 8);
        Assert.Equal(95, jobs.Filler!.ServerHours, 8);
        Assert.Equal(result.Summary.ServerHours, jobs.ByType[0].ServerHours + jobs.Filler.ServerHours, 8);
        Assert.Equal(result.Summary.ComputingIncomeChf,
            jobs.ByType[0].ComputingIncomeChf!.Value + jobs.Filler.ComputingIncomeChf, 8);
        Assert.Equal(1 * .90 + 95 * .66, result.Summary.ComputingIncomeChf, 8);
        Assert.Equal(0, result.Summary.DumpedHeatKwh, 8);
    }

    [Fact]
    public void ConstrainedFillerRunsOnlySoldCapacity()
    {
        var p = Parameters();
        var demand = Enumerable.Range(0, 24).Select(hour => new DemandHour(
            $"2025-01-01T{hour:00}:00", 0, true, 3.2, 0)).ToArray();
        var result = YearSimulation.Run(demand, p, new GreedyHeatScheduler(0),
            new SimulatedExecutor(), Mixed(1, .35));
        var jobs = result.Summary.Jobs!;
        Assert.Equal(1, jobs.ByType[0].ServerHours, 8);
        Assert.Equal(33.6, jobs.Filler!.ServerHours, 8);
        Assert.Equal(.6, result.Hours[0].ActualLoadPerServer, 8);
        Assert.Equal(.35, result.Hours[1].ActualLoadPerServer, 8);
        Assert.Equal(0, result.Summary.DumpedHeatKwh, 8);
    }

    [Fact]
    public void EmptyFiniteQueueWithFullySoldFillerMatchesUnlimitedReference()
    {
        var p = Parameters();
        var lines = File.ReadAllLines(Path.Combine(Root(),
            "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
        int header = Array.FindIndex(lines, x => x.StartsWith("time,", StringComparison.Ordinal));
        var weather = lines.Skip(header + 1).Where(x => x.Length > 0).Select(line =>
        {
            var fields = line.Split(',');
            return new WeatherHour(fields[0], double.Parse(fields[1], CultureInfo.InvariantCulture));
        }).ToArray();
        var demand = HeatDemand.Build(weather, p);
        var unlimited = YearSimulation.Run(demand, p,
            new GreedyHeatScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor(),
            JobScenario.Load(Path.Combine(Root(), "config", "job-scenarios", "unlimited.json")));
        var mixed = YearSimulation.Run(demand, p,
            new GreedyHeatScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor(), Mixed(0, 1));
        Assert.Equal(unlimited.Summary.Coverage, mixed.Summary.Coverage, 10);
        Assert.Equal(unlimited.Summary.DumpedHeatKwh, mixed.Summary.DumpedHeatKwh, 10);
        Assert.Equal(unlimited.Summary.ServerHours, mixed.Summary.ServerHours, 8);
        Assert.Equal(unlimited.Summary.BreakEvenChfPerServerHour!.Value,
            mixed.Summary.BreakEvenChfPerServerHour!.Value, 10);
        Assert.InRange(mixed.Summary.Coverage, .5804, .5806);
        Assert.InRange(mixed.Summary.ServerHours, 22660.5, 22661.5);
        Assert.Equal(0, mixed.Summary.Jobs!.ByType[0].ServerHours);
        Assert.Equal(mixed.Summary.ServerHours, mixed.Summary.Jobs.Filler!.ServerHours, 8);
    }

    [Fact]
    public void MixedScenarioRejectsUnsourcedOrOversoldFiller()
    {
        Assert.Throws<InvalidDataException>(() => (Mixed(1, 1) with
            { Filler = Mixed(1, 1).Filler! with
                { PriceChfPerServerHour = TestValue(0, "CHF/server-hour") } }).Validate());
        Assert.Throws<InvalidDataException>(() => Mixed(1, 1.1).Validate());
        Assert.Throws<InvalidDataException>(() => (Mixed(1, 1) with
            { Filler = Mixed(1, 1).Filler! with
                { SoldCapacityShare = new Parameter(.35, "fraction", "TODO(source)") } }).Validate());
        Assert.Throws<InvalidDataException>(() => (Mixed(1, 1) with
            { ExcessHeatPolicy = "run-and-dump" }).Validate());
        Assert.Throws<InvalidDataException>(() => (Mixed(1, 1) with
            { JobTypes = [Type("type-1", 1, 1, 1, 24)] }).Validate());
    }
}
