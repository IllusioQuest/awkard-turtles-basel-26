namespace Webhosting.Core;

public sealed record NodeCapacity(
    string NodeId, double CpuCapacityCores, long MemoryCapacityMiB, bool Healthy, bool AcceptingWork);

public sealed record WorkloadDemand(
    string WorkloadId, int Replicas, double CpuLimitCores, long MemoryLimitMiB);

public sealed record Assignment(string WorkloadId, string NodeId);
public sealed record PlacementShortfall(string WorkloadId, int MissingReplicas);
public sealed record PlacementPlan(IReadOnlyList<Assignment> Assignments, IReadOnlyList<PlacementShortfall> Shortfalls);

/// <summary>
/// Deterministic capacity-constrained placement with one replica of each workload per node.
/// This is infrastructure placement, not a heat model: callers supply node eligibility.
/// The input must contain the complete desired workload set sharing these node capacities.
/// </summary>
public static class Placement
{
    public static PlacementPlan Plan(
        IReadOnlyList<NodeCapacity> nodes, IReadOnlyList<WorkloadDemand> workloads,
        IReadOnlyList<Assignment> existing)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(workloads);
        ArgumentNullException.ThrowIfNull(existing);
        if (nodes.Select(n => n.NodeId).Distinct(StringComparer.Ordinal).Count() != nodes.Count ||
            nodes.Any(n => string.IsNullOrWhiteSpace(n.NodeId) || !double.IsFinite(n.CpuCapacityCores) ||
                n.CpuCapacityCores < 0 || n.MemoryCapacityMiB < 0))
            throw new ArgumentException("Nodes must have unique IDs and finite nonnegative capacities.", nameof(nodes));
        if (workloads.Select(w => w.WorkloadId).Distinct(StringComparer.Ordinal).Count() != workloads.Count ||
            workloads.Any(w => string.IsNullOrWhiteSpace(w.WorkloadId) || w.Replicas < 1 ||
                !double.IsFinite(w.CpuLimitCores) || w.CpuLimitCores <= 0 || w.MemoryLimitMiB <= 0))
            throw new ArgumentException("Workloads must have unique IDs and positive replica/resource requirements.", nameof(workloads));

        var available = nodes.Where(n => n.Healthy).OrderBy(n => n.NodeId, StringComparer.Ordinal)
            .ToDictionary(n => n.NodeId, n => new Budget(n), StringComparer.Ordinal);
        var demands = workloads.OrderBy(w => w.WorkloadId, StringComparer.Ordinal).ToArray();
        var result = new List<Assignment>();
        var placements = demands.ToDictionary(w => w.WorkloadId, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        // Reserve all retained replicas first, before filling gaps for any workload.
        // A node that rejects NEW work retains its running replicas until a separate safe drain.
        foreach (var demand in demands)
        {
            foreach (var prior in existing.Where(a => a.WorkloadId == demand.WorkloadId)
                         .OrderBy(a => a.NodeId, StringComparer.Ordinal))
            {
                if (placements[demand.WorkloadId].Count == demand.Replicas) break;
                if (placements[demand.WorkloadId].Contains(prior.NodeId) ||
                    !available.TryGetValue(prior.NodeId, out var budget) || !budget.Fits(demand)) continue;
                Assign(demand, budget);
            }
        }

        var shortfalls = new List<PlacementShortfall>();
        foreach (var demand in demands)
        {
            while (placements[demand.WorkloadId].Count < demand.Replicas)
            {
                var next = available.Values.Where(n => n.Node.AcceptingWork && n.Fits(demand) &&
                        !placements[demand.WorkloadId].Contains(n.Node.NodeId))
                    .OrderBy(n => n.AssignedCount).ThenBy(n => n.Node.NodeId, StringComparer.Ordinal).FirstOrDefault();
                if (next is null) break;
                Assign(demand, next);
            }
            var missing = demand.Replicas - placements[demand.WorkloadId].Count;
            if (missing > 0) shortfalls.Add(new(demand.WorkloadId, missing));
        }
        return new(result.OrderBy(a => a.WorkloadId, StringComparer.Ordinal)
            .ThenBy(a => a.NodeId, StringComparer.Ordinal).ToArray(), shortfalls);

        void Assign(WorkloadDemand demand, Budget budget)
        {
            budget.Reserve(demand);
            placements[demand.WorkloadId].Add(budget.Node.NodeId);
            result.Add(new(demand.WorkloadId, budget.Node.NodeId));
        }
    }

    private sealed class Budget(NodeCapacity node)
    {
        public NodeCapacity Node { get; } = node;
        private double CpuAvailableCores { get; set; } = node.CpuCapacityCores;
        private long MemoryAvailableMiB { get; set; } = node.MemoryCapacityMiB;
        public int AssignedCount { get; private set; }
        public bool Fits(WorkloadDemand demand) => CpuAvailableCores >= demand.CpuLimitCores &&
                                                   MemoryAvailableMiB >= demand.MemoryLimitMiB;
        public void Reserve(WorkloadDemand demand)
        {
            CpuAvailableCores -= demand.CpuLimitCores;
            MemoryAvailableMiB -= demand.MemoryLimitMiB;
            AssignedCount++;
        }
    }
}
