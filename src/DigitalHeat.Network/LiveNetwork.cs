using System.Globalization;
using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>A task file the live demo observed on disk (decision 0042): waiting in or running at one node.</summary>
public sealed record LiveTask(string Id, int Job, int Node, bool Running, int SpawnStep, int DueStep);

/// <summary>One file move the planner asks for: a waiting task of <paramref name="Job"/> to another node.</summary>
public sealed record LiveMove(string TaskId, int Job, int FromNode, int ToNode);

/// <summary>
/// Demo calibration of one live node (decision 0042). One node = one site with one worker that runs one
/// task at a time, so <c>ServerCount = 1</c> and one scheduler step = one hour (<c>HoursPerStep = 1</c>).
/// No tank (the slider is the heat demand of that hour) and no filler (φ = 0: a node without tasks pauses).
/// </summary>
public sealed record LiveCalibration(double ServerElectricKw, double HeatRecoveryShare, double ServerHoursPerTask,
    int ForecastHorizonSteps, double ElectricityPriceChfPerKwh, double HeatValueChfPerKwh)
{
    public double RecoverableKwhPerStep => ServerElectricKw * HeatRecoveryShare;
}

/// <summary>
/// Pure glue between observed task files and the unchanged network schedulers (decision 0042). It builds
/// the <see cref="NetworkState"/> a scheduler sees and turns its allocations into file moves.
/// </summary>
public static class LiveNetwork
{
    private static readonly DateTime FirstStepLocal = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Cohort id of the tasks of <paramref name="job"/> that currently sit at <paramref name="node"/>.</summary>
    public static int CohortId(int job, int node, int nodeCount) => job * nodeCount + node;

    public static (int Job, int Node) DecodeCohort(int cohortId, int nodeCount) =>
        (cohortId / nodeCount, cohortId % nodeCount);

    /// <summary>
    /// Cohorts per (job, current node), origin = the node the files are on now, so moved work belongs to
    /// its new node from the next step on (the one difference to the year simulation, where a cohort keeps
    /// its origin). A running task counts as one full task (its progress is not observed). Earliest deadline first.
    /// </summary>
    public static IReadOnlyList<Type1Cohort> BuildCohorts(IReadOnlyList<LiveTask> tasks, int nodeCount,
        double serverHoursPerTask)
    {
        var eligible = Enumerable.Repeat(true, nodeCount).ToArray();
        return tasks.GroupBy(t => (t.Job, t.Node))
            .Select(g => Type1Cohort.Observed(CohortId(g.Key.Job, g.Key.Node, nodeCount), g.Key.Node,
                g.Min(t => t.SpawnStep), g.Min(t => t.DueStep), g.Count(), serverHoursPerTask, eligible,
                g.Count() * serverHoursPerTask))
            .OrderBy(c => c.DueHour).ThenBy(c => c.Id).ToList();
    }

    /// <summary>
    /// Scheduler view of one step. Heat demand per node = slider share × recoverable heat at full load;
    /// the forecast is persistence (the current demand repeated), as the demo cannot know the future.
    /// </summary>
    public static NetworkState BuildState(int step, IReadOnlyList<string> nodeIds, IReadOnlyList<double> heatShares,
        IReadOnlyList<LiveTask> tasks, LiveCalibration cal)
    {
        int n = nodeIds.Count;
        if (heatShares.Count != n || heatShares.Any(s => !double.IsFinite(s) || s is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(heatShares));
        var sites = new NetworkSite[n];
        var states = new SiteHeatState[n];
        string Label(int k) => FirstStepLocal.AddHours(step + k).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        for (int i = 0; i < n; i++)
        {
            double demand = heatShares[i] * cal.RecoverableKwhPerStep;
            var hours = Enumerable.Range(0, cal.ForecastHorizonSteps + 1)
                .Select(k => new DemandHour(Label(k), 0, true, demand, 0)).ToArray();
            sites[i] = new NetworkSite(nodeIds[i], nodeIds[i], 0, 0, "CH", 1, cal.ServerElectricKw,
                cal.RecoverableKwhPerStep, 0, 0, 1, cal.ElectricityPriceChfPerKwh, cal.HeatValueChfPerKwh, hours);
            var signal = new HeatSignal(demand, 0, 0, 1, cal.RecoverableKwhPerStep);
            states[i] = new SiteHeatState(nodeIds[i], signal,
                HeatForecast.Build(hours, $"live-{nodeIds[i]}", 0, cal.ForecastHorizonSteps));
        }
        var work = new WorkView(BuildCohorts(tasks, n, cal.ServerHoursPerTask), new double[n]);
        return new NetworkState(step, Label(0), sites, states, work);
    }
}

/// <summary>
/// Turns allocations to other nodes into whole-task moves (decision 0042). An allocation is server-hours
/// for one step, a file is a whole task, so allocated server-hours accumulate per (job, from, to) while
/// the scheduler keeps allocating that pair in consecutive steps; a task moves once a whole task's worth
/// has accumulated. A pair the scheduler stops allocating loses its credit (the cause is gone).
/// Only waiting tasks move, the last in the queue first. One instance per demo run.
/// </summary>
public sealed class LiveTaskPlanner(int nodeCount, double serverHoursPerTask)
{
    private const double Epsilon = 1e-9;
    private Dictionary<(int Job, int From, int To), double> credit = [];

    public IReadOnlyList<LiveMove> Plan(IReadOnlyList<LiveTask> tasks, IReadOnlyList<WorkAllocation> allocations)
    {
        var next = new Dictionary<(int, int, int), double>();
        foreach (var a in allocations)
        {
            var (job, from) = LiveNetwork.DecodeCohort(a.CohortId, nodeCount);
            if (a.SiteIndex == from || a.ServerHours <= Epsilon) continue;
            var key = (job, from, a.SiteIndex);
            next[key] = next.GetValueOrDefault(key, credit.GetValueOrDefault(key)) + a.ServerHours;
        }
        var moves = new List<LiveMove>();
        var waiting = tasks.Where(t => !t.Running).GroupBy(t => (t.Job, t.Node))
            .ToDictionary(g => g.Key, g => new Stack<LiveTask>(g.OrderBy(t => t.Id, StringComparer.Ordinal)));
        foreach (var key in next.Keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2).ThenBy(k => k.Item3).ToList())
        {
            var (job, from, to) = key;
            while (next[key] + Epsilon >= serverHoursPerTask
                   && waiting.TryGetValue((job, from), out var queue) && queue.Count > 0)
            {
                var t = queue.Pop();
                moves.Add(new LiveMove(t.Id, job, from, to));
                next[key] -= serverHoursPerTask;
            }
            if (!waiting.TryGetValue((job, from), out var left) || left.Count == 0) next[key] = 0;
        }
        credit = next;
        return moves;
    }
}
