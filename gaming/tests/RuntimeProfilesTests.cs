using System.Text.Json;
using DigitalHeat.Gaming;
using Xunit;

public class RuntimeProfilesTests
{
    private const string Container = "123456789abc";
    private static RuntimeProfiles Profiles() => new(
        [new("game", "/opt/game", "/opt", "ubuntu", "/tmp/game.log", ["literal' ; touch /tmp/injected; #"],
            new() { ["match"] = ["+map", "demo"] }, new() { ["DISPLAY"] = ":20" }, ["opengl"])],
        [new("node", "remote-lab", "trusted-project", "session", "http://localhost:18883", "gpu")],
        [new("gpu", ["opengl"], "Synthetic fixture, not measured hardware")]);

    private sealed class Commands(string listing = Container) : IDockerCommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public string Run(IReadOnlyList<string> arguments)
        { Calls.Add(arguments.ToArray()); return listing; }
    }

    [Fact]
    public void TargetsExplicitEngineAndComposeScopeWithoutDispatching()
    {
        var commands = new Commands();
        var plan = new DockerDesktopRuntime(commands).Plan(Profiles(), "game", "node", "match");
        Assert.Single(commands.Calls);
        Assert.Equal(["--context", "remote-lab", "ps", "-q", "--filter", "label=com.docker.compose.project=trusted-project",
            "--filter", "label=com.docker.compose.service=session"], commands.Calls[0]);
        Assert.Equal(Container, plan.ContainerId);
        Assert.Contains("'literal'\"'\"' ; touch /tmp/injected; #'", plan.DockerArguments[^1]);
        Assert.Contains("digitalheat-desktop.lock flock -n /tmp/digitalheat-xonotic.lock", plan.DockerArguments[^1]);
        Assert.Contains("'+map' 'demo'", plan.DockerArguments[^1]);
        new DockerDesktopRuntime(commands).Dispatch(plan);
        Assert.Equal(2, commands.Calls.Count);
        Assert.Equal(plan.DockerArguments, commands.Calls[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789abc\nabcdef123456")]
    [InlineData("--host=untrusted")]
    public void RefusesMissingAmbiguousOrMalformedContainer(string listing) =>
        Assert.Throws<InvalidOperationException>(() => new DockerDesktopRuntime(new Commands(listing)).Plan(Profiles(), "game", "node"));

    [Fact]
    public void RejectsCapabilityMismatchBeforeDocker()
    {
        var profiles = Profiles() with { HardwareProfiles = [new("gpu", [], "fixture")] };
        var commands = new Commands();
        Assert.Throws<InvalidOperationException>(() => new DockerDesktopRuntime(commands).Plan(profiles, "game", "node"));
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public void RejectsMissingFieldsBadEnvironmentRootAndOptionPaths()
    {
        var profiles = Profiles(); var app = profiles.Applications[0];
        foreach (var invalid in new[] { app with { Executable = "--help" }, app with { User = "root" },
            app with { Environment = new() { ["BAD=NAME"] = "x" } }, app with { Arguments = null! } })
            Assert.Throws<InvalidDataException>(() => (profiles with { Applications = [invalid] }).Validate());
        Assert.Throws<InvalidDataException>(() => (profiles with { Nodes = [profiles.Nodes[0], profiles.Nodes[0]] }).Validate());
        Assert.Throws<InvalidDataException>(() => (profiles with { HardwareProfiles = [] }).Validate());
    }

    [Fact]
    public void UsesStrictSnakeCaseJson()
    {
        var json = JsonSerializer.Serialize(Profiles(), RuntimeProfiles.JsonOptions);
        Assert.Contains("hardware_profiles", json);
        Assert.Contains("working_directory", json);
        JsonSerializer.Deserialize<RuntimeProfiles>(json, RuntimeProfiles.JsonOptions)!.Validate();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RuntimeProfiles>(json.Replace("working_directory", "workingDirectory"), RuntimeProfiles.JsonOptions));
    }
}
