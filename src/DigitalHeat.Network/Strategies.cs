using DigitalHeat.Core;

namespace DigitalHeat.Network;

/// <summary>B0: every server at full load every hour; surplus heat is dumped (decision 0014 §8).</summary>
public sealed class AlwaysOnScheduler : INetworkScheduler
{
    public string Id => "B0";
    public string Label => "B0 always on (dumps surplus heat)";
    public NetworkDecision Decide(NetworkState state) =>
        new(state.Sites.Select(_ => new SchedulerDecision(1)).ToArray());
}

/// <summary>
/// B1: each site runs the existing <see cref="GreedyHeatScheduler"/> on its own signal, ignoring the
/// forecast. With one site it reproduces <see cref="YearSimulation"/> exactly.
/// </summary>
public sealed class LocalGreedyScheduler : INetworkScheduler
{
    public string Id => "B1";
    public string Label => "B1 local greedy (current hour only)";
    public NetworkDecision Decide(NetworkState state) =>
        new(state.Sites.Select((site, i) =>
            new GreedyHeatScheduler(site.MinimumLoadShare).Decide(state.States[i].Signal)).ToArray());
}
