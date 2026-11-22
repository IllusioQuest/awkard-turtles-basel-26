using DigitalHeat.Orchestration;
using System.Net.Http.Json;

/// <summary>Exact process started by a power-on, persisted before the launcher manifest is updated.</summary>
internal sealed record RestartedProcess(int Pid,DateTimeOffset StartedUtc);

/// <summary>
/// Power a fenced GPU worker back on (ADR 0073, T-87). Stages after "ready" (powered off):
/// "starting" (intent saved, no process proven yet), "started" (exact new process saved), "rejoined" (back in the
/// mesh; the record stays so retry deadlines and fenced frames remain available for this render).
/// </summary>
internal sealed partial class RenderSession
{
    private static readonly TimeSpan RestartReadyTimeout=TimeSpan.FromMinutes(3);

    public void RequestStartup(string peerId)
    {
        if(Interlocked.CompareExchange(ref shutdownBusy,1,0)!=0)return;
        Queue(async()=>{await controls.WaitAsync();try{await StartupCore(peerId);}
            finally{controls.Release();Volatile.Write(ref shutdownBusy,0);}});
    }

    private async Task StartupCore(string peerId)
    {
        var off=state.Shutdown??throw new InvalidOperationException("No powered-off node");
        if(off.Identity.PeerId!=peerId)throw new InvalidOperationException("Only the powered-off node can be powered on");
        if(off.Stage is not ("ready" or "starting" or "started"))throw new InvalidOperationException("Power off must finish before power on");
        bool rendering=state.Phase=="rendering",wasHeld=state.Held;
        if(rendering)Update(s=>s with{Held=true});
        await dispatch.WaitAsync();
        try{
            // Survivors stop accepting new frames while work is re-split; their running frames finish normally.
            if(rendering)await DirectHold(true);
            var power=new LocalNodePowerControl(cfg);
            if(state.Shutdown!.Stage=="ready"){
                Update(s=>s with{Shutdown=s.Shutdown! with{Stage="starting"},Error="Powering on "+peerId+"…"});
                var fresh=power.Restart(state.Shutdown!.Identity,(pid,started)=>
                    Update(s=>s with{Shutdown=s.Shutdown! with{Stage="started",Restarted=new(pid,started)}}));
                Update(s=>s with{Shutdown=s.Shutdown! with{Identity=fresh}});
            }else if(state.Shutdown!.Stage=="starting")
                throw new IOException("Power-on outcome is uncertain (no process was recorded). Check for a second controller of "+peerId+" and relaunch the lab.");
            else if(state.Shutdown!.Restarted is {} again)
                Update(s=>s with{Shutdown=s.Shutdown! with{Identity=power.Adopt(peerId,again.Pid,again.StartedUtc)}});
            Update(s=>s with{Error="Waiting for "+peerId+" to verify its GPU and recover…"});
            var node=await WaitUntilReady(peerId);
            // The restarted node starts held. Give it the current scene and this render's cancellation tombstones.
            if(state.Scene is not null){using var r=await http.PostAsJsonAsync(cfg.Peers[peerId]+"/prepared-scene",state.Scene,RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            if(state.Shutdown!.Plan?.CancelIds is {Length:>0} cancelled){using var r=await http.PostAsJsonAsync(cfg.Peers[peerId]+"/cancel",new RenderCancel(cancelled),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            foreach(var p in ActivePeerUrls){using var r=await http.PostAsJsonAsync(p.Value+"/readmit",new RenderWithdraw(peerId),RenderNodeHost.Json);r.EnsureSuccessStatusCode();}
            if(rendering)await RegisterRejoinNeeds(node);
            Update(s=>s with{Shutdown=s.Shutdown! with{Stage="rejoined"},Error=null});
            if(rendering&&!wasHeld){
                await DirectHold(false);await RefreshPeers(true);Update(s=>s with{Held=false});
                if(state.Phase is not ("complete" or "cancelled")&&running is not {IsCompleted:false}){stopRequested=false;running=Task.Run(Execute);}
            }
        }finally{dispatch.Release();}
    }

    /// <summary>Fresh, verified status of the restarted node with all of its old attempts released.</summary>
    private async Task<RenderPeerStatus> WaitUntilReady(string peerId)
    {
        await RefreshPeers(true);
        var reference=peers.FirstOrDefault(p=>p.PeerId!=peerId&&p.WorkerRole=="render");
        string last="no answer";
        for(var until=DateTimeOffset.UtcNow+RestartReadyTimeout;DateTimeOffset.UtcNow<until;await Task.Delay(1000)){
            RenderPeerStatus? status;
            try{status=await http.GetFromJsonAsync<RenderPeerStatus>(cfg.Peers[peerId]+"/status",RenderNodeHost.Json);}
            catch(Exception e)when(e is HttpRequestException or TaskCanceledException){last="not reachable yet";continue;}
            if(status is null||status.PeerId!=peerId){last="identity mismatch";continue;}
            if(status.Error is not null){last=status.Error;continue;}
            if(status.Image!=cfg.Image||reference is not null&&(status.RecipeId!=reference.RecipeId||status.RendererBuildHash!=reference.RendererBuildHash))
                throw new InvalidDataException(peerId+" restarted with a different renderer recipe or build");
            if(cfg.PreparedSceneMode&&!status.VerifiedBackends.Contains("EEVEE")){last="GPU not verified";continue;}
            if(status.Attempts.Any(a=>a.Phase!="released")){last="releasing frames stopped at power-off";continue;}
            return status;
        }
        throw new IOException(peerId+" is not ready yet ("+last+"). Press Power on again to keep waiting.");
    }

    /// <summary>
    /// Split only the frames that are neither finished nor running over all render workers by weight (ADR 0073).
    /// Finished and running frames stay where they are. Attempts this render lost on a worker (e.g. killed at
    /// power-off) are passed as superseded so their committed heat counts as other work, not as frames.
    /// </summary>
    private async Task RegisterRejoinNeeds(RenderPeerStatus node)
    {
        await RefreshPeers(true);if(pollingError is not null)throw new IOException(pollingError);
        var all=peers.Where(p=>p.PeerId!=node.PeerId).Append(node).ToArray();
        string prefix=RenderHeatNeed.RunPrefix(cfg.Id,state.Id);
        var needs=RenderFailoverPlan.RejoinNeeds(state.FrameCount??cfg.Frames,FrameIds(),all,state.FencedFrames,prefix,cfg.FrameHeatKwh);
        foreach(var need in needs){
            using var r=await http.PostAsJsonAsync(cfg.Peers[need.Key]+"/run-need",need.Value,RenderNodeHost.Json);
            r.EnsureSuccessStatusCode();
        }
    }
}

internal sealed partial class RenderSession
{
    private JobDeadline? RetryDeadline(int frame)=>state.Shutdown?.Plan?.Deadlines.GetValueOrDefault(frame)??state.RetryDeadlines.GetValueOrDefault(frame);
    private static Dictionary<int,JobDeadline> Merge(Dictionary<int,JobDeadline> a,Dictionary<int,JobDeadline> b)
    {
        var merged=new Dictionary<int,JobDeadline>(a);
        foreach(var pair in b)merged.TryAdd(pair.Key,pair.Value);
        return merged;
    }
}

internal sealed partial class RenderSession
{
    /// <summary>Test seam: run the power-on checks directly instead of through the control queue.</summary>
    internal Task StartupCoreForTest(string peerId)=>StartupCore(peerId);
}
