using DigitalHeat.Core;
using Webhosting.Gateway;

namespace Webhosting.Tests;

public class HostingPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    // Synthetic local lab fixtures, not estimates of production heat or capacity.
    private static HostingPlan Plan()
    {
        HostingNode[] nodes = [
            new("api-a", "api", "site-a", "CH", "http://127.0.0.1:8101", 8080),
            new("api-b", "api", "site-b", "CH", "http://127.0.0.1:8102", 8080),
            new("api-c", "api", "site-c", "DE", "http://127.0.0.1:8103", 8080),
            new("web-a", "frontend", "site-d", "CH", "http://127.0.0.1:8201", 8080),
            new("web-b", "frontend", "site-e", "CH", "http://127.0.0.1:8202", 8080),
            new("web-c", "frontend", "site-f", "DE", "http://127.0.0.1:8203", 8080)
        ];
        var plan = new HostingPlan("test", "synthetic test fixture", Now.AddHours(1), 2, 0,
            nodes, [new("api", "site-a", new("anywhere", "test contract", null)),
                new("frontend", "site-d", new("anywhere", "test contract", null))],
            nodes.ToDictionary(n => n.SiteId, _ => new HeatSignal(0, 0, 0, 2, 1)));
        plan.Validate();
        return plan;
    }

    private static HostingAdmission Ready(HostingPlan plan)
    {
        var admission = new HostingAdmission();
        foreach (var node in plan.Nodes) admission.SetReady(node.Id, true);
        return admission;
    }

    private static HashSet<string> Available(HostingPlan plan) => plan.Nodes.Select(n => n.Id).ToHashSet();

    [Fact]
    public void OverflowingFiniteInputsDisableHeatOptimization()
    {
        var plan = Plan();
        plan.HeatSignals!["site-a"] = new(double.MaxValue, 0, double.MaxValue, 1, 1);
        Assert.Null(plan.Scores(plan.Nodes, Now));
        plan.HeatSignals["site-a"] = new(1, 0, 0, double.MaxValue, 2);
        Assert.Null(plan.Scores(plan.Nodes, Now));
    }

    [Fact]
    public void ScoresMatchSharedGreedyHeatScheduler()
    {
        var plan = Plan() with { MinimumLoadShare = 0.2 };
        HeatSignal[] signals = [new(0, 0, 0, 2, 1), new(0.5, 0, 0, 2, 1),
            new(5, 0, 0, 2, 1), new(0, 1, 2, 2, 1), new(0, 2, 2, 2, 1), new(1, 0, 0, 4, 2)];
        for (int i = 0; i < plan.Nodes.Length; i++) plan.HeatSignals![plan.Nodes[i].SiteId] = signals[i];
        var scores = plan.Scores(plan.Nodes, Now);
        Assert.NotNull(scores);
        var scheduler = new GreedyHeatScheduler(plan.MinimumLoadShare);
        foreach (var node in plan.Nodes)
            Assert.Equal(scheduler.Decide(plan.HeatSignals![node.SiteId]).TargetLoadPerServer, scores[node.Id]);
    }

    [Theory]
    [InlineData("missing-spare")]
    [InlineData("stale")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("invalid-physics")]
    [InlineData("absent")]
    public void BadTelemetryFallsBackForWholeComponent(string fault)
    {
        var plan = Plan();
        plan.HeatSignals!["site-a"] = new(2, 0, 0, 2, 1);
        switch (fault)
        {
            case "missing-spare": plan.HeatSignals.Remove("site-c"); break;
            case "stale": plan = plan with { ValidUntilUtc = Now }; break;
            case "nan": plan.HeatSignals["site-c"] = new(double.NaN, 0, 0, 2, 1); break;
            case "infinity": plan.HeatSignals["site-c"] = new(0, 0, double.PositiveInfinity, 2, 1); break;
            case "invalid-physics": plan.HeatSignals["site-c"] = new(0, 2, 1, 2, 1); break;
            case "absent": plan = plan with { HeatSignals = null }; break;
        }
        Assert.Null(plan.Scores(plan.Nodes.Where(n => n.Component == "api"), Now));
        var admission = Ready(plan);
        // The unavailable spare still participates in completeness checking.
        HashSet<string> available = ["api-a", "api-b"];
        using var first = admission.Admit(plan, "api", available, Now, out var firstNode);
        using var second = admission.Admit(plan, "api", available, Now, out var secondNode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("api-a", firstNode!.Id);
        Assert.Equal("api-b", secondNode!.Id);
    }

    [Theory]
    [InlineData("origin-site", "site-a", "api-b")]
    [InlineData("country-allowlist", "site-a", "api-c")]
    [InlineData("origin-site", "missing-origin", "api-a")]
    public void LocalityDeniesSoleDestinationAndEmptyEligibleSet(string mode, string origin, string destination)
    {
        var plan = Plan();
        plan.Services[0] = new("api", origin, new(mode, "test contract", mode == "country-allowlist" ? ["CH"] : null));
        plan.Validate();
        var admission = Ready(plan);
        using var lease = admission.Admit(plan, "api", new HashSet<string> { destination }, Now, out var selected);
        Assert.Null(lease);
        Assert.Null(selected);
        Assert.Equal(0, admission.InFlight(destination));
    }

    [Theory]
    [InlineData("unknown", "test contract", null)]
    [InlineData("anywhere", "TODO(source)", null)]
    [InlineData("country-allowlist", "test contract", "ch")]
    [InlineData("country-allowlist", "test contract", null)]
    [InlineData("anywhere", "test contract", "CH")]
    public void MalformedPlacementPolicyFailsValidation(string mode, string source, string? country)
    {
        var plan = Plan();
        plan.Services[0] = plan.Services[0] with { PlacementPolicy = new(mode, source, country is null ? null : [country]) };
        Assert.Throws<InvalidDataException>(plan.Validate);
    }

    [Fact]
    public void AdmissionNeverBorrowsNodesFromAnotherComponent()
    {
        var plan = Plan();
        plan.HeatSignals!["site-d"] = new(100, 0, 0, 2, 1);
        var admission = Ready(plan);
        using var lease = admission.Admit(plan, "api", Available(plan), Now, out var selected);
        Assert.NotNull(lease);
        Assert.Equal("api", selected!.Component);
        using var unavailable = admission.Admit(plan, "api", new HashSet<string> { "web-a", "web-b", "web-c" }, Now, out selected);
        Assert.Null(unavailable);
        Assert.Null(selected);
    }

    [Fact]
    public void ZeroHeatKeepsServingAndTiesRotateInStableNodeOrder()
    {
        var plan = Plan();
        plan = plan with { Nodes = plan.Nodes.Reverse().ToArray() };
        var admission = Ready(plan);
        string[] expected = ["api-a", "api-b", "api-c", "api-a", "api-b", "api-c"];
        foreach (var id in expected)
        {
            using var lease = admission.Admit(plan, "api", Available(plan), Now, out var selected);
            Assert.NotNull(lease);
            Assert.Equal(id, selected!.Id);
        }
    }

    [Fact]
    public void DrainBlocksNewAdmissionWhileExistingLeaseCompletes()
    {
        var plan = Plan();
        var admission = Ready(plan);
        HashSet<string> available = ["api-a"];
        using var active = admission.Admit(plan, "api", available, Now, out var selected);
        Assert.NotNull(active);
        Assert.Equal("api-a", selected!.Id);
        Assert.Equal(1, admission.InFlight("api-a"));
        admission.Drain("api-a", true);
        using var blocked = admission.Admit(plan, "api", available, Now, out selected);
        Assert.Null(blocked);
        Assert.Null(selected);
        Assert.Equal(1, admission.InFlight("api-a"));
        active.Dispose();
        Assert.Equal(0, admission.InFlight("api-a"));
        active.Dispose();
        Assert.Equal(0, admission.InFlight("api-a"));
        admission.Drain("api-a", false);
        using var resumed = admission.Admit(plan, "api", available, Now, out selected);
        Assert.NotNull(resumed);
        Assert.Equal("api-a", selected!.Id);
    }
}
