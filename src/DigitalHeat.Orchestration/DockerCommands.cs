using System.Diagnostics;

namespace DigitalHeat.Orchestration;

public interface IDockerCommands { string Run(IReadOnlyList<string> arguments); }

/// <summary>Explicit argv; no host shell. Exceptions leave controller ownership uncertain, not released.</summary>
public sealed class DockerCommands : IDockerCommands
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30); // Lab control policy, not a performance figure.
    public string Run(IReadOnlyList<string> arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var a in arguments) process.StartInfo.ArgumentList.Add(a);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true); process.WaitForExit();
            throw new TimeoutException("Docker control exceeded the 30-second lab timeout; reconcile before retrying.");
        }
        var result = output.GetAwaiter().GetResult(); var detail = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException($"Docker {arguments.FirstOrDefault()} failed: {detail.Trim()}");
        return result;
    }
}
