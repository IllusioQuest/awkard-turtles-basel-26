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
    public void InfeasibleWorkSpreadsToEveryNodeWithFreeHeatInOneStepAndDoesNotComeBack()
    {
        var b2 = new NetworkGreedyJobsScheduler();
        var planner = new LiveTaskPlanner(4, 1);
        var tasks = Job(7, 0, 3, 0, 4);                          // node 1 is cold: no heat, no progress
        double[] heat = [0, 1, 0.4, 0];
        var (_, first) = Step(b2, planner, 0, heat, tasks);
        // B2 fills the warmest node (1 server-hour), then the next (0.4): both get a task at once
        Assert.Equal([new LiveMove("j0007-t02", 7, 0, 1), new LiveMove("j0007-t01", 7, 0, 2)], first);
        var later = new List<LiveMove>();
        for (int step = 1; step < 3; step++) later.AddRange(Step(b2, planner, step, heat, tasks).Moves);
        Assert.All(later, m => Assert.Equal(0, m.FromNode));    // nothing moves back or onwards without a cause
    }

    [Fact]
    public void ASmallAllocationStillMovesAWholeTask()
    {
        var tasks = Job(2, 0, 2, 0, 6);
        var (_, moves) = Step(new NetworkGreedyJobsScheduler(), new LiveTaskPlanner(4, 1), 0, [0, 0.4, 0, 0], tasks);
        Assert.Single(moves);                                   // 0.4 server-hours allocated → one task moves now
    }

    [Fact]
    public void AHouseWithSeveralServersTakesSeveralTasksAtOnce()
    {
        var cal3 = Cal with { ServersPerNode = 3, ServerHoursPerTask = 2 };
        var tasks = Job(9, 0, 6, 0, 20);
        var decision = new NetworkGreedyJobsScheduler().Decide(LiveNetwork.BuildState(0, Nodes, [0, 1, 1, 0], tasks, cal3));
        var moves = new LiveTaskPlanner(4, 2).Plan(tasks, decision.Allocations ?? []);
        // each warm house has 3 free server-hours this step → ⌈3 / 2⌉ = 2 tasks each, at the same time
        Assert.Equal(2, moves.Count(m => m.ToNode == 1));
        Assert.Equal(2, moves.Count(m => m.ToNode == 2));
        Assert.Equal(1, decision.PerSite[1].TargetLoadPerServer, 9);
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
