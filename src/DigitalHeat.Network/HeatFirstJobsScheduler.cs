using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// Explicit heat-first alternative to origin-preferring B2. Eligibility comes from the work adapter;
/// EDF orders jobs, then the common heat rank chooses sites. Existing scored baselines are unchanged.
/// </summary>
public sealed class HeatFirstJobsScheduler : INetworkScheduler
{
    public string Id => "mesh-heat-first";
    public string Label => "EDF work; maximum remaining useful heat among eligible sites; no origin preference";

    public NetworkDecision Decide(NetworkState state)
    {
        var work = GreedyWork.RequireWork(state);
        int count = state.Sites.Count;
        var free = state.Sites.Select((s, i) => GreedyWork.HeatLedTarget(s, state.States[i]) * GreedyWork.Capacity(s)).ToArray();
        var assigned = new double[count];
        var allocations = new List<WorkAllocation>();
        foreach (var job in work.Pending.OrderBy(j => j.DueHour).ThenBy(j => j.Id))
        {
            if (job.DueHour <= state.HourIndex) continue;
            double remaining = job.RemainingServerHours;
            while (remaining > WorkLedger.MissedEpsilonServerHours)
            {
                int best = -1;
                HeatPlacementRank bestRank = default;
                for (int i = 0; i < count; i++)
                {
                    if (!job.EligibleSites[i] || free[i] <= WorkLedger.MissedEpsilonServerHours) continue;
                    var site = state.Sites[i];
                    double usedHeat = assigned[i] * site.RecoverableKwhPerServerHour / site.HoursPerStep;
                    var rank = new HeatPlacementRank(HeatCapacity.RemainingKwh(state.States[i].Signal, usedHeat), site.Id);
                    if (best >= 0 && rank.CompareTo(bestRank) <= 0) continue;
                    best = i; bestRank = rank;
                }
                if (best < 0) break;
                double amount = Math.Min(remaining, free[best]);
                allocations.Add(new(job.Id, best, amount));
                assigned[best] += amount; free[best] -= amount; remaining -= amount;
            }
        }
        var filler = free.Select((amount, i) => Math.Max(0, Math.Min(amount, work.FillerAvailableServerHours[i]))).ToArray();
        var loads = state.Sites.Select((s, i) => new SchedulerDecision(Math.Clamp(
            (assigned[i] + filler[i]) / GreedyWork.Capacity(s), 0, 1))).ToArray();
        return new(loads, allocations, filler);
    }
}
