namespace DigitalHeat.Orchestration;

/// <summary>Built only from the validated owned snapshot and published with it under the admission lock.</summary>
internal sealed class AdmissionIndex(WorkloadInventory snapshot)
{
    public Dictionary<string, ComputeNode> NodesById { get; } = snapshot.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
    public Dictionary<string, ResourceDomain> DomainsById { get; } = snapshot.Domains.ToDictionary(d => d.Id, StringComparer.Ordinal);
    public Dictionary<string, ComputeNode[]> NodesBySite { get; } = snapshot.Nodes.GroupBy(n => n.SiteId, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
    public HashSet<string> ExternalIds { get; } = snapshot.ExternalAllocations.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
    public Dictionary<string, ResourceUsage> ExternalByNode { get; } = snapshot.ExternalAllocations.GroupBy(e => e.NodeId, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => ResourceUsage.External(g.ToArray()), StringComparer.Ordinal);
    public Dictionary<string, ResourceUsage> ExternalByDomain { get; } = ExternalDomains(snapshot);

    private static Dictionary<string, ResourceUsage> ExternalDomains(WorkloadInventory snapshot)
    {
        var nodes = snapshot.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        return snapshot.ExternalAllocations.GroupBy(e => nodes[e.NodeId].DomainId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ResourceUsage.External(g.ToArray()), StringComparer.Ordinal);
    }
}

/// <summary>Preserves original group enumeration and separate Sum operations; never subtract rounded totals.</summary>
internal sealed record ResourceUsage(double Cpu, decimal RamMib, decimal Slots, double Heat, HashSet<string?> Gpus)
{
    public static readonly ResourceUsage Empty = new(0, 0, 0, 0, []);
    public static ResourceUsage Owned(AllocationView[] claims) => new(
        claims.Where(c => !c.Paused).Sum(c => c.Request.Resources.CpuCores),
        claims.Sum(c => (decimal)c.Request.Resources.RamMib), claims.Sum(c => (decimal)c.Request.Resources.Slots),
        claims.Where(c => !c.Paused).Sum(c => c.Request.RecoverableHeatKwhPerHour),
        claims.Select(c => c.Reservation.GpuDeviceId).ToHashSet());
    public static ResourceUsage External(ExternalAllocation[] claims) => new(
        claims.Sum(c => c.Resources.CpuCores), claims.Sum(c => (decimal)c.Resources.RamMib),
        claims.Sum(c => (decimal)c.Resources.Slots), 0, claims.Select(c => c.GpuDeviceId).ToHashSet());
}
