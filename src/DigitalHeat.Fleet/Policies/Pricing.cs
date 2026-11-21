namespace DigitalHeat.Fleet;

/// <summary>Values behind the last Type 2 quote of <see cref="MockLoadPricing"/> (logging, tests).</summary>
public sealed record MockQuoteDiagnostics(
    double U, double CapacityKwh, double CommittedKwh, double PRefWeekChf, double AcceptProbability, double PriceChf);

internal static class PricingCommon
{
    public static Quote Type1(IJobView job) => new(
        job.Type.Price.ChfPerWorkUnit?.Value
            ?? throw new InvalidDataException($"Job type {job.Type.Id} is Type 1 but has no fixed price."),
        true);
}

/// <summary>
/// Type 1: the fixed price of the job type, accepted. Type 2: p_min, accepted (backlog and
/// cross-check runs, §7).
/// </summary>
public sealed class FixedOnlyPricing(PricingSection cfg) : IPricing
{
    public string Id => "fixed-only";

    public Quote Quote(IJobView job, IFleetView view) =>
        job.Type.Class == JobClass.Type1 ? PricingCommon.Type1(job) : new Quote(cfg.PMinChf, true);
}

/// <summary>
/// Mock Type 2 pricing (spec §7, §7.1). Type 1: fixed price, accepted. Type 2, per quote:
/// <list type="bullet">
/// <item>capacity_kWh = Σ over run hours t overlapping [arrival, deadline] of overlap_hours(t) × cap_kw(t),
/// cap_kw(t) = Σ houses min(servers × max_electric_kw, forecast_heat_kw(issued = now hour, t) ÷ heat_recovery_share)</item>
/// <item>committed_kWh = view.CommittedKwh(deadline) + remaining units × kWh per unit; u = min(1, committed ÷ capacity), capacity ≤ 0 gives u = 1</item>
/// <item>price = p_min + (p_max − p_min) × u^γ (CHF per billed server-hour; p_min = case break-even, p_max = top of the CPU range)</item>
/// <item>acceptance probability = min(1, (price ÷ p_ref_w)^ε), constant elasticity (§7); p_ref_w = p_ref × max(0.01, 1 + sd × N(0,1)) per week
/// (noise: Baughman et al. 2019, §7.1); Bernoulli draw from its own stream, one uniform per Type 2 quote</item>
/// </list>
/// Speed: the forecast for t ≥ issued + 72 h is at lead 3 and independent of the issue hour, so a static
/// array far[t] (issued = t − 72) with a prefix sum is built once (lazily extended); only the 72 near
/// hours are recomputed when the first quote of a new hour comes. A quote is then O(1) amortised.
/// </summary>
public sealed class MockLoadPricing : IPricing
{
    private const int NearHours = 72;
    private const int HoursPerWeek = 168;
    private const long MsPerHour = FleetTime.MsPerHour;

    private readonly PricingSection _cfg;
    private readonly Rng _acceptRng;
    private readonly Rng _refRng;
    private readonly List<double> _weekFactor = [];

    private int _nearIssued = -1;
    private readonly double[] _near = new double[NearHours];
    private readonly double[] _nearPrefix = new double[NearHours + 1];
    private readonly List<double> _farPrefix = [0.0];   // _farPrefix[k] = Σ far[0..k)

    public MockLoadPricing(PricingSection cfg, long runSeed)
    {
        _cfg = cfg;
        _acceptRng = new Rng(runSeed + 1_000_001);
        _refRng = new Rng(runSeed + 1_000_003);
    }

    public string Id => "mock-load";

    /// <summary>Diagnostics of the last Type 2 quote, null before the first one.</summary>
    public MockQuoteDiagnostics? LastQuote { get; private set; }

    public Quote Quote(IJobView job, IFleetView view)
    {
        if (job.Type.Class == JobClass.Type1) return PricingCommon.Type1(job);

        var req = job.Request;
        double capacity = WindowCapacityKwh(view, req.ArrivalMs, req.DeadlineMs);
        double committed = view.CommittedKwh(req.DeadlineMs) + job.RemainingUnits * job.Type.EnergyKwhPerWorkUnit.Value;
        double u = capacity <= 0 ? 1.0 : Math.Min(1.0, committed / capacity);
        double price = _cfg.PMinChf + (_cfg.PMaxChf - _cfg.PMinChf) * Math.Pow(u, _cfg.Gamma);

        int week = (view.StartHourOfWeek + (int)(req.ArrivalMs / MsPerHour)) / HoursPerWeek;
        double pRef = PRefOfWeek(week);
        double prob = Math.Min(1.0, Math.Pow(price / pRef, _cfg.Elasticity));
        bool accepted = _acceptRng.NextDouble() < prob;   // always one draw (stream stability)

        LastQuote = new MockQuoteDiagnostics(u, capacity, committed, pRef, prob, price);
        return new Quote(price, accepted);
    }

    private double PRefOfWeek(int week)
    {
        while (_weekFactor.Count <= week)
            _weekFactor.Add(Math.Max(0.01, 1.0 + _cfg.PRefNoiseSd * _refRng.Normal()));
        return _cfg.PRefChf * _weekFactor[week];
    }

    private static double CapKw(IFleetView v, int issued, int target)
    {
        double maxKw = v.ServerParams.MaxElectricKw, share = v.ServerParams.HeatRecoveryShare, sum = 0;
        var houses = v.Houses;
        for (int i = 0; i < houses.Count; i++)
        {
            var h = houses[i];
            sum += Math.Min(h.Servers.Count * maxKw, v.Forecast.HeatDemandKw(h.Id, issued, target) / share);
        }
        return sum;
    }

    private void EnsureNear(IFleetView v, int issued)
    {
        if (issued == _nearIssued) return;
        _nearIssued = issued;
        for (int k = 0; k < NearHours; k++)
        {
            _near[k] = CapKw(v, issued, issued + k);
            _nearPrefix[k + 1] = _nearPrefix[k] + _near[k];
        }
    }

    /// <summary>Makes far[t] and its prefix sum available for t &lt; hours (far[t] = 0 for t &lt; 72, never used).</summary>
    private void EnsureFar(IFleetView v, int hours)
    {
        while (_farPrefix.Count - 1 < hours)
        {
            int t = _farPrefix.Count - 1;
            double far = t < NearHours ? 0.0 : CapKw(v, t - NearHours, t);
            _farPrefix.Add(_farPrefix[^1] + far);
        }
    }

    private double CapAt(IFleetView v, int t)
    {
        int i = _nearIssued;
        if (t < i) return CapKw(v, i, t);
        if (t < i + NearHours) return _near[t - i];
        return _farPrefix[t + 1] - _farPrefix[t];
    }

    /// <summary>Σ cap_kw(t) over full hours lo ≤ t &lt; hi.</summary>
    private double RangeSum(IFleetView v, int lo, int hi)
    {
        if (hi <= lo) return 0;
        int i = _nearIssued;
        double sum = 0;
        for (int t = lo; t < Math.Min(hi, i); t++) sum += CapKw(v, i, t);          // before the issue hour (not expected)
        int a = Math.Max(lo, i), b = Math.Min(hi, i + NearHours);
        if (b > a) sum += _nearPrefix[b - i] - _nearPrefix[a - i];
        int c = Math.Max(lo, i + NearHours);
        if (hi > c) sum += _farPrefix[hi] - _farPrefix[c];
        return sum;
    }

    private double WindowCapacityKwh(IFleetView v, long arrivalMs, long deadlineMs)
    {
        if (deadlineMs <= arrivalMs) return 0;
        int h0 = (int)(arrivalMs / MsPerHour), h1 = (int)((deadlineMs - 1) / MsPerHour);
        EnsureNear(v, v.HourIndex);
        EnsureFar(v, h1 + 1);
        if (h0 == h1) return CapAt(v, h0) * (deadlineMs - arrivalMs) / MsPerHour;
        double first = CapAt(v, h0) * (((h0 + 1) * MsPerHour) - arrivalMs) / MsPerHour;
        double last = CapAt(v, h1) * (deadlineMs - h1 * MsPerHour) / MsPerHour;
        return first + RangeSum(v, h0 + 1, h1) + last;
    }
}

public static class PricingFactory
{
    /// <summary>"mock-load" | "fixed-only".</summary>
    public static IPricing FromConfig(PricingSection cfg, long runSeed) => cfg.Id switch
    {
        "mock-load" => new MockLoadPricing(cfg, runSeed),
        "fixed-only" => new FixedOnlyPricing(cfg),
        _ => throw new InvalidDataException($"Unknown pricing.id '{cfg.Id}'."),
    };
}
