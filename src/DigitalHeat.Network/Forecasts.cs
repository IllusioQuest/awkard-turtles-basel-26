using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>Builds the heat-demand forecast a site sees at a given hour.</summary>
public interface IForecastProvider
{
    /// <summary>Label written into results, e.g. "perfect foresight" (must be visible, decision 0024).</summary>
    string Label { get; }
    HeatForecast Build(int siteIndex, NetworkSite site, IReadOnlyList<DemandHour> paddedDemand, int hourIndex,
        int horizonHours);
}

/// <summary>Actual future demand. An upper-bound information set, never a realistic result.</summary>
public sealed class PerfectForesightForecasts : IForecastProvider
{
    public string Label => "perfect foresight (actual 2025 demand)";
    public HeatForecast Build(int siteIndex, NetworkSite site, IReadOnlyList<DemandHour> paddedDemand,
        int hourIndex, int horizonHours) =>
        HeatForecast.Build(paddedDemand, site.Id, hourIndex, horizonHours);
}

/// <summary>
/// Synthetic forecast error (decision 0024 §7): relative error on forecast hour k (k = 1..H),
/// e_k = rho · e_(k−1) + sigma_k · xi_k with xi standard normal, e_0 = 0, and
/// sigma_k = sigma24 · sqrt(k / 24), so uncertainty grows with lead time. A fresh, seeded draw is made
/// for every (site, hour), so runs are reproducible. Values of sigma24 and rho: TODO(source)
/// (candidate: Open-Meteo historical-forecast archive vs actual weather); shown as a labelled sweep.
/// </summary>
public sealed class Ar1ErrorForecasts(double sigma24, double rho, int seed) : IForecastProvider
{
    public string Label => $"synthetic AR(1) forecast error, sigma24={sigma24:R}, rho={rho:R}, seed={seed}";

    public HeatForecast Build(int siteIndex, NetworkSite site, IReadOnlyList<DemandHour> paddedDemand,
        int hourIndex, int horizonHours)
    {
        var rng = new Random(unchecked(seed * 1_000_003 + siteIndex * 10_007 + hourIndex));
        var errors = new double[horizonHours];
        double previous = 0;
        for (int k = 1; k <= horizonHours; k++)
        {
            double sigma = sigma24 * Math.Sqrt(k / 24.0);
            previous = rho * previous + sigma * StandardNormal(rng);
            errors[k - 1] = Math.Max(-1, previous);
        }
        return HeatForecast.Build(paddedDemand, site.Id, hourIndex, horizonHours, errors);
    }

    /// <summary>Box–Muller transform.</summary>
    private static double StandardNormal(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
