using System.Net.Http.Json;
using System.Text.Json;
using DigitalHeat.Orchestration;

internal sealed record RenderPeerConnection(string PeerId,string State,DateTimeOffset? CheckedUtc);

internal sealed record RenderSessionState(string Id,string Phase,bool Held,RenderArtifact[] Frames,string? Error=null)
{
    public NodeShutdown? Shutdown { get; init; }
    public Dictionary<int,int> FrameGenerations { get; init; }=[];
    public RenderArtifact[] FencedFrames { get; init; }=[];
    /// <summary>Original deadlines of retried frames from earlier power-offs in this render (ADR 0073).</summary>
    public Dictionary<int,JobDeadline> RetryDeadlines { get; init; }=[];
    public RenderArtifact? Video { get; init; }
    public PreparedScene? Scene { get; init; }
    public DateTimeOffset? PreparationStartedUtc { get; init; }
    public DateTimeOffset? PreparationFinishedUtc { get; init; }
    public DateTimeOffset? StartedUtc { get; init; }
    public DateTimeOffset? FinishedUtc { get; init; }
    public DateTimeOffset? PauseStartedUtc { get; init; }
    public double PausedSeconds { get; init; }
    public bool Recovered { get; init; }
    public string? RecipeImage { get; init; }
    public int? FrameCount { get; init; }
}

/// <summary>Multi-node render workflow only. MeshPeer owns all placement/admission/execution.</summary>
internal sealed partial class RenderSession
{
    private readonly RenderConfig cfg;
    private readonly HttpClient http;
    private readonly string path;
    private readonly object stateGate=new();
    private readonly SemaphoreSlim controls=new(1),poll=new(1);
    private RenderSessionState state;
    private RenderPeerStatus[] peers=[];
    private string? pollingError;
    private RenderPeerConnection[] connections=[];
    private DateTimeOffset? peerSnapshotUtc;
    private Task? running;
    private volatile bool stopRequested;
    private int restartPending;
    private readonly SemaphoreSlim queuedControls=new(1);
    public RenderSession(RenderConfig cfg,HttpClient http)
    {
        this.cfg=cfg;this.http=http;path=Path.Combine(cfg.StateDirectory,"render-session.json");
        state=File.Exists(path)?JsonSerializer.Deserialize<RenderSessionState>(File.ReadAllText(path),RenderNodeHost.Json)!:new("","idle",true,[]);
        if(state.Id!=""&&state.Phase is not ("complete" or "cancelled"))state=state with{Phase="recovery-paused",Held=true,Recovered=true,Error="Owner restarted; resume reconciles the same job identities."};
    }
    private void Update(Func<RenderSessionState,RenderSessionState> change){lock(stateGate){state=change(state);NativeRenderRuntime.WriteJson(path,state);}}
    private bool CanStart=>running is not {IsCompleted:false}&&state.Phase is "idle" or "complete" or "cancelled";
    public async Task RefreshPeers(bool wait=false)
    {
        if(wait)await poll.WaitAsync();else if(!await poll.WaitAsync(0))return;
        try{
            // Planned peers are intentionally absent here: only runtime Peers are probed.
            // Keep execution's all-or-nothing snapshot; independent probe results are UI facts.
            // Ask for the current run only (plus unfinished work); older released history is not needed here.
            string query=state.Id==""?"":"?job_prefix="+Uri.EscapeDataString(RenderHeatNeed.RunPrefix(cfg.Id,state.Id));
            var checkedPeers=await Task.WhenAll(ActivePeerUrls.Select(async p=>{
                try{
                    using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var status=await http.GetFromJsonAsync<RenderPeerStatus>(p.Value+"/status"+query,RenderNodeHost.Json,limit.Token)??throw new IOException("Empty peer status");
                    if(status.PeerId!=p.Key)throw new IOException("Peer identity mismatch");
                    return (Status:(RenderPeerStatus?)status,Connection:new RenderPeerConnection(p.Key,"reachable",DateTimeOffset.UtcNow));
                }catch{
                    return (Status:(RenderPeerStatus?)null,Connection:new RenderPeerConnection(p.Key,"unreachable",DateTimeOffset.UtcNow));
                }
            }));
            connections=checkedPeers.Select(p=>p.Connection).Concat(Fenced?[new RenderPeerConnection(state.Shutdown!.Identity.PeerId,"powered-off",DateTimeOffset.UtcNow)]:Array.Empty<RenderPeerConnection>()).ToArray();
            if(checkedPeers.Any(p=>p.Status is null)){
                pollingError="Waiting for configured peer(s): "+string.Join(", ",connections.Where(p=>p.State!="reachable").Select(p=>p.PeerId))+". No replacement work dispatched; work data is from the last complete snapshot.";
                return;
            }
            peers=checkedPeers.Select(p=>p.Status!).ToArray();peerSnapshotUtc=DateTimeOffset.UtcNow;pollingError=null;
            // A frame may finish while Hold drains the GPU. Publish that output even though
            // the dispatch loop is paused, so the preview/count never waits for Resume.
            if(state.Id!=""&&state.Held){
                var finished=CurrentFrames();
                if(finished.Length!=state.Frames.Length)Update(s=>s with{Frames=finished});
                var video=peers.SelectMany(p=>p.Results).FirstOrDefault(r=>r.JobId==cfg.Id+"."+state.Id+"-assemble");
                if(video is not null&&state.Video is null)Update(s=>s with{Video=video});
            }
        }catch(Exception e){pollingError="Waiting for peer; no replacement work dispatched: "+e.Message;}
        finally{poll.Release();}
    }
    public Task<object> LiveSnapshot()=>Task.FromResult(Snapshot());
    public object Snapshot()
    {
        RenderSessionState s;lock(stateGate)s=state;
        var timing=Timing(s,DateTimeOffset.UtcNow);var display=DisplayPeers();
        return new{node_shutdown_enabled=cfg.AllowLocalNodeShutdown,node_shutdown_busy=Volatile.Read(ref shutdownBusy)!=0,node_shutdown=s.Shutdown?.Stage,s.Id,s.Phase,s.Held,s.Error,restart_pending=Volatile.Read(ref restartPending)!=0,can_start_new=CanStart,configured_peers=cfg.Peers.Count,peer_connection_error=pollingError,
            network=new{origin_peer_id=cfg.Id,configured_peer_ids=cfg.Peers.Keys.ToArray(),planned_gpu_peers=cfg.PlannedGpuPeers,
                connections,peer_snapshot_utc=peerSnapshotUtc,work_data_stale=pollingError is not null},
            mode="multiple",frames_per_run=s.FrameCount??cfg.Frames,completed=s.Frames.Length,frames=s.Frames,video=s.Video,width=640,height=360,samples=32,
            engine=cfg.PreparedSceneMode?"Eevee":"Cycles",device=cfg.PreparedSceneMode?"CPU scene preparation → verified GPU frames":"Capability-gated multi-node frame jobs",
            elapsed_seconds=timing.Elapsed,paused_seconds=timing.Paused,active_seconds=timing.Active,
            preparation=s.Scene,preparation_started_utc=s.PreparationStartedUtc,preparation_finished_utc=s.PreparationFinishedUtc,
            preparation_seconds=s.PreparationStartedUtc is {} ps?((s.PreparationFinishedUtc??DateTimeOffset.UtcNow)-ps).TotalSeconds:0,
            pause_pending=s.Held&&peers.Any(p=>p.Attempts.Any(a=>a.Phase is "running" or "starting" or "resuming" or "pausing")),
            pause_note=cfg.PreparedSceneMode?"Pause finishes current frame/preparation, then blocks new work":"Pause gates new work and confirms owned CPU suspension",
            physical_host_note="Physical hosts: "+string.Join(", ",peers.Select(p=>p.PhysicalHostId).Distinct())+". Logical peers on one host share hardware.",peers=display,
            source=cfg.Source};
    }
    internal static (double Elapsed,double Paused,double Active) Timing(RenderSessionState s,DateTimeOffset now)
    {
        if(s.StartedUtc is not {} start)return (0,0,0);
        var end=s.FinishedUtc??now;
        double elapsed=Math.Max(0,(end-start).TotalSeconds);
        double paused=s.PausedSeconds+(s.PauseStartedUtc is {} p?Math.Max(0,(end-p).TotalSeconds):0);
        return (elapsed,paused,Math.Max(0,elapsed-paused));
    }
    public async Task Start()
    {
        await controls.WaitAsync();try{
            if(!CanStart)throw new InvalidOperationException("Stop the current run before starting another");
            await RefreshPeers();if(pollingError is not null||peers.Length!=ActivePeerCount)throw new IOException("Wait for all configured peers");
            if(peers.Any(p=>p.Attempts.Any(a=>a.Phase!="released")))throw new IOException("An owned peer job is still active; reconcile first");
            if(peers.Select(p=>p.Image).Distinct().Count()!=1||peers.Any(p=>p.Image!=cfg.Image))throw new InvalidDataException("Renderer recipe/image differs between peers");
            RenderGpuLayout.Validate(cfg,peers);
            if(cfg.PreparedSceneMode&&(!peers.Any(p=>p.VerifiedBackends.Contains("EEVEE"))||peers.Any(p=>p.RecipeId is null)||peers.Select(p=>p.RendererBuildHash).Distinct().Count()!=1))
                throw new InvalidDataException("Prepared-scene mode needs a verified GPU and matching Blender build/recipe");
            if(state.Id!="")NativeRenderRuntime.WriteJson(Path.Combine(cfg.StateDirectory,"render-session-"+state.Id+".json"),state);
            stopRequested=false;Update(_=>new(Guid.NewGuid().ToString("N")[..12],"preparing",false,[]){RecipeImage=cfg.Image,FrameCount=cfg.Frames,Shutdown=state.Shutdown is {Stage:"ready"} off?off with{Plan=null}:null});
            running=Task.Run(Execute);
        }finally{controls.Release();}
    }
    public void RequestStart()=>Queue(Start);
    public void RequestHold(bool hold)=>Queue(()=>Hold(hold));
    public void RequestStop()=>Queue(Stop);
    public void RequestRestart()
    {
        if(Interlocked.CompareExchange(ref restartPending,1,0)!=0)return;
        Queue(async()=>{try{await Stop();await Start();}finally{Volatile.Write(ref restartPending,0);}});
    }
    private void Queue(Func<Task> action)=>_=Task.Run(async()=>{await queuedControls.WaitAsync();try{await action();}catch(Exception e){Update(s=>s with{Error=e.Message});}finally{queuedControls.Release();}});
    private async Task SetPeersHold(bool hold)
    {
        var replies=await Task.WhenAll(ActivePeerUrls.Select(p=>p.Value).Select(async url=>{
            using var r=await http.PostAsJsonAsync(url+"/hold",new RenderHold(hold),RenderNodeHost.Json);r.EnsureSuccessStatusCode();
            return await r.Content.ReadFromJsonAsync<RenderPeerStatus>(RenderNodeHost.Json)??throw new IOException("No hold confirmation");
        }));
        var until=DateTimeOffset.UtcNow.AddMinutes(3); // Lab control bound; no GPU kernel preemption claim.
        while(hold&&replies.Any(p=>!p.Held||p.Attempts.Any(a=>a.Phase is "running" or "starting" or "resuming" or "pausing"))){
            if(DateTimeOffset.UtcNow>=until)throw new IOException("Pause still pending; workers remain held");
            await Task.Delay(500);await RefreshPeers();if(pollingError is not null)throw new IOException(pollingError);replies=peers;
        }
    }
    public async Task Hold(bool hold)
    {
        await controls.WaitAsync();try{
            if(!hold&&state.Shutdown is {} pending&&pending.Stage is not ("ready" or "rejoined")){
                if(pending.Stage is "starting" or "started")await StartupCore(pending.Identity.PeerId);else await ShutdownCore(pending.Identity.PeerId);
                return;
            }
            if(state.Phase is "idle" or "complete" or "cancelled")throw new InvalidOperationException("No resumable active run");
            Update(s=>s with{Held=true,Error=hold?"Pause pending: finishing active frame/preparation…":null});
            await SetPeersHold(hold);
            var now=DateTimeOffset.UtcNow;
            Update(s=>s with{Held=hold,PauseStartedUtc=hold&&s.StartedUtc is not null?s.PauseStartedUtc??now:null,
                PausedSeconds=s.PausedSeconds+(!hold&&s.PauseStartedUtc is {} p?(now-p).TotalSeconds:0),Error=null});
            if(!hold&&running is not {IsCompleted:false}){stopRequested=false;running=Task.Run(Execute);}
        }finally{controls.Release();}
    }
    public async Task Stop()
    {
        await controls.WaitAsync();try{
            if(state.Phase is "idle" or "cancelled")return;
            stopRequested=true;Update(s=>s with{Held=true,Error="Stopping after current frame; keeping results…"});
            await SetPeersHold(true);if(running is {} task)await task;
            string prefix=cfg.Id+"."+state.Id+"-";
            string[] ids=Enumerable.Range(1,state.FrameCount??cfg.Frames).SelectMany(f=>Enumerable.Range(0,state.FrameGenerations.GetValueOrDefault(f)+1).Select(g=>cfg.Id+"."+RenderFailoverPlan.LocalJob(state.Id,f,g))).Append(prefix+"prepare").Append(prefix+"assemble").ToArray();
            foreach(var url in ActivePeerUrls.Select(p=>p.Value)){using var r=await http.PostAsJsonAsync(url+"/cancel",new RenderCancel(ids),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            await RefreshPeers();if(pollingError is not null||peers.SelectMany(p=>p.Attempts).Any(a=>ids.Contains(a.Attempt.Request.Id)&&a.Phase!="released"))throw new IOException("Cancellation uncertain; keep held");
            var finished=DateTimeOffset.UtcNow;
            Update(s=>s with{Phase="cancelled",Held=true,FinishedUtc=finished,
                PausedSeconds=s.PausedSeconds+(s.PauseStartedUtc is {} p?(finished-p).TotalSeconds:0),PauseStartedUtc=null,
                Error="Stopped. Completed images/logs retained; ready for a new run."});
        }finally{controls.Release();}
    }
    private async Task Submit(string route,string id,int frame,string backend="CPU",string? scene=null)
    {
        if(stopRequested)return;
        using var reply=await http.PostAsJsonAsync(cfg.Peers[cfg.Id]+route,new FrameSubmit(id,frame,null,backend,scene,frame>0?RetryDeadline(frame):null),RenderNodeHost.Json);reply.EnsureSuccessStatusCode();
    }
    private async Task Prepare()
    {
        if(!cfg.PreparedSceneMode)return;
        if(state.PreparationStartedUtc is null)Update(s=>s with{PreparationStartedUtc=DateTimeOffset.UtcNow});
        string id=state.Id+"-prepare",global=cfg.Id+"."+id;
        while(state.Scene is null&&!stopRequested){
            if(state.Held){await Task.Delay(500);continue;}
            await RefreshPeers();if(pollingError is not null){await Task.Delay(1000);continue;}
            var result=peers.SelectMany(p=>p.Results).FirstOrDefault(r=>r.JobId==global);
            if(result is not null){
                var m=result.Metadata;Update(s=>s with{Scene=new(m.GetProperty("scene_sha256").GetString()!,m.GetProperty("recipe_id").GetString()!,m.GetProperty("blender").GetString()!,m.GetProperty("build_hash").GetString()!),PreparationFinishedUtc=DateTimeOffset.UtcNow});break;
            }
            CheckFailure([global],[]);
            if(!peers.SelectMany(p=>p.Jobs).Any(j=>j.Request.Id==global))try{await Submit("/prepare",id,0);}catch(HttpRequestException){/* Reconcile this same durable ID next poll. */}
            await Task.Delay(1000);
        }
        if(stopRequested)return;
        foreach(var url in ActivePeerUrls.Select(p=>p.Value)){using var r=await http.PostAsJsonAsync(url+"/prepared-scene",state.Scene,RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
    }
    /// <summary>ADR 0069: give each GPU node its weighted share of this run as heat need before frames are
    /// submitted. Idempotent: resume re-sends the same split. Placement itself stays with MeshPeer.</summary>
    private async Task RegisterRunNeed()
    {
        await RefreshPeers();if(pollingError is not null)throw new IOException(pollingError);
        var gpus=peers.Where(p=>p.WorkerRole=="render").Select(p=>(Id:p.PeerId,Weight:p.WorkWeight)).ToArray();
        if(gpus.Length==0)return;
        // Every render node gets its share, including 0, so no node keeps a previous run's leftover need.
        // A failed registration fails the run visibly rather than splitting it over fewer GPUs silently.
        int frames=state.FrameCount??cfg.Frames;string prefix=RenderHeatNeed.RunPrefix(cfg.Id,state.Id);
        foreach(var share in state.Shutdown?.Plan?.Needs??RenderHeatNeed.Allocate(frames,gpus)){
            using var r=await http.PostAsJsonAsync(cfg.Peers[share.Key]+"/run-need",new RunHeatNeed(prefix,share.Value,cfg.FrameHeatKwh),RenderNodeHost.Json);
            r.EnsureSuccessStatusCode();
        }
    }
    private async Task Assemble()
    {
        Update(s=>s with{Phase="assembling"});
        string assets=cfg.Native!.AssetsDirectory;var entries=new List<object>();
        foreach(var frame in state.Frames.OrderBy(f=>f.Metadata.GetProperty("frame").GetInt32())){
            int index=frame.Metadata.GetProperty("frame").GetInt32();
            if(index!=entries.Count+1)throw new InvalidDataException("Frame manifest is incomplete or duplicated");
            var shared=SharedImagePath(frame.PeerId,frame.JobId);
            var bytes=shared is not null?await File.ReadAllBytesAsync(shared):await http.GetByteArrayAsync(cfg.Peers[frame.PeerId]+"/image/"+frame.JobId);
            string hash=NativeRenderRuntime.Hash(bytes),file="frame-"+hash+".png";string path=Path.Combine(assets,file);
            if(!File.Exists(path))await File.WriteAllBytesAsync(path,bytes);
            entries.Add(new{frame=index,file,sha256=hash});
        }
        var manifest=new{recipe_id=state.Scene!.RecipeId,build_hash=state.Scene.BuildHash,scene_sha256=state.Scene.SceneSha256,
            fps=state.Frames[0].Metadata.GetProperty("fps").GetInt32(),frames=entries};
        byte[] json=JsonSerializer.SerializeToUtf8Bytes(manifest,RenderNodeHost.Json);string identity=NativeRenderRuntime.Hash(json);
        await File.WriteAllBytesAsync(Path.Combine(assets,identity+".frames.json"),json);
        foreach(var url in ActivePeerUrls.Select(p=>p.Value)){using var r=await http.PostAsJsonAsync(url+"/assembly-manifest",new AssemblyManifest(identity),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
        string id=state.Id+"-assemble",global=cfg.Id+"."+id;
        while(!stopRequested){
            if(state.Held){await Task.Delay(250);continue;}
            await RefreshPeers();if(pollingError is not null){await Task.Delay(500);continue;}
            var video=peers.SelectMany(p=>p.Results).FirstOrDefault(r=>r.JobId==global);
            if(video is not null){Update(s=>s with{Video=video});return;}
            CheckFailure([global],[]);
            if(!peers.SelectMany(p=>p.Jobs).Any(j=>j.Request.Id==global)){
                using var r=await http.PostAsJsonAsync(cfg.Peers[cfg.Id]+"/assemble",new AssemblySubmit(id,identity),RenderNodeHost.Json);r.EnsureSuccessStatusCode();
            }
            await Task.Delay(500);
        }
    }
    private void CheckFailure(string[] ids,RenderArtifact[] done)
    {
        if(peers.Any(p=>p.Error is not null))throw new IOException("Peer error: "+string.Join("; ",peers.Where(p=>p.Error!=null).Select(p=>p.PeerId+": "+p.Error)));
        if(peers.SelectMany(p=>p.Attempts).Any(a=>ids.Contains(a.Attempt.Request.Id)&&a.Phase=="released"&&!done.Any(f=>f.JobId==a.Attempt.Request.Id)))throw new IOException("An owned job failed or expired; inspect retained logs");
        if(peers.SelectMany(p=>p.Jobs).Any(j=>ids.Contains(j.Request.Id)&&j.Request.Deadline?.DueAtUtc<=DateTimeOffset.UtcNow&&!done.Any(f=>f.JobId==j.Request.Id)))throw new IOException("A waiting job expired without completion");
    }
    private async Task Execute()
    {
        try{
            if(state.RecipeImage!=cfg.Image||state.FrameCount!=cfg.Frames)throw new InvalidDataException("Saved recipe/frame settings changed; retain ownership");
            await SetPeersHold(false);await Prepare();if(stopRequested)return;
            if(cfg.HeatNeedPerRun)await RegisterRunNeed();if(stopRequested)return;
            Update(s=>s with{Phase="rendering",StartedUtc=s.StartedUtc??DateTimeOffset.UtcNow,Held=false});
            while(!stopRequested){
                if(state.Held){await Task.Delay(500);continue;}
                await dispatch.WaitAsync();try{
                    if(state.Held)continue;
                    await RefreshPeers();if(pollingError is not null){await Task.Delay(1000);continue;}
                    string[] ids=FrameIds();var done=CurrentFrames();
                    CheckFailure(ids,done);
                    if(cfg.PreparedSceneMode&&done.Any(f=>f.Metadata.GetProperty("scene_sha256").GetString()!=state.Scene!.SceneSha256||f.Metadata.GetProperty("recipe_id").GetString()!=state.Scene.RecipeId||f.Metadata.GetProperty("engine").GetString()!="BLENDER_EEVEE"||f.Metadata.GetProperty("build_hash").GetString()!=state.Scene.BuildHash))throw new IOException("Frame manifest differs from the prepared scene");
                    if(done.Length!=state.Frames.Length)Update(s=>s with{Frames=done});
                    if(done.Length==cfg.Frames){if(cfg.AssembleVideo){await Assemble();if(stopRequested)return;}Update(s=>s with{Phase="complete",FinishedUtc=DateTimeOffset.UtcNow,Held=false});return;}
                    var known=peers.SelectMany(p=>p.Jobs).Select(j=>j.Request.Id).ToHashSet(StringComparer.Ordinal);
                    // Feed at most one new frame per GPU peer each pass. The old one-frame
                    // per-second pacing starved parallel workers; MeshPeer still owns placement.
                    int remaining=Math.Max(1,peers.Count(p=>p.PhysicalGpuId is not null));
                    for(int frame=1;frame<=cfg.Frames&&remaining>0;frame++){
                        string id=LocalFrameJob(frame);if(known.Contains(cfg.Id+"."+id))continue;
                        string backend=cfg.PreparedSceneMode?"EEVEE":RenderDeviceAllocation.Backend(frame,cfg.Frames,cfg.GpuFrameShare,peers.SelectMany(p=>p.VerifiedBackends).FirstOrDefault(b=>b!="CPU"));
                        try{await Submit("/frames",id,frame,backend,state.Scene?.SceneSha256);remaining--;}catch(Exception e)when(e is HttpRequestException or TaskCanceledException){pollingError="Submission uncertain; reconciling the same frame ID";break;}
                    }
                }finally{dispatch.Release();}
                await Task.Delay(1000);
            }
        }catch(Exception e){
            if(stopRequested)return;
            Update(s=>s with{Phase="failed",Held=true,Error=e.Message});try{await SetPeersHold(true);}catch{}
        }
    }
    public static async Task Verify(string configPath)
    {
        var cfg=JsonSerializer.Deserialize<RenderConfig>(File.ReadAllText(configPath),RenderNodeHost.Json)!;
        cfg=cfg with{Frames=2,StateDirectory=Path.Combine(cfg.StateDirectory,"verification-"+Guid.NewGuid().ToString("N"))};Directory.CreateDirectory(cfg.StateDirectory);
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};http.DefaultRequestHeaders.Add("X-Mesh-Token",Environment.GetEnvironmentVariable("MESH_TOKEN"));
        var session=new RenderSession(cfg,http);
        // Native host computes its recipe identity at startup; diagnostics use that published identity.
        var readyUntil=DateTimeOffset.UtcNow.AddSeconds(90);
        do{await session.RefreshPeers();if(session.peers.Length==cfg.Peers.Count)break;await Task.Delay(1000);}while(DateTimeOffset.UtcNow<readyUntil);
        if(session.peers.Length!=session.ActivePeerCount)throw new IOException("Configured workers did not become ready");
        cfg=cfg with{Image=session.peers.First(p=>p.PeerId==cfg.Id).Image};session=new(cfg,http);
        await session.Start();bool paused=false;var until=DateTimeOffset.UtcNow.AddMinutes(10);
        while(DateTimeOffset.UtcNow<until){
            await Task.Delay(1000);await session.RefreshPeers();
            if(!paused&&session.peers.Any(p=>p.Attempts.Any(a=>a.Phase=="running"&&a.Attempt.Request.Id.Contains(session.state.Id)&&int.TryParse(a.Attempt.Profile.Command[0],out _)))){
                await session.Hold(true);await Task.Delay(2000);
                if(session.peers.Any(p=>p.Attempts.Any(a=>a.Phase=="running")))throw new IOException("Frame boundary did not drain");
                await session.Hold(false);paused=true;
            }
            if(session.state.Phase=="failed")throw new IOException(session.state.Error);
            if(session.state.Phase=="complete"){
                var result=new{mode="real_multi_node_pipeline_verification",pause_verified=paused,snapshot=session.Snapshot()};
                NativeRenderRuntime.WriteJson(Path.Combine(Path.GetDirectoryName(cfg.StateDirectory)!,"native-verification.json"),result);Console.WriteLine(JsonSerializer.Serialize(result,RenderNodeHost.Json));return;
            }
        }
        await session.Hold(true);throw new TimeoutException("Verification held after ten minutes");
    }
}
