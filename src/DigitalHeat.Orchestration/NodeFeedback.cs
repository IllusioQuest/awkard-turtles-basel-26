using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Orchestration;

public sealed record HeatReporter(string SiteId, string NodeId);
public sealed record NodeFeedbackPolicy(int SchemaVersion, double FallbackIntervalSeconds,
    double MaxReportAgeSeconds, int MaxReportBytes, int MaxQueuedJobs, string Source, HeatReporter[] HeatReporters)
{
    public static NodeFeedbackPolicy Load(string path) => JsonSerializer.Deserialize<NodeFeedbackPolicy>(File.ReadAllText(path), WorkloadJson.Options)
        ?? throw new InvalidDataException("Missing feedback policy.");

    public void Validate(WorkloadInventory inventory)
    {
        if (SchemaVersion != 1 || !double.IsFinite(FallbackIntervalSeconds) || FallbackIntervalSeconds <= 0
            || !double.IsFinite(MaxReportAgeSeconds) || MaxReportAgeSeconds <= 0 || MaxReportBytes <= 0
            || MaxQueuedJobs <= 0 || string.IsNullOrWhiteSpace(Source) || HeatReporters is null
            || HeatReporters.Any(r => r is null) || HeatReporters.Select(r => r.SiteId).Distinct().Count() != inventory.Sites.Length
            || HeatReporters.Length != inventory.Sites.Length
            || inventory.Nodes.Any(n => !SafeId(n.Id))
            || HeatReporters.Any(r => !inventory.Nodes.Any(n => n.Id == r.NodeId && n.SiteId == r.SiteId)))
            throw new InvalidDataException("Invalid feedback policy or heat authority bindings.");
    }

    internal static bool SafeId(string id) => !string.IsNullOrWhiteSpace(id)
        && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
}

/// <summary>Complete replacement state, never an additive heat credit. Heat uses the hourly baseline convention.</summary>
public sealed record NodeFeedback(string NodeId, long Sequence, DateTimeOffset ObservedAtUtc,
    DateTimeOffset ValidUntilUtc, DateTimeOffset IntervalStartUtc, bool AcceptingWork, HeatSignal? Heat, string Source,
    string? HeatBasis = null);
public sealed record NodeFeedbackState(string FleetId, string BindingsIdentity, NodeFeedback[] Reports);

/// <summary>Trusted local reports; no networking, runtime actions or ownership release.</summary>
public sealed class NodeFeedbackInbox
{
    private readonly string directory, statePath, fleetId, bindingsIdentity;
    private readonly NodeFeedbackPolicy policy;
    private readonly Dictionary<string, NodeFeedback> reports = new(StringComparer.Ordinal);
    private readonly HashSet<string> invalid = new(StringComparer.Ordinal);

    public NodeFeedbackInbox(string directory, string stateDirectory, string fleetId, NodeFeedbackPolicy policy, WorkloadInventory inventory)
    {
        policy.Validate(inventory);
        this.directory = Path.GetFullPath(directory);
        this.policy = policy;
        this.fleetId = fleetId;
        bindingsIdentity = WorkloadJson.Identity(policy);
        Directory.CreateDirectory(this.directory);
        Directory.CreateDirectory(stateDirectory);
        statePath = Path.Combine(stateDirectory, "node-feedback-state.json");
        if (!File.Exists(statePath)) return;
        var state = JsonSerializer.Deserialize<NodeFeedbackState>(File.ReadAllText(statePath), WorkloadJson.Options)
            ?? throw new InvalidDataException("Invalid feedback journal.");
        if (state.FleetId != fleetId || state.BindingsIdentity != bindingsIdentity || state.Reports is null
            || state.Reports.Any(r => r is null) || state.Reports.Select(r => r.NodeId).Distinct().Count() != state.Reports.Length)
            throw new InvalidDataException("Feedback journal identity mismatch.");
        foreach (var report in state.Reports) reports.Add(report.NodeId, report);
    }

    public static void Publish(string directory, NodeFeedback report)
    {
        if (!NodeFeedbackPolicy.SafeId(report.NodeId)) throw new ArgumentException("Invalid node identity.");
        Directory.CreateDirectory(directory);
        WriteAtomic(Path.Combine(directory, "node-" + report.NodeId + ".json"), report);
    }

    // Reads only configured paths, a bounded one-report-per-node snapshot. Bad files fail closed per node.
    public string[] Read(WorkloadInventory inventory, DateTimeOffset now)
    {
        var errors = new List<string>();
        foreach (var node in inventory.Nodes)
        {
            string path = Path.Combine(directory, "node-" + node.Id + ".json");
            try
            {
                if (!File.Exists(path)) { invalid.Add(node.Id); continue; }
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                if (stream.Length > policy.MaxReportBytes) throw new InvalidDataException("Report exceeds size policy.");
                var report = JsonSerializer.Deserialize<NodeFeedback>(stream, WorkloadJson.Options)
                    ?? throw new InvalidDataException("Empty report.");
                ValidateReport(report, node, inventory, now);
                AcceptReport(report);
                invalid.Remove(node.Id);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                invalid.Add(node.Id);
                errors.Add($"{node.Id}: {error.Message}");
            }
        }
        return errors.ToArray();
    }

    private void AcceptReport(NodeFeedback report)
    {
        reports.TryGetValue(report.NodeId, out var prior);
        if (prior is not null && report.Sequence <= prior.Sequence)
        {
            if (report.Sequence != prior.Sequence || report != prior)
                throw new InvalidDataException("Old sequence or conflicting replay.");
            return; // Identical retries must not renew expiry or cause another durable write.
        }

        reports[report.NodeId] = report;
        try
        {
            WriteAtomic(statePath, new NodeFeedbackState(fleetId, bindingsIdentity, reports.Values.ToArray()));
        }
        catch
        {
            // Never enable admission from a report whose sequence watermark was not persisted.
            if (prior is null) reports.Remove(report.NodeId);
            else reports[report.NodeId] = prior;
            throw;
        }
    }

    private void ValidateReport(NodeFeedback r, ComputeNode node, WorkloadInventory inventory, DateTimeOffset now)
    {
        if (r.NodeId != node.Id || r.Sequence <= 0 || string.IsNullOrWhiteSpace(r.Source)
            || r.ObservedAtUtc.Offset != TimeSpan.Zero || r.ValidUntilUtc.Offset != TimeSpan.Zero
            || r.IntervalStartUtc != inventory.IntervalStartUtc || r.IntervalStartUtc.Offset != TimeSpan.Zero
            || r.ObservedAtUtc < inventory.IntervalStartUtc || r.ObservedAtUtc > now || now >= r.ValidUntilUtc
            || r.ValidUntilUtc <= r.ObservedAtUtc || r.ValidUntilUtc > inventory.IntervalEndUtc
            || r.ValidUntilUtc - r.ObservedAtUtc > TimeSpan.FromSeconds(policy.MaxReportAgeSeconds))
            throw new InvalidDataException("Report identity, sequence, interval or freshness invalid.");
        if (r.Heat is not {} heat) return;
        var site = inventory.Sites.Single(s => s.Id == node.SiteId);
        if (r.HeatBasis != "hourly_baseline" || !policy.HeatReporters.Any(b => b.SiteId == site.Id && b.NodeId == node.Id)
            || heat.ServerCount != site.Heat.ServerCount || heat.TankCapacityKwh != site.Heat.TankCapacityKwh
            || heat.RecoverableKwhPerServerAtFullLoad != site.Heat.RecoverableKwhPerServerAtFullLoad)
            throw new InvalidDataException("Unauthorized heat reporter or changed fixed hardware parameters.");
        HeatCapacity.RemainingKwh(heat);
    }

    public WorkloadInventory Project(WorkloadInventory basis, long revision, DateTimeOffset now)
    {
        bool Fresh(NodeFeedback r) => !invalid.Contains(r.NodeId) && r.IntervalStartUtc == basis.IntervalStartUtc
            && r.ObservedAtUtc <= now && now < r.ValidUntilUtc;
        var heat = policy.HeatReporters.ToDictionary(b => b.SiteId, b => reports.TryGetValue(b.NodeId, out var r)
            && Fresh(r) && r.Heat is not null ? r : null, StringComparer.Ordinal);
        var until = basis.ResourceValidUntilUtc;
        foreach (var report in reports.Values.Where(Fresh))
            if (until is {} expiry && report.ValidUntilUtc < expiry) until = report.ValidUntilUtc;
        return basis with
        {
            Revision = revision,
            ResourceValidUntilUtc = until, // Never extend the physical inventory's independent freshness.
            Sites = basis.Sites.Select(s => heat[s.Id] is {} r ? s with { Heat = r.Heat! } : s).ToArray(),
            Nodes = basis.Nodes.Select(n => n with { Ready = n.Ready && heat[n.SiteId] is not null
                && reports.TryGetValue(n.Id, out var r) && Fresh(r) && r.AcceptingWork }).ToArray()
        };
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, WorkloadJson.Options); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
