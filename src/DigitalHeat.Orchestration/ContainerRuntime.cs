using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DigitalHeat.JobRuntime;

namespace DigitalHeat.Orchestration;

public sealed record VolumeBinding(string StorageId, string VolumeName, string ContainerPath);
public sealed record GpuBinding(string DeviceId, string? NvidiaDeviceId, string[] LinuxDevices, string[] ReadOnlyBinds);
public sealed record RuntimeNode(string NodeId, string DockerContext, string Network, GpuBinding[] Gpus, VolumeBinding[] Volumes);
/// <summary>Trusted operator profile. EnvFile contains secrets locally; its contents are never journalled.</summary>
public sealed record ContainerProfile(string Id, string Image, string User, string[] Command, string? EnvFile,
    string[] LoopbackPorts, string[] StartupCommand, string[] ReadyCommand)
{
    public const long DefaultSharedMemoryMib = 64; // Docker's ordinary Linux /dev/shm default.
    public long ShmMib { get; init; } = DefaultSharedMemoryMib;
}
public sealed record RuntimeAttempt(string FleetId, string AttemptId, WorkloadRequest Request,
    WorkloadReservation Reservation, RuntimeNode Node, ContainerProfile Profile);
public sealed record ContainerObservation(string State, string? ContainerId, bool Ready)
{
    public bool Terminal => State is "exited" or "dead";
}

public interface IWorkloadRuntime
{
    void Preflight(RuntimeAttempt attempt) => DockerWorkloadRuntime.Validate(attempt);
    IReadOnlyList<string> Discover(string fleetId, IReadOnlyList<RuntimeNode> nodes);
    ContainerObservation Observe(RuntimeAttempt attempt);
    void Create(RuntimeAttempt attempt);
    void Start(RuntimeAttempt attempt);
    void Stop(RuntimeAttempt attempt);
    void Remove(RuntimeAttempt attempt);
    double ApplyBatchLoad(RuntimeAttempt attempt, double targetLoad);
}

/// <summary>Only exact fleet/attempt/spec-owned containers can be mutated. No stop/remove by caller-supplied ID.</summary>
public sealed class DockerWorkloadRuntime(IDockerCommands docker) : IWorkloadRuntime
{
    private const long BytesPerMib = 1024L * 1024L;
    private const long NanoCpuPerCore = 1_000_000_000;
    public static string Name(RuntimeAttempt a) => "dhw-" + Hash(a.FleetId)[..12] + "-" + a.AttemptId;
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string SpecHash(RuntimeAttempt a) => Hash(WorkloadJson.Identity(a));
    public static string VolumePrefix(string fleetId) => "dhw-" + Hash(fleetId)[..12] + "-restore-";
    private string Run(RuntimeNode node, params string[] arguments) => docker.Run(new[] { "--context", node.DockerContext }.Concat(arguments).ToArray());

    public void Preflight(RuntimeAttempt a)
    {
        Validate(a);
        if (a.Profile.EnvFile is {} file && !File.Exists(file)) throw new InvalidDataException("Required local env file is missing; no workloads changed.");
        Run(a.Node, "image", "inspect", a.Profile.Image); // Fail before pausing any existing batch.
    }

    public IReadOnlyList<string> Discover(string fleetId, IReadOnlyList<RuntimeNode> nodes)
    {
        var result = new List<string>();
        foreach (var context in nodes.Select(n => n.DockerContext).Distinct(StringComparer.Ordinal))
        {
            var node = nodes.First(n => n.DockerContext == context);
            var ids = Run(node, "ps", "-a", "-q", "--filter", "label=digitalheat.fleet=" + fleetId)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var id in ids)
            {
                if (!Regex.IsMatch(id, "^[a-f0-9]{12,64}$")) throw new InvalidDataException("Malformed Docker identity.");
                using var document = JsonDocument.Parse(Run(node, "inspect", id));
                var labels = document.RootElement[0].GetProperty("Config").GetProperty("Labels");
                result.Add(labels.GetProperty("digitalheat.attempt").GetString() ?? throw new InvalidDataException("Unlabelled owned attempt."));
            }
        }
        if (result.Distinct().Count() != result.Count) throw new InvalidDataException("Attempt identity appears on multiple engines.");
        return result;
    }

    public ContainerObservation Observe(RuntimeAttempt a)
    {
        var ids = Run(a.Node, "ps", "-a", "-q", "--filter", "name=^/" + Name(a) + "$")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0) return new("missing", null, false);
        if (ids.Length != 1 || !Regex.IsMatch(ids[0], "^[a-f0-9]{12,64}$")) throw new InvalidDataException("Ambiguous workload identity.");
        using var document = JsonDocument.Parse(Run(a.Node, "inspect", ids[0]));
        var c = document.RootElement[0];
        var labels = c.GetProperty("Config").GetProperty("Labels");
        foreach (var pair in new[] { ("digitalheat.fleet", a.FleetId), ("digitalheat.attempt", a.AttemptId),
            ("digitalheat.workload", a.Request.Id), ("digitalheat.spec", SpecHash(a)) })
            if (!labels.TryGetProperty(pair.Item1, out var value) || value.GetString() != pair.Item2)
                throw new InvalidOperationException("Container ownership/spec mismatch; no mutation authorized.");
        var host = c.GetProperty("HostConfig");
        if (host.GetProperty("NanoCpus").GetInt64() != checked((long)Math.Round(a.Request.Resources.CpuCores * NanoCpuPerCore))
            || host.GetProperty("Memory").GetInt64() != checked(a.Request.Resources.RamMib * BytesPerMib))
            throw new InvalidOperationException("Runtime resource limits drifted; reconciliation required.");
        var state = c.GetProperty("State");
        string status = state.GetProperty("Paused").GetBoolean() ? "paused" : state.GetProperty("Status").GetString() ?? "unknown";
        bool ready = status == "running";
        if (ready && a.Profile.ReadyCommand.Length != 0)
        {
            try { Run(a.Node, new[] { "exec", "--user", a.Profile.User, ids[0] }.Concat(a.Profile.ReadyCommand).ToArray()); }
            catch (InvalidOperationException) { ready = false; }
        }
        return new(status, ids[0], ready);
    }

    public void Create(RuntimeAttempt a)
    {
        Validate(a);
        var args = new List<string> { "create", "--pull", "never", "--name", Name(a), "--restart", "no", "--user", a.Profile.User,
            "--cpus", a.Request.Resources.CpuCores.ToString("R", CultureInfo.InvariantCulture), "--memory", a.Request.Resources.RamMib + "m",
            "--shm-size", a.Profile.ShmMib + "m",
            "--cap-drop", "ALL", "--security-opt", "no-new-privileges:true", "--network", a.Node.Network,
            "--label", "digitalheat.fleet=" + a.FleetId, "--label", "digitalheat.attempt=" + a.AttemptId,
            "--label", "digitalheat.workload=" + a.Request.Id, "--label", "digitalheat.spec=" + SpecHash(a) };
        if (a.Profile.EnvFile is {} env) args.AddRange(["--env-file", env]);
        foreach (var port in a.Profile.LoopbackPorts) args.AddRange(["--publish", port]);
        if (a.Reservation.GpuDeviceId is {} gpu)
        {
            var binding = a.Node.Gpus.Single(g => g.DeviceId == gpu);
            if (binding.NvidiaDeviceId is {} id) args.AddRange(["--gpus", "device=" + id]);
            foreach (var device in binding.LinuxDevices) args.AddRange(["--device", device]);
            foreach (var mount in binding.ReadOnlyBinds) args.AddRange(["--volume", mount + ":ro"]);
        }
        if (a.Request.StorageId is {} storage)
        {
            var volume = a.Node.Volumes.Single(v => v.StorageId == storage);
            args.AddRange(["--mount", $"type=volume,source={volume.VolumeName},target={volume.ContainerPath}"]);
        }
        args.Add(a.Profile.Image); args.AddRange(a.Profile.Command);
        Run(a.Node, args.ToArray());
    }

    public void Start(RuntimeAttempt a)
    {
        var observation = Observe(a);
        if (observation.State != "created") throw new InvalidOperationException("Only an observed, owned created container may start.");
        Run(a.Node, "start", observation.ContainerId!);
        if (a.Profile.StartupCommand.Length != 0)
            Run(a.Node, new[] { "exec", "-d", "--user", a.Profile.User, observation.ContainerId! }.Concat(a.Profile.StartupCommand).ToArray());
    }

    public void Stop(RuntimeAttempt a)
    {
        if (!a.Request.Lifecycle.AllowStop) throw new InvalidOperationException("Owner did not authorize stop.");
        var observation = Observe(a);
        if (observation.State is not ("running" or "paused" or "created")) throw new InvalidOperationException("Container is not in a stoppable observed state.");
        // Docker stop handles paused containers; never mutate unowned containers or use a global prune.
        Run(a.Node, "stop", "--time", "10", observation.ContainerId!);
    }

    public void Remove(RuntimeAttempt a)
    {
        if (!a.Request.Lifecycle.AllowStop) throw new InvalidOperationException("Owner did not authorize removal.");
        var observation = Observe(a);
        if (!observation.Terminal && observation.State != "created") throw new InvalidOperationException("Only terminal/unstarted containers may be removed.");
        Run(a.Node, "rm", observation.ContainerId!); // No -v: persistent local data survives.
    }

    public double ApplyBatchLoad(RuntimeAttempt a, double targetLoad)
    {
        if (!double.IsFinite(targetLoad) || targetLoad is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(targetLoad));
        if (a.Request.Kind != "batch" || !a.Request.Lifecycle.AllowPause) throw new InvalidOperationException("Protected/non-pausable workload.");
        var observed = Observe(a);
        if (observed.State is not ("running" or "paused")) throw new InvalidOperationException("Batch container not available.");
        var executor = new DockerJobExecutor(new ScopedControl(this, a), observed.ContainerId!, a.Request.Resources.CpuCores, supportsThrottle: false);
        // Fresh executor state is unknown. For positive load explicitly unpause the owned container first;
        // its declared quota is unchanged. The shared executor remains the heat-load control implementation.
        if (targetLoad > 0 && observed.State == "paused") Run(a.Node, "unpause", observed.ContainerId!);
        return executor.ApplyTargetLoad(targetLoad);
    }

    private sealed class ScopedControl(DockerWorkloadRuntime runtime, RuntimeAttempt attempt) : IContainerControl
    {
        private string Id(string id)
        {
            var observed = runtime.Observe(attempt);
            if (observed.ContainerId != id) throw new InvalidOperationException("Container identity changed.");
            return id;
        }
        public void Pause(string id) => runtime.Run(attempt.Node, "pause", Id(id));
        public void Unpause(string id) => runtime.Run(attempt.Node, "unpause", Id(id));
        public void SetCpuLimit(string id, double cores) => runtime.Run(attempt.Node, "update", "--cpus", cores.ToString("R", CultureInfo.InvariantCulture), Id(id));
        public double ReadCpuPercent(string id)
        {
            var raw = runtime.Run(attempt.Node, "stats", "--no-stream", "--format", "{{.CPUPerc}}", Id(id)).Trim().TrimEnd('%');
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || !double.IsFinite(percent) || percent < 0)
                throw new InvalidDataException("Invalid Docker CPU sample.");
            return percent;
        }
    }

    public static void Validate(RuntimeAttempt a)
    {
        static bool Token(string? value) => value is not null && Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
        static bool Text(string? value) => value is not null && !value.Contains('\0');
        WorkloadPlacement.ValidateRequest(a.Request);
        if (!Token(a.FleetId) || !Regex.IsMatch(a.AttemptId, "^[a-f0-9]{32}$") || a.Request.Kind == "legacy"
            || a.Node.NodeId != a.Reservation.NodeId || !Token(a.Node.DockerContext) || !Token(a.Node.Network)
            || a.Node.Gpus is null || a.Node.Volumes is null || a.Profile is null || !Token(a.Profile.Id)
            || string.IsNullOrWhiteSpace(a.Profile.Image) || a.Profile.Image.StartsWith('-') || !Text(a.Profile.Image)
            || !Token(a.Profile.User) || a.Profile.User is "0" or "root" || a.Request.Resources.CpuCores < 0.01
            || a.Request.Resources.RamMib > long.MaxValue / BytesPerMib || a.Request.Resources.CpuCores > long.MaxValue / (double)NanoCpuPerCore
            || a.Profile.ShmMib <= 0 || a.Profile.ShmMib > a.Request.Resources.RamMib
            || a.Profile.Command is null || a.Profile.Command.Any(x => !Text(x)) || a.Profile.StartupCommand is null || a.Profile.StartupCommand.Any(x => !Text(x))
            || a.Profile.ReadyCommand is null || a.Profile.ReadyCommand.Any(x => !Text(x)) || a.Profile.LoopbackPorts is null
            || a.Request.Kind == "interactive" && a.Profile.ReadyCommand.Length == 0
            || a.Profile.LoopbackPorts.Any(p => !Regex.IsMatch(p, "^127\\.0\\.0\\.1:[0-9]{1,5}:[0-9]{1,5}(/tcp|/udp)?$"))
            || a.Profile.EnvFile is not null && !Text(a.Profile.EnvFile)) throw new InvalidDataException("Invalid trusted runtime profile.");
        foreach (var gpu in a.Node.Gpus)
            if (gpu.LinuxDevices is null || gpu.ReadOnlyBinds is null
                || gpu.NvidiaDeviceId is null && gpu.LinuxDevices.Length == 0
                || gpu.NvidiaDeviceId is not null && !Regex.IsMatch(gpu.NvidiaDeviceId, "^[a-zA-Z0-9_.-]+$")
                || gpu.LinuxDevices.Any(d => !Regex.IsMatch(d, "^/dev/(dxg|dri/[a-zA-Z0-9_-]+)$"))
                || gpu.ReadOnlyBinds.Any(b => b is not ("/usr/lib/wsl/lib:/usr/lib/wsl/lib" or "/usr/lib/wsl/drivers:/usr/lib/wsl/drivers")))
                throw new InvalidDataException("Unsupported GPU device/bind mapping.");
        if (a.Reservation.GpuDeviceId is {} device && a.Node.Gpus.Count(g => g.DeviceId == device) != 1)
            throw new InvalidDataException("Selected GPU has no unique runtime binding.");
        foreach (var volume in a.Node.Volumes)
            if (!Token(volume.VolumeName) || string.IsNullOrWhiteSpace(volume.StorageId) || !Regex.IsMatch(volume.ContainerPath, "^/[a-zA-Z0-9_/-]+$")
                || volume.ContainerPath == "/" || volume.ContainerPath.Contains("..", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported local volume binding.");
        if (a.Request.StorageId is {} storage && a.Node.Volumes.Count(v => v.StorageId == storage) != 1)
            throw new InvalidDataException("Missing node-local volume binding.");
    }
}
