namespace DigitalHeat.Fleet;

// Shared contracts of the fleet simulation (docs/model/fleet-simulation.md). Ids are dense indices
// into the engine's arrays: HouseId into the house list, ServerId into the global server list,
// JobId into the sorted job request list, ChunkId in creation order.

public readonly record struct HouseId(int Value);
public readonly record struct ServerId(int Value);
public readonly record struct JobId(int Value);
public readonly record struct ChunkId(int Value);

/// <summary>Clock: long milliseconds since run start (§3). Run hour h = t / MsPerHour.</summary>
public static class FleetTime
{
    public const long MsPerSecond = 1_000;
    public const long MsPerHour = 3_600_000;
    public const double SecondsPerHour = 3_600;

    public static int HourOf(long timeMs) => checked((int)(timeMs / MsPerHour));
    public static double Hours(long durationMs) => durationMs / (double)MsPerHour;
}

/// <summary>A sourced number from config: {value, unit, source}.</summary>
public sealed record Quantity(double Value, string Unit, string Source);

/// <summary>
/// Deterministic PRNG (xoshiro256**, seeded with SplitMix64), stable across .NET versions,
/// unlike System.Random. One instance per stream (§6: one stream per job type).
/// </summary>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;
    private double? _spareNormal;

    public Rng(long seed)
    {
        ulong x = unchecked((ulong)seed);
        _s0 = SplitMix(ref x); _s1 = SplitMix(ref x); _s2 = SplitMix(ref x); _s3 = SplitMix(ref x);
    }

    private static ulong SplitMix(ref ulong x)
    {
        ulong z = unchecked(x += 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextULong()
    {
        ulong result = unchecked(Rotl(_s1 * 5, 7) * 9);
        ulong t = _s1 << 17;
        _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3; _s2 ^= t; _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform in [min, max).</summary>
    public double Uniform(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Standard normal (Box–Muller, the spare value is cached).</summary>
    public double Normal()
    {
        if (_spareNormal is double spare) { _spareNormal = null; return spare; }
        double u1;
        do { u1 = NextDouble(); } while (u1 <= double.Epsilon);
        double u2 = NextDouble();
        double r = Math.Sqrt(-2.0 * Math.Log(u1));
        _spareNormal = r * Math.Sin(2 * Math.PI * u2);
        return r * Math.Cos(2 * Math.PI * u2);
    }
}
