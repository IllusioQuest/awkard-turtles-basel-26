using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// Runs the same network work scheduler inside Core's reference-house loop when compute work is
/// unlimited. The demand series supplies the NetworkState time axis and forecast; the shared policy uses
/// only the current signal. Finite cohorts require WorkLedger and are intentionally unsupported.
/// </summary>
public sealed class SingleSiteUnlimitedNetworkScheduler : IScheduler
{
    private readonly NetworkSite site;
    private readonly INetworkScheduler networkScheduler;
    private readonly IReadOnlyList<DemandHour> paddedDemand;
    private int hourIndex;

    public SingleSiteUnlimitedNetworkScheduler(NetworkSite site, INetworkScheduler networkScheduler)
    {
        this.site = site;
        this.networkScheduler = networkScheduler;
        if (site.Demand.Count == 0) throw new ArgumentException("A demand series is required.", nameof(site));
        var last = site.Demand[^1];
        var next = DateTime.ParseExact(last.LocalTime, "yyyy-MM-dd'T'HH:mm",
            System.Globalization.CultureInfo.InvariantCulture).AddHours(1);
        paddedDemand = site.Demand.Append(last with
            { LocalTime = next.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture) }).ToArray();
    }

    public SchedulerDecision Decide(HeatSignal heatSignal)
    {
        if (hourIndex >= site.Demand.Count) throw new InvalidOperationException("Demand series exhausted.");
        var hour = site.Demand[hourIndex];
        if (heatSignal.DemandKwh != hour.TotalHeatKwh || heatSignal.ServerCount != site.ServerCount
            || heatSignal.TankCapacityKwh != site.TankCapacityKwh
            || heatSignal.RecoverableKwhPerServerAtFullLoad != site.RecoverableKwhPerServerHour)
            throw new InvalidOperationException("Reference-house signal differs from the network site's inputs.");
        var forecast = HeatForecast.Build(paddedDemand, site.Id, hourIndex, 1);
        var state = new NetworkState(hourIndex, hour.LocalTime, [site],
            [new SiteHeatState(site.Id, heatSignal, forecast)],
            new WorkView([], [site.ServerCount * site.HoursPerStep]));
        var decision = networkScheduler.Decide(state);
        if (decision.PerSite.Count != 1) throw new InvalidOperationException("Expected exactly one site decision.");
        hourIndex++;
        return decision.PerSite[0];
    }
}
