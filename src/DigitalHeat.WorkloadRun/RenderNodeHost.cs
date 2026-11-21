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
    public GpuBinding? Gpu { get; init; }
    public string? GpuBackend { get; init; }
}
internal sealed record FrameSubmit(string JobId, int Frame, string? PinnedNode, string Backend="CPU");
internal sealed record RenderHold(bool Hold);
internal sealed record RenderArtifact(string JobId, string PeerId, JsonElement Metadata);
internal sealed record RenderGpuStats(double UtilizationPercent,long UsedMib,long TotalMib,DateTimeOffset ObservedUtc,
    string Scope="Physical GPU totals, including any other workloads");
internal sealed record RenderPeerStatus(string PeerId, string PhysicalHostId, string Image, bool Held,
    double HeatDemandKwh, string Device, string? Error, OriginJob[] Jobs, AttemptRecord[] Attempts,
    RenderArtifact[] Results, JsonElement? ResourceStats)
{
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
        var cfg=JsonSerializer.Deserialize<RenderConfig>(File.ReadAllText(path),Json)!;
        string token=Environment.GetEnvironmentVariable("MESH_TOKEN") ?? throw new InvalidDataException("Missing lab token");
        var docker=new DockerCommands();
        Directory.CreateDirectory(cfg.StateDirectory);
        string engine=docker.Run(["info","--format","{{.ID}} "]).Trim();
        string enginePath=Path.Combine(cfg.StateDirectory,"engine-id");
        if(File.Exists(enginePath)&&File.ReadAllText(enginePath)!=engine)throw new InvalidDataException("Engine changed; inspect before recovery");
        File.WriteAllText(enginePath,engine);
        // This first portable image supports CPU. GPU is never advertised from OpenGL capability.
        string probe=docker.Run(["run","--rm","--network","none",cfg.Image,"--probe"]);
        string device=probe.Split('\n').FirstOrDefault(l=>l.StartsWith("RENDER_DEVICES=")) ?? throw new InvalidDataException("No Blender device report");
        File.WriteAllText(Path.Combine(cfg.StateDirectory,"devices.json"),device["RENDER_DEVICES=".Length..]);
        string? gpuBackend=null;
        if(cfg.Gpu is {} gpu && cfg.GpuBackend is {} backend){
            try{
                var args=new List<string>{"run","--rm","--network","none"};
                if(gpu.NvidiaDeviceId is {} nvidia)args.AddRange(["--gpus","device="+nvidia]);
                foreach(var d in gpu.LinuxDevices)args.AddRange(["--device",d]);
                foreach(var b in gpu.ReadOnlyBinds)args.AddRange(["--volume",b+":ro"]);
                args.AddRange([cfg.Image,"--verify-gpu",backend]);
                if(docker.Run(args).Contains("RENDER_GPU_VERIFIED="+backend,StringComparison.Ordinal))gpuBackend=backend;
            }catch(Exception e){Console.Error.WriteLine("GPU probe failed; explicit CPU fallback: "+e.Message);}
        }
        string output=Path.Combine(cfg.StateDirectory,"frames");Directory.CreateDirectory(output);
        var runtime=new ArtifactRuntime(docker,output,cfg.Id);
        var outgoing=Channel.CreateBounded<(string From,string To,PeerMessage Message)>(256);
        var inbox=Channel.CreateBounded<Action>(256);
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
        http.DefaultRequestHeaders.Add("X-Mesh-Token",token);
        string[] backends=gpuBackend is null?["CPU"]:[gpuBackend,"CPU"];
        string[] manifestBackends=["CPU","OPTIX","CUDA","HIP","ONEAPI"];
        var profiles=(from f in Enumerable.Range(1,cfg.Frames) from b in manifestBackends select (f,b)).ToDictionary(x=>"landscape-"+x.b+"-"+x.f,
            x=>new ContainerProfile("landscape-"+x.b+"-"+x.f,cfg.Image,"65534",[x.f.ToString(),x.b],null,[],[],[]) {ShmMib=64});
        long revision=0;
        WorkloadInventory Inventory(){
            var now=DateTimeOffset.UtcNow;var hour=new DateTimeOffset(now.Year,now.Month,now.Day,now.Hour,0,0,TimeSpan.Zero);
            var res=new ResourceAmount(cfg.CpuCores,cfg.RamMib,1);
            return new(++revision,hour,hour.AddHours(1),cfg.Source,
                [new(cfg.Id,"CH",new(cfg.HeatDemandKwh,0,0,1,.8),0)],
                [new(cfg.PhysicalHostId+"-"+cfg.Id,res,gpuBackend is null?[]:[new(cfg.Gpu!.DeviceId,gpuBackend)])],
                [new(cfg.Id+"-worker",cfg.Id,cfg.PhysicalHostId+"-"+cfg.Id,true,true,res,[],gpuBackend is null?[]:[cfg.Gpu!.DeviceId],[],[])],[])
                {ResourceObservedAtUtc=now,ResourceValidUntilUtc=now.AddSeconds(30)};
        }
        using var peer=new MeshPeer("landscape",cfg.Id,Path.Combine(cfg.StateDirectory,"owner"),Inventory(),
            [new(cfg.Id+"-worker","default","none",gpuBackend is null?[]:[cfg.Gpu!],[])],profiles,cfg.Peers.Keys,
            new Transport(outgoing.Writer),runtime,TimeProvider.System,TimeSpan.FromSeconds(20));
        // On restart operator must explicitly resume: no unattended comparison continuation.
        peer.SetHold(true);
        object gate=new();string? error=null;JsonElement? stats=null;RenderGpuStats? gpuStats=null;
        RenderPeerStatus Status(){lock(gate)return new(cfg.Id,cfg.PhysicalHostId,cfg.Image,peer.Held,cfg.HeatDemandKwh,
            gpuBackend is null?"CPU fallback (no verified Cycles GPU)":gpuBackend+" verified GPU; CPU available",error,peer.Jobs(),peer.Attempts(),runtime.Results(),stats)
            {VerifiedBackends=backends,PhysicalGpuId=gpuBackend is null?null:cfg.Gpu!.DeviceId,GpuStats=gpuStats};}
        var builder=WebApplication.CreateBuilder();builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower);
        var app=builder.Build();var stop=app.Lifetime.ApplicationStopping;
        app.Use(async(ctx,next)=>{
            // Browser UI is reachable only through the local-only published home peer.
            bool browser=ctx.Request.Path.StartsWithSegments("/ui");
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
        app.MapPost("/hold",(RenderHold h)=>{lock(gate){peer.SetHold(h.Hold);return Results.Json(Status(),Json);}});
        app.MapPost("/frames",(FrameSubmit f)=>{
            if(f.Frame<1||f.Frame>cfg.Frames||!manifestBackends.Contains(f.Backend)||!System.Text.RegularExpressions.Regex.IsMatch(f.JobId,"^[a-z0-9-]{1,80}$"))return Results.BadRequest();
            lock(gate){
                if(peer.Held)return Results.Conflict(new {error="Origin held"});
                var prior=peer.Jobs().SingleOrDefault(j=>j.Request.Id==cfg.Id+"."+f.JobId);
                if(prior is null){
                    var now=DateTimeOffset.UtcNow;
                    var r=new WorkloadRequest(f.JobId,"batch",cfg.Id,new("anywhere",cfg.Source,null),
                        new(cfg.CpuCores,cfg.RamMib,1),cfg.Source,cfg.FrameHeatKwh,cfg.Source,[],f.Backend=="CPU"?null:f.Backend,f.PinnedNode,null,[],new(true,true,false))
                        {Deadline=new(now,now.AddSeconds(cfg.DeadlineSeconds),cfg.Source,true)};
                    peer.Submit(r,"landscape-"+f.Backend+"-"+f.Frame);
                }
                return Results.Ok(new {job_id=cfg.Id+"."+f.JobId});
            }
        });
        app.MapGet("/image/{job}",(string job)=>{
            if(!System.Text.RegularExpressions.Regex.IsMatch(job,"^[a-z0-9.-]{1,100}$"))return Results.BadRequest();
            string file=Path.Combine(output,job,"frame.png");return File.Exists(file)?Results.File(file,"image/png"):Results.NotFound();
        });
        var comparison=new RenderComparison(cfg,http);
        app.MapGet("/ui",()=>Results.Content(File.ReadAllText("/app/render.html"),"text/html"));
        app.MapGet("/ui/status",async()=>Results.Json(await comparison.LiveSnapshot(),Json));
        app.MapPost("/ui/render",async()=>{await comparison.Start();return Results.Accepted();});
        app.MapPost("/ui/pause",async()=>{await comparison.Hold(true);return Results.Ok();});
        app.MapPost("/ui/resume",async()=>{await comparison.Hold(false);return Results.Ok();});
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
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));
            while(await timer.WaitForNextTickAsync(stop)){
                lock(gate)try{
                    while(inbox.Reader.TryRead(out var action))action();
                    peer.Update(Inventory());peer.Tick();error=null;
                }catch(Exception e){error=e.Message;}
            }
        },stop);
        _=Task.Run(async()=>{
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
            while(await timer.WaitForNextTickAsync(stop))try{
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

    private sealed class ArtifactRuntime(IDockerCommands docker,string directory,string peer):IWorkloadRuntime
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
            if(observed.ExitCode==0){
                string dest=Path.Combine(directory,a.Request.Id);Directory.CreateDirectory(dest);
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
