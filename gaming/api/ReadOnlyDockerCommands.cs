using System.Diagnostics;
using System.Text;
using DigitalHeat.Gaming;
using System.Text.RegularExpressions;

/// <summary>Only the monitor's fixed read-only commands; no shell and no lifecycle commands.</summary>
public sealed class ReadOnlyDockerCommands(ServicePolicy policy, CancellationToken deadline) : IDockerCommandRunner
{
    public string Run(IReadOnlyList<string> arguments)
    {
        deadline.ThrowIfCancellationRequested();
        if (arguments.Count < 3 || arguments[0] != "--context") throw new InvalidOperationException("Missing explicit Docker context.");
        string command = arguments[2];
        static bool Id(string value) => Regex.IsMatch(value, "^[a-f0-9]{12,64}$");
        bool allowed = command switch
        {
            "context" => arguments.Count == 7 && arguments[3] == "inspect" && arguments[4] == arguments[1]
                && arguments[5] == "--format" && arguments[6] == "{{json .Endpoints.docker}}",
            "top" => arguments.Count == 6 && Id(arguments[3]) && arguments[4] == "-eo" && arguments[5] == "pid,args",
            "ps" => arguments.Count == 8 && arguments[3] == "-q" && arguments[4] == "--filter" && arguments[6] == "--filter"
                && new[] { arguments[5], arguments[7] }.All(f => Regex.IsMatch(f, "^label=com\\.docker\\.compose\\.(project|service)=[A-Za-z0-9_.-]+$")),
            "inspect" => arguments.Count == 6 && arguments[3] == "--format" && arguments[4] == "{{.State.Running}} {{.State.Paused}}" && Id(arguments[5]),
            "stats" => arguments.Count >= 7 && arguments[3] == "--no-stream" && arguments[4] == "--format" && arguments[5] == "{{json .}}" && arguments.Skip(6).All(Id),
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException("Only fixed monitor observation commands are allowed.");
        using var process = new Process { StartInfo = new("docker")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline);
        timeout.CancelAfter(TimeSpan.FromSeconds(policy.DockerCommandTimeoutSeconds));
        var output = ReadBounded(process.StandardOutput.BaseStream, retain: true, timeout.Token);
        var error = ReadBounded(process.StandardError.BaseStream, retain: false, timeout.Token);
        try { process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (deadline.IsCancellationRequested) throw;
            throw new TimeoutException("docker_observation_timeout");
        }
        string text = output.GetAwaiter().GetResult();
        _ = error.GetAwaiter().GetResult(); // Never return stderr, host details or process arguments to snapshots/logs.
        if (process.ExitCode != 0) throw new InvalidOperationException("docker_observation_unavailable");
        return text;
    }
    private async Task<string> ReadBounded(Stream stream, bool retain, CancellationToken cancel)
    {
        const int BufferBytes = 8192;
        using var result = new MemoryStream(); var buffer = new byte[BufferBytes]; long total = 0; int count;
        while ((count = await stream.ReadAsync(buffer, cancel)) > 0)
        {
            total += count;
            // Continue draining to avoid blocking the child, but never retain beyond the policy cap.
            if (retain && total <= policy.DockerObservation.MaxResponseBytes) result.Write(buffer, 0, count);
        }
        if (total > policy.DockerObservation.MaxResponseBytes) throw new InvalidOperationException("docker_observation_response_too_large");
        return retain ? Encoding.UTF8.GetString(result.ToArray()) : "";
    }
}
