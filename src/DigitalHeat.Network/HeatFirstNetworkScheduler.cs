using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>
/// Hourly cohort adapter for the shared heat-first policy. Eligibility and work accounting remain
/// with the work ledger; origin never outranks useful heat. Also supports unlimited work.
/// </summary>
public sealed class HeatFirstNetworkScheduler(int transferDelayHours = 0) : INetworkScheduler
{
    public string Id => "mesh-heat-first";
    public string Label => "EDF work; maximum remaining useful heat among eligible sites; no origin preference";

    public NetworkDecision Decide(NetworkState state)
    {
        if (transferDelayHours < 0) throw new ArgumentOutOfRangeException(nameof(transferDelayHours));
        int count = state.Sites.Count;
        var work = state.Work ?? new WorkView([], state.Sites.Select(NetworkWorkCapacity.Capacity).ToArray());
        var free = state.Sites.Select((s, i) => NetworkWorkCapacity.HeatLedTarget(s, state.States[i]) * NetworkWorkCapacity.Capacity(s)).ToArray();
        var assigned = new double[count];
        var allocations = new List<WorkAllocation>();
        foreach (var job in work.Pending.OrderBy(j => new JobPriority(j.DueHour, j.Id.ToString("D10"))))
        {
            if (job.DueHour <= state.HourIndex) continue;
            double remaining = job.RemainingServerHours;
            while (remaining > WorkLedger.MissedEpsilonServerHours)
            {
                int best = -1;
                HeatPlacementRank bestRank = default;
                for (int i = 0; i < count; i++)
                {
                    if (!job.EligibleSites[i] || free[i] <= WorkLedger.MissedEpsilonServerHours
                        || i != job.OriginSite && state.HourIndex < job.ArrivalHour + transferDelayHours) continue;
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
            (assigned[i] + filler[i]) / NetworkWorkCapacity.Capacity(s), 0, 1))).ToArray();
        return new(loads, allocations, filler);
    }
}
