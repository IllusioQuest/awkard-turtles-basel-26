using DigitalHeat.Orchestration;

internal sealed record FrameRetryPlan(Dictionary<int,int> Generations,string[] CancelIds,Dictionary<string,int> Needs,RenderArtifact[] FencedFrames)
{
    public Dictionary<int,JobDeadline> Deadlines { get; init; }=[];
}
internal sealed record NodeShutdown(string Stage,LocalNodeIdentity Identity,RenderPeerStatus LastPeer,FrameRetryPlan? Plan=null)
{
    /// <summary>Process started by a power-on (ADR 0073), saved before the launcher manifest is updated.</summary>
    public RestartedProcess? Restarted { get; init; }
}
internal static class RenderFailoverPlan
{
    /// <summary>
    /// Heat need per render worker when a powered-off worker rejoins a running render (ADR 0073): frames that are
    /// neither finished nor running are split over all workers by weight; finished and running frames count for the
    /// worker that has them. Run-prefixed attempts released without a result (e.g. killed at power-off) are listed as
    /// superseded so their committed heat counts as other work.
    /// </summary>
    internal static Dictionary<string,RunHeatNeed> RejoinNeeds(int frames,IEnumerable<string> currentIds,RenderPeerStatus[] peers,
        RenderArtifact[] fenced,string prefix,double frameHeatKwh)
    {
        var current=currentIds.ToHashSet(StringComparer.Ordinal);
        var done=peers.SelectMany(p=>p.Results).Concat(fenced).Where(f=>current.Contains(f.JobId)).DistinctBy(f=>f.JobId).ToArray();
        var doneIds=done.Select(f=>f.JobId).ToHashSet(StringComparer.Ordinal);
        var running=peers.SelectMany(p=>p.Attempts.Where(a=>a.Phase!="released"&&current.Contains(a.Attempt.Request.Id)&&!doneIds.Contains(a.Attempt.Request.Id))
            .Select(a=>(Peer:p.PeerId,Id:a.Attempt.Request.Id))).DistinctBy(a=>a.Id).ToArray();
        var workers=peers.Where(p=>p.WorkerRole=="render").ToArray();
        int open=frames-done.Length-running.Length;
        if(open<0||workers.Length==0)throw new InvalidDataException("Cannot conserve frame work across workers");
        var shares=RenderHeatNeed.Allocate(open,workers.Select(p=>(p.PeerId,p.WorkWeight)).ToArray());
        foreach(var w in workers)shares[w.PeerId]+=done.Count(f=>f.PeerId==w.PeerId)+running.Count(a=>a.Peer==w.PeerId);
        if(shares.Values.Sum()!=frames)throw new InvalidDataException("Frame redistribution does not conserve work");
        return workers.ToDictionary(w=>w.PeerId,w=>new RunHeatNeed(prefix,shares[w.PeerId],frameHeatKwh){
            SupersededIds=w.Attempts.Where(a=>a.Phase=="released"&&a.Attempt.Request.Id.StartsWith(prefix,StringComparison.Ordinal)&&!doneIds.Contains(a.Attempt.Request.Id))
                .Select(a=>a.Attempt.Request.Id).Distinct(StringComparer.Ordinal).ToArray()},StringComparer.Ordinal);
    }
    internal static string LocalJob(string run,int frame,int generation)=>run+(generation==0?"":"-retry"+generation)+"-"+frame.ToString("D3");
    internal static FrameRetryPlan Create(string origin,string run,int frames,Dictionary<int,int> generations,
        RenderPeerStatus[] active,OriginJob[] jobs,RenderArtifact[] fenced,string failed,IReadOnlySet<string> fencedAttemptIds)
    {
        var ids=Enumerable.Range(1,frames).ToDictionary(f=>origin+"."+LocalJob(run,f,generations.GetValueOrDefault(f)),f=>f,StringComparer.Ordinal);
        var done=active.SelectMany(p=>p.Results).Concat(fenced).Where(r=>ids.ContainsKey(r.JobId)).DistinctBy(r=>r.JobId).ToArray();
        var doneFrames=done.Select(r=>ids[r.JobId]).ToHashSet();var next=new Dictionary<int,int>(generations);var cancel=new List<string>();var deadlines=new Dictionary<int,JobDeadline>();
        foreach(var pair in ids){
            if(doneFrames.Contains(pair.Value))continue;
            if(fencedAttemptIds.Contains(pair.Key)||jobs.Any(j=>j.Request.Id==pair.Key&&j.Target==failed)){
                // A retry keeps the logical frame's original queue-inclusive deadline.
                deadlines[pair.Value]=jobs.Single(j=>j.Request.Id==pair.Key).Request.Deadline
                    ??throw new InvalidDataException("Missing original frame deadline");
                next[pair.Value]=generations.GetValueOrDefault(pair.Value)+1;cancel.Add(pair.Key);
            }
        }
        var inflight=active.SelectMany(p=>p.Attempts.Select(a=>(Peer:p.PeerId,Record:a)))
            .Where(a=>a.Record.Phase!="released"&&ids.ContainsKey(a.Record.Attempt.Request.Id)&&!doneFrames.Contains(ids[a.Record.Attempt.Request.Id]))
            .DistinctBy(a=>a.Record.Attempt.Request.Id).ToArray();
        var workers=active.Where(p=>p.WorkerRole=="render").ToArray();
        int remaining=frames-doneFrames.Count-inflight.Length;
        if(remaining<0||workers.Length==0)throw new InvalidDataException("Cannot conserve frame work across remaining workers");
        var shares=RenderHeatNeed.Allocate(remaining,workers.Select(p=>(p.PeerId,p.WorkWeight)).ToArray());
        foreach(var w in workers)shares[w.PeerId]+=done.Count(r=>r.PeerId==w.PeerId)+inflight.Count(a=>a.Peer==w.PeerId);
        if(shares.Values.Sum()+fenced.Length!=frames)throw new InvalidDataException("Frame redistribution does not conserve work");
        return new(next,cancel.ToArray(),shares,fenced){Deadlines=deadlines};
    }
}
