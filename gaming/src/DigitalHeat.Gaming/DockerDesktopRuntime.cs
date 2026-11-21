using System.Text.RegularExpressions;

namespace DigitalHeat.Gaming;

/// <summary>Application lifecycle boundary; separate from shared batch pause/throttle control.</summary>
public interface IDockerCommandRunner : DigitalHeat.Orchestration.IDockerCommands { }

public sealed class DockerCommandRunner : IDockerCommandRunner
{
    private readonly DigitalHeat.Orchestration.DockerCommands commands = new();
    public string Run(IReadOnlyList<string> arguments) => commands.Run(arguments);
}

public sealed record DesktopLaunchPlan(string ApplicationId, string NodeId, string ContainerId, string BrowserUrl, string[] DockerArguments);

/// <summary>Launches a trusted application in one already provisioned Linux streaming desktop.</summary>
public sealed class DockerDesktopRuntime(IDockerCommandRunner commands)
{
    public DesktopLaunchPlan Plan(RuntimeProfiles profiles, string applicationId, string nodeId, string? variant = null)
    {
        profiles.Validate();
        var app = profiles.Applications.SingleOrDefault(a => a.Id == applicationId) ?? throw new ArgumentException("Unknown application profile.");
        var node = profiles.Nodes.SingleOrDefault(n => n.Id == nodeId) ?? throw new ArgumentException("Unknown node profile.");
        var hardware = profiles.HardwareProfiles.Single(h => h.Id == node.HardwareProfileId);
        if (app.RequiredCapabilities.Except(hardware.Capabilities, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("Node lacks required declared application capabilities.");
        var arguments = app.Arguments.ToList();
        if (variant is not null)
        {
            if (!app.Variants.TryGetValue(variant, out var extra)) throw new ArgumentException("Unknown application variant.");
            arguments.AddRange(extra);
        }
        var listing = commands.Run(["--context", node.DockerContext, "ps", "-q", "--filter", $"label=com.docker.compose.project={node.ComposeProject}", "--filter", $"label=com.docker.compose.service={node.ComposeService}"]);
        var ids = listing.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length != 1 || !Regex.IsMatch(ids[0], "^[a-f0-9]{12,64}$")) throw new InvalidOperationException("Expected exactly one running container in the configured node scope.");
        // One application owns the desktop. Game-specific launchers and locks are retired.
        static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
        var command = "exec flock -n /tmp/digitalheat-desktop.lock sh -c " +
            Quote("log=$1; shift; exec \"$@\" >> \"$log\" 2>&1") + " sh " + string.Join(" ", new[] { app.LogPath, app.Executable }.Concat(arguments).Select(Quote));
        var docker = new List<string> { "--context", node.DockerContext, "exec", "-d", "--user", app.User, "--workdir", app.WorkingDirectory };
        foreach (var environment in app.Environment.OrderBy(e => e.Key, StringComparer.Ordinal)) docker.AddRange(["--env", $"{environment.Key}={environment.Value}"]);
        docker.AddRange([ids[0], "sh", "-c", command]);
        return new(app.Id, node.Id, ids[0], node.BrowserUrl, docker.ToArray());
    }
    // A detached exec acknowledgment is not readiness. Verify gameplay independently.
    public void Dispatch(DesktopLaunchPlan plan) => commands.Run(plan.DockerArguments);
}
