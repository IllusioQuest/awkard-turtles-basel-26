using System.Globalization;
using DigitalHeat.Core;

namespace DigitalHeat.Network;

public sealed record SiteSummary(string SiteId, double ServerCount, double TotalHeatDemandKwh,
    double ServerHeatUsedKwh, double BackupHeatKwh, double DumpedHeatKwh, double FinalTankKwh,
    double ServerHours, double Utilisation, double Coverage, double ElectricityCostChf,
    double HeatValueChf, double? CaseBreakEvenChfPerServerHour);

public sealed record SiteResult(NetworkSite Site, IReadOnlyList<HourResult> Hours, SiteSummary Summary);

public sealed record NetworkResult(string SchedulerId, string SchedulerLabel, string ForecastLabel,
    int HorizonHours, IReadOnlyList<SiteResult> Sites, WorkResult? Work = null);

/// <summary>
/// Hourly loop over all sites (decision 0024). Each hour: build every site's signal and forecast,
/// ask the network scheduler once, actuate each site through <see cref="SchedulerController.Apply"/>
/// and its <see cref="IExecutor"/>, then settle the realised physics with the shared
/// <see cref="HeatBalance.Step"/> on actual (not forecast) demand.
/// </summary>
public static class NetworkSimulation
{
    public static NetworkResult Run(IReadOnlyList<NetworkSite> sites, INetworkScheduler scheduler,
        IForecastProvider forecasts, int horizonHours, IReadOnlyList<IExecutor>? executors = null,
        WorkLedger? work = null)
    {
        if (sites.Count == 0) throw new ArgumentException("No sites.", nameof(sites));
        if (horizonHours < 1) throw new ArgumentOutOfRangeException(nameof(horizonHours));
        int hours = sites[0].Demand.Count;
        if (sites.Any(s => s.Demand.Count != hours || s.Demand.Count == 0))
            throw new ArgumentException("All sites need the same non-empty number of hours.", nameof(sites));
        for (int h = 0; h < hours; h++)
            if (sites.Any(s => s.Demand[h].LocalTime != sites[0].Demand[h].LocalTime))
                throw new ArgumentException("Sites must share the same hourly time axis.", nameof(sites));
        executors ??= sites.Select(_ => (IExecutor)new SimulatedExecutor()).ToArray();
        if (executors.Count != sites.Count) throw new ArgumentException("One executor per site.", nameof(executors));

        var padded = sites.Select(s => PadOneHour(s.Demand)).ToArray();
        var tanks = new double[sites.Count];
        var rows = sites.Select(_ => new List<HourResult>(hours)).ToArray();

        for (int h = 0; h < hours; h++)
        {
            work?.BeginHour(h);
            int horizon = Math.Min(horizonHours, hours - h);
            var states = new SiteHeatState[sites.Count];
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var signal = new HeatSignal(site.Demand[h].TotalHeatKwh, tanks[i], site.TankCapacityKwh,
                    site.ServerCount, site.RecoverableKwhPerServerHour);
                states[i] = new SiteHeatState(site.Id, signal, forecasts.Build(i, site, padded[i], h, horizon));
            }
            var decision = scheduler.Decide(new NetworkState(h, sites[0].Demand[h].LocalTime, sites, states, work?.View()));
            if (decision.PerSite.Count != sites.Count)
                throw new InvalidOperationException("Network decision must contain one entry per site.");
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var hour = site.Demand[h];
                var signal = states[i].Signal;
                var applied = SchedulerController.Apply(signal,
                    new CommittedDecisionScheduler(signal, decision.PerSite[i]), executors[i]);
                double produced = applied.ActualLoadPerServer * site.ServerCount * site.RecoverableKwhPerServerHour;
                var balance = HeatBalance.Step(produced, hour.TotalHeatKwh, tanks[i], site.TankCapacityKwh);
                tanks[i] = balance.TankStoredKwh;
                if (work is not null)
                {
                    // All achieved load is filler when a strategy plans no allocations (step-1 strategies).
                    double achievedServerHours = applied.ActualLoadPerServer * site.ServerCount * site.HoursPerStep;
                    work.Apply(i, decision.Allocations ?? [],
                        decision.FillerServerHours?[i] ?? achievedServerHours, achievedServerHours);
                }
                rows[i].Add(new HourResult(hour.LocalTime, hour.TotalHeatKwh, hour.SpaceHeatKwh, hour.HotWaterKwh,
                    applied.TargetLoadPerServer, applied.ActualLoadPerServer, produced, balance.DirectUsedKwh,
                    balance.TankChargeKwh, balance.TankDischargeKwh, balance.TankStoredKwh, balance.DumpedHeatKwh,
                    balance.BackupHeatKwh,
                    applied.ActualLoadPerServer * site.ServerCount * site.ServerElectricKw * site.HoursPerStep));
            }
        }
        var results = sites.Select((s, i) => new SiteResult(s, rows[i], Summarise(s, rows[i]))).ToArray();
        return new NetworkResult(scheduler.Id, scheduler.Label, forecasts.Label, horizonHours, results, work?.Finish());
    }

    /// <summary>Same formulas as <see cref="YearSimulation"/> (case break-even: electricity − used heat value).</summary>
    public static SiteSummary Summarise(NetworkSite site, IReadOnlyList<HourResult> rows)
    {
        double total = rows.Sum(r => r.DemandKwh), backup = rows.Sum(r => r.BackupHeatKwh);
        double used = total - backup;
        double serverHours = rows.Sum(r => r.ActualLoadPerServer * site.ServerCount * site.HoursPerStep);
        double electricity = rows.Sum(r => r.ServerElectricKwh) * site.ElectricityPriceChfPerKwh;
        double heatValue = used * site.HeatValueChfPerKwh;
        return new SiteSummary(site.Id, site.ServerCount, total, used, backup, rows.Sum(r => r.DumpedHeatKwh),
            rows.Count > 0 ? rows[^1].TankStoredKwh : 0, serverHours,
            serverHours / (site.ServerCount * rows.Count * site.HoursPerStep), total > 0 ? used / total : 0,
            electricity, heatValue, serverHours > 0 ? (electricity - heatValue) / serverHours : null);
    }

    /// <summary>
    /// Appends one copy of the last hour so the final hour still has a one-hour forecast
    /// (HeatForecast needs at least one future hour). Affects only the last hour's forecast.
    /// </summary>
    private static IReadOnlyList<DemandHour> PadOneHour(IReadOnlyList<DemandHour> demand)
    {
        var last = demand[^1];
        var next = DateTime.ParseExact(last.LocalTime, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture).AddHours(1);
        return demand.Append(last with { LocalTime = next.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) })
            .ToArray();
    }
}
