using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// One heat site of the network (decision 0024). All numbers come from a source-labelled parameter
/// set; <see cref="FromReference"/> maps the case's reference house. Location fields feed the map.
/// </summary>
public sealed record NetworkSite(
    string Id,
    string Name,
    double LatitudeDeg,
    double LongitudeDeg,
    string CountryCode,
    double ServerCount,
    double ServerElectricKw,
    double RecoverableKwhPerServerHour,
    double TankCapacityKwh,
    double MinimumLoadShare,
    double HoursPerStep,
    double ElectricityPriceChfPerKwh,
    double HeatValueChfPerKwh,
    IReadOnlyList<DemandHour> Demand)
{
    /// <summary>Recoverable heat of all servers at full load in one step, kWh.</summary>
    public double MaxHeatKwh => ServerCount * RecoverableKwhPerServerHour;

    /// <summary>Reference house: every value from config/reference-house.json (case parameters).</summary>
    public static NetworkSite FromReference(string id, string name, double latitudeDeg, double longitudeDeg,
        string countryCode, IReadOnlyList<DemandHour> demand, ReferenceParameters p) => new(
        id, name, latitudeDeg, longitudeDeg, countryCode,
        p.ServerCount.Value, p.ServerElectricKw.Value,
        p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * p.HoursPerStep.Value,
        p.TankCapacityKwh.Value, p.MinimumLoadShare.Value, p.HoursPerStep.Value,
        p.ElectricityPriceChfPerKwh.Value,
        p.ElectricityPriceChfPerKwh.Value / p.HeatPumpEfficiency.Value, demand);
}

/// <summary>What a network scheduler sees in one hour: every site's signal and forecast.</summary>
public sealed record NetworkState(int HourIndex, string LocalTime, IReadOnlyList<NetworkSite> Sites,
    IReadOnlyList<SiteHeatState> States);

/// <summary>One decision per site, in the order of <see cref="NetworkState.Sites"/>.</summary>
public sealed record NetworkDecision(IReadOnlyList<SchedulerDecision> PerSite);

public interface INetworkScheduler
{
    /// <summary>Short id used in result files, e.g. "B1".</summary>
    string Id { get; }
    /// <summary>Human-readable label incl. what information the strategy uses (forecast kind).</summary>
    string Label { get; }
    NetworkDecision Decide(NetworkState state);
}

/// <summary>
/// Feeds a site's committed network decision through the unchanged actuation path
/// (<see cref="SchedulerController.Apply"/> → <see cref="IExecutor"/>). Throws if the signal it is
/// asked about is not the one the decision was made on, so a stale decision can never be applied.
/// </summary>
public sealed class CommittedDecisionScheduler(HeatSignal decidedOn, SchedulerDecision decision) : IScheduler
{
    public SchedulerDecision Decide(HeatSignal heatSignal)
    {
        if (heatSignal != decidedOn)
            throw new InvalidOperationException("Heat signal differs from the one the network decision was made on.");
        return decision;
    }
}
