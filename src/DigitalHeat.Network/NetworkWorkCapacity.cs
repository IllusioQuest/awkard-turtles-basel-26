using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>Unit conversions for the hourly cohort adapter.</summary>
internal static class NetworkWorkCapacity
{
    /// <summary>Shared heat-first load at one site.</summary>
    public static double HeatLedTarget(NetworkSite site, SiteHeatState state) =>
        new HeatFirstScheduler(site.MinimumLoadShare).Decide(state.Signal).TargetLoadPerServer;

    public static double Capacity(NetworkSite site) => site.ServerCount * site.HoursPerStep;

}
