using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalHeat.Core;

namespace Webhosting.Gateway;

public sealed record HostingNode(string Id, string Component, string SiteId, string CountryCode,
    string Address, int ContainerPort);
public sealed record HostingService(string Component, string DataOriginSiteId, JobPlacementPolicy PlacementPolicy);
public sealed record HostingPlan(string Revision, string Source, DateTimeOffset ValidUntilUtc,
    int MinimumServingReplicas, double MinimumLoadShare, HostingNode[] Nodes,
    HostingService[] Services, Dictionary<string, HeatSignal>? HeatSignals)
{
    [JsonIgnore] public string Fingerprint { get; init; } = "";
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Revision) || string.IsNullOrWhiteSpace(Source)
            || MinimumServingReplicas < 2 || !double.IsFinite(MinimumLoadShare)
            || MinimumLoadShare is < 0 or > 1 || Nodes is null || Services is null
            || Nodes.Length == 0 || Services.Length == 0
            || Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != Nodes.Length
            || Nodes.Select(n => n.SiteId).Distinct(StringComparer.Ordinal).Count() != Nodes.Length
            || Services.Select(s => s.Component).Distinct(StringComparer.Ordinal).Count() != Services.Length)
            throw new InvalidDataException("Invalid hosting plan identity or replica policy.");
        foreach (var service in Services)
        {
            if (string.IsNullOrWhiteSpace(service.Component) || string.IsNullOrWhiteSpace(service.DataOriginSiteId))
                throw new InvalidDataException("Missing service identity.");
            service.PlacementPolicy.Validate();
            if (Nodes.Count(n => n.Component == service.Component) < MinimumServingReplicas + 1)
                throw new InvalidDataException("Each component needs a spare in addition to serving replicas.");
        }
        foreach (var node in Nodes)
            if (string.IsNullOrWhiteSpace(node.Id) || node.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
                || string.IsNullOrWhiteSpace(node.SiteId) || node.ContainerPort is < 1 or > 65535
                || !Services.Any(s => s.Component == node.Component)
                || !Uri.TryCreate(node.Address, UriKind.Absolute, out var uri) || uri.Scheme != "http"
                || uri.Host != "127.0.0.1" || uri.AbsolutePath != "/" || uri.Query.Length != 0)
                throw new InvalidDataException("Invalid local lab node.");
    }

    public bool Eligible(HostingNode node)
    {
        var service = Services.Single(s => s.Component == node.Component);
        return JobPlacement.IsEligible(service.PlacementPolicy,
            new JobPlacementContext(service.DataOriginSiteId, node.SiteId, node.CountryCode));
    }

    // A null result means use all eligible nodes and do not scale down, never invent heat values.
    public Dictionary<string, double>? Scores(IEnumerable<HostingNode> nodes, DateTimeOffset now)
    {
        if (now >= ValidUntilUtc || HeatSignals is null) return null;
        var scheduler = new GreedyHeatScheduler(MinimumLoadShare);
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!HeatSignals.TryGetValue(node.SiteId, out var h) || h is null
                || !new[] { h.DemandKwh, h.TankStoredKwh, h.TankCapacityKwh,
                    h.ServerCount, h.RecoverableKwhPerServerAtFullLoad }.All(double.IsFinite)
                || !double.IsFinite(h.DemandKwh + h.TankCapacityKwh)
                || !double.IsFinite(h.ServerCount * h.RecoverableKwhPerServerAtFullLoad)) return null;
            try
            {
                double score = scheduler.Decide(h).TargetLoadPerServer;
                if (!double.IsFinite(score) || score is < 0 or > 1) return null;
                scores.Add(node.Id, score);
            }
            catch (ArgumentException) { return null; }
        }
        return scores;
    }
}

public sealed class HostingPlanReader(string path)
{
    public HostingPlan? Read()
    {
        try
        {
            string text = File.ReadAllText(path);
            var plan = JsonSerializer.Deserialize<HostingPlan>(text, HostingPlan.JsonOptions);
            plan?.Validate();
            return plan is null ? null : plan with { Fingerprint = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))) };
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException
            or InvalidOperationException or NullReferenceException) { return null; }
    }
}

/// <summary>Admission and drain share a lock, including the selection-to-forwarding interval.</summary>
public sealed class HostingAdmission
{
    private readonly object gate = new();
    private readonly Dictionary<string, (bool Ready, bool Draining, int InFlight)> states = new();
    private long sequence;
    public string Phase { get; set; } = "restore";

    public void SetReady(string id, bool ready)
    {
        lock (gate) { var s = states.GetValueOrDefault(id); states[id] = (ready, s.Draining, s.InFlight); }
    }
    public bool Ready(string id) { lock (gate) return states.GetValueOrDefault(id).Ready; }
    public void Drain(string id, bool enabled)
    {
        lock (gate) { var s = states.GetValueOrDefault(id); states[id] = (s.Ready, enabled, s.InFlight); }
    }
    public int InFlight(string id) { lock (gate) return states.GetValueOrDefault(id).InFlight; }

    public IDisposable? Admit(HostingPlan plan, string component, IReadOnlySet<string> available,
        DateTimeOffset now, out HostingNode? selected)
    {
        lock (gate)
        {
            var allowed = plan.Nodes.Where(n => n.Component == component && plan.Eligible(n)).ToArray();
            // Incomplete telemetry falls back for the whole component, including a missing spare.
            var scores = plan.Scores(allowed, now);
            var ready = allowed.Where(n => available.Contains(n.Id) &&
                states.GetValueOrDefault(n.Id) is { Ready: true, Draining: false }).OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
            if (scores is not null && ready.Length > 0)
            {
                double best = ready.Max(n => scores[n.Id]);
                ready = ready.Where(n => scores[n.Id] == best).ToArray();
            }
            selected = ready.Length == 0 ? null : ready[(int)((ulong)sequence++ % (uint)ready.Length)];
            if (selected is null) return null;
            var s = states[selected.Id]; states[selected.Id] = (s.Ready, s.Draining, s.InFlight + 1);
            return new Lease(this, selected.Id);
        }
    }

    public object Snapshot(HostingPlan? plan) => new
    {
        phase = Phase, revision = plan?.Revision,
        replicas = plan?.Nodes.Select(n => new { node_id = n.Id, component = n.Component,
            ready = Ready(n.Id), in_flight = InFlight(n.Id) }).ToArray()
    };

    private sealed class Lease(HostingAdmission owner, string id) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            lock (owner.gate)
            {
                if (disposed) return;
                disposed = true;
                var s = owner.states[id]; owner.states[id] = (s.Ready, s.Draining, s.InFlight - 1);
            }
        }
    }
}
