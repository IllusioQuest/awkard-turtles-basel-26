using System.Globalization;
using System.Text.Json;

namespace DigitalHeat.Fleet;

/// <summary>Things a distribution config may refer to (derived scale factors, the repo root for data files).</summary>
public sealed record DistributionContext(string RepoRoot, double? WorkUnitsPerSecondAtOneKw = null);

/// <summary>JSON helpers for sourced config values.</summary>
public static class InputJson
{
    public static JsonElement Prop(JsonElement e, string name, string where) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v : throw new InvalidDataException($"{where}: missing '{name}'.");

    public static string NonEmptySource(JsonElement e, string where)
    {
        string? s = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("source", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
        if (string.IsNullOrWhiteSpace(s)) throw new InvalidDataException($"{where}: 'source' must not be empty (use TODO(source) if unknown).");
        return s;
    }

    public static string Unit(JsonElement e, string where)
    {
        string? s = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("unit", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
        if (string.IsNullOrWhiteSpace(s)) throw new InvalidDataException($"{where}: 'unit' must not be empty.");
        return s;
    }

    public static double Number(JsonElement e, string name, string where)
    {
        var v = Prop(e, name, where);
        if (v.ValueKind != JsonValueKind.Number) throw new InvalidDataException($"{where}: '{name}' must be a number.");
        double d = v.GetDouble();
        if (!double.IsFinite(d)) throw new InvalidDataException($"{where}: '{name}' must be finite.");
        return d;
    }

    /// <summary>{value, unit, source}; the source must not be empty.</summary>
    public static Quantity Quantity(JsonElement e, string where) =>
        new(Number(e, "value", where), Unit(e, where), NonEmptySource(e, where));
}

public sealed class FixedDistribution(double value, string unit, string source) : IDistribution
{
    public double Sample(Rng rng) => value;
    public double Mean => value;
    public string Unit { get; } = unit;
    public string Source { get; } = source;
}

public sealed class UniformDistribution(double min, double max, string unit, string source) : IDistribution
{
    public double Sample(Rng rng) => rng.Uniform(min, max);
    public double Mean => (min + max) / 2;
    public string Unit { get; } = unit;
    public string Source { get; } = source;
}

/// <summary>Lognormal with the given median and sigma = SD of ln(X). Mean = median × exp(sigma² ÷ 2).</summary>
public sealed class LogNormalDistribution(double median, double sigma, string unit, string source) : IDistribution
{
    public double Sample(Rng rng) => sigma == 0 ? median : median * Math.Exp(sigma * rng.Normal());
    public double Mean => median * Math.Exp(sigma * sigma / 2);
    public string Unit { get; } = unit;
    public string Source { get; } = source;
}

public sealed class ChoiceDistribution : IDistribution
{
    private readonly double[] _values;
    private readonly double[] _cumulative;
    public ChoiceDistribution(double[] values, double[]? weights, string unit, string source)
    {
        if (values.Length == 0) throw new InvalidDataException("choice: values must not be empty.");
        weights ??= values.Select(_ => 1.0).ToArray();
        if (weights.Length != values.Length || weights.Any(w => w < 0) || weights.Sum() <= 0)
            throw new InvalidDataException("choice: weights must match values and sum to a positive number.");
        _values = values;
        double total = weights.Sum(), acc = 0;
        _cumulative = weights.Select(w => acc += w / total).ToArray();
        Mean = values.Zip(weights, (v, w) => v * w / total).Sum();
        Unit = unit; Source = source;
    }
    public double Sample(Rng rng)
    {
        double u = rng.NextDouble();
        int i = Array.FindIndex(_cumulative, c => u < c);
        return _values[i < 0 ? _values.Length - 1 : i];
    }
    public double Mean { get; }
    public string Unit { get; }
    public string Source { get; }
}

/// <summary>
/// Empirical quantile table (inverse CDF, piecewise linear): q = 0 maps to <c>min</c>, values between listed
/// quantiles are interpolated linearly, u above the top quantile is capped at its value. Several value columns
/// are summed with the same u: a comonotonic approximation (input and output tokens are treated as perfectly
/// rank-correlated), which overstates the spread of the sum.
/// </summary>
public sealed class QuantileDistribution : IDistribution
{
    private readonly double[] _q;
    private readonly double[] _v;
    private readonly double _scale;

    public QuantileDistribution(double[] quantiles, double[] values, double min, double scale, string unit, string source)
    {
        if (quantiles.Length == 0 || quantiles.Length != values.Length)
            throw new InvalidDataException("empirical: quantile table is empty or ragged.");
        var order = Enumerable.Range(0, quantiles.Length).OrderBy(i => quantiles[i]).ToArray();
        _q = new double[order.Length + 1];
        _v = new double[order.Length + 1];
        _q[0] = 0; _v[0] = min;
        for (int i = 0; i < order.Length; i++) { _q[i + 1] = quantiles[order[i]]; _v[i + 1] = values[order[i]]; }
        for (int i = 1; i < _q.Length; i++)
            if (_q[i] <= _q[i - 1] || _v[i] < _v[i - 1])
                throw new InvalidDataException("empirical: quantiles must increase strictly and values not decrease (min included).");
        _scale = scale;
        Unit = unit; Source = source;
        double mean = 0;
        for (int i = 0; i + 1 < _q.Length; i++) mean += (_q[i + 1] - _q[i]) * (_v[i] + _v[i + 1]) / 2;
        mean += (1 - _q[^1]) * _v[^1];
        Mean = mean * scale;
    }

    public double Inverse(double u)
    {
        if (u >= _q[^1]) return _v[^1] * _scale;
        int i = Array.BinarySearch(_q, u);
        if (i >= 0) return _v[i] * _scale;
        i = ~i - 1;
        double f = (u - _q[i]) / (_q[i + 1] - _q[i]);
        return (_v[i] + f * (_v[i + 1] - _v[i])) * _scale;
    }

    public double Sample(Rng rng) => Inverse(rng.NextDouble());
    public double Mean { get; }
    public string Unit { get; }
    public string Source { get; }
}

/// <summary>
/// Empirical histogram (bins like "&lt;1 min", "1-10 min", "&gt;3 d"), log-uniform within a bin. Open ends use
/// min_s / max_s. Weights are normalised. <c>scale</c> multiplies the sampled seconds (e.g. to tasks).
/// </summary>
public sealed class HistogramDistribution : IDistribution
{
    private readonly double[] _lo, _hi, _cumulative;
    private readonly double _scale;

    public HistogramDistribution(IReadOnlyList<(string Bin, double Weight)> bins, double minS, double maxS, double scale,
        string unit, string source)
    {
        if (bins.Count == 0 || minS <= 0 || maxS <= minS) throw new InvalidDataException("histogram: need bins and 0 < min_s < max_s.");
        _lo = new double[bins.Count]; _hi = new double[bins.Count];
        for (int i = 0; i < bins.Count; i++) (_lo[i], _hi[i]) = ParseBin(bins[i].Bin, minS, maxS);
        double total = bins.Sum(b => b.Weight);
        if (total <= 0) throw new InvalidDataException("histogram: weights must sum to > 0.");
        double acc = 0;
        _cumulative = bins.Select(b => acc += b.Weight / total).ToArray();
        _scale = scale;
        Unit = unit; Source = source;
        double mean = 0;
        for (int i = 0; i < bins.Count; i++)
            mean += bins[i].Weight / total * (_hi[i] - _lo[i]) / Math.Log(_hi[i] / _lo[i]);
        Mean = mean * scale;
    }

    private static double UnitSeconds(string u) => u switch
    {
        "s" or "sec" => 1, "min" => 60, "h" => 3600, "d" => 86400,
        _ => throw new InvalidDataException($"histogram: unknown time unit '{u}'.")
    };

    private static (double lo, double hi) ParseBin(string bin, double minS, double maxS)
    {
        var parts = bin.Trim().Split(' ', 2);
        if (parts.Length != 2) throw new InvalidDataException($"histogram: cannot parse bin '{bin}'.");
        double unit = UnitSeconds(parts[1]);
        string r = parts[0];
        double Num(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) * unit;
        if (r.StartsWith('<')) return (minS, Num(r[1..]));
        if (r.StartsWith('>')) return (Num(r[1..]), maxS);
        var ab = r.Split('-');
        if (ab.Length != 2) throw new InvalidDataException($"histogram: cannot parse bin '{bin}'.");
        return (Num(ab[0]), Num(ab[1]));
    }

    public double Sample(Rng rng)
    {
        double u = rng.NextDouble();
        int i = Array.FindIndex(_cumulative, c => u < c);
        if (i < 0) i = _cumulative.Length - 1;
        double x = Math.Exp(Math.Log(_lo[i]) + rng.NextDouble() * (Math.Log(_hi[i]) - Math.Log(_lo[i])));
        return x * _scale;
    }
    public double Mean { get; }
    public string Unit { get; }
    public string Source { get; }
}

public static class Distribution
{
    public const string ScaleWorkUnitsPerSecondAtOneKw = "work_units_per_second_at_1_kw";

    /// <summary>Parses <c>{dist, ..., unit, source}</c> (§5.1). Unit and source are mandatory.</summary>
    public static IDistribution Parse(JsonElement e, DistributionContext ctx, string where)
    {
        string kind = InputJson.Prop(e, "dist", where).GetString() ?? "";
        string unit = InputJson.Unit(e, where);
        string source = InputJson.NonEmptySource(e, where);
        double N(string n) => InputJson.Number(e, n, where);
        switch (kind)
        {
            case "fixed": return new FixedDistribution(N("value"), unit, source);
            case "uniform":
                if (N("max") < N("min")) throw new InvalidDataException($"{where}: max < min.");
                return new UniformDistribution(N("min"), N("max"), unit, source);
            case "lognormal":
                if (N("median") <= 0 || N("sigma") < 0) throw new InvalidDataException($"{where}: need median > 0, sigma >= 0.");
                return new LogNormalDistribution(N("median"), N("sigma"), unit, source);
            case "choice":
                var values = InputJson.Prop(e, "values", where).EnumerateArray().Select(x => x.GetDouble()).ToArray();
                double[]? weights = e.TryGetProperty("weights", out var w) ? w.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
                return new ChoiceDistribution(values, weights, unit, source);
            case "empirical": return ParseEmpirical(e, ctx, where, unit, source);
            default: throw new InvalidDataException($"{where}: unknown dist '{kind}'.");
        }
    }

    private static double ParseScale(JsonElement e, DistributionContext ctx, string where)
    {
        if (!e.TryGetProperty("scale", out var s)) return 1;
        if (s.ValueKind == JsonValueKind.Number) return s.GetDouble();
        if (s.ValueKind == JsonValueKind.String && s.GetString() == ScaleWorkUnitsPerSecondAtOneKw)
            return ctx.WorkUnitsPerSecondAtOneKw ?? throw new InvalidDataException($"{where}: scale needs the job type's energy per work unit.");
        return InputJson.Quantity(s, where + ".scale").Value;
    }

    private static IDistribution ParseEmpirical(JsonElement e, DistributionContext ctx, string where, string unit, string source)
    {
        var table = CsvTable.Read(Path.Combine(ctx.RepoRoot, InputJson.Prop(e, "file", where).GetString()!));
        var filter = e.TryGetProperty("filter", out var f)
            ? f.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
            : new Dictionary<string, string>();
        var rows = table.Rows.Where(r => filter.All(kv => table.Str(r, kv.Key) == kv.Value)).ToList();
        if (rows.Count == 0) throw new InvalidDataException($"{where}: no rows of {table.Path} match the filter.");
        double scale = ParseScale(e, ctx, where);
        if (e.TryGetProperty("bin_column", out var binCol))
        {
            string weightCol = InputJson.Prop(e, "weight_column", where).GetString()!;
            var bins = rows.Select(r => (table.Str(r, binCol.GetString()!), table.Num(r, weightCol))).ToList();
            return new HistogramDistribution(bins, InputJson.Quantity(InputJson.Prop(e, "min_s", where), where + ".min_s").Value,
                InputJson.Quantity(InputJson.Prop(e, "max_s", where), where + ".max_s").Value, scale, unit, source);
        }
        string qCol = InputJson.Prop(e, "quantile_column", where).GetString()!;
        var valueCols = InputJson.Prop(e, "value_columns", where).EnumerateArray().Select(x => x.GetString()!).ToArray();
        var qs = new List<double>(); var vs = new List<double>();
        foreach (var r in rows)
        {
            // The "mean" row is not a quantile and is ignored.
            if (!double.TryParse(table.Str(r, qCol), NumberStyles.Float, CultureInfo.InvariantCulture, out double q)) continue;
            qs.Add(q);
            vs.Add(valueCols.Sum(c => table.Num(r, c)));
        }
        return new QuantileDistribution(qs.ToArray(), vs.ToArray(), N(e, "min", where), scale, unit, source);
    }

    private static double N(JsonElement e, string name, string where) => InputJson.Number(e, name, where);
}

/// <summary>Exact Poisson sampler: Knuth's multiplication for λ &lt; 30, PTRS (Hörmann 1993) otherwise.</summary>
public static class Poisson
{
    /// <summary>Switch point between the two methods (Hörmann 1993 recommends PTRS for λ ≥ 10; 30 keeps Knuth cheap and exact).</summary>
    public const double PtrsThreshold = 30;

    public static int Sample(Rng rng, double lambda)
    {
        if (lambda < 0 || !double.IsFinite(lambda)) throw new ArgumentOutOfRangeException(nameof(lambda));
        if (lambda == 0) return 0;
        if (lambda < PtrsThreshold)
        {
            double limit = Math.Exp(-lambda), p = 1;
            int k = 0;
            do { k++; p *= rng.NextDouble(); } while (p > limit);
            return k - 1;
        }
        // Hörmann, W. (1993), "The transformed rejection method for generating Poisson random variables",
        // Insurance: Mathematics and Economics 12, algorithm PTRS.
        double slam = Math.Sqrt(lambda), logLam = Math.Log(lambda);
        double b = 0.931 + 2.53 * slam, a = -0.059 + 0.02483 * b;
        double invAlpha = 1.1239 + 1.1328 / (b - 3.4), vr = 0.9277 - 3.6224 / (b - 2);
        while (true)
        {
            double u = rng.NextDouble() - 0.5, v = rng.NextDouble();
            double us = 0.5 - Math.Abs(u);
            long k = (long)Math.Floor((2 * a / us + b) * u + lambda + 0.43);
            if (us >= 0.07 && v <= vr) return (int)k;
            if (k < 0 || (us < 0.013 && v > us)) continue;
            if (Math.Log(v) + Math.Log(invAlpha) - Math.Log(a / (us * us) + b) <= -lambda + k * logLam - LogFactorial(k))
                return (int)k;
        }
    }

    /// <summary>ln(k!): exact sum for k ≤ 20, Stirling series with three correction terms above (error &lt; 1e-12).</summary>
    public static double LogFactorial(long k)
    {
        if (k < 2) return 0;
        if (k <= 20) { double s = 0; for (int i = 2; i <= k; i++) s += Math.Log(i); return s; }
        double x = k;
        return x * Math.Log(x) - x + 0.5 * Math.Log(2 * Math.PI * x) + 1 / (12 * x) - 1 / (360 * x * x * x) + 1 / (1260 * Math.Pow(x, 5));
    }
}
