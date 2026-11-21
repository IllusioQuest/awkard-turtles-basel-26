using System.Diagnostics;
using DigitalHeat.Gaming;

/// <summary>Only the monitor's fixed read-only commands; no shell and no lifecycle commands.</summary>
public sealed class ReadOnlyDockerCommands(ServicePolicy policy, CancellationToken deadline) : IDockerCommandRunner
{
    public string Run(IReadOnlyList<string> arguments)
    {
        deadline.ThrowIfCancellationRequested();
        if (arguments.Count < 3 || arguments[0] != "--context") throw new InvalidOperationException("Missing explicit Docker context.");
        string command = arguments[2];
        if (command is not ("ps" or "inspect" or "stats" or "exec") ||
            (command == "exec" && (arguments.Count != 7 || arguments[4] != "pgrep" || arguments[5] != "-f")))
            throw new InvalidOperationException("Only fixed monitor observation commands are allowed.");
        using var process = new Process { StartInfo = new("docker")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline);
        timeout.CancelAfter(TimeSpan.FromSeconds(policy.DockerCommandTimeoutSeconds));
        try { process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        string text = output.GetAwaiter().GetResult();
        string detail = error.GetAwaiter().GetResult();
        if (process.ExitCode == 1 && command == "exec" && string.IsNullOrWhiteSpace(detail)) return ""; // pgrep: no matching process; Docker errors have stderr.
        if (process.ExitCode != 0) throw new InvalidOperationException($"Docker observation failed ({command}): {detail.Trim()}");
        return text;
    }
}
