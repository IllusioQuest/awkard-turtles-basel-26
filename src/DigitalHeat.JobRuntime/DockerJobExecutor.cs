using DigitalHeat.Core;

namespace DigitalHeat.JobRuntime;

/// <summary>Maps heat-led 0..1 requests to Docker freeze and CPU quota controls.</summary>
public sealed class DockerJobExecutor : IExecutor
{
    private const double MinimumDockerCpuCores = 0.01; // Docker CLI --cpus granularity; see ADR 0006.
    private readonly IContainerControl control;
    private readonly string containerId;
    private readonly double allowedCpuCores;
    private readonly bool supportsThrottle;
    private bool? paused;

    public DockerJobExecutor(IContainerControl control, string containerId, double allowedCpuCores,
        bool supportsThrottle)
    {
        if (string.IsNullOrWhiteSpace(containerId) || !double.IsFinite(allowedCpuCores)
            || allowedCpuCores < MinimumDockerCpuCores)
            throw new ArgumentOutOfRangeException(nameof(allowedCpuCores));
        this.control = control;
        this.containerId = containerId;
        this.allowedCpuCores = allowedCpuCores;
        this.supportsThrottle = supportsThrottle;
    }

    public double ApplyTargetLoad(double targetLoadPerServer)
    {
        if (!double.IsFinite(targetLoadPerServer) || targetLoadPerServer is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(targetLoadPerServer));
        if (targetLoadPerServer == 0)
        {
            if (paused != true) control.Pause(containerId);
            paused = true;
            return 0;
        }
        // A freeze-only job runs at its full declared allowance whenever heat is requested.
        if (!supportsThrottle) targetLoadPerServer = 1;
        // Set quota before unpausing so the job cannot briefly run at its former full quota.
        control.SetCpuLimit(containerId, Math.Max(MinimumDockerCpuCores, targetLoadPerServer * allowedCpuCores));
        if (paused == true) control.Unpause(containerId);
        paused = false;
        return Math.Clamp(control.ReadCpuPercent(containerId) / (100 * allowedCpuCores), 0, 1);
    }

    /// <summary>Restores the attached job to its declared CPU allowance after a replay.</summary>
    public void Release()
    {
        control.SetCpuLimit(containerId, allowedCpuCores);
        if (paused == true) control.Unpause(containerId);
        paused = false;
    }
}
