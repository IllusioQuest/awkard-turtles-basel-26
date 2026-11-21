using DigitalHeat.Core;
using Xunit;

namespace DigitalHeat.Core.Tests;

public sealed class HardwareProfileTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static string Dir() => Path.Combine(Root(), "config", "hardware");

    [Fact]
    public void AllProfilesLoadAndAreNormalisedToTheCaseServer()
    {
        var reference = ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));
        var profiles = HardwareProfile.LoadDirectory(Dir());
        Assert.Equal(["cpu-server", "gpu-consumer-rtx4090", "gpu-inference-l4"], profiles.Select(x => x.Id));
        foreach (var p in profiles)
        {
            Assert.Equal(reference.ServerElectricKw.Value, p.ServerElectricKw.Value);
            Assert.StartsWith("variant", p.Status);
        }
    }

    [Fact]
    public void ReferenceDefaultsAreUnchangedByProfiles()
    {
        var reference = ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));
        Assert.Equal(1, reference.ServerElectricKw.Value);
        Assert.Equal(0.8, reference.HeatRecoveryShare.Value);
    }

    [Fact]
    public void DepreciationIsPurchasePriceOverLifetime()
    {
        var cpu = HardwareProfile.Load(Path.Combine(Dir(), "cpu-server.json"));
        Assert.Equal(50743.37 / 6, cpu.DepreciationChfPerServerYear, 6);
        Assert.Equal(436, cpu.TranscriptionAudioHoursPerServerHour.Value);
    }

    [Fact]
    public void TodoValuesStayVisibleAsNull()
    {
        var l4 = HardwareProfile.Load(Path.Combine(Dir(), "gpu-inference-l4.json"));
        Assert.False(l4.IdleElectricKw.IsKnown);
        Assert.StartsWith("TODO(source)", l4.IdleElectricKw.Source);
    }

    [Theory]
    [InlineData("\"unit\": \"kW/server\", \"source\": \"docs/case/challenge.md: rack-server",
                "\"unit\": \"W/server\", \"source\": \"docs/case/challenge.md: rack-server")]
    [InlineData("\"value\": 50743.37", "\"value\": 40000")]
    [InlineData("\"value\": 0.205, \"unit\": \"kW/server\", \"source\": \"derived",
                "\"value\": null, \"unit\": \"kW/server\", \"source\": \"derived")]
    public void InvalidProfilesAreRejected(string from, string to)
    {
        var text = File.ReadAllText(Path.Combine(Dir(), "cpu-server.json"));
        Assert.Contains(from, text);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, text.Replace(from, to));
            Assert.Throws<InvalidDataException>(() => HardwareProfile.Load(path));
        }
        finally { File.Delete(path); }
    }
}
