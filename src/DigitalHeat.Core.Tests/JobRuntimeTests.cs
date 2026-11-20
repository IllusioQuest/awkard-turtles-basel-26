using DigitalHeat.JobRuntime;
using System.Globalization;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class JobRuntimeTests
{
    [Fact]
    public void MapsFreezeThrottleAndResumeToContainerControl()
    {
        var control = new FakeControl { CpuPercent = 80 };
        var executor = new DockerJobExecutor(control, "job-container", 2, true);
        Assert.Equal(0, executor.ApplyTargetLoad(0));
        Assert.Equal(0.4, executor.ApplyTargetLoad(0.25), 9);
        Assert.Equal(0.4, executor.ApplyTargetLoad(1), 9);
        executor.Release();
        Assert.Equal(new[] { "pause", "cpu:0.5", "unpause", "stats", "cpu:2", "stats", "cpu:2" },
            control.Calls);
    }

    [Fact]
    public void FreezeOnlyJobUsesFullAllowanceForPositiveRequest()
    {
        var control = new FakeControl { CpuPercent = 50 };
        var executor = new DockerJobExecutor(control, "job-container", 1, false);
        Assert.Equal(0.5, executor.ApplyTargetLoad(0.25), 9);
        Assert.Equal(new[] { "cpu:1", "stats" }, control.Calls);
    }

    [Fact]
    public void RejectsInvalidLoadAndCpuAllowance()
    {
        var control = new FakeControl();
        Assert.Throws<ArgumentOutOfRangeException>(() => new DockerJobExecutor(control, "id", 0, true));
        var executor = new DockerJobExecutor(control, "id", 1, true);
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.ApplyTargetLoad(double.NaN));
        Assert.Empty(control.Calls);
    }

    private sealed class FakeControl : IContainerControl
    {
        public List<string> Calls { get; } = [];
        public double CpuPercent { get; init; }
        public void Pause(string _) => Calls.Add("pause");
        public void Unpause(string _) => Calls.Add("unpause");
        public void SetCpuLimit(string _, double cpuCores) =>
            Calls.Add("cpu:" + cpuCores.ToString("R", CultureInfo.InvariantCulture));
        public double ReadCpuPercent(string _) { Calls.Add("stats"); return CpuPercent; }
    }
}
