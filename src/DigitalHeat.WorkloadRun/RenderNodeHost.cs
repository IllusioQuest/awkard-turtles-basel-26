using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

internal sealed record RenderConfig(string Id, string Image, string StateDirectory, string PhysicalHostId,
    Dictionary<string,string> Peers, double HeatDemandKwh, double CpuCores, long RamMib,
    double FrameHeatKwh, int Frames, double DeadlineSeconds, string Source)
{
    // Display-only deployment intent. Never used by MeshPeer or counted as live capacity.
    public string[] PlannedGpuPeers { get; init; } = [];
    public NativeRenderOptions? Native { get; init; }
    public bool PreparedSceneMode { get; init; }
    public bool ServeUi { get; init; } = true; // Remote workers expose token-protected peer APIs only.
    public GpuBinding? Gpu { get; init; }
    public string? GpuBackend { get; init; }
    public double GpuFrameShare { get; init; } = .8; // User-approved workload mix; not a utilization promise.
}
internal sealed record FrameSubmit(string JobId, int Frame, string? PinnedNode, string Backend="CPU",string? SceneSha256=null);
internal sealed record RenderHold(bool Hold);
internal sealed record RenderCancel(string[] JobIds);
internal sealed record RenderArtifact(string JobId, string PeerId, JsonElement Metadata);
internal sealed record RenderGpuStats(double UtilizationPercent,long UsedMib,long TotalMib,DateTimeOffset ObservedUtc,
    string Scope="Physical GPU totals, including any other workloads");
internal sealed record RenderPeerStatus(string PeerId, string PhysicalHostId, string Image, bool Held,
    double HeatDemandKwh, string Device, string? Error, OriginJob[] Jobs, AttemptRecord[] Attempts,
    RenderArtifact[] Results, JsonElement? ResourceStats)
{
    public string? RecipeId { get; init; }
    public string? RendererBuildHash { get; init; }
    public bool FrameBoundaryPause { get; init; }
    public string[] VerifiedBackends { get; init; } = ["CPU"];
    public string? PhysicalGpuId { get; init; }
    public RenderGpuStats? GpuStats { get; init; }
}

/// <summary>Trusted renderer adapter, not a second scheduler: all frame placement/ownership uses MeshPeer.</summary>
internal static class RenderNodeHost
{
    internal static readonly JsonSerializerOptions Json = WorkloadJson.Options;
    public static async Task Run(string path)
    {
        // This adapter calls the existing synchronous Docker CLI. Reserve control workers so a
        // one-core render quota cannot starve ASP.NET status/message continuations behind it.
        const int MinimumControlWorkers = 8;
        ThreadPool.GetMinThreads(out var workers,out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers,MinimumControlWorkers),completionPorts);
        var cfg=JsonSerializer.Deserialize<RenderConfig>(File.ReadAllText(path),Json)!;
        string token=Environment.GetEnvironmentVariable("MESH_TOKEN") ?? throw new InvalidDataException("Missing lab token");
        var docker=new DockerCommands();
        Directory.CreateDirectory(cfg.StateDirectory);
        string output=Path.Combine(cfg.StateDirectory,"frames");Directory.CreateDirectory(output);
        NativeRenderRuntime? native=cfg.Native is null?null:new(cfg);
        IRenderRuntime runtime;RenderDeviceReport gpuProbe;string engine;
        if(native is not null){
            if(!cfg.PreparedSceneMode)throw new InvalidDataException("Native runtime requires the prepared-scene pipeline");
            cfg=cfg with{Image=native.ProfileImage};
            runtime=native;gpuProbe=native.Probe();engine="native:"+Path.GetFullPath(cfg.Native!.Executable);
        }else{
            runtime=new ArtifactRuntime(docker,output,cfg.Id);gpuProbe=RenderDeviceProbe.Run(cfg,docker);
            engine=docker.Run(["info","--format","{{.ID}} "]).Trim();
        }
        string enginePath=Path.Combine(cfg.StateDirectory,"engine-id");
        if(File.Exists(enginePath)&&File.ReadAllText(enginePath)!=engine)throw new InvalidDataException("Executor changed; inspect before recovery");
        File.WriteAllText(enginePath,engine);
        File.WriteAllText(Path.Combine(cfg.StateDirectory,"devices.json"),gpuProbe.Devices.GetRawText());
        string? gpuBackend=gpuProbe.VerifiedBackend;string gpuDiagnostic=gpuProbe.Diagnostic;
        var outgoing=Channel.CreateBounded<(string From,string To,PeerMessage Message)>(256);
        var inbox=Channel.CreateBounded<Action>(256);
        if(native is not null)native.Wake=()=>inbox.Writer.TryWrite(()=>{});
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(15)};
        http.DefaultRequestHeaders.Add("X-Mesh-Token",token);
        string[] backends=gpuBackend is null?["CPU"]:[gpuBackend,"CPU"];
        string[] manifestBackends=cfg.PreparedSceneMode?["EEVEE"]:["CPU","OPTIX","CUDA","HIP","ONEAPI"];
        var profiles=(from f in Enumerable.Range(1,cfg.Frames) from b in manifestBackends select (f,b)).ToDictionary(x=>"landscape-"+x.b+"-"+x.f,
            x=>new ContainerProfile("landscape-"+x.b+"-"+x.f,cfg.Image,"65534",[x.f.ToString(),x.b],null,[],[],[]) {ShmMib=64});
        if(cfg.PreparedSceneMode){profiles.Clear();profiles.Add("scene-prepare",new("scene-prepare",cfg.Image,"65534",["prepare"],null,[],[],[]));}
        if(native is not null)foreach(var scene in native.SavedScenes()){
            native.RegisterScene(scene);
            for(int frame=1;frame<=cfg.Frames;frame++){
                string id=PreparedProfile(scene.SceneSha256,frame);
                profiles[id]=new(id,cfg.Image,"65534",[frame.ToString(),"EEVEE",scene.SceneSha256],null,[],[],[]);
            }
        }
        long revision=0;
        WorkloadInventory Inventory(){
            var now=DateTimeOffset.UtcNow;var hour=new DateTimeOffset(now.Year,now.Month,now.Day,now.Hour,0,0,TimeSpan.Zero);
            var res=new ResourceAmount(cfg.CpuCores,cfg.RamMib,1);
            return new(++revision,hour,hour.AddHours(1),cfg.Source,
                [new(cfg.Id,"CH",new(cfg.HeatDemandKwh,0,0,1,.8),0)],
                [new(cfg.PhysicalHostId+"-"+cfg.Id,res,gpuBackend is null?[]:[new(cfg.Gpu!.DeviceId,gpuBackend)])],
                [new(cfg.Id+"-worker",cfg.Id,cfg.PhysicalHostId+"-"+cfg.Id,true,true,res,cfg.PreparedSceneMode?["scene_prepare"]:[],gpuBackend is null?[]:[cfg.Gpu!.DeviceId],[],[])],[])
                {ResourceObservedAtUtc=now,ResourceValidUntilUtc=now.AddSeconds(30)};
        }
        using var peer=new MeshPeer("landscape",cfg.Id,Path.Combine(cfg.StateDirectory,"owner"),Inventory(),
            [new(cfg.Id+"-worker","default","none",gpuBackend is null?[]:[cfg.Gpu!],[])],profiles,cfg.Peers.Keys,
            new Transport(outgoing.Writer),runtime,TimeProvider.System,TimeSpan.FromSeconds(20));
        // On restart operator must explicitly resume: no unattended session continuation.
        peer.SetHold(true);
        object gate=new();string? error=null;JsonElement? stats=null;RenderGpuStats? gpuStats=null;
        RenderPeerStatus CaptureStatus()=>new(cfg.Id,cfg.PhysicalHostId,cfg.Image,peer.Held,cfg.HeatDemandKwh,
            gpuBackend is null?(cfg.PreparedSceneMode?gpuDiagnostic:"CPU fallback: "+gpuDiagnostic):gpuBackend+" verified GPU; "+(cfg.Native is {EnableGpu:true,KeepGpuWarm:true}?"warm frame process":"CPU available"),error,peer.Jobs(),peer.Attempts(),runtime.Results(),stats)
            {RendererBuildHash=gpuProbe.Devices.TryGetProperty("build_hash",out var build)?build.GetString():null,RecipeId=native?.RecipeId,FrameBoundaryPause=native is not null,VerifiedBackends=backends,PhysicalGpuId=gpuBackend is null?null:cfg.Gpu!.DeviceId,GpuStats=gpuStats};
        var published=CaptureStatus();
        RenderPeerStatus Status()=>Volatile.Read(ref published);
        var builder=WebApplication.CreateBuilder();builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower);
        var app=builder.Build();
        app.Lifetime.ApplicationStopped.Register(()=>native?.Dispose());var stop=app.Lifetime.ApplicationStopping;
        app.Use(async(ctx,next)=>{
            // Browser UI is reachable only through the local-only published home peer.
            bool browser=ctx.Request.Path.StartsWithSegments("/ui");
            if(browser && !cfg.ServeUi){ctx.Response.StatusCode=404;return;}
            if(!browser && ctx.Request.Headers["X-Mesh-Token"]!=token){ctx.Response.StatusCode=401;return;}
            if(browser && ctx.Request.Method=="POST" && ctx.Request.Headers["X-Render-Request"]!="local")
            {ctx.Response.StatusCode=403;return;}
            await next(ctx);
        });
        app.MapGet("/status",()=>Results.Json(Status(),Json));
        app.MapPost("/messages",(MeshEnvelope packet)=>{
            if(!cfg.Peers.ContainsKey(packet.Sender))return Results.BadRequest();
            return inbox.Writer.TryWrite(()=>peer.Receive(packet.Sender,packet.Message))?Results.Accepted():Results.StatusCode(503);
        });
        app.MapPost("/hold",(RenderHold h)=>{lock(gate){peer.SetHold(h.Hold);Volatile.Write(ref published,CaptureStatus());return Results.Json(Status(),Json);}});
        app.MapPost("/cancel",(RenderCancel c)=>{lock(gate){peer.CancelBatchWorkloads(c.JobIds);Volatile.Write(ref published,CaptureStatus());return Results.Ok();}});
        app.MapPost("/frames",(FrameSubmit f)=>{
            if(f.Frame<1||f.Frame>cfg.Frames||!manifestBackends.Contains(f.Backend)||(cfg.PreparedSceneMode&&(f.SceneSha256 is null||!profiles.ContainsKey(PreparedProfile(f.SceneSha256,f.Frame))))||!System.Text.RegularExpressions.Regex.IsMatch(f.JobId,"^[a-z0-9-]{1,80}$"))return Results.BadRequest();
            if(Status().Held)return Results.Conflict(new {error="Origin held"});
            if(!inbox.Writer.TryWrite(()=>{
                // Durable intent is idempotent; a lost HTTP acknowledgement retries this ID.
                if(peer.Held)return;
                var prior=peer.Jobs().SingleOrDefault(j=>j.Request.Id==cfg.Id+"."+f.JobId);
                if(prior is null){
                    var now=DateTimeOffset.UtcNow;
                    var r=new WorkloadRequest(f.JobId,"batch",cfg.Id,new("anywhere",cfg.Source,null),
                        new(cfg.CpuCores,cfg.RamMib,1),cfg.Source,cfg.FrameHeatKwh,cfg.Source,[],f.Backend=="CPU"?null:f.Backend,f.PinnedNode,null,[],new(!cfg.PreparedSceneMode,true,false))
                        {Deadline=new(now,now.AddSeconds(cfg.DeadlineSeconds),cfg.Source,true)};
                    peer.Submit(r,cfg.PreparedSceneMode?PreparedProfile(f.SceneSha256!,f.Frame):"landscape-"+f.Backend+"-"+f.Frame);
                }
            }))return Results.StatusCode(503);
            return Results.Accepted(value:new {job_id=cfg.Id+"."+f.JobId});
        });
        app.MapPost("/prepare",(FrameSubmit f)=>{
            if(native is null||!System.Text.RegularExpressions.Regex.IsMatch(f.JobId,"^[a-z0-9-]{1,80}$"))return Results.BadRequest();
            if(Status().Held)return Results.Conflict();
            return inbox.Writer.TryWrite(()=>{
                if(peer.Held)return;
                var now=DateTimeOffset.UtcNow;
                var request=new WorkloadRequest(f.JobId,"batch",cfg.Id,new("anywhere",cfg.Source,null),
                    new(cfg.CpuCores,cfg.RamMib,1),cfg.Source,cfg.FrameHeatKwh,cfg.Source,["scene_prepare"],null,null,null,[],new(false,true,false))
                    {Deadline=new(now,now.AddSeconds(cfg.DeadlineSeconds),cfg.Source,true)};
                if(!peer.Jobs().Any(j=>j.Request.Id==cfg.Id+"."+f.JobId))peer.Submit(request,"scene-prepare");
            })?Results.Accepted():Results.StatusCode(503);
        });
        app.MapPost("/prepared-scene",(PreparedScene scene)=>{
            if(native is null)return Results.BadRequest();
            lock(gate){
                // Serialize registration with native ownership/artifact writes.
                native.RegisterScene(scene);
                for(int frame=1;frame<=cfg.Frames;frame++){
                    string id=PreparedProfile(scene.SceneSha256,frame);
                    profiles[id]=new(id,cfg.Image,"65534",[frame.ToString(),"EEVEE",scene.SceneSha256],null,[],[],[]);
                }
                peer.Publish();
            }
            return Results.Ok(scene);
        });
        app.MapGet("/image/{job}",(string job)=>{
            if(!System.Text.RegularExpressions.Regex.IsMatch(job,"^[a-z0-9.-]{1,100}$"))return Results.BadRequest();
            string file=native?.ImagePath(job)??Path.Combine(output,job,"frame.png");return File.Exists(file)?Results.File(file,"image/png"):Results.NotFound();
        });
        var session=new RenderSession(cfg,http);
        app.MapGet("/ui/theme.css",()=>Results.Content(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"dashboard.css")),"text/css"));
        app.MapGet("/ui",()=>Results.Content(File.ReadAllText(Path.Combine(cfg.Native?.ScriptsDirectory??AppContext.BaseDirectory,"render.html")),"text/html"));
        app.MapGet("/ui/status",async()=>Results.Json(await session.LiveSnapshot(),Json));
        app.MapPost("/ui/render",async()=>{await session.Start();return Results.Accepted();});
        app.MapPost("/ui/pause",()=>{session.RequestHold(true);return Results.Accepted();});
        app.MapPost("/ui/resume",()=>{session.RequestHold(false);return Results.Accepted();});
        app.MapPost("/ui/stop",()=>{session.RequestStop();return Results.Accepted();});
        app.MapGet("/ui/image/{node}/{job}",async(string node,string job)=>{
            if(!cfg.Peers.TryGetValue(node,out var url)||!System.Text.RegularExpressions.Regex.IsMatch(job,"^[a-z0-9.-]{1,100}$"))return Results.BadRequest();
            var bytes=await http.GetByteArrayAsync(url+"/image/"+job);return Results.File(bytes,"image/png");
        });
        _=Task.Run(async()=>{
            await foreach(var p in outgoing.Reader.ReadAllAsync(stop))try{
                using var response=await http.PostAsJsonAsync(cfg.Peers[p.To]+"/messages",new MeshEnvelope(p.From,p.Message),Json,stop);
                response.EnsureSuccessStatusCode();
            }catch(Exception e)when(e is HttpRequestException or TaskCanceledException){Console.Error.WriteLine("Peer delivery deferred: "+p.To);}
        },stop);
        _=Task.Run(async()=>{
            var refreshAt=DateTimeOffset.MinValue;DateTimeOffset? observedHour=null;
            while(!stop.IsCancellationRequested){
                // Messages and process exits wake admission immediately. Do not wait for a poll
                // tick, and do not leave abandoned channel waits behind on timer wakeups.
                using var wake=CancellationTokenSource.CreateLinkedTokenSource(stop);
                await Task.WhenAny(inbox.Reader.WaitToReadAsync(wake.Token).AsTask(),Task.Delay(TimeSpan.FromSeconds(2),wake.Token));
                wake.Cancel();
                lock(gate)try{
                    for(int count=0;count<32&&inbox.Reader.TryRead(out var action);count++)action();
                    var now=DateTimeOffset.UtcNow;var hour=new DateTimeOffset(now.Year,now.Month,now.Day,now.Hour,0,0,TimeSpan.Zero);
                    // Unchanged synthetic inventory must remain valid long enough for a remote
                    // offer/proposal round trip. Refresh freshness every 10s (valid for 30s),
                    // rather than invalidating every outstanding offer on every 2s control tick.
                    if(now>=refreshAt||observedHour!=hour){peer.Update(Inventory());refreshAt=now.AddSeconds(10);observedHour=hour;}
                    peer.Tick();error=null;
                }catch(Exception e){error=e.Message;Console.Error.WriteLine(e);}
                lock(gate)Volatile.Write(ref published,CaptureStatus());
            }
        },stop);
        _=Task.Run(async()=>{
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));
            do{await session.RefreshPeers();}while(await timer.WaitForNextTickAsync(stop));
        },stop);
        _=Task.Run(async()=>{
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
            while(await timer.WaitForNextTickAsync(stop))try{
                if(native is not null){lock(gate)stats=native.Stats();continue;}
                string raw=docker.Run(["stats","--no-stream","--format","{{json .}}"]);
                var rows=raw.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(l=>JsonSerializer.Deserialize<JsonElement>(l)).ToArray();
                lock(gate)stats=JsonSerializer.SerializeToElement(rows);
                if(gpuBackend is "CUDA" or "OPTIX"){
                    try{
                        string id=docker.Run(["ps","-q","--filter","label=digitalheat.fleet=landscape."+cfg.Id]).Split('\n',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
                        if(id!=""){
                            string values=docker.Run(["exec",id,"nvidia-smi","--query-gpu=utilization.gpu,memory.used,memory.total","--format=csv,noheader,nounits"]).Split('\n')[0];
                            var v=values.Split(',').Select(s=>double.Parse(s.Trim(),System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                            lock(gate)gpuStats=new(v[0],(long)v[1],(long)v[2],DateTimeOffset.UtcNow);
                        }else lock(gate)gpuStats=null;
                    }catch{lock(gate)gpuStats=null;}
                }
            }catch{lock(gate)stats=null;}
        },stop);
        await app.RunAsync();
    }
    private sealed class Transport(ChannelWriter<(string From,string To,PeerMessage Message)> q):IPeerMeshTransport
    {public void Send(string from,string to,PeerMessage message)=>q.TryWrite((from,to,message));}

    internal static string PreparedProfile(string hash,int frame)=>"prepared-"+hash+"-"+frame;
    private sealed class ArtifactRuntime(IDockerCommands docker,string directory,string peer):IRenderRuntime
    {
        private readonly DockerWorkloadRuntime inner=new(docker);
        public void Preflight(RuntimeAttempt a)=>inner.Preflight(a);
        public IReadOnlyList<string> Discover(string f,IReadOnlyList<RuntimeNode> n)=>inner.Discover(f,n);
        public ContainerObservation Observe(RuntimeAttempt a)=>inner.Observe(a);
        public void Create(RuntimeAttempt a)=>inner.Create(a);
        public void Start(RuntimeAttempt a)=>inner.Start(a);
        public void Stop(RuntimeAttempt a)=>inner.Stop(a);
        public double ApplyBatchLoad(RuntimeAttempt a,double load)=>inner.ApplyBatchLoad(a,load);
        public void Remove(RuntimeAttempt a){
            var observed=inner.Observe(a);
            string dest=Path.Combine(directory,a.Request.Id);Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest,"render.log"),docker.Run(["logs",observed.ContainerId!]));
            if(observed.ExitCode==0){
                docker.Run(["cp",observed.ContainerId+":/work/.",dest]);
                if(!File.Exists(Path.Combine(dest,"frame.png"))||!File.Exists(Path.Combine(dest,"result.json")))
                    throw new InvalidDataException("Successful process has no complete render output");
            }
            inner.Remove(a);
        }
        public RenderArtifact[] Results()=>Directory.GetDirectories(directory).Where(d=>File.Exists(Path.Combine(d,"result.json"))&&File.Exists(Path.Combine(d,"frame.png")))
            .Select(d=>new RenderArtifact(Path.GetFileName(d),peer,JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(d,"result.json"))))).ToArray();
    }
}
