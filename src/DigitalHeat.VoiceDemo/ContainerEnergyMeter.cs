using System.Globalization;

namespace DigitalHeat.VoiceDemo;

/// <summary>
/// Measured energy of one container on this machine. CPU time comes from the container's cgroup
/// (<c>cpu.stat usage_usec</c>, exact per container). Energy is the CPU package counter (Intel/AMD RAPL,
/// <c>/sys/class/powercap/intel-rapl:0/energy_uj</c>), attributed to the container by its share of
/// all busy CPU time in each interval (<c>/proc/stat</c>), the method Scaphandre and Kepler use.
/// Package only: RAM, disk and display are not included. Without a readable RAPL counter only CPU time is reported.
/// </summary>
public sealed class ContainerEnergyMeter
{
    private const string Rapl = "/sys/class/powercap/intel-rapl:0";
    private readonly string cpuStatPath;
    private readonly double usecPerTick;
    private readonly long raplRangeUj;
    private long lastUj, lastBusyUsec, lastContainerUsec;
    private DateTime lastSample;
    private bool started;

    public double CpuSeconds { get; private set; }
    public double EnergyJoules { get; private set; }
    public double PowerWatts { get; private set; }
    public bool HasEnergy { get; }
    public double EnergyKwh => EnergyJoules / 3.6e6;

    public ContainerEnergyMeter(string containerId, long clockTicksPerSecond)
    {
        cpuStatPath = new[] { $"/sys/fs/cgroup/system.slice/docker-{containerId}.scope/cpu.stat", $"/sys/fs/cgroup/docker/{containerId}/cpu.stat" }
            .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("No cgroup v2 cpu.stat for the container.");
        usecPerTick = 1e6 / clockTicksPerSecond;
        try { raplRangeUj = long.Parse(File.ReadAllText(Path.Combine(Rapl, "max_energy_range_uj")).Trim(), CultureInfo.InvariantCulture); ReadUj(); HasEnergy = true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException) { HasEnergy = false; }
    }

    /// <summary>Take one sample; returns false once the container's cgroup is gone (exited and removed).</summary>
    public bool Sample()
    {
        long container;
        try { container = ReadContainerUsec(); } catch (IOException) { return false; }
        long busy = ReadBusyUsec(); long uj = HasEnergy ? ReadUj() : 0; var now = DateTime.UtcNow;
        if (started)
        {
            long dContainer = Math.Max(0, container - lastContainerUsec), dBusy = busy - lastBusyUsec;
            CpuSeconds += dContainer / 1e6;
            if (HasEnergy)
            {
                long dUj = uj >= lastUj ? uj - lastUj : uj + raplRangeUj - lastUj; // counter wrap
                double share = dBusy > 0 ? Math.Min(1, (double)dContainer / dBusy) : 0;
                double joules = dUj / 1e6 * share;
                EnergyJoules += joules;
                double seconds = (now - lastSample).TotalSeconds;
                PowerWatts = seconds > 0 ? joules / seconds : 0;
            }
        }
        lastContainerUsec = container; lastBusyUsec = busy; lastUj = uj; lastSample = now; started = true;
        return true;
    }

    private long ReadContainerUsec()
    {
        foreach (var line in File.ReadLines(cpuStatPath))
            if (line.StartsWith("usage_usec ", StringComparison.Ordinal)) return long.Parse(line[11..], CultureInfo.InvariantCulture);
        throw new IOException("usage_usec missing");
    }

    private long ReadBusyUsec()
    {
        // cpu  user nice system idle iowait irq softirq steal guest guest_nice (clock ticks); busy = all but idle and iowait.
        var f = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8)
            .Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        return (long)((f[0] + f[1] + f[2] + f[5] + f[6] + f[7]) * usecPerTick);
    }

    private static long ReadUj() => long.Parse(File.ReadAllText(Path.Combine(Rapl, "energy_uj")).Trim(), CultureInfo.InvariantCulture);
}
