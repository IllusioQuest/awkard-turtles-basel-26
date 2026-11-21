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
}
