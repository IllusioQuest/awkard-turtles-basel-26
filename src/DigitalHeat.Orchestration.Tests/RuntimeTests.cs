using System.Text.Json;
using DigitalHeat.Orchestration;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class RuntimeTests
{
    private static RuntimeAttempt Attempt() => new("fixture", new string('a', 32), PlacementTests.Request("batch", "batch", 0.5),
        new("batch", "spa", "b", "host", null, 0.8, 14), CoordinatorTests.Node("b"), CoordinatorTests.Profile);

    private sealed class Commands : IDockerCommands
    {
        public List<string[]> Calls = [];
        public Dictionary<string, string> Labels = [];
        public string State = "created";
        public bool Exists, BadLimits, ExistingVolume;
        public string Run(IReadOnlyList<string> args)
        {
            Calls.Add(args.ToArray()); var a = args.Skip(2).ToArray();
            if (a[0] == "create")
            {
                for (int i = 0; i < a.Length - 1; i++) if (a[i] == "--label") { var parts = a[i + 1].Split('=', 2); Labels[parts[0]] = parts[1]; }
                Exists = true; return "abcdef123456";
            }
            if (a[0] == "ps") return Exists ? "abcdef123456" : "";
            if (a[0] == "inspect") return JsonSerializer.Serialize(new[] { new { Config = new { Labels },
                HostConfig = new { NanoCpus = BadLimits ? 100 : 500_000_000L, Memory = 64L * 1024 * 1024 },
                State = new { Status = State, Paused = State == "paused" } } });
            if (a[0] == "start") State = "running";
            if (a[0] == "pause") State = "paused";
            if (a[0] == "unpause") State = "running";
            if (a[0] == "stop") State = "exited";
            if (a[0] == "rm") Exists = false;
            if (a[0] == "stats") return "25.0%";
            if (a[0] == "volume" && a[1] == "ls" && ExistingVolume) return "existing";
            return "";
        }
    }

    [Fact] public void ContainerLimitsAndOwnershipAreEnforcedBeforeMutation()
    {
        var commands = new Commands(); var runtime = new DockerWorkloadRuntime(commands); var a = Attempt();
        runtime.Create(a);
        var create = commands.Calls.Single();
        Assert.Contains("--context", create); Assert.Contains("fixture-context", create);
        Assert.Contains("--cpus", create); Assert.Contains("0.5", create); Assert.Contains("64m", create);
        Assert.Contains("--pull", create); Assert.DoesNotContain("--privileged", create);
        runtime.Start(a); Assert.True(runtime.Observe(a).Ready);
        commands.Labels["digitalheat.fleet"] = "someone-else";
        int prior = commands.Calls.Count;
        Assert.Throws<InvalidOperationException>(() => runtime.Stop(a));
        Assert.DoesNotContain(commands.Calls.Skip(prior), c => c.Contains("stop"));
    }

    [Fact] public void DriftedLimitsBlockControlAndInteractiveWorkCannotFreeze()
    {
        var commands = new Commands(); var runtime = new DockerWorkloadRuntime(commands); var a = Attempt(); runtime.Create(a);
        commands.BadLimits = true;
        Assert.Throws<InvalidOperationException>(() => runtime.Start(a));
        Assert.Throws<InvalidOperationException>(() => runtime.ApplyBatchLoad(a with { Request = PlacementTests.Request() }, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.ApplyBatchLoad(a, double.NaN));
    }

    [Fact] public void SharedBatchExecutorUsesOwnedPauseAndDeclaredQuota()
    {
        var commands = new Commands(); var runtime = new DockerWorkloadRuntime(commands); var a = Attempt(); runtime.Create(a); runtime.Start(a);
        Assert.Equal(0, runtime.ApplyBatchLoad(a, 0)); Assert.Equal("paused", runtime.Observe(a).State);
        runtime.ApplyBatchLoad(a, 1); Assert.Equal("running", runtime.Observe(a).State);
        Assert.Contains(commands.Calls, c => c.Contains("pause")); Assert.Contains(commands.Calls, c => c.Contains("update"));
    }

    [Fact] public void BackupsCannotBorrowAnotherWorkloadsStorageOrOverwriteExistingVolumes()
    {
        var commands = new Commands(); var service = new LocalVolumeBackup(commands); var a = Attempt();
        a = a with { Request = a.Request with { StorageId = "mine", PinnedNodeId = "b" },
            Node = a.Node with { Volumes = [new("mine", "mine-volume", "/data"), new("other", "other-volume", "/other")] } };
        var record = new AttemptRecord(a, "released");
        Assert.Throws<InvalidOperationException>(() => service.Backup(record, "other", "backups", "save.tar.gz", "local-image"));
        Assert.Empty(commands.Calls);
        Assert.Throws<InvalidOperationException>(() => service.RestoreEmpty(record, "mine", "backups", "save.tar.gz", "local-image", "someone-elses-volume"));
        commands.ExistingVolume = true;
        Assert.Throws<InvalidOperationException>(() => service.RestoreEmpty(record, "mine", "backups", "save.tar.gz", "local-image", DockerWorkloadRuntime.VolumePrefix("fixture") + "new"));
        Assert.DoesNotContain(commands.Calls, c => c.Contains("run"));
    }
}
