using DigitalHeat.Orchestration;

/// <summary>One render run's heat need on one GPU node: its weighted share of the run's frames (ADR 0069).</summary>
internal sealed record RunHeatNeed(string RunPrefix, int Frames, double FrameHeatKwh);

/// <summary>
/// Render-lab heat need per run (ADR 0069). Node weights describe how much heat each node NEEDS, not an
/// hourly allowance. Each GPU node publishes, through the existing <c>HeatSignal.DemandKwh</c>, its share
/// of the current run plus whatever it already committed this hour, so the unchanged heat-first placement
/// (free-share rank, hard heat cap; ADR 0056) splits the run in proportion to need and the need starts
/// again with every run. No scheduler code is changed.
/// </summary>
internal static class RenderHeatNeed
{
    /// <summary>Head-room so that k frames of x kWh fit a need of k*x kWh despite floating-point sums.</summary>
    internal const double EpsilonKwh = 1e-6;

    /// <summary>Largest-remainder split of a run's frames by node weight; ties go to the smaller node ID.</summary>
    internal static Dictionary<string,int> Allocate(int frames,IReadOnlyCollection<(string Id,double Weight)> nodes)
    {
        if(frames<0||nodes.Count==0||nodes.Any(n=>!double.IsFinite(n.Weight)||n.Weight<0)||nodes.Sum(n=>n.Weight)<=0)
            throw new ArgumentException("Need positive weights and a non-negative frame count");
        double total=nodes.Sum(n=>n.Weight);
        var exact=nodes.Select(n=>(n.Id,Value:frames*n.Weight/total)).ToArray();
        var result=exact.ToDictionary(e=>e.Id,e=>(int)Math.Floor(e.Value+1e-9),StringComparer.Ordinal);
        int left=frames-result.Values.Sum();
        foreach(var e in exact.OrderByDescending(e=>e.Value-Math.Floor(e.Value+1e-9)).ThenBy(e=>e.Id,StringComparer.Ordinal).Take(left))result[e.Id]++;
        return result;
    }

    /// <summary>Job-ID prefix shared by one run's frames: "{origin}.{run id}-".</summary>
    internal static string RunPrefix(string originId,string runId)=>originId+"."+runId+"-";

    /// <summary>
    /// DemandKwh for the hour starting at <paramref name="hourStartUtc"/>: heat already committed this hour by
    /// other work, plus the current run's need not yet covered by frames that finished in earlier hours. Free heat is
    /// then exactly the run's remaining need. Without a registered run the configured demand applies.
    /// </summary>
    internal static double DemandKwh(RunHeatNeed? need,IEnumerable<AttemptRecord> attempts,DateTimeOffset hourStartUtc,double configuredDemandKwh)
    {
        if(need is null)return configuredDemandKwh;
        double otherThisHour=0,currentEarlierHours=0;
        foreach(var a in attempts){
            var hours=a.CommittedHeatIntervalsUtc??[];if(hours.Length==0)continue;
            double heat=a.Attempt.Reservation.RecoverableHeatKwhPerHour;
            bool current=a.Attempt.Request.Id.StartsWith(need.RunPrefix,StringComparison.Ordinal);
            bool thisHour=hours.Contains(hourStartUtc);
            if(!current&&thisHour)otherThisHour+=heat;
            // Only finished frames count as covered: a frame still running or paused across the hour
            // needs heat again in the new hour (and is committed there once the coordinator updates).
            else if(current&&!thisHour&&a.Phase is "released" or "terminal")currentEarlierHours+=heat;
        }
        return otherThisHour+Math.Max(0,need.Frames*need.FrameHeatKwh-currentEarlierHours)+EpsilonKwh;
    }
}
