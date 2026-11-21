using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class FleetSimulationTests
{
    private static Scenario ReferenceJanuary()
    {
        var root = Scenario.FindRepoRoot(AppContext.BaseDirectory);
        var s = Scenario.Load(Path.Combine(root, "config/fleet/scenarios/reference.json"));
        s.Run.EndLocal = "2025-02-01T00:00";
        return s;
    }

    [Fact]
    public void SameScenarioGivesByteIdenticalOutputExceptTiming()
    {
        string a = Path.Combine(Path.GetTempPath(), "fleet-det-a-" + Guid.NewGuid());
        string b = Path.Combine(Path.GetTempPath(), "fleet-det-b-" + Guid.NewGuid());
        try
        {
            FleetOutput.Write(FleetSimulation.Run(ReferenceJanuary()), a);
            FleetOutput.Write(FleetSimulation.Run(ReferenceJanuary()), b);
            foreach (var f in Directory.GetFiles(a).Where(f => Path.GetFileName(f) != "run.json"))
                Assert.Equal(File.ReadAllBytes(f), File.ReadAllBytes(Path.Combine(b, Path.GetFileName(f))));
            Assert.True(File.Exists(Path.Combine(a, "run.json")));
        }
        finally { Directory.Delete(a, true); Directory.Delete(b, true); }
    }

    [Fact]
    public void ReferenceHouseWholeYearMatchesCaseDemand()
    {
        var s = ReferenceJanuary();
        s.Run.EndLocal = "2026-01-01T00:00";
        s.Demand.VolumeScale = s.JobTypes.ToDictionary(Path.GetFileNameWithoutExtension, _ => 0.01);
        var run = FleetSimulation.Run(s);
        Assert.Equal(206, run.Result.Houses[0].HeatingDays);
        Assert.Equal(26850.5, run.Result.Houses[0].Account.SpaceHeatKwh, 1);
    }
}
