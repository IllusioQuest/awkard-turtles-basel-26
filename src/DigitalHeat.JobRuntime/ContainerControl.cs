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

/// <summary>Optional batched observation so dashboards do not start one Docker CLI process per node.</summary>
public interface IBatchedContainerControl
{
    IReadOnlyDictionary<string, double> ReadCpuPercents(IReadOnlyList<string> containerIds);
}

/// <summary>All Docker CLI calls are confined to this class. Arguments never pass through a shell.</summary>
public sealed class DockerCliContainerControl : IContainerControl, IBatchedContainerControl
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
        return ParseCpuPercent(output);
    }

    public IReadOnlyDictionary<string, double> ReadCpuPercents(IReadOnlyList<string> containerIds)
    {
        if (containerIds.Count == 0) return new Dictionary<string, double>();
        string[] arguments = ["stats", "--no-stream", "--format", "{{.Name}}={{.CPUPerc}}", .. containerIds];
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string line in Run(arguments).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0 || !result.TryAdd(line[..separator], ParseCpuPercent(line[(separator + 1)..])))
                throw new InvalidDataException($"Invalid Docker stats row: {line}");
        }
        foreach (string id in containerIds)
            if (!result.ContainsKey(id)) throw new InvalidDataException($"Docker stats omitted container {id}.");
        return result;
    }

    private static double ParseCpuPercent(string output)
    {
        if (!double.TryParse(output.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture,
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
