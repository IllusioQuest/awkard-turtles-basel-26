using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalHeat.Orchestration;

internal sealed record NativeRenderOptions(string Executable,string ScriptsDirectory,string AssetsDirectory,
    string GraphicsApi="opengl",string ExpectedVersion="5.3.0",int Threads=1,bool EnableGpu=false,bool KeepGpuWarm=false,double WarmIdleTimeoutSeconds=120,bool ReusePreparedScene=false);
internal sealed record PreparedScene(string SceneSha256,string RecipeId,string Blender,string BuildHash);
internal interface IRenderRuntime:IWorkloadRuntime { RenderArtifact[] Results(); }
internal sealed record NativeOwnership(string FleetId,string AttemptId,string SpecHash,string Activation,
    bool LaunchRequested=false,int? Pid=null,DateTimeOffset? StartedUtc=null,int? ExitCode=null,bool Released=false);

/// <summary>Trusted native scenes only. Platform/GPU settings stay outside MeshPeer/placement.
/// CPU/RAM are admission requests, not native OS-enforced quotas. Native frames finish before hold.</summary>
internal sealed partial class NativeRenderRuntime : IRenderRuntime,IDisposable
{
    private readonly RenderConfig cfg;
    private readonly NativeRenderOptions options;
    private readonly string root;
    private readonly string scriptsDirectory;
    private readonly Dictionary<string,Process> children=[];
    private readonly Dictionary<string,RenderArtifact> completedResults=[];
    private readonly Dictionary<int,(DateTimeOffset At,TimeSpan Cpu)> samples=[];
    private string? blenderBuildHash;
    private readonly FileSystemWatcher? completionWatcher;
    public string RecipeId { get; }
    public Action? Wake { get; set; }
    public string ProfileImage=>"scene:"+RecipeId;
    private const long MaximumSceneBytes=16*1024*1024; // Bounded procedural-scene lab cache, not a model number.
    internal static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public NativeRenderRuntime(RenderConfig cfg)
    {
        this.cfg=cfg;options=cfg.Native!;root=Path.Combine(cfg.StateDirectory,"native-attempts");
        Directory.CreateDirectory(root);Directory.CreateDirectory(options.AssetsDirectory);
        if(options.Threads<1||!File.Exists(options.Executable))throw new InvalidDataException("Invalid native Blender executable/threads");
        if(!double.IsFinite(options.WarmIdleTimeoutSeconds)||options.WarmIdleTimeoutSeconds<=0)throw new InvalidDataException("Invalid warm-worker idle timeout seconds");
        // Same recipe across OS-specific executables; actual Blender version/build is checked in artifacts.
        string[] files=["native_worker.py","scene_builder.py","scene_settings.json","warm_worker.py"];
        var contents=files.ToDictionary(f=>f,f=>File.ReadAllBytes(Path.Combine(options.ScriptsDirectory,f)));
        RecipeId=Hash(Encoding.UTF8.GetBytes(options.ExpectedVersion+string.Concat(files.Select(f=>Hash(contents[f])))));
        scriptsDirectory=Path.Combine(cfg.StateDirectory,"recipe",RecipeId);Directory.CreateDirectory(scriptsDirectory);
        foreach(var file in contents){
            string target=Path.Combine(scriptsDirectory,file.Key);
            if(File.Exists(target)&&Hash(File.ReadAllBytes(target))!=Hash(file.Value))throw new InvalidDataException("Immutable recipe cache changed");
            if(!File.Exists(target)){File.WriteAllBytes(target+".tmp",file.Value);File.Move(target+".tmp",target);}
        }

        if(options.KeepGpuWarm&&options.EnableGpu){
            // Warm frames do not exit Blender. Wake the unchanged owner loop on atomic
            // result publication; its timer remains the fallback if an OS event is lost.
            completionWatcher=new(root,"exit.json"){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName};
            completionWatcher.Created+=(_,_)=>Wake?.Invoke();completionWatcher.Renamed+=(_,_)=>Wake?.Invoke();
            completionWatcher.EnableRaisingEvents=true;
        }
    }
    public void Dispose()=>completionWatcher?.Dispose();
    private string DirectoryFor(RuntimeAttempt a)=>Path.Combine(root,a.AttemptId);
    private string RecordPath(RuntimeAttempt a)=>Path.Combine(DirectoryFor(a),"owned.json");
    internal static void WriteJson(string path,object value)
    {
        string temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,value,RenderNodeHost.Json);stream.Flush(true);}
        JournalReplace.Move(temp,path);
    }
    private NativeOwnership Read(RuntimeAttempt a)
    {
        var r=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(RecordPath(a)),RenderNodeHost.Json)!;
        if(r.FleetId!=a.FleetId||r.AttemptId!=a.AttemptId||r.SpecHash!=Hash(Encoding.UTF8.GetBytes(WorkloadJson.Identity(a))))
            throw new InvalidOperationException("Native attempt identity mismatch; no process mutation permitted");
        return r;
    }
    private ProcessStartInfo StartInfo(string directory,string script="native_worker.py")
    {
        var p=new ProcessStartInfo(options.Executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory,
            RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--factory-startup","--disable-autoexec","--background","--gpu-backend",options.GraphicsApi,
            "--threads",options.Threads.ToString(),"--python-exit-code","1","--python",Path.Combine(scriptsDirectory,script),"--",Path.Combine(directory,"spec.json")})p.ArgumentList.Add(arg);
        return p;
    }
    public RenderDeviceReport Probe()
    {
        string dir=Path.Combine(cfg.StateDirectory,"native-probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        WriteJson(Path.Combine(dir,"spec.json"),new{mode=options.EnableGpu?"probe":"info",directory=Path.GetFullPath(dir),recipe_id=RecipeId,
            settings_file=Path.Combine(scriptsDirectory,"scene_settings.json")});
        using var process=new Process{StartInfo=StartInfo(Path.GetFullPath(dir))};process.Start();
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(90000)){process.Kill(true);process.WaitForExit();throw new TimeoutException("Native capability test exceeded 90 seconds");}
        File.WriteAllText(Path.Combine(dir,"stdout.log"),stdout.GetAwaiter().GetResult());File.WriteAllText(Path.Combine(dir,"stderr.log"),stderr.GetAwaiter().GetResult());
        if(process.ExitCode!=0)throw new InvalidDataException("Native capability test failed; see "+dir);
        var report=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"result.json")));
        if(report.GetProperty("blender").GetString()!=options.ExpectedVersion)throw new InvalidDataException("Native Blender version differs from recipe");
        blenderBuildHash=report.GetProperty("build_hash").GetString();
        if(cfg.WorkerRole=="assemble"&&!report.GetProperty("ffmpeg_available").GetBoolean())throw new InvalidDataException("Blender FFmpeg assembly support unavailable");
        return new(options.EnableGpu?"EEVEE":null,options.EnableGpu?"EEVEE hardware context verified: "+report.GetProperty("device").GetString():cfg.WorkerRole=="assemble"?"CPU video assembly available (Blender FFmpeg)":"CPU scene preparation available",report);
    }
    public PreparedScene RegisterScene(PreparedScene scene)
    {
        if(scene.RecipeId!=RecipeId||scene.Blender!=options.ExpectedVersion||scene.BuildHash!=blenderBuildHash)throw new InvalidDataException("Prepared scene recipe/version mismatch");
        string path=AssetPath(scene.SceneSha256);
        if(!File.Exists(path)||new FileInfo(path).Length>MaximumSceneBytes||Hash(File.ReadAllBytes(path))!=scene.SceneSha256)
            throw new InvalidDataException("Prepared scene is absent or its hash/size differs");
        WriteJson(Path.Combine(cfg.StateDirectory,"registered-scene-"+scene.SceneSha256+".json"),scene);
        return scene;
    }
    public PreparedScene[] SavedScenes()
    {
        var scenes=new List<PreparedScene>();
        foreach(string path in Directory.GetFiles(cfg.StateDirectory,"registered-scene-*.json")){
            try{
                var scene=JsonSerializer.Deserialize<PreparedScene>(File.ReadAllText(path),RenderNodeHost.Json);
                if(scene is not null&&scene.RecipeId==RecipeId&&scene.SceneSha256 is not null&&System.Text.RegularExpressions.Regex.IsMatch(scene.SceneSha256,"^[a-f0-9]{64}$"))scenes.Add(scene);
                else Console.Error.WriteLine("Ignoring incompatible scene cache registration: "+Path.GetFileName(path));
            }catch(JsonException){Console.Error.WriteLine("Ignoring malformed scene cache registration: "+Path.GetFileName(path));}
        }
        return scenes.ToArray();
    }
    private PreparedScene? ReusableScene()
    {
        foreach(var scene in SavedScenes().OrderBy(s=>s.SceneSha256,StringComparer.Ordinal)){
            if(scene.Blender!=options.ExpectedVersion||scene.BuildHash!=blenderBuildHash)continue;
            string path=AssetPath(scene.SceneSha256);
            if(File.Exists(path)&&new FileInfo(path).Length<=MaximumSceneBytes&&Hash(File.ReadAllBytes(path))==scene.SceneSha256)return scene;
        }
        return null;
    }
    private string AssetPath(string hash)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-f0-9]{64}$"))throw new InvalidDataException("Invalid scene content identity");
        return Path.Combine(options.AssetsDirectory,hash+".blend");
    }
    public void ValidateAttempt(RuntimeAttempt a)=>RuntimeAttemptValidation.Validate(a);
    public void Preflight(RuntimeAttempt a)
    {
        ValidateAttempt(a);
        if(a.Profile.Image!=ProfileImage||a.Profile.Command.Length==0)throw new InvalidDataException("Untrusted native render profile");
        var cmd=a.Profile.Command;
        if(cmd[0]=="prepare"&&cmd.Length==1){if(cfg.WorkerRole is not ("general" or "prepare"))throw new InvalidDataException("Worker is not a scene preparer");return;}
        if(cmd[0]=="assemble"&&cmd.Length==2){if(cfg.WorkerRole!="assemble")throw new InvalidDataException("Worker is not a video assembler");ValidateAssembly(cmd[1]);return;}
        if(cfg.WorkerRole is not ("general" or "render"))throw new InvalidDataException("Worker is not a frame renderer");
        if(cmd.Length!=3||cmd[1]!="EEVEE"||!options.EnableGpu||!int.TryParse(cmd[0],out int frame)||frame<1||frame>cfg.Frames||!File.Exists(AssetPath(cmd[2])))
            throw new InvalidDataException("Native frame profile/capability/artifact is invalid");
    }
    public IReadOnlyList<string> Discover(string fleet,IReadOnlyList<RuntimeNode> nodes)=>System.IO.Directory.GetFiles(root,"owned.json",SearchOption.AllDirectories)
        .Select(p=>JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(p),RenderNodeHost.Json)!)
        .Where(r=>r.FleetId==fleet&&!r.Released).Select(r=>r.AttemptId).ToArray();
    public void Create(RuntimeAttempt a)
    {
        Preflight(a);Directory.CreateDirectory(DirectoryFor(a));
        if(File.Exists(RecordPath(a))){Read(a);return;}
        WriteJson(Path.Combine(DirectoryFor(a),"job.json"),new{job_id=a.Request.Id});
        WriteJson(RecordPath(a),new NativeOwnership(a.FleetId,a.AttemptId,Hash(Encoding.UTF8.GetBytes(WorkloadJson.Identity(a))),Guid.NewGuid().ToString("N")));
    }
    public void Start(RuntimeAttempt a)
    {
        Preflight(a); // A changed executable/recipe must not resume an old created intent.
        var r=Read(a);if(r.LaunchRequested)throw new IOException("Native launch already requested; reconcile ownership first");
        r=r with{LaunchRequested=true};WriteJson(RecordPath(a),r); // Durable uncertainty BEFORE spawning.
        string dir=Path.GetFullPath(DirectoryFor(a));var cmd=a.Profile.Command;
        var cachedScene=cmd[0]=="prepare"&&options.ReusePreparedScene?ReusableScene():null;
        WriteJson(Path.Combine(dir,"spec.json"),new{mode=cmd[0]=="prepare"?"prepare":cmd[0]=="assemble"?"assemble":"frame",directory=dir,activation=r.Activation,
            dispatched_utc=DateTimeOffset.UtcNow,recipe_id=RecipeId,settings_file=Path.Combine(scriptsDirectory,"scene_settings.json"),
            frame=cmd[0] is "prepare" or "assemble"?0:int.Parse(cmd[0]),expected_frames=cfg.Frames,
            reusable_scene_path=cachedScene is null?null:AssetPath(cachedScene.SceneSha256),reusable_scene_sha256=cachedScene?.SceneSha256,
            manifest_path=cmd[0]=="assemble"?AssemblyPath(cmd[1]):null,scene_sha256=cmd.Length==3?cmd[2]:null,scene_path=cmd.Length==3?AssetPath(cmd[2]):null});
        if(IsWarm(a)){StartWarm(a,r);return;}
        var p=new Process{StartInfo=StartInfo(dir),EnableRaisingEvents=true};p.Exited+=(_,_)=>Wake?.Invoke();p.Start();children[a.AttemptId]=p;
        var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        _=Task.Run(async()=>{File.WriteAllText(Path.Combine(dir,"stdout.log"),await output);File.WriteAllText(Path.Combine(dir,"stderr.log"),await error);});
        r=r with{Pid=p.Id,StartedUtc=p.StartTime.ToUniversalTime()};WriteJson(RecordPath(a),r);
        File.WriteAllText(Path.Combine(dir,"activate"),r.Activation); // No rendering before PID/start identity is durable.
    }
    private Process? OwnedProcess(NativeOwnership r)
    {
        if(r.Pid is null)return null;
        Process p;
        try{p=Process.GetProcessById(r.Pid.Value);}catch(ArgumentException){return null;}
        try{
            _=p.SafeHandle; // Retain the exact OS process handle before checking identity/mutating.
            if(p.HasExited){p.Dispose();return null;}
            if(p.StartTime.ToUniversalTime()!=r.StartedUtc||!string.Equals(Path.GetFullPath(p.MainModule!.FileName),Path.GetFullPath(options.Executable),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
                throw new IOException("Native PID identity changed; retain ownership and do not kill/relaunch");
            return p;
        }catch{p.Dispose();throw;}
    }
    public ContainerObservation Observe(RuntimeAttempt a)
    {
        if(!File.Exists(RecordPath(a)))return new("missing",null,false);
        var r=Read(a);if(r.Released)return new("missing",null,false);
        if(!r.LaunchRequested)return new("created",a.AttemptId,false);
        if(r.Pid is null)throw new IOException("Ambiguous native spawn: activation was not granted; inspect before recovery");
        if(IsWarm(a))return ObserveWarm(a,r);
        using var p=OwnedProcess(r);if(p is not null)return new("running",a.AttemptId,true);
        int code=1;string exit=Path.Combine(DirectoryFor(a),"exit.json");
        if(File.Exists(exit))code=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(exit)).GetProperty("code").GetInt32();
        return new("exited",a.AttemptId,false){ExitCode=r.ExitCode??code};
    }
    public void Stop(RuntimeAttempt a)
    {
        if(!a.Request.Lifecycle.AllowStop)throw new InvalidOperationException("Owner did not authorize stopping native work");
        var r=Read(a);if(IsWarm(a)){StopWarm(a,r);return;}using var p=OwnedProcess(r);if(p is not null){p.Kill(true);if(!p.WaitForExit(30000))throw new IOException("Native stop remains unconfirmed; retain ownership");}
        WriteJson(RecordPath(a),r with{ExitCode=1});
    }
    public void Remove(RuntimeAttempt a)
    {
        var r=Read(a);var observed=Observe(a);if(observed.State=="created"){WriteJson(RecordPath(a),r with{Released=true,ExitCode=1});return;}
        if(!observed.Terminal)throw new IOException("Native attempt is not terminal");
        if(observed.ExitCode==0){
            string dir=DirectoryFor(a);var metadata=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"result.json")));
            if(metadata.GetProperty("recipe_id").GetString()!=RecipeId)throw new InvalidDataException("Native output recipe mismatch");
            if(metadata.GetProperty("stage").GetString()=="prepare"){
                string hash=metadata.GetProperty("scene_sha256").GetString()!;string source=Path.Combine(dir,"scene.blend");
                if(new FileInfo(source).Length>MaximumSceneBytes||Hash(File.ReadAllBytes(source))!=hash)throw new InvalidDataException("Prepared scene output mismatch");
                string target=AssetPath(hash);
                if(File.Exists(target)){if(Hash(File.ReadAllBytes(target))!=hash)throw new InvalidDataException("Immutable scene asset changed");}
                else{string temp=target+"."+a.AttemptId+".tmp";File.Copy(source,temp,true);JournalReplace.Move(temp,target);}
            }else if(metadata.GetProperty("stage").GetString()=="assemble"){if(!File.Exists(Path.Combine(dir,"video.mp4"))||new FileInfo(Path.Combine(dir,"video.mp4")).Length==0)throw new InvalidDataException("Video output missing");}
            else{
                var published=RenderFrameStore.Publish(options.AssetsDirectory,Path.Combine(dir,"frame.png"),metadata);
                WriteJson(Path.Combine(dir,"result.json"),published); // shared bytes and discovery precede released success.
            }
        }
        if(IsWarm(a))ReleaseWarm(a,r);
        WriteJson(RecordPath(a),r with{Released=true,ExitCode=observed.ExitCode});
        if(children.Remove(a.AttemptId,out var child))child.Dispose();
    }
    public double ApplyBatchLoad(RuntimeAttempt a,double load)=>throw new NotSupportedException("Native jobs pause only at frame boundaries");
    public RenderArtifact[] Results()
    {
        // Released successful artifacts are immutable. Rebuild lazily after owner restart;
        // never cache pending/failed attempts, and never replace ownership reconciliation.
        var results=new List<RenderArtifact>();
        foreach(string dir in Directory.GetDirectories(root)){
            if(completedResults.TryGetValue(dir,out var cached)){results.Add(cached);continue;}
            string record=Path.Combine(dir,"owned.json");if(!File.Exists(record))continue;
            var owned=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(record),RenderNodeHost.Json)!;
            if(!owned.Released||owned.ExitCode!=0)continue;
            var artifact=new RenderArtifact(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"job.json"))).GetProperty("job_id").GetString()!,cfg.Id,
                JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"result.json"))));
            completedResults.Add(dir,artifact);results.Add(artifact);
        }
        return results.ToArray();
    }
    public string? ImagePath(string job)=>Directory.GetDirectories(root).FirstOrDefault(d=>File.Exists(Path.Combine(d,"job.json"))&&JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(d,"job.json"))).GetProperty("job_id").GetString()==job) is {} dir?Path.Combine(dir,"frame.png"):null;
    private string AssemblyPath(string hash)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-f0-9]{64}$"))throw new InvalidDataException("Invalid assembly identity");
        return Path.Combine(options.AssetsDirectory,hash+".frames.json");
    }
    public void ValidateAssembly(string hash)
    {
        byte[] bytes=File.ReadAllBytes(AssemblyPath(hash));
        if(bytes.Length>MaximumSceneBytes||Hash(bytes)!=hash)throw new InvalidDataException("Assembly manifest hash/size mismatch");
        var m=JsonSerializer.Deserialize<JsonElement>(bytes);
        if(m.GetProperty("recipe_id").GetString()!=RecipeId||m.GetProperty("build_hash").GetString()!=blenderBuildHash||m.GetProperty("frames").GetArrayLength()!=cfg.Frames)
            throw new InvalidDataException("Assembly recipe/build/frame count mismatch");
        string scene=m.GetProperty("scene_sha256").GetString()!;
        if(!SavedScenes().Any(s=>s.SceneSha256==scene&&s.RecipeId==RecipeId&&s.BuildHash==blenderBuildHash))throw new InvalidDataException("Assembly scene is not registered for this recipe/build");
        int index=0;
        foreach(var f in m.GetProperty("frames").EnumerateArray()){
            string file=f.GetProperty("file").GetString()!;
            if(f.GetProperty("frame").GetInt32()!=++index||!System.Text.RegularExpressions.Regex.IsMatch(file,@"^frame-[a-f0-9]{64}\.png$")||
               Hash(File.ReadAllBytes(Path.Combine(options.AssetsDirectory,file)))!=f.GetProperty("sha256").GetString())throw new InvalidDataException("Assembly frame index/path/hash mismatch");
        }
    }
    public string? VideoPath(string job)=>ImagePath(job) is {} image?Path.Combine(Path.GetDirectoryName(image)!,"video.mp4"):null;
    public JsonElement Stats()
    {
        var now=DateTimeOffset.UtcNow;var rows=new List<object>();
        var owned=children.ToDictionary(p=>p.Key,p=>p.Value);
        var borrowed=new List<Process>();
        foreach(var pair in warmJobs.ToArray()){var process=OwnedProcess(pair.Value);if(process is not null){owned[pair.Key]=process;borrowed.Add(process);}}
        foreach(var pair in owned){
            var p=pair.Value;if(p.HasExited)continue;var cpu=p.TotalProcessorTime;
            double? percent=samples.TryGetValue(p.Id,out var prior)?100*(cpu-prior.Cpu).TotalSeconds/(now-prior.At).TotalSeconds:null;
            samples[p.Id]=(now,cpu);rows.Add(new{attempt=pair.Key,pid=p.Id,cpu_percent_one_core=percent,ram_mib=p.WorkingSet64/(1024d*1024),source="Measured native process; admission resources are not OS quotas"});
        }
        foreach(var process in borrowed)process.Dispose();
        return JsonSerializer.SerializeToElement(rows);
    }
}
