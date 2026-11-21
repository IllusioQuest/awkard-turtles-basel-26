using DigitalHeat.Core;
using DigitalHeat.Network;
using Google.OrTools.LinearSolver;

namespace DigitalHeat.Network.Lp;

/// <summary>
/// Rolling-horizon network LP with Type 1 work (decision 0025 §4). Window = current hour + forecast
/// (H hours). Per site s and window hour k:
///   heat:   a·u = direct + charge + dump;  direct + discharge + backup = D;  tank S_k = S_(k−1) + charge − discharge ≤ cap
///   work:   u·Δt = Σ_c x[c,s,k] + filler[s,k],  0 ≤ filler ≤ φ·N·Δt,  μN ≤ u ≤ N
///   Type 1: Σ_(s eligible, k before due) x[c,s,k] + miss_c = remaining_c   (cohorts due inside the window)
///           Σ x[c,s,k] ≤ remaining_c                                       (cohorts due after the window)
/// Lexicographic stages: (0) min dump, (1) max used heat, (2) min missed Type 1 server-hours,
/// (3) max server-hours, (4) min moved server-hours (keeps work at its origin unless a move helps).
/// Only pending (arrived) work is known; future arrivals are not foreseen. Commits the current hour:
/// load per site, Type 1 allocations and filler.
/// </summary>
public sealed class RollingHorizonWorkLpScheduler : INetworkScheduler
{
    private const double RelativeTolerance = 1e-9, AbsoluteTolerance = 1e-9, ZeroDumpKwh = 1e-7;

    public string Id => "LP";
    public string Label => "LP rolling horizon with Type 1 cohorts (heat-led, lexicographic: dump, used heat, missed work, server-hours, moves)";

    public NetworkDecision Decide(NetworkState state)
    {
        var work = state.Work ?? throw new InvalidOperationException("The work LP needs a work model (decision 0025).");
        int n = state.Sites.Count;
        int window = 1 + state.States.Min(s => s.Forecast.Hours.Count);
        using var solver = Solver.CreateSolver("GLOP") ?? throw new InvalidOperationException("GLOP not available.");
        var u = new Variable[n, window];
        var filler = new Variable[n, window];
        var dump = new List<Variable>(); var used = new List<Variable>(); var load = new List<Variable>();
        var workAtSiteHour = new Dictionary<(int s, int k), List<Variable>>();

        for (int s = 0; s < n; s++)
        {
            var site = state.Sites[s];
            var signal = state.States[s].Signal;
            Variable? previousTank = null;
            for (int k = 0; k < window; k++)
            {
                double demand = k == 0 ? signal.DemandKwh : state.States[s].Forecast.Hours[k - 1].NeededHeatKwh;
                u[s, k] = solver.MakeNumVar(site.MinimumLoadShare * site.ServerCount, site.ServerCount, "");
                filler[s, k] = solver.MakeNumVar(0, work.FillerAvailableServerHours[s], "");
                var direct = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var charge = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var discharge = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var backup = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var z = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var tank = solver.MakeNumVar(0, site.TankCapacityKwh, "");
                var usedHeat = solver.MakeNumVar(0, double.PositiveInfinity, "");
                solver.Add(site.RecoverableKwhPerServerHour * u[s, k] == direct + charge + z);
                solver.Add(direct + discharge + backup == demand);
                solver.Add(usedHeat == direct + discharge);
                solver.Add(previousTank is null
                    ? tank == signal.TankStoredKwh + charge - discharge
                    : tank == previousTank + charge - discharge);
                previousTank = tank;
                dump.Add(z); used.Add(usedHeat); load.Add(u[s, k]);
                workAtSiteHour[(s, k)] = [];
            }
        }

        var x = new Dictionary<(int c, int s, int k), Variable>();
        var missed = new List<Variable>();
        var moved = new List<Variable>();
        for (int ci = 0; ci < work.Pending.Count; ci++)
        {
            var c = work.Pending[ci];
            int lastHour = Math.Min(window, c.DueHour - state.HourIndex); // hours k < lastHour are before the due hour
            var cohortConstraint = c.DueHour - state.HourIndex <= window
                ? solver.MakeConstraint(c.RemainingServerHours, c.RemainingServerHours, "")
                : solver.MakeConstraint(0, c.RemainingServerHours, "");
            if (c.DueHour - state.HourIndex <= window)
            {
                var m = solver.MakeNumVar(0, c.RemainingServerHours, "");
                cohortConstraint.SetCoefficient(m, 1);
                missed.Add(m);
            }
            for (int s = 0; s < n; s++)
            {
                if (!c.EligibleSites[s]) continue;
                for (int k = 0; k < lastHour; k++)
                {
                    var v = solver.MakeNumVar(0, double.PositiveInfinity, "");
                    x[(ci, s, k)] = v;
                    cohortConstraint.SetCoefficient(v, 1);
                    workAtSiteHour[(s, k)].Add(v);
                    if (s != c.OriginSite) moved.Add(v);
                }
            }
        }
        // Load equals the work done: Type 1 plus filler (as the ledger caps load at available work).
        for (int s = 0; s < n; s++)
            for (int k = 0; k < window; k++)
            {
                var balance = solver.MakeConstraint(0, 0, "");
                balance.SetCoefficient(u[s, k], state.Sites[s].HoursPerStep);
                balance.SetCoefficient(filler[s, k], -1);
                foreach (var v in workAtSiteHour[(s, k)]) balance.SetCoefficient(v, -1);
            }

        double dumpOpt = Stage(solver, dump, maximise: false);
        if (dumpOpt <= ZeroDumpKwh) foreach (var z in dump) z.SetUb(0);
        else Bound(solver, dump, double.NegativeInfinity, dumpOpt + Tol(dumpOpt));
        double usedOpt = Stage(solver, used, maximise: true);
        Bound(solver, used, usedOpt - Tol(usedOpt), double.PositiveInfinity);
        if (missed.Count > 0)
        {
            double missOpt = Stage(solver, missed, maximise: false);
            Bound(solver, missed, double.NegativeInfinity, missOpt + Tol(missOpt));
        }
        double loadOpt = Stage(solver, load, maximise: true);
        // Add a bound only if another stage is solved after it: a changed model invalidates the solution values.
        if (moved.Count > 0)
        {
            Bound(solver, load, loadOpt - Tol(loadOpt), double.PositiveInfinity);
            Stage(solver, moved, maximise: false);
        }

        var decisions = new SchedulerDecision[n];
        var fillerNow = new double[n];
        for (int s = 0; s < n; s++)
        {
            decisions[s] = new SchedulerDecision(Math.Clamp(u[s, 0].SolutionValue() / state.Sites[s].ServerCount, 0, 1));
            fillerNow[s] = Math.Max(0, filler[s, 0].SolutionValue());
        }
        var allocations = x.Where(e => e.Key.k == 0 && e.Value.SolutionValue() > 1e-9)
            .Select(e => new WorkAllocation(work.Pending[e.Key.c].Id, e.Key.s, e.Value.SolutionValue()))
            .ToArray();
        return new NetworkDecision(decisions, allocations, fillerNow);
    }

    private static double Tol(double v) => Math.Abs(v) * RelativeTolerance + AbsoluteTolerance;

    private static void Bound(Solver solver, List<Variable> vars, double lower, double upper)
    {
        var c = solver.MakeConstraint(lower, upper, "");
        foreach (var v in vars) c.SetCoefficient(v, 1);
    }

    private static double Stage(Solver solver, List<Variable> vars, bool maximise)
    {
        var objective = solver.Objective();
        objective.Clear();
        foreach (var v in vars) objective.SetCoefficient(v, 1);
        if (maximise) objective.SetMaximization(); else objective.SetMinimization();
        var status = solver.Solve();
        if (status != Solver.ResultStatus.OPTIMAL)
            throw new InvalidOperationException($"Work LP stage not optimal: {status}.");
        return objective.Value();
    }
}
