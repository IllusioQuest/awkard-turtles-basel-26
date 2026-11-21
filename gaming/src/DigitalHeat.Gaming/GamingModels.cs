using DigitalHeat.Core;

namespace DigitalHeat.Gaming;

/// <summary>Hourly site signal. External commitments exclude claims owned by SessionPlacement.</summary>
public sealed record GamingSite(string Id, string CountryCode, HeatSignal Heat,
    double ExternalCommittedHeatKwh);

/// <summary>Allocatable slots exclude external workloads but include this coordinator's held slots.</summary>
public sealed record GamingNode(string Id, string SiteId, bool Ready, bool NetworkEligible,
    int AllocatableSessionSlots);

/// <summary>Trusted inventory for one aligned UTC hour; revisions increase across updates.</summary>
public sealed record GamingSnapshot(long Revision, DateTimeOffset IntervalStartUtc, DateTimeOffset IntervalEndUtc,
    string Source, GamingSite[] Sites, GamingNode[] Nodes);

public sealed record SessionRequest(string Id, string OriginSiteId, JobPlacementPolicy PlacementPolicy,
    double RecoverableHeatKwhPerHour);

/// <summary>A held compute/heat claim, not evidence of a launched game.</summary>
public sealed record SessionReservation(string SessionId, string SiteId, string NodeId,
    double RecoverableHeatKwhPerHour, double AvailableHeatKwhBeforeReservation);
