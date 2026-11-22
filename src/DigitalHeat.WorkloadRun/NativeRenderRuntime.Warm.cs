using System.Diagnostics;
using System.Text.Json;
using DigitalHeat.Orchestration;

internal sealed record NativeWarmOwnership(NativeOwnership Process,string? ActiveActivation=null,
    string? ActiveDirectory=null,bool Retiring=false);

internal sealed partial class NativeRenderRuntime
{
    private readonly Dictionary<string,NativeOwnership> warmJobs=[];
    private string WarmDirectory=>Path.Combine(cfg.StateDirectory,"native-warm-worker");
    private string WarmRecord=>Path.Combine(WarmDirectory,"owned.json");
    private bool IsWarm(RuntimeAttempt a)=>options.KeepGpuWarm&&options.EnableGpu&&a.Profile.Command[0]!="prepare";

    // Same byte-range lock as warm_worker.py: serialize dispatch, idle retirement and stop.
    // This is local executor ownership, never distributed placement/heat accounting.
    private FileStream WarmGate()
    {
        if(OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Warm native mode requires Windows/Linux byte-range locks; keep warm mode disabled on macOS");
        Directory.CreateDirectory(WarmDirectory);
        var file=new FileStream(Path.Combine(WarmDirectory,"gate.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
        try{
            if(file.Length==0)file.SetLength(1);
            var until=DateTimeOffset.UtcNow.AddSeconds(5);
            while(true)try{file.Lock(0,1);return file;}catch(IOException)when(DateTimeOffset.UtcNow<until){Thread.Sleep(10);}
        }catch{file.Dispose();throw;}
    }
    private NativeWarmOwnership ReadWarm(string fleet)
    {
        var pool=JsonSerializer.Deserialize<NativeWarmOwnership>(File.ReadAllText(WarmRecord),RenderNodeHost.Json)!;
        if(pool.Process.FleetId!=fleet||pool.Process.SpecHash!=RecipeId)throw new IOException("Warm worker fleet/recipe changed; retain ownership");
        return pool;
    }
    private bool WarmCompleted(RuntimeAttempt a,NativeOwnership owned,out int code)
    {
        code=1;string path=Path.Combine(DirectoryFor(a),"exit.json");if(!File.Exists(path))return false;
        var marker=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
        if(marker.GetProperty("activation").GetString()!=owned.Activation||marker.GetProperty("recipe_id").GetString()!=RecipeId)
            throw new IOException("Warm frame completion identity mismatch; retain ownership");
        code=marker.GetProperty("code").GetInt32();return true;
    }
    private void StartWarm(RuntimeAttempt a,NativeOwnership owned)
    {
        using var gate=WarmGate();
        NativeWarmOwnership? pool=File.Exists(WarmRecord)?ReadWarm(a.FleetId):null;
        if(pool is not null){
            if(pool.Process.LaunchRequested&&pool.Process.Pid is null)throw new IOException("Ambiguous warm worker spawn; inspect before relaunch");
            using var old=OwnedProcess(pool.Process);
            if(pool.ActiveActivation is not null)throw new IOException("Unresolved warm frame assignment; no command overwritten");
            if(old is not null&&pool.Retiring&&!old.WaitForExit(3000))throw new IOException("Warm worker retirement unconfirmed");
            if(old is null||old.HasExited)pool=null;
        }
        if(pool is null){
            var process=new NativeOwnership(a.FleetId,"warm-"+cfg.Id,RecipeId,Guid.NewGuid().ToString("N"),LaunchRequested:true);
            pool=new(process,owned.Activation,DirectoryFor(a));WriteJson(WarmRecord,pool); // Intent BEFORE spawn.
            File.Delete(Path.Combine(WarmDirectory,"activate"));
            WriteJson(Path.Combine(WarmDirectory,"spec.json"),new{directory=WarmDirectory,activation=process.Activation,recipe_id=RecipeId,idle_timeout_seconds=options.WarmIdleTimeoutSeconds});
            string logs=Path.Combine(WarmDirectory,"logs",process.Activation);Directory.CreateDirectory(logs);
            var p=new Process{StartInfo=StartInfo(WarmDirectory,"warm_worker.py"),EnableRaisingEvents=true};p.Exited+=(_,_)=>Wake?.Invoke();p.Start();
            pool=pool with{Process=process with{Pid=p.Id,StartedUtc=p.StartTime.ToUniversalTime()}};WriteJson(WarmRecord,pool);
            var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
            // Capture this generation before another StartWarm can replace the active record.
            string activation=process.Activation;int pid=p.Id;var started=p.StartTime.ToUniversalTime();
            _=Task.Run(async()=>{
                try{
                    File.WriteAllText(Path.Combine(logs,"stdout.log"),await output);
                    File.WriteAllText(Path.Combine(logs,"stderr.log"),await error);
                    await p.WaitForExitAsync();
                    WriteJson(Path.Combine(logs,"process-exit.json"),new{activation,pid,started_utc=started,
                        native_exit_code=p.ExitCode,native_exit_code_hex=p.ExitCode.ToString("X8"),observed_utc=DateTimeOffset.UtcNow});
                }catch(Exception e){Console.Error.WriteLine("Warm process diagnostics: "+e);}
                finally{p.Dispose();}
            });
        }else{
            pool=pool with{ActiveActivation=owned.Activation,ActiveDirectory=DirectoryFor(a)};WriteJson(WarmRecord,pool);
        }
        owned=owned with{Pid=pool.Process.Pid,StartedUtc=pool.Process.StartedUtc};WriteJson(RecordPath(a),owned);
        warmJobs[a.AttemptId]=owned;
        File.WriteAllText(Path.Combine(DirectoryFor(a),"activate"),owned.Activation);
        File.WriteAllText(Path.Combine(WarmDirectory,"activate"),pool.Process.Activation);
    }
    private ContainerObservation ObserveWarm(RuntimeAttempt a,NativeOwnership owned)
    {
        // A valid result wins even if the idle worker subsequently exits.
        if(WarmCompleted(a,owned,out int code))return new("exited",a.AttemptId,false){ExitCode=owned.ExitCode??code};
        if(owned.ExitCode is {} stopped)return new("exited",a.AttemptId,false){ExitCode=stopped};
        using var gate=WarmGate();var pool=ReadWarm(a.FleetId);
        // Completion may have committed while waiting for the shared gate.
        if(WarmCompleted(a,owned,out code))return new("exited",a.AttemptId,false){ExitCode=owned.ExitCode??code};
        if(pool.ActiveActivation!=owned.Activation)throw new IOException("Warm frame assignment differs; retain ownership");
        using var p=OwnedProcess(owned);
        return p is null?new("exited",a.AttemptId,false){ExitCode=1}:new("running",a.AttemptId,true);
    }
    private void StopWarm(RuntimeAttempt a,NativeOwnership owned)
    {
        using var gate=WarmGate();
        // A delayed stop for completed frame A must never kill frame B sharing the same PID.
        if(WarmCompleted(a,owned,out _))return;
        if(!owned.LaunchRequested){WriteJson(RecordPath(a),owned with{ExitCode=1});return;}
        var pool=ReadWarm(a.FleetId);
        if(pool.ActiveActivation!=owned.Activation||pool.Process.Pid!=owned.Pid||pool.Process.StartedUtc!=owned.StartedUtc)
            throw new IOException("Stop does not own the active warm frame");
        using var p=OwnedProcess(owned);if(p is not null){p.Kill(true);if(!p.WaitForExit(30000))throw new IOException("Warm stop remains unconfirmed");}
        WriteJson(RecordPath(a),owned with{ExitCode=1});
        WriteJson(WarmRecord,pool with{ActiveActivation=null,ActiveDirectory=null,Retiring=true});
    }
    private void ReleaseWarm(RuntimeAttempt a,NativeOwnership owned)
    {
        using var gate=WarmGate();
        if(File.Exists(WarmRecord)){
            var pool=ReadWarm(a.FleetId);
            if(pool.ActiveActivation==owned.Activation)WriteJson(WarmRecord,pool with{ActiveActivation=null,ActiveDirectory=null});
            else if(pool.ActiveActivation is not null&&!WarmCompleted(a,owned,out _))throw new IOException("Unresolved warm assignment; retain frame slot");
        }
        warmJobs.Remove(a.AttemptId);
    }
}
