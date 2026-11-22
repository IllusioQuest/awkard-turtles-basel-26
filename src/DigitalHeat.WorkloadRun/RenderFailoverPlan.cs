using DigitalHeat.Orchestration;

internal sealed record FrameRetryPlan(Dictionary<int,int> Generations,string[] CancelIds,Dictionary<string,int> Needs,RenderArtifact[] FencedFrames)
{
    public Dictionary<int,JobDeadline> Deadlines { get; init; }=[];
}
internal sealed record NodeShutdown(string Stage,LocalNodeIdentity Identity,RenderPeerStatus LastPeer,FrameRetryPlan? Plan=null);
internal static class RenderFailoverPlan
{
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
