using DigitalHeat.JobRuntime;
using System.Text.Json;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class AlwaysOnCatalogTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("batch")]
    public void LegacyAndExplicitBatchRemainControllable(string? mode)
    {
        var json = Fields();
        if (mode is not null) json["serviceMode"] = mode;
        var job = Load(json);
        Assert.Equal("batch", job.ServiceMode);
        JobDescriptorLoader.RequireBatchControl(job);
    }

    [Fact]
    public void AlwaysOnLoadsButCannotUseBatchControl()
    {
        var json = Fields();
        json["serviceMode"] = "always-on";
        json["interruptible"] = false;
        json["supportedControlLevels"] = Array.Empty<string>();
        var job = Load(json);
        Assert.Equal("always-on", job.ServiceMode);
        Assert.Throws<InvalidDataException>(() => JobDescriptorLoader.RequireBatchControl(job));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("")]
    public void ExplicitNullOrUnknownModeFails(string? mode)
    {
        var json = Fields(); json["serviceMode"] = mode;
        Assert.Throws<InvalidDataException>(() => Load(json));
    }

    [Theory]
    [InlineData("interruptible")]
    [InlineData("controls")]
    [InlineData("deadline")]
    public void ContradictoryAlwaysOnFails(string contradiction)
    {
        var json = Fields();
        json["serviceMode"] = "always-on";
        json["interruptible"] = contradiction == "interruptible";
        json["supportedControlLevels"] = contradiction == "controls" ? new[] { "freeze" } : Array.Empty<string>();
        if (contradiction == "deadline") json["deadlineHours"] = new { value = 1, unit = "hours", source = "test" };
        Assert.Throws<InvalidDataException>(() => Load(json));
    }

    [Theory]
    [InlineData("frontend")]
    [InlineData("api")]
    public void CommittedHostingComponentsLoadAsAlwaysOn(string component)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "config/jobs"))) root = root.Parent;
        Assert.NotNull(root);
        var job = JobDescriptorLoader.Load(Path.Combine(root!.FullName, "config/jobs", $"webhosting-{component}.json"));
        Assert.Equal("always-on", job.ServiceMode);
        Assert.Null(job.EnergyKwhPerWorkUnit);
        Assert.Throws<InvalidDataException>(() => JobDescriptorLoader.RequireBatchControl(job));
    }

    private static Dictionary<string, object?> Fields() => new()
    {
        ["id"] = "test", ["containerImage"] = "test:local", ["interruptible"] = true,
        ["supportedControlLevels"] = new[] { "freeze" }, ["workUnit"] = "unit",
        ["placementPolicy"] = new { mode = "anywhere", source = "Synthetic unit test" }
    };
    private static JobDescriptor Load(Dictionary<string, object?> json)
    {
        string path = Path.GetTempFileName();
        try { File.WriteAllText(path, JsonSerializer.Serialize(json)); return JobDescriptorLoader.Load(path); }
        finally { File.Delete(path); }
    }
}
