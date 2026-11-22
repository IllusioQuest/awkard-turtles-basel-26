using System.Net.Http.Json;
using System.Text.Json;

internal sealed partial class RenderSession
{
    private readonly SemaphoreSlim dispatch=new(1);
    private int shutdownBusy;
    private bool Fenced=>state.Shutdown is {} n&&n.Stage!="intent";
    private IEnumerable<KeyValuePair<string,string>> ActivePeerUrls=>cfg.Peers.Where(p=>!Fenced||p.Key!=state.Shutdown!.Identity.PeerId);
    private int ActivePeerCount=>cfg.Peers.Count-(Fenced?1:0);
    private string LocalFrameJob(int frame)=>RenderFailoverPlan.LocalJob(state.Id,frame,state.FrameGenerations.GetValueOrDefault(frame));
    private string[] FrameIds()=>Enumerable.Range(1,state.FrameCount??cfg.Frames).Select(f=>cfg.Id+"."+LocalFrameJob(f)).ToArray();
    private RenderArtifact[] CurrentFrames()
    {
        var ids=FrameIds().ToHashSet(StringComparer.Ordinal);
        return peers.SelectMany(p=>p.Results).Concat(state.FencedFrames).Where(f=>ids.Contains(f.JobId))
            .DistinctBy(f=>f.JobId).OrderBy(f=>f.Metadata.GetProperty("frame").GetInt32()).ToArray();
    }
    private RenderPeerStatus[] DisplayPeers()=>Fenced?[..peers.Where(p=>p.PeerId!=state.Shutdown!.Identity.PeerId),state.Shutdown!.LastPeer with{PoweredOff=true,Held=true,Error=null,Jobs=[],Attempts=[],Results=state.FencedFrames,
        HeatNeedFrames=state.FencedFrames.Length,HeatDemandKwh=state.FencedFrames.Length*cfg.FrameHeatKwh}]:peers;
    public string? SharedImagePath(string node,string job)
    {
        var frame=state.Frames.Concat(state.FencedFrames).FirstOrDefault(f=>f.PeerId==node&&f.JobId==job);
        return frame is not null&&cfg.Native is not null&&frame.Metadata.TryGetProperty("image_sha256",out _)?RenderFrameStore.VerifiedPath(cfg.Native.AssetsDirectory,frame.Metadata):null;
    }
    public void RequestShutdown(string peerId)
    {
        if(Interlocked.CompareExchange(ref shutdownBusy,1,0)!=0)return;
        Queue(async()=>{await controls.WaitAsync();try{await ShutdownCore(peerId);}catch{
            if(state.Shutdown is not null)Update(s=>s with{Held=true});throw;
        }finally{controls.Release();Volatile.Write(ref shutdownBusy,0);}});
    }
    private async Task DirectHold(bool hold,string? skip=null)
    {
        foreach(var p in ActivePeerUrls.Where(p=>p.Key!=skip)){using var r=await http.PostAsJsonAsync(p.Value+"/hold",new RenderHold(hold),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
    }
    private async Task ShutdownCore(string peerId)
    {
        if(state.Shutdown is {Stage:"ready"})return;
        if(state.Shutdown is null){
            if(state.Phase!="rendering"||state.Scene is null)throw new InvalidOperationException("Power off is available during frame rendering");
            await RefreshPeers(true);
            var target=peers.Single(p=>p.PeerId==peerId&&p.WorkerRole=="render");
            var identity=new LocalNodePowerControl(cfg).Prepare(peerId);
            Update(s=>s with{Held=true,Shutdown=new("intent",identity,target with{Jobs=[],Attempts=[],Results=[]}),Error="Powering off node and confirming ownership…"});
        }else if(state.Shutdown.Identity.PeerId!=peerId)throw new InvalidOperationException("Only one node shutdown is supported in this lab");
        Update(s=>s with{Held=true});
        await dispatch.WaitAsync();
        try{
            // Holds prevent new accepts, but AllowPause=false frames on surviving nodes finish normally.
            await DirectHold(true,peerId);
            if(state.Shutdown!.Stage=="intent"){
                new LocalNodePowerControl(cfg).StopAndConfirm(state.Shutdown.Identity);
                Update(s=>s with{Shutdown=s.Shutdown! with{Stage="fenced"}});
            }
            foreach(var p in ActivePeerUrls){using var r=await http.PostAsJsonAsync(p.Value+"/withdraw",new RenderWithdraw(peerId),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            if(state.Shutdown!.Plan is null){
                await RefreshPeers(true);if(pollingError is not null)throw new IOException(pollingError);
                var identity=state.Shutdown.Identity;var current=FrameIds().ToHashSet(StringComparer.Ordinal);
                var recovered=RenderFrameStore.Recover(identity,cfg.Native!.AssetsDirectory,current,state.Scene!);
                var oldIds=Directory.GetFiles(Path.Combine(identity.StateDirectory,"native-attempts"),"job.json",SearchOption.AllDirectories)
                    .Select(p=>JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(p)).GetProperty("job_id").GetString()!).ToHashSet(StringComparer.Ordinal);
                var origin=await http.GetFromJsonAsync<RenderPeerStatus>(cfg.Peers[cfg.Id]+"/status",RenderNodeHost.Json)??throw new IOException("No origin snapshot");
                var plan=RenderFailoverPlan.Create(cfg.Id,state.Id,state.FrameCount??cfg.Frames,state.FrameGenerations,peers,origin.Jobs,recovered,peerId,oldIds);
                // One durable update precedes cancellation, partial registrations and replacement submissions.
                Update(s=>s with{Shutdown=s.Shutdown! with{Stage="planned",Plan=plan},FrameGenerations=plan.Generations,FencedFrames=plan.FencedFrames});
            }
            var saved=state.Shutdown!.Plan!;
            foreach(var p in ActivePeerUrls){using var r=await http.PostAsJsonAsync(p.Value+"/cancel",new RenderCancel(saved.CancelIds),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            foreach(var share in saved.Needs){using var r=await http.PostAsJsonAsync(cfg.Peers[share.Key]+"/run-need",new RunHeatNeed(RenderHeatNeed.RunPrefix(cfg.Id,state.Id),share.Value,cfg.FrameHeatKwh),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            Update(s=>s with{Shutdown=s.Shutdown! with{Stage="ready"},Error=null});
            await DirectHold(false);await RefreshPeers(true);
            Update(s=>s with{Held=false,Error=null});
            if(state.Phase is not ("complete" or "cancelled")&&running is not {IsCompleted:false}){stopRequested=false;running=Task.Run(Execute);}
        }finally{dispatch.Release();}
    }
}
