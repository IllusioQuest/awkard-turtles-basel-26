using System.Globalization;
using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>A task file the live demo observed on disk (decision 0042): waiting in or running at one node.</summary>
public sealed record LiveTask(string Id, int Job, int Node, bool Running, int SpawnStep, int DueStep);

/// <summary>One file move the planner asks for: a waiting task of <paramref name="Job"/> to another node.</summary>
public sealed record LiveMove(string TaskId, int Job, int FromNode, int ToNode);

/// <summary>
/// Demo calibration of one live node (decision 0042). One node = one site with <c>ServersPerNode</c>
/// worker slots (each runs one task at a time) and one scheduler step = one hour (<c>HoursPerStep = 1</c>).
/// No tank (the slider is the heat demand of that hour). Filler (Type 2 research work) is available for
/// <c>FillerAvailability</c> (φ) × all servers of a node, as in the year simulation (decision 0025).
/// </summary>
public sealed record LiveCalibration(double ServerElectricKw, double HeatRecoveryShare, double ServerHoursPerTask,
    int ForecastHorizonSteps, double ElectricityPriceChfPerKwh, double HeatValueChfPerKwh, int ServersPerNode = 1,
    double FillerAvailability = 0)
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
    /// Scheduler view of one step. Heat demand per node = slider share × recoverable heat of all its servers at full load;
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
            double demand = heatShares[i] * cal.RecoverableKwhPerStep * cal.ServersPerNode;
            var hours = Enumerable.Range(0, cal.ForecastHorizonSteps + 1)
                .Select(k => new DemandHour(Label(k), 0, true, demand, 0)).ToArray();
            sites[i] = new NetworkSite(nodeIds[i], nodeIds[i], 0, 0, "CH", cal.ServersPerNode, cal.ServerElectricKw,
                cal.RecoverableKwhPerStep, 0, 0, 1, cal.ElectricityPriceChfPerKwh, cal.HeatValueChfPerKwh, hours);
            var signal = new HeatSignal(demand, 0, 0, cal.ServersPerNode, cal.RecoverableKwhPerStep);
            states[i] = new SiteHeatState(nodeIds[i], signal,
                HeatForecast.Build(hours, $"live-{nodeIds[i]}", 0, cal.ForecastHorizonSteps));
        }
        var filler = Enumerable.Repeat(cal.FillerAvailability * cal.ServersPerNode, n).ToArray();
        var work = new WorkView(BuildCohorts(tasks, n, cal.ServerHoursPerTask), filler);
        return new NetworkState(step, Label(0), sites, states, work);
    }
}

/// <summary>
/// Turns allocations to other nodes into whole-task moves (decision 0042). An allocation is server-hours
/// for one step, a file is a whole task: every allocation of a job to another node moves
/// ⌈server-hours ÷ server-hours per task⌉ waiting tasks there at once (at least one), so a job spreads to
/// all nodes the scheduler picked in the same step. The moved task then belongs to its new node (see
/// <see cref="LiveNetwork.BuildCohorts"/>), which may slightly over-commit it for that step. Only waiting
/// tasks move, the last in the queue first.
/// </summary>
public sealed class LiveTaskPlanner(int nodeCount, double serverHoursPerTask)
{
    private const double MinimumServerHours = 1e-6;

    public IReadOnlyList<LiveMove> Plan(IReadOnlyList<LiveTask> tasks, IReadOnlyList<WorkAllocation> allocations)
    {
        var moves = new List<LiveMove>();
        var waiting = tasks.Where(t => !t.Running).GroupBy(t => (t.Job, t.Node))
            .ToDictionary(g => g.Key, g => new Stack<LiveTask>(g.OrderBy(t => t.Id, StringComparer.Ordinal)));
        var wanted = allocations
            .Select(a => (Cohort: LiveNetwork.DecodeCohort(a.CohortId, nodeCount), a.SiteIndex, a.ServerHours))
            .Where(a => a.SiteIndex != a.Cohort.Node && a.ServerHours > MinimumServerHours)
            .GroupBy(a => (a.Cohort.Job, From: a.Cohort.Node, To: a.SiteIndex))
            .OrderBy(g => g.Key.Job).ThenBy(g => g.Key.From).ThenBy(g => g.Key.To);
        foreach (var g in wanted)
        {
            int count = (int)Math.Ceiling(g.Sum(a => a.ServerHours) / serverHoursPerTask - 1e-9);
            for (int k = 0; k < count && waiting.TryGetValue((g.Key.Job, g.Key.From), out var q) && q.Count > 0; k++)
                moves.Add(new LiveMove(q.Pop().Id, g.Key.Job, g.Key.From, g.Key.To));
        }
        return moves;
    }
}
