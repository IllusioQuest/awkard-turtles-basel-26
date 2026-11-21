using System.Text.RegularExpressions;

namespace DigitalHeat.Orchestration;

/// <summary>Cold local-volume backup only. No host directory mount, running-world snapshot or replicated storage.</summary>
public sealed class LocalVolumeBackup(IDockerCommands docker)
{
    public void Backup(AttemptRecord record, string storageId, string backupVolume, string archiveName, string toolImage)
        => Run(record, storageId, backupVolume, archiveName, toolImage, restore: false, targetVolume: null);

    public void RestoreEmpty(AttemptRecord record, string storageId, string backupVolume, string archiveName, string toolImage, string newVolume)
        => Run(record, storageId, backupVolume, archiveName, toolImage, restore: true, targetVolume: newVolume);

    private void Run(AttemptRecord record, string storageId, string backupVolume, string archiveName, string image, bool restore, string? targetVolume)
    {
        static bool Token(string s) => Regex.IsMatch(s, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
        if (record.Phase != "released" || storageId != record.Attempt.Request.StorageId || !Token(backupVolume) || !Token(archiveName) || !archiveName.EndsWith(".tar.gz", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(image) || image.StartsWith('-') || image.Contains('\0') || targetVolume is not null && !Token(targetVolume))
            throw new InvalidOperationException("Cold backup/restore requires a released owned attempt and safe local names.");
        DockerWorkloadRuntime.Validate(record.Attempt);
        var node = record.Attempt.Node;
        var source = node.Volumes.Single(v => v.StorageId == storageId).VolumeName;
        var volume = restore ? targetVolume! : source;
        if (volume == backupVolume || restore && volume == source) throw new InvalidOperationException("Backup, original and restore destination must be distinct.");
        string RunDocker(params string[] args) => docker.Run(new[] { "--context", node.DockerContext }.Concat(args).ToArray());
        if (!restore) RunDocker("volume", "inspect", source); // Never silently back up a typo-created empty source.
        else
        {
            if (!volume.StartsWith(DockerWorkloadRuntime.VolumePrefix(record.Attempt.FleetId), StringComparison.Ordinal))
                throw new InvalidOperationException("Restore destination must use this fleet's restore namespace.");
            if (RunDocker("volume", "ls", "-q", "--filter", "name=^" + Regex.Escape(volume) + "$").Trim().Length != 0)
                throw new InvalidOperationException("Restore destination already exists; no overwrite permitted.");
            RunDocker("volume", "inspect", backupVolume); // Never silently create a missing archive volume.
        }
        if (RunDocker("ps", "-q", "--filter", "volume=" + volume).Trim().Length != 0)
            throw new InvalidOperationException("A running/paused container uses the data volume; cold operation refused.");
        const string BackupScript = "test ! -e /backup/\"$1\" && exec tar -czf /backup/\"$1\" -C /data .";
        const string RestoreScript = "test -z \"$(ls -A /data)\" && exec tar -xzf /backup/\"$1\" -C /data";
        var arguments = new List<string> { "run", "--rm", "--pull", "never", "--network", "none", "--read-only", "--cap-drop", "ALL",
            "--cap-add", "DAC_OVERRIDE",
            "--security-opt", "no-new-privileges:true", "--cpus", "0.25", "--memory", "64m", "--user", "0",
            "--mount", $"type=volume,source={volume},target=/data" + (restore ? "" : ",readonly"),
            "--mount", $"type=volume,source={backupVolume},target=/backup" + (restore ? ",readonly" : "") };
        // DAC_OVERRIDE permits private owned files; the source is mounted read-only. Restore also
        // preserves the application's uid/gid in its new empty volume. No host filesystem is mounted.
        if (restore) arguments.AddRange(["--cap-add", "CHOWN", "--cap-add", "FOWNER"]);
        arguments.AddRange([image, "sh", "-c", restore ? RestoreScript : BackupScript, "sh", archiveName]);
        RunDocker(arguments.ToArray());
    }
}
