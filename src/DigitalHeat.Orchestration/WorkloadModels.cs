using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

public static class WorkloadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
    public static string Identity<T>(T value) => JsonSerializer.Serialize(value, Options);
}

/// <summary>CPU-equivalent quota, not pinned cores. Slots bound application/desktop concurrency.</summary>
public sealed record ResourceAmount(double CpuCores, long RamMib, int Slots);
public sealed record GpuDevice(string Id, string Capability, long? VramMib = null);
public sealed record ResourceDomain(string Id, ResourceAmount Capacity, GpuDevice[] Gpus);
public sealed record WorkloadSite(string Id, string CountryCode, HeatSignal Heat, double ExternalCommittedHeatKwh);
public sealed record LatencyLimit(string PeerId, double MaxRttMs);
public sealed record LatencyObservation(string PeerId, double RttMs);
public sealed record ComputeNode(string Id, string SiteId, string DomainId, bool Ready, bool NetworkEligible,
    ResourceAmount Capacity, string[] Capabilities, string[] GpuDeviceIds, string[] StorageIds,
    LatencyObservation[] Latencies);
/// <summary>External allocations exclude this coordinator's claim IDs, even after restart.</summary>
public sealed record ExternalAllocation(string Id, string NodeId, ResourceAmount Resources, string? GpuDeviceId);
public sealed record WorkloadInventory(long Revision, DateTimeOffset IntervalStartUtc, DateTimeOffset IntervalEndUtc,
    string Source, WorkloadSite[] Sites, ResourceDomain[] Domains, ComputeNode[] Nodes, ExternalAllocation[] ExternalAllocations)
{
    // Legacy slot-only adapters omit telemetry. Real resource admissions require both timestamps.
    public DateTimeOffset? ResourceObservedAtUtc { get; init; }
    public DateTimeOffset? ResourceValidUntilUtc { get; init; }
}
public sealed record LifecyclePolicy(bool AllowPause, bool AllowStop, bool Restartable);
/// <summary>Absolute operator-submitted batch budget; never renewed by retry, pause or restart.</summary>
public sealed record JobDeadline(DateTimeOffset SubmittedAtUtc, DateTimeOffset DueAtUtc, string Source, bool StopWhenOverdue);
public sealed record WorkloadRequest(string Id, string Kind, string OriginSiteId, JobPlacementPolicy PlacementPolicy,
    ResourceAmount Resources, string ResourceSource, double RecoverableHeatKwhPerHour, string HeatSource,
    string[] RequiredCapabilities, string? GpuCapability, string? PinnedNodeId, string? StorageId,
    LatencyLimit[] LatencyLimits, LifecyclePolicy Lifecycle)
{
    public long? MinGpuVramMib { get; init; }
    // Omit only this new optional field: legacy container spec hashes must remain byte-compatible.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JobDeadline? Deadline { get; init; }
}
public sealed record WorkloadReservation(string WorkloadId, string SiteId, string NodeId, string DomainId,
    string? GpuDeviceId, double RecoverableHeatKwhPerHour, double AvailableHeatKwhBeforeReservation);
public sealed record AllocationView(WorkloadRequest Request, WorkloadReservation Reservation, bool Paused);
