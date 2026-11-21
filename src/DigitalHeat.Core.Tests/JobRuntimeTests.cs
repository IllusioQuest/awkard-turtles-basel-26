using DigitalHeat.JobRuntime;
using DigitalHeat.Core;
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

    [Fact]
    public void PlacementRequiresDataOriginSiteForSiteBoundJobs()
    {
        var policy = new JobPlacementPolicy("origin-site", "customer data agreement", null);
        Assert.True(JobPlacement.IsEligible(policy, new("customer-a", "customer-a", null)));
        Assert.False(JobPlacement.IsEligible(policy, new("customer-a", "customer-b", null)));
        Assert.False(JobPlacement.IsEligible(policy, new(null, "customer-a", null)));
        Assert.False(JobPlacement.IsEligible(policy, new("customer-a", null, null)));
    }

    [Fact]
    public void PlacementRequiresAllowedExecutionCountry()
    {
        var policy = new JobPlacementPolicy("country-allowlist", "customer data agreement", ["CH"]);
        Assert.True(JobPlacement.IsEligible(policy, new(null, null, "CH")));
        Assert.False(JobPlacement.IsEligible(policy, new(null, null, "DE")));
        Assert.False(JobPlacement.IsEligible(policy, new(null, null, null)));
        Assert.False(JobPlacement.IsEligible(policy, new(null, null, "ch")));
    }

    [Fact]
    public void UnrestrictedPlacementNeedsExplicitSourcedPolicy()
    {
        Assert.True(JobPlacement.IsEligible(new("anywhere", "job owner declaration", null),
            new(null, null, null)));
        Assert.Throws<InvalidDataException>(() => JobPlacement.IsEligible(
            new("anywhere", "TODO(source)", null), new(null, null, null)));
        Assert.Throws<InvalidDataException>(() => JobPlacement.IsEligible(
            new("unknown", "job owner declaration", null), new(null, null, null)));
        Assert.Throws<InvalidDataException>(() => JobPlacement.IsEligible(
            new("country-allowlist", "job owner declaration", []), new(null, null, "CH")));
        Assert.Throws<InvalidDataException>(() => JobPlacement.IsEligible(
            new("country-allowlist", "job owner declaration", ["CH", "CH"]), new(null, null, "CH")));
    }

    [Fact]
    public void DescriptorLoaderRejectsMissingPolicy()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"id":"test","containerImage":"example","interruptible":true,"supportedControlLevels":["freeze"],"workUnit":"batch"}""");
            Assert.Throws<InvalidDataException>(() => JobDescriptorLoader.Load(path));
            File.WriteAllText(path, """{"id":"test","containerImage":"example","interruptible":true,"supportedControlLevels":["freeze"],"workUnit":"batch","placementPolicy":{"mode":"origin-site","source":"customer data agreement"}}""");
            Assert.Equal("origin-site", JobDescriptorLoader.Load(path).PlacementPolicy.Mode);
            File.WriteAllText(path, """{"id":"test","containerImage":"example","interruptible":true,"supportedControlLevels":["freeze"],"workUnit":"batch","placementPolicy":{"mode":"country-allowlist","allowedCountryCodes":["CH"],"source":"customer data agreement"}}""");
            Assert.Equal("CH", Assert.Single(JobDescriptorLoader.Load(path).PlacementPolicy.AllowedCountryCodes!));
            File.WriteAllText(path, """{"id":"test","containerImage":"example","interruptible":true,"supportedControlLevels":["freeze"],"workUnit":"batch","placementPolicy":{"mode":"anywhere","source":"customer data agreement","allowedSiteIds":["customer-a"]}}""");
            Assert.Throws<System.Text.Json.JsonException>(() => JobDescriptorLoader.Load(path));
        }
        finally { File.Delete(path); }
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
