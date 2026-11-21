using Xunit;

namespace DigitalHeat.Orchestration.Tests;

// Synthetic admission checks only: these heat values are not measured CS power.
public class GamingGpuPlacementTests
{
    [Fact]
    public void GpuRequirementFiltersNodesBeforeUsefulHeatRanking()
    {
        var inventory = PlacementTests.Inventory();
        inventory = inventory with { Nodes = [inventory.Nodes[0], inventory.Nodes[1] with { GpuDeviceIds = [] }] };
        var placement = new WorkloadPlacement();
        placement.Update(inventory, PlacementTests.Now);

        // Spa has more useful heat capacity, but cannot render this GPU-required job.
        Assert.Equal("a", placement.TryReserve(PlacementTests.Request(gpu: "opengl"), PlacementTests.Now)!.NodeId);
        placement.Release("game");
        placement.Update(PlacementTests.Inventory() with { Revision = 2 }, PlacementTests.Now);
        Assert.Equal("b", placement.TryReserve(PlacementTests.Request(gpu: "opengl"), PlacementTests.Now)!.NodeId);
    }

    [Fact]
    public void BusySharedGpuCannotSilentlyFallBackToSoftware()
    {
        var inventory = PlacementTests.Inventory() with
        {
            ExternalAllocations = [new("existing-renderer", "a", new(.1, 16, 1), "gpu0")]
        };
        var placement = new WorkloadPlacement();
        placement.Update(inventory, PlacementTests.Now);
        Assert.Null(placement.TryReserve(PlacementTests.Request(gpu: "opengl"), PlacementTests.Now));
        Assert.Empty(placement.Allocations());
    }

    [Fact]
    public void MoreHeatCannotMoveAnActiveInteractiveGpuSession()
    {
        var placement = new WorkloadPlacement();
        var inventory = PlacementTests.Inventory();
        placement.Update(inventory, PlacementTests.Now);
        var request = PlacementTests.Request(gpu: "opengl");
        var held = placement.TryReserve(request, PlacementTests.Now);
        Assert.Equal("b", held!.NodeId);
        placement.Update(inventory with
        {
            Revision = 2,
            Sites = inventory.Sites.Select(s => s.Id == "home"
                ? s with { Heat = s.Heat with { DemandKwh = 100 } } : s).ToArray()
        }, PlacementTests.Now);
        Assert.Equal(held, placement.TryReserve(request, PlacementTests.Now));
        Assert.Throws<InvalidOperationException>(() => placement.ObservePaused(request.Id));
    }
}
