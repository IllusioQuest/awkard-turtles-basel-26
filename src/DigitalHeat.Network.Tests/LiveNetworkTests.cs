using DigitalHeat.Network;
using DigitalHeat.Network.Lp;
using Xunit;

namespace DigitalHeat.Network.Tests;

/// <summary>Live demo glue (decision 0042): observed files → NetworkState → unchanged scheduler → file moves.</summary>
public class LiveNetworkTests
{
    private static readonly string[] Nodes = ["n1", "n2", "n3", "n4"];
    private static readonly LiveCalibration Cal = new(1, 0.8, 1, 6, 0.3325, 0.133);

    private static List<LiveTask> Job(int job, int node, int count, int spawn, int due) =>
        Enumerable.Range(0, count).Select(k => new LiveTask($"j{job:0000}-t{k:00}", job, node, false, spawn, due)).ToList();

    /// <summary>Runs one step: B2 decides, the planner moves files, and tasks change node like the files would.</summary>
    private static (NetworkDecision Decision, IReadOnlyList<LiveMove> Moves) Step(INetworkScheduler s, LiveTaskPlanner p,
        int step, double[] heat, List<LiveTask> tasks)
    {
        var decision = s.Decide(LiveNetwork.BuildState(step, Nodes, heat, tasks, Cal));
        var moves = p.Plan(tasks, decision.Allocations ?? []);
        foreach (var m in moves)
        {
            int i = tasks.FindIndex(t => t.Id == m.TaskId);
            tasks[i] = tasks[i] with { Node = m.ToNode };
        }
        return (decision, moves);
    }

    [Fact]
    public void LoadFollowsHeatAndIdleNodesPause()
    {
        var tasks = Job(1, 0, 2, 0, 10);
        var (d, moves) = Step(new NetworkGreedyJobsScheduler(), new LiveTaskPlanner(4, 1), 0, [0.5, 1, 1, 0], tasks);
        Assert.Equal(0.5, d.PerSite[0].TargetLoadPerServer, 9);  // heat-led, work available
        Assert.Equal(0, d.PerSite[1].TargetLoadPerServer, 9);    // heat wanted but no work: φ = 0, pause
        Assert.Empty(moves);                                     // 2 tasks ≤ 0.5 × 10 steps: feasible at home
    }

    [Fact]
    public void InfeasibleWorkMovesToTheNodeWithMostFreeHeatAndDoesNotComeBack()
    {
        var b2 = new NetworkGreedyJobsScheduler();
        var planner = new LiveTaskPlanner(4, 1);
        var tasks = Job(7, 0, 3, 0, 4);                          // node 1 is cold: no heat, no progress
        double[] heat = [0, 1, 0.4, 0];
        var (_, first) = Step(b2, planner, 0, heat, tasks);
        Assert.Equal([new LiveMove("j0007-t02", 7, 0, 1)], first);  // last task moves first, to the warmest node
        var all = new List<LiveMove>(first);
        for (int step = 1; step < 3; step++) all.AddRange(Step(b2, planner, step, heat, tasks).Moves);
        Assert.All(all, m => Assert.Equal(0, m.FromNode));      // nothing moves back or onwards without a cause
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void FractionalAllocationsAccumulateIntoWholeTasks()
    {
        var b2 = new NetworkGreedyJobsScheduler();
        var planner = new LiveTaskPlanner(4, 1);
        var tasks = Job(2, 0, 2, 0, 6);
        double[] heat = [0, 0.4, 0, 0];                         // only 0.4 server-hours per step elsewhere
        Assert.Empty(Step(b2, planner, 0, heat, tasks).Moves);
        Assert.Empty(Step(b2, planner, 1, heat, tasks).Moves);
        Assert.Single(Step(b2, planner, 2, heat, tasks).Moves); // 0.4 + 0.4 + 0.4 ≥ 1 task
    }

    [Fact]
    public void RunningTasksNeverMove()
    {
        var tasks = Job(3, 0, 1, 0, 2);
        tasks[0] = tasks[0] with { Running = true };
        var (d, moves) = Step(new NetworkGreedyJobsScheduler(), new LiveTaskPlanner(4, 1), 0, [0, 1, 1, 1], tasks);
        Assert.Contains(d.Allocations!, a => a.SiteIndex != 0);
        Assert.Empty(moves);
    }

    [Fact]
    public void LpRunsOnTheLiveStateAndMovesInfeasibleWork()
    {
        var tasks = Job(5, 0, 2, 0, 3);
        var (d, moves) = Step(new RollingHorizonWorkLpScheduler(), new LiveTaskPlanner(4, 1), 0, [0, 1, 1, 0], tasks);
        Assert.Equal(4, d.PerSite.Count);
        Assert.NotEmpty(moves);                                   // the LP may spread moves over its horizon
        Assert.All(moves, m => Assert.NotEqual(0, m.ToNode));
    }
}
