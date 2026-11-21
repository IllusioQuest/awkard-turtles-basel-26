using System.Diagnostics;
using System.Globalization;

namespace DigitalHeat.JobRuntime;

/// <summary>Small boundary that Nikita's node runtime may implement with its own Docker client.</summary>
public interface IContainerControl
{
    void Pause(string containerId);
    void Unpause(string containerId);
    void SetCpuLimit(string containerId, double cpuCores);
    double ReadCpuPercent(string containerId);
}

/// <summary>All Docker CLI calls are confined to this class. Arguments never pass through a shell.</summary>
public sealed class DockerCliContainerControl : IContainerControl
{
    private const int CommandTimeoutMilliseconds = 10_000; // Demo policy: ADR 0009.
    private readonly string dockerPath;

    public DockerCliContainerControl(string dockerPath = "docker") => this.dockerPath = dockerPath;

    public void Pause(string containerId) => Run("pause", containerId);
    public void Unpause(string containerId) => Run("unpause", containerId);
    public void SetCpuLimit(string containerId, double cpuCores) =>
        Run("update", "--cpus", cpuCores.ToString("R", CultureInfo.InvariantCulture), containerId);

    public double ReadCpuPercent(string containerId)
    {
        var output = Run("stats", "--no-stream", "--format", "{{.CPUPerc}}", containerId).Trim();
        if (!double.TryParse(output.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture,
                out double percent) || !double.IsFinite(percent) || percent < 0)
            throw new InvalidDataException($"Invalid Docker CPU percentage: {output}");
        return percent;
    }

    private string Run(params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(dockerPath)
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(CommandTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"Docker {arguments[0]} exceeded the 10-second demo timeout.");
        }
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Docker {arguments[0]} failed: {error.Trim()}");
        return output;
    }
}
