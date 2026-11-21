using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class IndexedAdmissionTests
{
    [Fact] public void SeededDecisionsMatchPreOptimizationOracleAcrossPauseResumeAndInventoryChanges()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var rng = new Random(seed);
            var old = new BaselineWorkloadPlacement(); var indexed = new WorkloadPlacement();
            var sites = Enumerable.Range(0, 4).Select(i => new WorkloadSite($"s{i}", i == 3 ? "DE" : "CH", new(2 + i, 0, 4, 1, .8), 0)).ToArray();
            var domains = Enumerable.Range(0, 4).Select(i => new ResourceDomain($"d{i}", new(4, 512, 8), [new($"g{i}", "opengl", 8192)])).ToArray();
            var allNodes = Enumerable.Range(0, 16).Select(i => new ComputeNode($"n{i:D2}", sites[i % 4].Id, domains[i % 4].Id, true, true,
                new(2, 256, 4), ["game"], [$"g{i % 4}"], ["world"], [new("player", 10 + i)])).ToArray();
            var inventory = new WorkloadInventory(1, PlacementTests.Now, PlacementTests.Now.AddHours(1), "Synthetic differential fixture", sites, domains, allNodes, [])
            { ResourceObservedAtUtc = PlacementTests.Now, ResourceValidUntilUtc = PlacementTests.Now.AddMinutes(1) };
            old.Update(inventory, PlacementTests.Now); indexed.Update(inventory, PlacementTests.Now);
            var live = new List<string>();
            for (int step = 0; step < 80; step++)
            {
                if (step > 0 && step % 13 == 0)
                {
                    inventory = inventory with { Revision = inventory.Revision + 1,
                        Sites = sites.Select(s => s with { Heat = s.Heat with { DemandKwh = rng.NextDouble() * 5 } }).ToArray(),
                        Nodes = allNodes.Where((_, i) => step < 40 || i != 0).Select(n => n with { Ready = rng.Next(5) != 0 }).ToArray(),
                        ExternalAllocations = [new("external", "n15", new(.1, 16, 1), null)] };
                    old.Update(inventory, PlacementTests.Now); indexed.Update(inventory, PlacementTests.Now);
                }
                bool batch = rng.Next(2) == 0, gpu = rng.Next(4) == 0;
                string? pin = rng.Next(5) == 0 ? "n15" : null;
                var request = new WorkloadRequest($"r{step}", batch ? "batch" : "interactive", "s0",
                    new(rng.Next(2) == 0 ? "origin-site" : "country-allowlist", "Synthetic differential authorization", null),
                    new(new[] { .1, .2, .3, .5, 1, 2 }[rng.Next(6)], rng.Next(1, 5) * 32, 1), "Synthetic", .1 + rng.Next(4) * .2,
                    "Synthetic", [rng.Next(8) == 0 ? "missing" : "game"], gpu ? "opengl" : null, pin, pin is null ? null : "world",
                    [new("player", 20)], new(batch, true, false)) { MinGpuVramMib = gpu ? 4096 : null };
                if (request.PlacementPolicy.Mode == "country-allowlist") request = request with { PlacementPolicy = request.PlacementPolicy with { AllowedCountryCodes = ["CH"] } };
                Assert.Equal(old.CanAdmitAfterBatchPause(request, PlacementTests.Now), indexed.CanAdmitAfterBatchPause(request, PlacementTests.Now));
                var expected = old.TryReserve(request, PlacementTests.Now); var actual = indexed.TryReserve(request, PlacementTests.Now);
                Assert.Equal(expected, actual);
                if (actual is not null) live.Add(request.Id);
                if (live.Count > 0 && rng.Next(3) == 0)
                {
                    string id = live[rng.Next(live.Count)];
                    var claim = indexed.Allocations().Single(c => c.Request.Id == id);
                    if (claim.Request.Kind == "batch")
                    {
                        old.ObservePaused(id); indexed.ObservePaused(id);
                        Assert.Equal(old.TryClaimResume(id, PlacementTests.Now), indexed.TryClaimResume(id, PlacementTests.Now));
                    }
                    if (rng.Next(2) == 0) { Assert.Equal(old.Release(id), indexed.Release(id)); live.Remove(id); }
                }
                Assert.Equal(WorkloadJson.Identity(old.Allocations()), WorkloadJson.Identity(indexed.Allocations()));
            }
        }
    }

    [Fact] public void ExactAndAdjacentFloatingCapacityBoundariesPreserveSummationOrder()
    {
        foreach (double capacity in new[] { Math.BitDecrement(.6), .6, Math.BitIncrement(.6) })
        {
            var source = PlacementTests.Inventory();
            var inventory = source with { Nodes = source.Nodes.Select(n => n with { Capacity = new(capacity, 256, 8) }).ToArray(),
                Domains = [source.Domains[0] with { Capacity = new(capacity, 256, 8) }] };
            var old = new BaselineWorkloadPlacement(); var indexed = new WorkloadPlacement(); old.Update(inventory, PlacementTests.Now); indexed.Update(inventory, PlacementTests.Now);
            foreach (var (cpu, id) in new[] { (.1, "one"), (.2, "two"), (.3, "three") })
                Assert.Equal(old.TryReserve(PlacementTests.Request(id, cpu: cpu), PlacementTests.Now), indexed.TryReserve(PlacementTests.Request(id, cpu: cpu), PlacementTests.Now));
        }
    }

    [Fact] public void NestedInputAndReturnedArrayMutationsCannotAlterOwnedIndexesOrClaims()
    {
        var inventory = PlacementTests.Inventory();
        var request = PlacementTests.Request(gpu: "opengl") with { PlacementPolicy = new("country-allowlist", "Synthetic", ["CH"]),
            RequiredCapabilities = ["game"], LatencyLimits = [new("player", 30)] };
        var placement = new WorkloadPlacement(); placement.Update(inventory, PlacementTests.Now);
        var reservation = placement.TryReserve(request, PlacementTests.Now)!;
        inventory.Domains[0].Gpus[0] = new("changed", "other", 1);
        inventory.Nodes[1].Capabilities[0] = "changed"; inventory.Nodes[1].GpuDeviceIds[0] = "changed";
        request.PlacementPolicy.AllowedCountryCodes![0] = "US"; request.RequiredCapabilities[0] = "changed"; request.LatencyLimits[0] = new("other", 0);
        var returned = placement.Allocations(); returned[0].Request.PlacementPolicy.AllowedCountryCodes![0] = "DE";
        returned[0].Request.RequiredCapabilities[0] = "changed again";
        var owned = placement.Allocations().Single(); Assert.Equal("CH", owned.Request.PlacementPolicy.AllowedCountryCodes![0]);
        Assert.Equal("game", owned.Request.RequiredCapabilities[0]); Assert.Equal("player", owned.Request.LatencyLimits[0].PeerId);
        Assert.Equal("gpu0", reservation.GpuDeviceId); Assert.True(placement.CanStartHeld(owned.Request.Id, PlacementTests.Now));
    }
}
