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
    // Windows reports a process start time exactly. Linux derives it from a boot time each .NET process computes for
    // itself, so another process (launcher, origin) sees the same start a few milliseconds apart; an exact comparison
    // there treats a live controller as gone and skips the kill. PID + executable + start within 1 s still identify it.
    internal static readonly TimeSpan StartTimeTolerance=OperatingSystem.IsWindows()?TimeSpan.Zero:TimeSpan.FromSeconds(1);
    private static Process? ExactProcess(int pid,DateTimeOffset started,string executable)
    {
        Process p;try{p=Process.GetProcessById(pid);}catch(ArgumentException){return null;}
        try{
            _=p.SafeHandle;
            if(p.HasExited||(p.StartTime.ToUniversalTime()-started.UtcDateTime).Duration()>StartTimeTolerance){p.Dispose();return null;}
            // A process that is already exiting (e.g. killed with its controller's tree, not yet reaped) has no
            // readable executable. Give it a moment to finish; fail closed if it neither exits nor shows its module.
            var module=p.MainModule;
            if(module is null){
                if(p.WaitForExit(2000)){p.Dispose();return null;}
                throw new IOException("Process identity unreadable; fencing not confirmed");
            }
            if(!SamePath(module.FileName,executable))throw new IOException("Process executable identity changed");
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
    /// <summary>
    /// Power a fenced worker back on (ADR 0073). First re-confirm with the verified stop above that nothing of the
    /// recorded controller and renderer identities can still run, then start the same controller with the node's
    /// unchanged config and state directory (journals, heat history, artifacts). <paramref name="launched"/> must
    /// persist the exact PID/start before anything else; the launcher manifest is then updated so a later
    /// power-off and Stop-RenderLab address the new process.
    /// </summary>
    internal LocalNodeIdentity Restart(LocalNodeIdentity fenced,Action<int,DateTimeOffset> launched)
    {
        StopAndConfirm(fenced);
        var start=new ProcessStartInfo(fenced.ControllerExecutable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=fenced.StateDirectory};
        foreach(string arg in new[]{Path.Combine(AppContext.BaseDirectory,"DigitalHeat.WorkloadRun.dll"),"render-node",Path.Combine(fenced.StateDirectory,"config.json")})start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_URLS"]=cfg.Peers[fenced.PeerId]; // MESH_TOKEN is inherited from this lab process
        start.Environment["RENDER_NODE_OWN_LOGS"]="1";
        using var process=Process.Start(start)??throw new IOException("Controller did not start");
        var started=new DateTimeOffset(process.StartTime.ToUniversalTime(),TimeSpan.Zero);
        launched(process.Id,started);
        RecordProcess(fenced.LabDirectory,fenced.PeerId,process.Id,started);
        return Prepare(fenced.PeerId);
    }
    /// <summary>Replay after an interrupted power-on: the saved new process must still be the one running.</summary>
    internal LocalNodeIdentity Adopt(string peerId,int pid,DateTimeOffset started)
    {
        string lab=Path.GetDirectoryName(Path.GetFullPath(cfg.Native!.AssetsDirectory))!;
        using var process=ExactProcess(pid,started,Environment.ProcessPath!)??throw new IOException("The powered-on controller is no longer running; power it on again after a lab restart");
        RecordProcess(lab,peerId,pid,started);
        return Prepare(peerId);
    }
    private static void RecordProcess(string lab,string peerId,int pid,DateTimeOffset started)
    {
        string file=Path.Combine(lab,"processes.json");
        var rows=JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(file)).EnumerateArray()
            .Select(r=>new{id=r.GetProperty("id").GetInt32(),started_utc=r.GetProperty("started_utc").GetString()!,node=r.GetProperty("node").GetString()!}).ToList();
        if(rows.Count(r=>r.node==peerId)!=1)throw new InvalidDataException("Ambiguous controller identity");
        rows=rows.Select(r=>r.node==peerId?new{id=pid,started_utc=started.UtcDateTime.ToString("o"),node=peerId}:r).ToList();
        NativeRenderRuntime.WriteJson(file,rows);
    }
}
