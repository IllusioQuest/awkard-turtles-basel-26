using System.Text.Json;
using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class ResidentialCompositionTests
{
    [Fact]
    public void CompositionOnlyPopulationCannotBeLoadedAsThermalHouseFile()
    {
        string root = Scenario.FindRepoRoot(AppContext.BaseDirectory);
        string path = Path.Combine(root, "data/derived/fleet/populations/bfs-residential-2020-covered-1000.nodes.json");
        // The composition array deliberately has no HouseSpec thermal defaults or `houses` wrapper.
        Assert.Throws<JsonException>(() => HouseFiles.Read(path));
    }
}
