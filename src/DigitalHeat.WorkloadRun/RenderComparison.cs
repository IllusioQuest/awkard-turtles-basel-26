using System.Net.Http.Json;
using System.Text.Json;

internal sealed record RenderLeg(string Mode, DateTimeOffset? StartedUtc=null, DateTimeOffset? FinishedUtc=null,
    double PausedSeconds=0, DateTimeOffset? PauseStartedUtc=null, int Completed=0, RenderArtifact[]? Frames=null)
{
    public double ElapsedSeconds => StartedUtc is {} s ? ((FinishedUtc??DateTimeOffset.UtcNow)-s).TotalSeconds : 0;
    public double TotalPausedSeconds => PausedSeconds+(PauseStartedUtc is {} p ? (DateTimeOffset.UtcNow-p).TotalSeconds : 0);
    public double ActiveSeconds => Math.Max(0,ElapsedSeconds-TotalPausedSeconds);
}
internal sealed record ComparisonState(string Id,string Phase,bool Held,RenderLeg[] Legs,string? Error=null)
{
    public bool Recovered { get; init; }
}

/// <summary>Benchmark driver only. It submits ordinary frame jobs; MeshPeer owns all placement.</summary>
internal sealed class RenderComparison(RenderConfig cfg,HttpClient http)
{
    private readonly SemaphoreSlim serial=new(1);
    private readonly string path=Path.Combine(cfg.StateDirectory,"comparison.json");
    private ComparisonState state=Load(cfg);
    private RenderPeerStatus[] peers=[];
    private Task? run;
    private static ComparisonState Load(RenderConfig cfg){
        string p=Path.Combine(cfg.StateDirectory,"comparison.json");
        var s=File.Exists(p)?JsonSerializer.Deserialize<ComparisonState>(File.ReadAllText(p),RenderNodeHost.Json):null;
        return s is null?new("","idle",true,[]):s with {Phase=s.Phase=="complete"?"complete":"recovery-paused",Held=true,Recovered=true,
            Error=s.Phase=="complete"?null:"Owner restarted. Resume reconciles existing frame IDs; no replacement starts."};
    }
    private void Save(){string tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(state,RenderNodeHost.Json));File.Move(tmp,path,true);}
    public object Snapshot(){
        var s=state;
        bool fair=!s.Recovered&&s.Legs.Length==2&&s.Legs.All(l=>l.FinishedUtc is not null&&l.Completed==cfg.Frames&&l.TotalPausedSeconds==0)
            &&s.Legs.SelectMany(l=>l.Frames??[]).Select(f=>f.Metadata.GetProperty("backend").GetString()).Distinct().Count()==1;
        return new {s.Id,s.Phase,s.Held,s.Error,frames_per_leg=cfg.Frames,width=640,height=360,samples=32,
            device="GPU preferred; "+SelectedBackend()+" selected consistently for both legs",physical_host_note="Physical hosts: "+string.Join(", ",peers.Select(p=>p.PhysicalHostId).Distinct())+". Engines on the same host share its hardware.",
            pause_note="CPU process freeze verified by Docker; no claim of GPU kernel quiescence",peers,
            legs=s.Legs.Select(l=>new{l.Mode,l.Completed,l.StartedUtc,l.FinishedUtc,l.ElapsedSeconds,l.ActiveSeconds,paused_seconds=l.TotalPausedSeconds,frames=l.Frames??[]}),
            speedup=fair&&s.Legs[1].ActiveSeconds>0?s.Legs[0].ActiveSeconds/s.Legs[1].ActiveSeconds:(double?)null,
            speedup_note=fair?"Single active wall time / multi active wall time; single leg runs first, cache/thermal order bias remains":"Only shown after both legs finish without pauses and with matching backend",
            source=cfg.Source};
    }
    public async Task<object> LiveSnapshot(){
        try{peers=await ReadPeers();}catch(Exception e){return new {phase="waiting-for-peers",held=true,error=e.Message,peers};}
        return Snapshot();
    }
    private string SelectedBackend()=>peers.SelectMany(p=>p.VerifiedBackends).Where(b=>b!="CPU")
        .GroupBy(b=>b).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault()??"CPU fallback";
    public async Task Start(){
        await serial.WaitAsync();try{
            if(run is {IsCompleted:false})throw new InvalidOperationException("Comparison already running");
            if(state.Phase is not ("idle" or "complete"))throw new InvalidOperationException("Existing run requires reconciliation; do not overlap comparisons");
            state=new(Guid.NewGuid().ToString("N")[..12],"preparing",true,[new("single"),new("multiple")]);Save();
            run=Task.Run(Execute);
        }finally{serial.Release();}
    }
    private async Task<RenderPeerStatus[]> ReadPeers()=>await Task.WhenAll(cfg.Peers.Select(async pair=>
        await http.GetFromJsonAsync<RenderPeerStatus>(pair.Value+"/status",RenderNodeHost.Json)??throw new IOException("No status: "+pair.Key)));
    private async Task SetPeersHold(bool hold){
        var replies=await Task.WhenAll(cfg.Peers.Select(async p=>{
            using var r=await http.PostAsJsonAsync(p.Value+"/hold",new RenderHold(hold),RenderNodeHost.Json);r.EnsureSuccessStatusCode();
            return await r.Content.ReadFromJsonAsync<RenderPeerStatus>(RenderNodeHost.Json)??throw new IOException("Empty pause confirmation");
        }));
        if(hold&&replies.Any(p=>!p.Held||p.Attempts.Any(a=>a.Phase is "running" or "starting" or "resuming" or "pausing")))
            throw new IOException("Pause uncertain: at least one owned live frame did not confirm pause");
    }
    public async Task Hold(bool hold){
        await serial.WaitAsync();try{
            state=state with {Held=true,Error=hold?"Confirming pause on every peer…":null};Save();
            try{await SetPeersHold(hold);}catch(Exception e){state=state with{Error="Hold uncertain: "+e.Message};Save();throw;}
            var now=DateTimeOffset.UtcNow;
            var legs=state.Legs.Select(l=>l.StartedUtc is not null&&l.FinishedUtc is null
                ? hold?l with{PauseStartedUtc=l.PauseStartedUtc??now}:l with{PausedSeconds=l.TotalPausedSeconds,PauseStartedUtc=null}:l).ToArray();
            state=state with {Held=hold,Legs=legs,Error=null};Save();
            if(!hold&&(run is null||run.IsCompleted)&&state.Id!=""&&state.Phase!="complete")run=Task.Run(Execute);
        }finally{serial.Release();}
    }
    private async Task Execute(){
        try{
            peers=await ReadPeers();
            if(peers.Select(p=>p.Image).Distinct().Count()!=1)throw new InvalidDataException("Different Blender image digests; comparison invalid");
            var gpuIds=peers.Where(p=>p.PhysicalGpuId!=null).Select(p=>p.PhysicalGpuId).ToArray();
            if(gpuIds.Length!=gpuIds.Distinct().Count())throw new InvalidDataException("Shared physical GPU cannot be advertised by independent peer domains");
            string selectedBackend=SelectedBackend();if(selectedBackend=="CPU fallback")selectedBackend="CPU";
            string single=peers.Where(p=>p.VerifiedBackends.Contains(selectedBackend)).OrderByDescending(p=>p.HeatDemandKwh)
                .ThenBy(p=>p.PeerId,StringComparer.Ordinal).First().PeerId;
            if(state.Phase=="preparing"){await SetPeersHold(false);state=state with{Held=false};}
            for(int leg=0;leg<2;leg++){
                if(state.Legs[leg].FinishedUtc is not null)continue;
                await serial.WaitAsync();try{
                    var ls=state.Legs.ToArray();ls[leg]=ls[leg] with{StartedUtc=ls[leg].StartedUtc??DateTimeOffset.UtcNow};
                    state=state with{Phase=leg==0?"single":"multiple",Legs=ls};Save();
                }finally{serial.Release();}
                while(true){
                    if(state.Held){await Task.Delay(500);continue;}
                    peers=await ReadPeers();
                    if(peers.Any(p=>p.Error is not null))throw new IOException("Peer error: "+string.Join(";",peers.Where(p=>p.Error!=null).Select(p=>p.PeerId+": "+p.Error)));
                    string prefix=cfg.Id+"."+state.Id+"-"+leg+"-";
                    var done=peers.SelectMany(p=>p.Results).Where(f=>f.JobId.StartsWith(prefix,StringComparison.Ordinal)).OrderBy(f=>f.JobId,StringComparer.Ordinal).ToArray();
                    var attempts=peers.SelectMany(p=>p.Attempts).Where(a=>a.Attempt.Request.Id.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
                    if(attempts.Any(a=>a.Deadline?.Status=="missed"||a.Phase=="released"&&!done.Any(f=>f.JobId==a.Attempt.Request.Id)))
                        throw new IOException("A render frame failed or expired; results are incomplete");
                    await serial.WaitAsync();try{
                        var ls=state.Legs.ToArray();ls[leg]=ls[leg] with{Completed=done.Length,Frames=done};
                        if(done.Length==cfg.Frames)ls[leg]=ls[leg] with{FinishedUtc=DateTimeOffset.UtcNow};
                        state=state with{Legs=ls};Save();
                        if(done.Length==cfg.Frames)break;
                        if(!state.Held){
                            // One unacknowledged submission per poll bounds HTTP/actor pressure; durable IDs make retries idempotent.
                            var known=peers.SelectMany(p=>p.Jobs).Select(j=>j.Request.Id).ToHashSet(StringComparer.Ordinal);
                            for(int frame=1;frame<=cfg.Frames;frame++){
                                string id=state.Id+"-"+leg+"-"+frame.ToString("D3");
                                if(known.Contains(cfg.Id+"."+id))continue;
                                using var r=await http.PostAsJsonAsync(cfg.Peers[cfg.Id]+"/frames",new FrameSubmit(id,frame,leg==0?single+"-worker":null,selectedBackend),RenderNodeHost.Json);
                                r.EnsureSuccessStatusCode();break;
                            }
                        }
                    }finally{serial.Release();}
                    await Task.Delay(1000);
                }
            }
            state=state with{Phase="complete",Held=false};Save();
        }catch(Exception e){state=state with{Phase="failed",Held=true,Error=e.Message};Save();try{await SetPeersHold(true);}catch{}}
    }
}
