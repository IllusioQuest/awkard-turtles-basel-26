using DigitalHeat.Core;
using DigitalHeat.Network;
using Google.OrTools.LinearSolver;

namespace DigitalHeat.Network.Lp;

/// <summary>Input of one site for one planning window: demand per hour (index 0 = current hour).</summary>
public sealed record LpSiteWindow(NetworkSite Site, IReadOnlyList<double> DemandKwh, double InitialTankKwh);

/// <summary>Plan of one site: server load (servers, not share) per window hour and totals.</summary>
public sealed record LpSitePlan(IReadOnlyList<double> LoadServers, double UsedHeatKwh, double DumpedHeatKwh,
    double ServerHours, double FinalTankKwh);

/// <summary>
/// Heat-led LP over a time window for any number of sites (decision 0014 §3–4, heat-led mode of
/// decision 0024 §6). Per site s and hour t:
///   a·u = direct + charge + dump            (server heat goes somewhere)
///   direct + discharge + backup = D         (demand is met by server heat, tank or backup)
///   S_t = S_(t−1) + charge − discharge,  0 ≤ S_t ≤ capacity   (lossless tank, case)
///   μ·N ≤ u ≤ N
/// Solved lexicographically: (0) minimise dump, (1) maximise used heat (direct + discharge),
/// (2) maximise server-hours. Each later stage keeps the earlier optimum within a small tolerance.
/// Sites are independent until work moves between them (step 2).
/// </summary>
public static class HeatLedLp
{
    // Later stages may give up at most this much of an earlier optimum (numerical slack only).
    private const double RelativeTolerance = 1e-9;
    private const double AbsoluteToleranceKwh = 1e-9;
    private const double ZeroDumpKwh = 1e-7;

    public static IReadOnlyList<LpSitePlan> Solve(IReadOnlyList<LpSiteWindow> windows)
    {
        using var solver = Solver.CreateSolver("GLOP") ?? throw new InvalidOperationException("GLOP not available.");
        var u = new List<Variable[]>(); var dump = new List<Variable[]>(); var used = new List<Variable[]>();
        var tank = new List<Variable[]>();
        foreach (var w in windows)
        {
            var site = w.Site; int t = w.DemandKwh.Count;
            var us = new Variable[t]; var zs = new Variable[t]; var usedS = new Variable[t]; var ss = new Variable[t];
            for (int k = 0; k < t; k++)
            {
                us[k] = solver.MakeNumVar(site.MinimumLoadShare * site.ServerCount, site.ServerCount, "");
                var direct = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var charge = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var discharge = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var backup = solver.MakeNumVar(0, double.PositiveInfinity, "");
                zs[k] = solver.MakeNumVar(0, double.PositiveInfinity, "");
                ss[k] = solver.MakeNumVar(0, site.TankCapacityKwh, "");
                usedS[k] = solver.MakeNumVar(0, double.PositiveInfinity, "");
                solver.Add(site.RecoverableKwhPerServerHour * us[k] == direct + charge + zs[k]);
                solver.Add(direct + discharge + backup == w.DemandKwh[k]);
                solver.Add(usedS[k] == direct + discharge);
                if (k == 0) solver.Add(ss[k] == w.InitialTankKwh + charge - discharge);
                else solver.Add(ss[k] == ss[k - 1] + charge - discharge);
            }
            u.Add(us); dump.Add(zs); used.Add(usedS); tank.Add(ss);
        }

        // Stage 0: minimise dump. Stage 1: maximise used heat. Stage 2: maximise server load.
        double dumpOpt = SolveStage(solver, dump, maximise: false);
        if (dumpOpt <= ZeroDumpKwh)
            foreach (var z in dump.SelectMany(x => x)) z.SetUb(0); // no dump at all: make it exact
        else
            AddSumBound(solver, dump, double.NegativeInfinity, dumpOpt + Tolerance(dumpOpt));
        double usedOpt = SolveStage(solver, used, maximise: true);
        AddSumBound(solver, used, usedOpt - Tolerance(usedOpt), double.PositiveInfinity);
        SolveStage(solver, u, maximise: true);

        return windows.Select((w, i) => new LpSitePlan(
            u[i].Select(v => v.SolutionValue()).ToArray(),
            used[i].Sum(v => v.SolutionValue()), dump[i].Sum(v => v.SolutionValue()),
            u[i].Sum(v => v.SolutionValue()) * w.Site.HoursPerStep,
            tank[i][^1].SolutionValue())).ToArray();
    }

    private static double Tolerance(double value) => Math.Abs(value) * RelativeTolerance + AbsoluteToleranceKwh;

    /// <summary>Adds lower ≤ Σ vars ≤ upper, set coefficient by coefficient (no deep expression trees).</summary>
    private static void AddSumBound(Solver solver, List<Variable[]> vars, double lower, double upper)
    {
        var constraint = solver.MakeConstraint(lower, upper, "");
        foreach (var v in vars.SelectMany(x => x)) constraint.SetCoefficient(v, 1);
    }

    private static double SolveStage(Solver solver, List<Variable[]> vars, bool maximise)
    {
        var objective = solver.Objective();
        objective.Clear();
        foreach (var v in vars.SelectMany(x => x)) objective.SetCoefficient(v, 1);
        if (maximise) objective.SetMaximization(); else objective.SetMinimization();
        var status = solver.Solve();
        if (status != Solver.ResultStatus.OPTIMAL)
            throw new InvalidOperationException($"LP stage not optimal: {status}.");
        return solver.Objective().Value();
    }
}

/// <summary>Full-year LP with perfect foresight: an upper bound only, never a scored result.</summary>
public static class FullYearUpperBound
{
    public static IReadOnlyList<LpSitePlan> Solve(IReadOnlyList<NetworkSite> sites) =>
        HeatLedLp.Solve(sites.Select(s => new LpSiteWindow(s, s.Demand.Select(h => h.TotalHeatKwh).ToArray(), 0))
            .ToArray());
}
