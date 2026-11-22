using System.Diagnostics;
using System.Text.Json;

internal sealed record LocalNodeIdentity(string PeerId,string LabDirectory,string ManifestSha256,
    int Pid,DateTimeOffset StartedUtc,string ControllerExecutable,string RendererExecutable,string StateDirectory);

/// <summary>Trusted local lab only. A timeout is never proof of fencing.</summary>
internal sealed class LocalNodePowerControl(RenderConfig cfg)
{
    private static bool SamePath(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),
        OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    internal LocalNodeIdentity Prepare(string peerId)
    {
        if(!cfg.AllowLocalNodeShutdown||cfg.Native is null||peerId==cfg.Id||!cfg.Peers.ContainsKey(peerId))
            throw new InvalidOperationException("Local node shutdown is not enabled for this target");
        string lab=Path.GetDirectoryName(Path.GetFullPath(cfg.Native.AssetsDirectory))!;
        if(!SamePath(cfg.StateDirectory,Path.Combine(lab,cfg.Id)))throw new InvalidDataException("Origin is not a presentation lab node");
        string target=Path.Combine(lab,peerId),relative=Path.GetRelativePath(lab,target);
        if(relative.StartsWith("..")||Path.IsPathRooted(relative))throw new InvalidDataException("Target outside lab");
        var node=JsonSerializer.Deserialize<RenderConfig>(File.ReadAllText(Path.Combine(target,"config.json")),RenderNodeHost.Json)!;
        if(node.Id!=peerId||node.WorkerRole!="render"||node.Native is null||!SamePath(node.StateDirectory,target)||
           !SamePath(node.Native.Executable,cfg.Native.Executable)||!SamePath(node.Native.AssetsDirectory,cfg.Native.AssetsDirectory))
            throw new InvalidDataException("Target is not this lab's GPU worker");
        byte[] bytes=File.ReadAllBytes(Path.Combine(lab,"processes.json"));
        var records=JsonSerializer.Deserialize<JsonElement>(bytes).EnumerateArray().Where(r=>r.GetProperty("node").GetString()==peerId).ToArray();
        if(records.Length!=1)throw new InvalidDataException("Ambiguous controller identity");
        var record=records[0];int pid=record.GetProperty("id").GetInt32();
        if(pid==Environment.ProcessId)throw new InvalidDataException("Cannot stop origin");
        var started=record.GetProperty("started_utc").GetDateTimeOffset();
        string descriptor=NativeRenderRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(new{peerId,pid,started,config=NativeRenderRuntime.Hash(File.ReadAllBytes(Path.Combine(target,"config.json")))}));
        return new(peerId,lab,descriptor,pid,started,
            Environment.ProcessPath!,node.Native.Executable,target);
    }
    private static Process? ExactProcess(int pid,DateTimeOffset started,string executable)
    {
        Process p;try{p=Process.GetProcessById(pid);}catch(ArgumentException){return null;}
        try{
            _=p.SafeHandle;
            if(p.HasExited||p.StartTime.ToUniversalTime()!=started.UtcDateTime){p.Dispose();return null;}
            if(!SamePath(p.MainModule!.FileName,executable))throw new IOException("Process executable identity changed");
            return p;
        }catch{p.Dispose();throw;}
    }
    private static void Terminate(int pid,DateTimeOffset started,string executable)
    {
        using var p=ExactProcess(pid,started,executable);if(p is null)return;
        p.Kill(entireProcessTree:true);
        if(!p.WaitForExit(10000))throw new IOException("Process termination is not confirmed");
    }
    internal void StopAndConfirm(LocalNodeIdentity identity)
    {
        var expected=Prepare(identity.PeerId);
        if(expected!=identity)throw new IOException("Lab manifest changed; fencing not confirmed");
        // Stop the launcher first. Then no further renderer ownership intents can be created.
        Terminate(identity.Pid,identity.StartedUtc,identity.ControllerExecutable);
        string warm=Path.Combine(identity.StateDirectory,"native-warm-worker","owned.json");
        NativeWarmOwnership? pool=File.Exists(warm)?JsonSerializer.Deserialize<NativeWarmOwnership>(File.ReadAllText(warm),RenderNodeHost.Json):null;
        var owned=Directory.GetFiles(Path.Combine(identity.StateDirectory,"native-attempts"),"owned.json",SearchOption.AllDirectories)
            .Select(p=>JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(p),RenderNodeHost.Json)!).Where(r=>!r.Released&&r.LaunchRequested).ToArray();
        var identities=new List<NativeOwnership>();if(pool is not null)identities.Add(pool.Process);
        foreach(var record in owned){
            if(record.Pid is null){
                if(pool?.ActiveActivation==record.Activation&&pool.Process.Pid is not null)continue;
                throw new IOException("Launch intent without durable PID; retries remain held");
            }
            identities.Add(record);
        }
        foreach(var record in identities.DistinctBy(r=>(r.Pid,r.StartedUtc))){
            if(record.Pid is null||record.StartedUtc is null)throw new IOException("Uncertain renderer identity; retries remain held");
            Terminate(record.Pid.Value,record.StartedUtc.Value,identity.RendererExecutable);
        }
        // Retained handles verified exit. Do not edit the stopped node's uncertain ownership journal.
    }
}
