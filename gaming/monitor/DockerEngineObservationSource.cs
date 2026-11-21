using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>Local persistent read-only Engine API; explicit-context CLI fallback for unsupported transports.</summary>
public sealed class DockerEngineObservationSource : IDisposable
{
    private sealed record Connection(string Endpoint, DateTimeOffset ResolvedAt, DockerReadOnlyApi? Reader)
    {
        public int Failed;
    }
    private readonly MonitorSetup setup;
    private readonly DockerObservationOptions options;
    private readonly TimeSpan commandTimeout;
    private readonly Func<CancellationToken, IDockerCommandRunner> commands;
    private readonly Func<string, HttpClient> clients;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim reads, refresh = new(1, 1);
    private readonly ConcurrentDictionary<string, Connection> connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Context, string Endpoint, string Id), DockerCpuSample> samples = new();
    public DockerEngineObservationSource(MonitorSetup setup, DockerObservationOptions options, TimeSpan commandTimeout,
        Func<CancellationToken, IDockerCommandRunner> commandFactory, TimeProvider? timeProvider = null)
        : this(setup, options, commandTimeout, commandFactory, DockerReadOnlyApi.LocalClient, timeProvider) { }
    internal DockerEngineObservationSource(MonitorSetup setup, DockerObservationOptions options, TimeSpan commandTimeout,
        Func<CancellationToken, IDockerCommandRunner> commandFactory, Func<string, HttpClient> clientFactory, TimeProvider? timeProvider = null)
    {
        options.Validate(); this.setup = setup; this.options = options; this.commandTimeout = commandTimeout;
        commands = commandFactory; clients = clientFactory; clock = timeProvider ?? TimeProvider.System;
        reads = new(options.MaxConcurrentReads, options.MaxConcurrentReads);
    }
    private async Task<Connection> Connect(string context, CancellationToken cancel)
    {
        if (connections.TryGetValue(context, out var previous) && clock.GetUtcNow() - previous.ResolvedAt < TimeSpan.FromSeconds(options.EndpointCacheSeconds)) return previous;
        string json = await Task.Run(() => commands(cancel).Run(["--context", context, "context", "inspect", context, "--format", "{{json .Endpoints.docker}}"]), cancel);
        using var endpoint = JsonDocument.Parse(json);
        string host = endpoint.RootElement.GetProperty("Host").GetString() ?? throw new DockerReadException("missing_context_endpoint");
        if (previous is not null && previous.Endpoint == host)
        {
            var renewed = previous with { ResolvedAt = clock.GetUtcNow() }; connections[context] = renewed; return renewed;
        }
        previous?.Reader?.Dispose();
        DockerReadOnlyApi? reader = null;
        if (DockerReadOnlyApi.IsLocalEndpoint(host))
        {
            reader = new(clients(host), options, reads, commandTimeout);
            try { await reader.Initialize(cancel); } catch { reader.Dispose(); throw; }
        }
        var connection = new Connection(host, clock.GetUtcNow(), reader); connections[context] = connection;
        foreach (var key in samples.Keys.Where(k => k.Context == context && k.Endpoint != host)) samples.TryRemove(key, out _);
        return connection;
    }
    public async Task<IReadOnlyDictionary<string, DockerNodeState>> ProbeAsync(CancellationToken cancel = default)
    {
        if (!await refresh.WaitAsync(0, cancel)) throw new InvalidOperationException("Observation refresh already running.");
        try
        {
            var groups = setup.Config.Nodes.GroupBy(n => setup.Profiles.Nodes.Single(p => p.Id == n.NodeId).DockerContext).ToArray();
            var batches = await Task.WhenAll(groups.Select(g => Context(g.Key, g.ToArray(), cancel)));
            cancel.ThrowIfCancellationRequested();
            return batches.SelectMany(b => b).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }
        finally { refresh.Release(); }
    }
    private async Task<Dictionary<string, DockerNodeState>> Context(string context, MonitorNode[] nodes, CancellationToken cancel)
    {
        try
        {
            var connection = await Connect(context, cancel);
            if (connection.Reader is null)
            {
                var fallback = await Task.Run(() => new DockerProbe(setup, commands(cancel)).Probe(nodes.Select(n => n.NodeId).ToArray(), cancel), cancel);
                return fallback.ToDictionary(p => p.Key, p => p.Value with { ObservationSource = "docker_cli_fallback" }, StringComparer.Ordinal);
            }
            using var list = await connection.Reader.Containers(cancel);
            if (list.RootElement.ValueKind != JsonValueKind.Array) throw new DockerReadException("invalid_container_list");
            var listed = list.RootElement.EnumerateArray().ToArray(); var at = clock.GetUtcNow();
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var tasks = nodes.Select(async node =>
            {
                var profile = setup.Profiles.Nodes.Single(p => p.Id == node.NodeId);
                bool Match(JsonElement container)
                {
                    if (!container.TryGetProperty("Labels", out var labels) || labels.ValueKind != JsonValueKind.Object) return false;
                    return labels.TryGetProperty("com.docker.compose.project", out var project) && project.GetString() == profile.ComposeProject
                        && labels.TryGetProperty("com.docker.compose.service", out var service) && service.GetString() == profile.ComposeService;
                }
                var matching = listed.Where(Match).ToArray();
                if (matching.Length != 1) return new KeyValuePair<string, DockerNodeState>(node.NodeId,
                    new(false, null, matching.Length == 0 ? "not running" : "ambiguous") { ObservedAtUtc = at, ObservationSource = "local_engine_api" });
                string id = matching[0].GetProperty("Id").GetString() ?? "";
                if (!Regex.IsMatch(id, "^[a-f0-9]{64}$")) throw new DockerReadException("invalid_container_id");
                string state = matching[0].GetProperty("State").GetString() ?? "unknown";
                if (state != "running") return new(node.NodeId, new(false, null,
                    state is "paused" or "exited" or "dead" or "created" or "restarting" or "removing" ? "not available (stopped or paused)" : "docker error: unknown_state")
                    { ObservedAtUtc = at, ObservationSource = "local_engine_api" });
                lock (selected) selected.Add(id);
                return new(node.NodeId, await ObserveRunning(context, connection, node, id, at, cancel));
            }).ToArray();
            var result = (await Task.WhenAll(tasks)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            // Finish sibling reads before replacing/disposal of their shared connection.
            if (connection.Failed != 0 && connections.TryRemove(context, out var broken)) broken.Reader?.Dispose();
            cancel.ThrowIfCancellationRequested();
            foreach (var key in samples.Keys.Where(k => k.Context == context && (k.Endpoint != connection.Endpoint || !selected.Contains(k.Id)))) samples.TryRemove(key, out _);
            return result;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (connections.TryRemove(context, out var broken)) broken.Reader?.Dispose();
            return nodes.ToDictionary(n => n.NodeId, _ => new DockerNodeState(false, null, "docker error: observation_unavailable")
                { ObservedAtUtc = clock.GetUtcNow(), ObservationSource = "unavailable" }, StringComparer.Ordinal);
        }
    }
    private async Task<DockerNodeState> ObserveRunning(string context, Connection connection, MonitorNode node, string id, DateTimeOffset at, CancellationToken cancel)
    {
        bool? running = null; string detail = "running " + id[..12];
        if (!setup.IncompatibleNodes.ContainsKey(node.NodeId))
        {
            try
            {
                using var top = await connection.Reader!.Processes(id, cancel);
                string executable = setup.Profiles.Applications.Single(a => a.Id == setup.Config.ApplicationId).Executable;
                if (!top.RootElement.TryGetProperty("Titles", out var titles) || !top.RootElement.TryGetProperty("Processes", out var processes)) throw new DockerReadException("process_list_unavailable");
                int column = Array.FindIndex(titles.EnumerateArray().Select(t => t.GetString()).ToArray(), t => t is "COMMAND" or "CMD" or "ARGS");
                if (column < 0 || processes.ValueKind != JsonValueKind.Array) throw new DockerReadException("process_columns_unavailable");
                // Raw command lines are transiently matched, never returned, retained or logged.
                running = processes.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > column
                    && p[column].GetString()?.Contains(executable, StringComparison.Ordinal) == true);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { throw; }
            catch (Exception) { Interlocked.Exchange(ref connection.Failed, 1); detail += "; application probe unavailable"; }
        }
        var key = (context, connection.Endpoint, id);
        try
        {
            using var stats = await connection.Reader!.Stats(id, cancel);
            samples.TryGetValue(key, out var old);
            var value = DockerStats.Read(stats.RootElement, old, clock.GetUtcNow());
            if (value.Next is {} next) samples[key] = next; else samples.TryRemove(key, out _);
            return new(true, running, detail, value.CpuPercent, value.MemoryUsage)
            { ObservedAtUtc = at, ResourcesObservedAtUtc = value.SampledAtUtc, CpuSampleIntervalSeconds = value.CpuSampleIntervalSeconds, ObservationSource = "local_engine_api" };
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            samples.TryRemove(key, out _);
            // Force context re-resolution after a local read failure; no insecure transport fallback.
            Interlocked.Exchange(ref connection.Failed, 1);
            return new(true, running, detail + "; resource stats unavailable") { ObservedAtUtc = at, ObservationSource = "local_engine_api" };
        }
    }
    public void Dispose()
    {
        foreach (var connection in connections.Values) connection.Reader?.Dispose();
        connections.Clear(); samples.Clear(); reads.Dispose(); refresh.Dispose();
    }
}
