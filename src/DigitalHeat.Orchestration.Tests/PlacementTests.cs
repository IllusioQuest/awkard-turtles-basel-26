using DigitalHeat.Core;
using DigitalHeat.Orchestration;
using DigitalHeat.Network;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class PlacementTests
{
    internal static readonly DateTimeOffset Now = new(2025, 7, 15, 17, 0, 0, TimeSpan.Zero);
    internal static WorkloadRequest Request(string id = "game", string kind = "interactive", double cpu = 1, long ram = 64, string? gpu = null) =>
        new(id, kind, "home", new("anywhere", "Synthetic test authorization", null), new(cpu, ram, 1), "Synthetic resource fixture",
            0.8, "Synthetic heat fixture, not a measurement", [], gpu, null, null, [], new(kind == "batch", true, false));
    internal static WorkloadInventory Inventory() => new(1, Now, Now.AddHours(1), "Synthetic fixture",
        [new("home", "CH", new(1, 0, 2, 1, 0.8), 0), new("spa", "CH", new(4, 0, 10, 1, 0.8), 0)],
        [new("host", new(2, 256, 4), [new("gpu0", "opengl")])],
        [new("a", "home", "host", true, true, new(2, 256, 2), ["game"], ["gpu0"], ["world"], [new("player", 10)]),
         new("b", "spa", "host", true, true, new(2, 256, 2), ["game"], ["gpu0"], [], [new("player", 20)])], [])
        { ResourceObservedAtUtc = Now, ResourceValidUntilUtc = Now.AddMinutes(1) };
    private static WorkloadPlacement Start(WorkloadInventory? inventory = null)
    { var p = new WorkloadPlacement(); p.Update(inventory ?? Inventory(), Now); return p; }

    [Fact] public void LargestUsefulHeatWinsAndOwnedHeatIsSharedPerSite()
    {
        var p = Start(); Assert.Equal("b", p.TryReserve(Request(), Now)!.NodeId);
        Assert.Equal(14 - 0.8, p.TryReserve(Request("two"), Now)!.AvailableHeatKwhBeforeReservation, 8);
        Assert.Null(p.TryReserve(Request("three"), Now)); // shared physical CPU is full, despite two logical nodes
    }

    [Fact] public void NetworkSelectedSiteIsBindingAndInventoryVersionMustMatch()
    {
        var p = Start();
        var request = Request();
        var homeSignal = Inventory().Sites[0].Heat;
        Assert.Null(p.TryReserveAtSite(request, "missing", homeSignal, 1, Now, Now));
        Assert.Null(p.TryReserveAtSite(request, "home", homeSignal, 2, Now, Now));
        Assert.Null(p.TryReserveAtSite(request, "home", homeSignal, 1, Now.AddHours(-1), Now));
        Assert.Null(p.TryReserveAtSite(request, "home", homeSignal with { DemandKwh = 9 }, 1, Now, Now));
        var home = p.TryReserveAtSite(request, "home", homeSignal, 1, Now, Now);
        Assert.Equal("a", home!.NodeId);
        Assert.Throws<InvalidOperationException>(() => p.TryReserveAtSite(request, "spa", Inventory().Sites[1].Heat, 1, Now, Now));
        Assert.Single(p.Allocations());

        var busy = Start(Inventory() with { ExternalAllocations = [new("occupied", "a", new(2, 64, 1), null)] });
        Assert.Null(busy.TryReserveAtSite(Request("new"), "home", homeSignal, 1, Now, Now));
        Assert.Empty(busy.Allocations()); // The eligible spa is not a fallback.
    }

    [Fact] public void B2SelectedSiteCanBeAdmittedWithoutAnIndependentCrossSiteChoice()
    {
        NetworkSite Site(string id, double heatKwh) => new(id, id, 0, 0, "CH", 1, 1, 1, 0, 0, 1, 0.3, 0.1,
            [new("2025-07-15T18:00", 0, false, heatKwh, 0), new("2025-07-15T19:00", 0, false, heatKwh, 0)]);
        var sites = new[] { Site("home", 0), Site("spa", 1) };
        var ledger = new WorkLedger(sites, [new Type1Spec(1, 1, 1,
            new JobPlacementPolicy("anywhere", "Synthetic test authorization", null)), null], [0, 0], 2, 24);
        ledger.BeginHour(0);
        var states = sites.Select((s, i) => new SiteHeatState(s.Id,
            new HeatSignal(s.Demand[0].TotalHeatKwh, 0, 0, 1, 1),
            HeatForecast.Build(s.Demand, s.Id, 0, 1))).ToArray();
        var decision = new NetworkGreedyJobsScheduler().Decide(new NetworkState(0, sites[0].Demand[0].LocalTime,
            sites, states, ledger.View()));
        var chosenSite = sites[decision.Allocations!.Single().SiteIndex];
        Assert.Equal("spa", chosenSite.Id);

        var fixture = Inventory();
        var inventory = fixture with { Sites = fixture.Sites.Select(s => s with
            { Heat = states[Array.FindIndex(sites, x => x.Id == s.Id)].Signal }).ToArray() };
        var placement = Start(inventory);
        var reservation = placement.TryReserveAtSite(Request("job", "batch"), chosenSite.Id,
            states[1].Signal, inventory.Revision, inventory.IntervalStartUtc, Now);
        Assert.Equal("spa", reservation!.SiteId);
        Assert.Equal("b", reservation.NodeId);
    }

    [Fact] public async Task LastPhysicalCpuAndGpuCanOnlyBeReservedOnce()
    {
        var p = Start();
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() => p.TryReserve(Request(i.ToString(), gpu: "opengl"), Now))));
        Assert.Single(results, r => r is not null); Assert.Single(p.Allocations());
    }

    [Fact] public void LowHeatNodeIsUsedWhenHighHeatNodeDoesNotFit()
    {
        var i = Inventory(); i = i with { Nodes = [i.Nodes[0], i.Nodes[1] with { Capacity = new(0.5, 32, 2) }] };
        Assert.Equal("a", Start(i).TryReserve(Request(), Now)!.NodeId);
    }

    [Fact] public void RequestedGpuMemoryCannotUseUnknownOrInsufficientDeviceMemory()
    {
        var request = Request(gpu: "opengl") with { MinGpuVramMib = 4096 };
        Assert.Null(Start().TryReserve(request, Now));
        var i = Inventory(); i = i with { Domains = [i.Domains[0] with { Gpus = [new("gpu0", "opengl", 2048)] }] };
        Assert.Null(Start(i).TryReserve(request, Now));
        i = i with { Domains = [i.Domains[0] with { Gpus = [new("gpu0", "opengl", 8192)] }] };
        Assert.NotNull(Start(i).TryReserve(request, Now));
    }

    [Fact] public void ExternalClaimsAndPhysicalGpuAreNotCountedPerContainer()
    {
        var i = Inventory() with { ExternalAllocations = [new("existing-game", "a", new(1, 200, 1), "gpu0")] };
        var p = Start(i);
        Assert.Null(p.TryReserve(Request(gpu: "opengl"), Now));
        Assert.Null(p.TryReserve(Request(ram: 64), Now)); // only 56 MiB physical budget remains
        Assert.NotNull(p.TryReserve(Request(ram: 32), Now));
        var observedTwice = i with { Revision = 2, ExternalAllocations = [new("game", "a", new(1, 32, 1), null)] };
        Assert.Throws<InvalidDataException>(() => p.Update(observedTwice, Now));
    }

    [Fact] public void PausingOnlyReleasesCpuAndModelHeatNotRamGpuOrSlots()
    {
        var p = Start(); var batch = Request("batch", "batch", 2, 200, "opengl");
        Assert.NotNull(p.TryReserve(batch, Now)); p.ObservePaused("batch");
        Assert.Null(p.TryReserve(Request("gpu", ram: 32, gpu: "opengl"), Now));
        Assert.Null(p.TryReserve(Request("ram", ram: 64), Now));
        Assert.NotNull(p.TryReserve(Request("small", ram: 32), Now));
        Assert.False(p.TryClaimResume("batch", Now));
        p.Release("small"); Assert.True(p.TryClaimResume("batch", Now));
        Assert.False(p.Allocations().Single().Paused);
        Assert.Throws<InvalidOperationException>(() => { var game = Start(); game.TryReserve(Request(), Now); game.ObservePaused("game"); });
    }

    [Fact] public void LocalityCapabilityStorageAndMeasuredLatencyFailClosed()
    {
        var p = Start(); var request = Request() with { PlacementPolicy = new("origin-site", "fixture contract", null),
            PinnedNodeId = "a", StorageId = "world", RequiredCapabilities = ["game"], LatencyLimits = [new("player", 15)] };
        Assert.Equal("a", p.TryReserve(request, Now)!.NodeId); p.Release("game");
        Assert.Null(p.TryReserve(request with { Id = "unknown", LatencyLimits = [new("missing-peer", 15)] }, Now));
        Assert.Null(p.TryReserve(request with { Id = "slow", LatencyLimits = [new("player", 5)] }, Now));
        Assert.Null(p.TryReserve(request with { Id = "storage", StorageId = "missing" }, Now));
        Assert.Null(p.TryReserve(request with { Id = "cap", RequiredCapabilities = ["missing"] }, Now));
        Assert.Null(p.TryReserve(request with { Id = "country", PlacementPolicy = new("country-allowlist", "fixture", ["DE"]) }, Now));
    }

    [Fact] public void MissingOrStaleResourceTelemetryBlocksNewClaimsButNotIdempotentRetry()
    {
        Assert.Null(Start(Inventory() with { ResourceObservedAtUtc = null, ResourceValidUntilUtc = null }).TryReserve(Request(), Now));
        var p = Start(); var held = p.TryReserve(Request(), Now);
        Assert.Equal(held, p.TryReserve(Request(), Now.AddMinutes(2)));
        Assert.Null(p.TryReserve(Request("new"), Now.AddMinutes(2)));
        Assert.Throws<InvalidDataException>(() => p.TryReserve(Request(cpu: 0.5), Now));
    }

    [Fact] public void SnapshotRemovalCannotEraseHeldResourcesAndExternalArraysAreCopied()
    {
        var p = Start(); p.TryReserve(Request(), Now);
        var i = Inventory(); p.Update(i with { Revision = 2, Nodes = [i.Nodes[0]] }, Now);
        i.Nodes[0].GpuDeviceIds[0] = "mutated"; // caller mutation cannot change the ledger's copy
        Assert.Equal("a", p.TryReserve(Request("second"), Now)!.NodeId);
        Assert.Null(p.TryReserve(Request("third"), Now));
        Assert.Throws<InvalidDataException>(() => p.Update(i with { Revision = 3, Nodes = [i.Nodes[1] with { SiteId = "home" }] }, Now));
    }

    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)]
    public void RejectsInvalidResourceNumbers(double cpu) => Assert.Throws<InvalidDataException>(() => Start().TryReserve(Request(cpu: cpu), Now));

    [Fact] public void RestoringOwnershipPreservesPauseSemanticsAndRejectsCorruptJournal()
    {
        var p = Start(); p.TryReserve(Request("batch", "batch", 2), Now); p.ObservePaused("batch");
        var restored = new WorkloadPlacement(); restored.Restore(p.Allocations()); restored.Update(Inventory(), Now);
        Assert.NotNull(restored.TryReserve(Request("game"), Now));
        Assert.False(restored.TryClaimResume("batch", Now));
        var corrupt = p.Allocations()[0];
        Assert.Throws<InvalidDataException>(() => new WorkloadPlacement().Restore([corrupt with { Reservation = corrupt.Reservation with { NodeId = "" } }]));
    }
}
