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
/// deadline first, use their origin's free heat-led capacity first; any rest goes to the eligible site
/// with the most free heat-led capacity (ties: lower site index). Filler fills what is left. Origin
/// first keeps work at home unless a move helps (no arbitrary flows, decision 0025 review).
/// </summary>
public sealed class NetworkGreedyJobsScheduler : INetworkScheduler
{
    public string Id => "B2";
    public string Label => "B2 network greedy: Type 1 EDF at origin, overflow to the eligible site with most free heat-led capacity";

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
        foreach (var c in work.Pending)
        {
            double r = c.RemainingServerHours;
            if (c.EligibleSites[c.OriginSite]) r -= Assign(c, c.OriginSite, r);
            while (r > WorkLedger.MissedEpsilonServerHours)
            {
                int best = -1;
                for (int j = 0; j < n; j++)
                    if (c.EligibleSites[j] && free[j] > WorkLedger.MissedEpsilonServerHours && (best < 0 || free[j] > free[best]))
                        best = j;
                if (best < 0) break;
                r -= Assign(c, best, r);
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
