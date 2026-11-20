using Webhosting.Core;

namespace Webhosting.Tests;

public class PlacementTests
{
    // Small synthetic capacities are test fixtures, not estimates of production hardware.
    private static readonly NodeCapacity[] Nodes = [new("a", 2, 512, true, true), new("b", 2, 512, true, true), new("c", 2, 512, true, true)];
    private static readonly WorkloadDemand[] Workloads = [new("demo/api", 2, 1, 128), new("demo/frontend", 2, 1, 128)];

    [Fact]
    public void ReplicasUseDistinctNodesAndStayWithinCapacity()
    {
        var plan = Placement.Plan(Nodes, Workloads, []);
        Assert.Empty(plan.Shortfalls);
        Assert.All(Workloads, w => Assert.Equal(w.Replicas,
            plan.Assignments.Where(a => a.WorkloadId == w.WorkloadId).Select(a => a.NodeId).Distinct().Count()));
        Assert.All(Nodes, n => Assert.True(plan.Assignments.Count(a => a.NodeId == n.NodeId) <= n.CpuCapacityCores));
    }

    [Fact]
    public void EverySingleNodeFailureCanBeRepaired()
    {
        var initial = Placement.Plan(Nodes, Workloads, []);
        foreach (var failed in Nodes)
        {
            var plan = Placement.Plan(Nodes.Select(n => n with { Healthy = n.NodeId != failed.NodeId }).ToArray(), Workloads, initial.Assignments);
            Assert.Empty(plan.Shortfalls);
            Assert.DoesNotContain(plan.Assignments, a => a.NodeId == failed.NodeId);
            Assert.Equal(4, plan.Assignments.Count);
        }
    }

    [Fact]
    public void ReconciliationAndInputOrderDoNotMoveHealthyReplicas()
    {
        var initial = Placement.Plan(Nodes, Workloads, []);
        var repeated = Placement.Plan(Nodes.Reverse().ToArray(), Workloads.Reverse().ToArray(), initial.Assignments.Reverse().ToArray());
        Assert.Equal(initial.Assignments, repeated.Assignments);
    }

    [Fact]
    public void InsufficientNodesReportShortfallInsteadOfColocatingReplicas()
    {
        var plan = Placement.Plan([Nodes[0]], [new("api", 2, 1, 128)], []);
        Assert.Single(plan.Assignments);
        Assert.Equal(new PlacementShortfall("api", 1), Assert.Single(plan.Shortfalls));
    }

    [Fact]
    public void MemoryLimitsPreventOvercommit()
    {
        var plan = Placement.Plan([new("a", 100, 128, true, true)], [new("a", 1, 1, 128), new("b", 1, 1, 128)], []);
        Assert.Single(plan.Assignments);
        Assert.Single(plan.Shortfalls);
    }

    [Fact]
    public void RejectingNewWorkDoesNotEvictRunningWork()
    {
        NodeCapacity[] closed = [Nodes[0] with { AcceptingWork = false }];
        WorkloadDemand[] demand = [new("api", 1, 1, 128)];
        Assert.Single(Placement.Plan(closed, demand, [new("api", "a")]).Assignments);
        Assert.Empty(Placement.Plan(closed, demand, []).Assignments);
    }

    [Fact]
    public void ExistingLaterWorkloadIsReservedBeforeFillingEarlierGap()
    {
        var plan = Placement.Plan([new("a", 1, 128, true, true)],
            [new("alpha", 1, 1, 128), new("zeta", 1, 1, 128)], [new("zeta", "a")]);
        Assert.Equal(new Assignment("zeta", "a"), Assert.Single(plan.Assignments));
        Assert.Equal("alpha", Assert.Single(plan.Shortfalls).WorkloadId);
    }

    [Fact]
    public void DuplicateExistingReportsDoNotConsumeExtraCapacity()
    {
        var plan = Placement.Plan(Nodes, [new("api", 2, 1, 128)], [new("api", "a"), new("api", "a")]);
        Assert.Equal(2, plan.Assignments.Count);
        Assert.Empty(plan.Shortfalls);
    }

    [Fact]
    public void InvalidResourceValuesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Placement.Plan(Nodes, [new("api", 1, double.NaN, 128)], []));
        Assert.Throws<ArgumentException>(() => Placement.Plan([Nodes[0], Nodes[0]], Workloads, []));
    }
}
