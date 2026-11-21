using System.Text.RegularExpressions;
namespace DigitalHeat.Orchestration;

/// <summary>Adapter-independent request/profile identity. Device passthrough syntax belongs to the executor.</summary>
public static class RuntimeAttemptValidation
{
    private const long BytesPerMib=1024L*1024;
    private const long NanoCpuPerCore=1_000_000_000;
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
            || a.Profile.EnvFile is not null && !Text(a.Profile.EnvFile)) throw new InvalidDataException("Invalid trusted runtime profile.");
        if(a.Node.Gpus.Any(g=>g is null||string.IsNullOrWhiteSpace(g.DeviceId)))throw new InvalidDataException("Invalid logical GPU binding");
        if (a.Reservation.GpuDeviceId is {} device && a.Node.Gpus.Count(g => g.DeviceId == device) != 1)
            throw new InvalidDataException("Selected GPU has no unique runtime binding.");
        if (a.Request.StorageId is {} storage && a.Node.Volumes.Count(v => v.StorageId == storage) != 1)
            throw new InvalidDataException("Missing node-local volume binding.");
    }
}
