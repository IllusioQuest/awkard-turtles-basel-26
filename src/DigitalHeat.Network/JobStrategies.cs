using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>Helpers shared by the work-aware greedy strategies (decision 0025 §4).</summary>
internal static class GreedyWork
{
    /// <summary>Heat-led target load per server of the existing greedy rule at one site.</summary>
    public static double HeatLedTarget(NetworkSite site, SiteHeatState state) =>
        new GreedyHeatScheduler(site.MinimumLoadShare).Decide(state.Signal).TargetLoadPerServer;

    public static double Capacity(NetworkSite site) => site.ServerCount * site.HoursPerStep;

    public static WorkView RequireWork(NetworkState state) =>
        state.Work ?? throw new InvalidOperationException("This strategy needs a work model (decision 0025).");
}

/// <summary>
/// B1-jobs: every site runs the local greedy load, capped at the work it has (as Core's
/// JobQueueExecutor caps it). Its own eligible Type 1 cohorts go first, earliest deadline first, then
/// filler. No work moves. With one site and no filler it matches YearSimulation in finite "wait" mode.
/// </summary>
public sealed class LocalGreedyJobsScheduler : INetworkScheduler
{
    public string Id => "B1";
    public string Label => "B1-jobs local greedy, own Type 1 work EDF, then filler; no moves";

    public NetworkDecision Decide(NetworkState state)
    {
        var work = GreedyWork.RequireWork(state);
        var decisions = new SchedulerDecision[state.Sites.Count];
        var filler = new double[state.Sites.Count];
        var allocations = new List<WorkAllocation>();
        for (int i = 0; i < state.Sites.Count; i++)
        {
            var site = state.Sites[i];
            double capacity = GreedyWork.Capacity(site);
            var own = work.Pending.Where(c => c.OriginSite == i && c.EligibleSites[i]).ToArray();
            double available = own.Sum(c => c.RemainingServerHours) + work.FillerAvailableServerHours[i];
            // Same expression as JobQueueExecutor.ApplyTargetLoad, so one site reproduces it exactly.
            double target = Math.Min(GreedyWork.HeatLedTarget(site, state.States[i]), available / capacity);
            double left = target * capacity;
            foreach (var c in own)
            {
                if (left <= 0) break;
                double a = Math.Min(c.RemainingServerHours, left);
                allocations.Add(new WorkAllocation(c.Id, i, a));
                left -= a;
            }
            filler[i] = Math.Max(0, Math.Min(left, work.FillerAvailableServerHours[i]));
            decisions[i] = new SchedulerDecision(target);
        }
        return new NetworkDecision(decisions, allocations, filler);
    }
}

/// <summary>
/// B2 network greedy: each site's heat-led capacity is the local greedy load. Type 1 cohorts, earliest
/// deadline first, use their origin's free heat-led capacity first. Work moves only if the origin cannot
/// finish it in time: an EDF feasibility test per origin compares the cumulative pending work up to each
/// cohort's due hour with the origin's capacity until then, estimated by persistence (this hour's
/// heat-led server-hours × hours left; no forecast). Only that excess goes to the eligible site with the
/// most free heat-led capacity (ties: lower site index). Filler fills what is left. So work stays at
/// home unless a move helps (no arbitrary flows, decision 0025 review).
/// </summary>
public sealed class NetworkGreedyJobsScheduler : INetworkScheduler
{
    public string Id => "B2";
    public string Label => "B2 network greedy: Type 1 EDF at origin; moves only work the origin cannot finish by its deadline (persistence estimate) to the eligible site with most free heat-led capacity";

    public NetworkDecision Decide(NetworkState state)
    {
        var work = GreedyWork.RequireWork(state);
        int n = state.Sites.Count;
        var heatLed = new double[n];
        var free = new double[n];
        var assigned = new double[n];
        for (int i = 0; i < n; i++)
            free[i] = heatLed[i] = GreedyWork.HeatLedTarget(state.Sites[i], state.States[i]) * GreedyWork.Capacity(state.Sites[i]);
        var allocations = new List<WorkAllocation>();
        var cumulative = new double[n];   // pending own work per origin up to the current cohort's due hour
        var movedFrom = new double[n];    // work already sent away from each origin this hour
        foreach (var c in work.Pending)
        {
            double r = c.RemainingServerHours;
            int o = c.OriginSite;
            bool home = c.EligibleSites[o];
            if (home) cumulative[o] += r;
            // EDF feasibility at the origin: what cannot be done at home before the due hour must move.
            double homeCapacityUntilDue = home ? heatLed[o] * (c.DueHour - state.HourIndex) : 0;
            double mustMove = Math.Min(r, Math.Max(0, cumulative[o] - homeCapacityUntilDue - movedFrom[o]));
            double stay = r - mustMove;
            if (home) stay -= Assign(c, o, stay);
            // Work that must leave is still done at home now if the origin has capacity left this hour.
            double toMove = mustMove - (home ? Assign(c, o, mustMove) : 0);
            while (toMove > WorkLedger.MissedEpsilonServerHours)
            {
                int best = -1;
                for (int j = 0; j < n; j++)
                    if (j != o && c.EligibleSites[j] && free[j] > WorkLedger.MissedEpsilonServerHours
                        && (best < 0 || free[j] > free[best]))
                        best = j;
                if (best < 0) break;
                double moved = Assign(c, best, toMove);
                toMove -= moved;
                movedFrom[o] += moved;
            }
        }
        var filler = new double[n];
        var decisions = new SchedulerDecision[n];
        for (int i = 0; i < n; i++)
        {
            double capacity = GreedyWork.Capacity(state.Sites[i]);
            filler[i] = Math.Max(0, Math.Min(free[i], work.FillerAvailableServerHours[i]));
            double target = Math.Min(heatLed[i], assigned[i] + filler[i]) / capacity;
            decisions[i] = new SchedulerDecision(Math.Clamp(target, 0, 1));
        }
        return new NetworkDecision(decisions, allocations, filler);

        double Assign(Type1Cohort c, int site, double wanted)
        {
            double a = Math.Min(wanted, free[site]);
            if (a <= 0) return 0;
            free[site] -= a;
            assigned[site] += a;
            allocations.Add(new WorkAllocation(c.Id, site, a));
            return a;
        }
    }
}
