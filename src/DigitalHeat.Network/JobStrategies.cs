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
/// Heat-led capacity reserved for the next Type 1 cohort (decision 0029). Filler may not fill the tank
/// beyond a reserve kept free for it. Estimates use persistence only (no scenario knowledge, no forecast):
/// expected next cohort = server-hours of the site's own cohorts that arrived in the last 24 h; expected
/// heat-led capacity during that cohort's deadline window = mean heat demand of the last 24 observed hours
/// ÷ recoverable heat per server-hour × deadline hours. Reserve (kWh) = min(tank capacity, recoverable kWh
/// per server-hour × max(0, expected cohort − expected capacity)). One instance per simulation run: it keeps
/// the last 24 hourly demands per site and must be called once per hour in time order.
/// </summary>
internal sealed class Type1HeatReserve
{
    private const int PersistenceHours = 24;
    private Queue<double>[]? recentDemand;
    // Cohorts seen per origin site (id → arrival hour, total server-hours, deadline hours). Completed cohorts
    // leave the ledger's pending list, so the reserve keeps its own memory of the last 24 h of arrivals.
    private readonly Dictionary<int, (int Site, int Arrival, double ServerHours, int DeadlineHours)> seen = [];

    public void Observe(NetworkState state)
    {
        recentDemand ??= state.Sites.Select(_ => new Queue<double>()).ToArray();
        for (int i = 0; i < state.Sites.Count; i++)
        {
            var q = recentDemand[i];
            q.Enqueue(state.States[i].Signal.DemandKwh);
            while (q.Count > PersistenceHours) q.Dequeue();
        }
        foreach (var c in state.Work!.Pending)
            seen.TryAdd(c.Id, (c.OriginSite, c.ArrivalHour, c.TotalServerHours, c.DueHour - c.ArrivalHour));
        foreach (var id in seen.Where(e => e.Value.Arrival <= state.HourIndex - PersistenceHours).Select(e => e.Key).ToList())
            seen.Remove(id);
    }

    public double ReserveKwh(NetworkState state, int site)
    {
        var s = state.Sites[site];
        var own = seen.Values.Where(c => c.Site == site).ToArray();
        if (own.Length == 0 || s.TankCapacityKwh <= 0) return 0;
        double expectedCohort = own.Sum(c => c.ServerHours);
        double deadlineHours = own.Max(c => c.DeadlineHours);
        double meanDemandKwh = recentDemand![site].Average();
        double expectedCapacity = meanDemandKwh / s.RecoverableKwhPerServerHour * deadlineHours;
        return Math.Min(s.TankCapacityKwh, s.RecoverableKwhPerServerHour * Math.Max(0, expectedCohort - expectedCapacity));
    }

    /// <summary>Most filler server-hours this hour that keep the reserve free after the hour.</summary>
    public double FillerCap(NetworkState state, int site, double type1ServerHours)
    {
        var s = state.Sites[site];
        var signal = state.States[site].Signal;
        double headroomKwh = s.TankCapacityKwh - ReserveKwh(state, site) - signal.TankStoredKwh + signal.DemandKwh;
        return Math.Max(0, headroomKwh / s.RecoverableKwhPerServerHour - type1ServerHours);
    }
}

/// <summary>
/// B1-jobs: every site runs the local greedy load, capped at the work it has (as Core's
/// JobQueueExecutor caps it). Its own eligible Type 1 cohorts go first, earliest deadline first, then
/// filler. No work moves. With one site and no filler it matches YearSimulation in finite "wait" mode.
/// </summary>
public sealed class LocalGreedyJobsScheduler(bool reserveForType1 = false) : INetworkScheduler
{
    private readonly Type1HeatReserve? reserve = reserveForType1 ? new Type1HeatReserve() : null;

    public string Id => reserve is null ? "B1" : "B1-reserve";
    public string Label => reserve is null
        ? "B1-jobs local greedy, own Type 1 work EDF, then filler; no moves"
        : "B1-reserve: B1-jobs, but filler keeps tank room free for the next Type 1 cohort (persistence, decision 0029); no moves, no forecast";

    public NetworkDecision Decide(NetworkState state)
    {
        var work = GreedyWork.RequireWork(state);
        reserve?.Observe(state);
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
            if (reserve is not null)
            {
                double type1 = target * capacity - left;
                filler[i] = Math.Min(filler[i], reserve.FillerCap(state, i, type1));
                target = (type1 + filler[i]) / capacity;
            }
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
public sealed class NetworkGreedyJobsScheduler(bool reserveForType1 = false, int transferDelayHours = 0) : INetworkScheduler
{
    private readonly Type1HeatReserve? reserve = reserveForType1 ? new Type1HeatReserve() : null;

    public string Id => reserve is null ? "B2" : "B2-reserve";
    public string Label => (reserve is null ? "B2" : "B2-reserve") +
        " network greedy: Type 1 EDF at origin; moves only work the origin cannot finish by its deadline (persistence estimate) to the eligible site with most free heat-led capacity" +
        (reserve is null ? "" : "; filler keeps tank room free for the next Type 1 cohort (decision 0029)");

    public NetworkDecision Decide(NetworkState state)
    {
        var work = GreedyWork.RequireWork(state);
        reserve?.Observe(state);
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
            // Transfer delay (decision 0030): data staged at arrival may run elsewhere only from arrival + d.
            if (state.HourIndex < c.ArrivalHour + transferDelayHours)
            {
                movedFrom[o] += toMove; // blocked excess leaves the home queue in the EDF test (no double count)
                toMove = 0;
            }
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
            if (reserve is not null) filler[i] = Math.Min(filler[i], reserve.FillerCap(state, i, assigned[i]));
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
