using System.Globalization;
using System.Text.Json;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>A configured number with unit and source (AGENTS.md §5.2: every number has a source).</summary>
public sealed record SourcedValue(double Value, string Unit, string Source);

public sealed record ElcomMunicipality(string Id, string Source);

/// <summary>Maps one live gaming node to one heat site of the network scenario.</summary>
public sealed record MonitorNode(string NodeId, string SiteId, int SessionSlots, string SessionSlotsSource,
    string LiveDemand, ElcomMunicipality ElcomMunicipality);

/// <summary>
/// Configuration of the T-42 heat monitor (<c>gaming/deploy/heat-monitor.json</c>). Paths are relative
/// to the repository root. Trusted operator configuration; unknown JSON fields are rejected.
/// </summary>
public sealed record MonitorConfig(string Scenario, string ReferenceParameters, string RuntimeProfiles,
    string ApplicationId, string PlayerOriginSiteId, SourcedValue SessionHeatKwhPerHour,
    SourcedValue DatasetUtcOffsetHours, string ReplayStartLocal, string WinterJumpLocal, string SummerJumpLocal,
    string OpenMeteoForecastEndpoint, string ElcomEndpoint, string ElcomCategory, MonitorNode[] Nodes)
{
    public const string CaseFormula = "case-formula";
    public const string ReplaySameHour = "replay-same-hour";
    public const string LocalHourFormat = "yyyy-MM-dd'T'HH:mm";

    public static MonitorConfig Load(string path)
    {
        var config = JsonSerializer.Deserialize<MonitorConfig>(File.ReadAllText(path), global::DigitalHeat.Gaming.RuntimeProfiles.JsonOptions)
            ?? throw new InvalidDataException("Missing heat monitor configuration.");
        config.Validate();
        return config;
    }

    public void Validate()
    {
        static void Require(bool valid, string what)
        { if (!valid) throw new InvalidDataException($"Invalid heat monitor configuration: {what}."); }
        static bool Text(string? s) => !string.IsNullOrWhiteSpace(s);
        static bool Sourced(string? s) => Text(s) && !s!.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase);
        static bool Https(string? s) => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

        Require(Text(Scenario) && Text(ReferenceParameters) && Text(RuntimeProfiles), "file paths");
        Require(Text(ApplicationId) && Text(PlayerOriginSiteId), "application and player origin site");
        Require(SessionHeatKwhPerHour is not null && double.IsFinite(SessionHeatKwhPerHour.Value)
            && SessionHeatKwhPerHour.Value > 0 && SessionHeatKwhPerHour.Unit == "kWh/h per session" && Sourced(SessionHeatKwhPerHour.Source), "session_heat_kwh_per_hour needs a positive value and a source");
        Require(DatasetUtcOffsetHours is not null && double.IsFinite(DatasetUtcOffsetHours.Value)
            && Math.Abs(DatasetUtcOffsetHours.Value) <= 14 && DatasetUtcOffsetHours.Value == Math.Round(DatasetUtcOffsetHours.Value)
            && DatasetUtcOffsetHours.Unit == "hours" && Sourced(DatasetUtcOffsetHours.Source), "dataset_utc_offset_hours needs a whole-hour value and a source");
        Require(IsLocalHour(ReplayStartLocal) && IsLocalHour(WinterJumpLocal) && IsLocalHour(SummerJumpLocal), "local hour labels (yyyy-MM-ddTHH:00)");
        Require(Https(OpenMeteoForecastEndpoint) && Https(ElcomEndpoint) && Text(ElcomCategory), "API endpoints (https) and ElCom category");
        Require(Nodes is { Length: > 0 } && Nodes.All(n => n is not null), "nodes");
        Require(Nodes!.Select(n => n.NodeId).Distinct(StringComparer.Ordinal).Count() == Nodes.Length, "unique node ids");
        foreach (var n in Nodes)
        {
            Require(Text(n.NodeId) && Text(n.SiteId), $"node/site id of '{n.NodeId}'");
            Require(n.SessionSlots > 0 && Sourced(n.SessionSlotsSource), $"session slots and source of '{n.NodeId}'");
            Require(n.LiveDemand is CaseFormula or ReplaySameHour, $"live_demand of '{n.NodeId}' ({CaseFormula} or {ReplaySameHour})");
            Require(n.ElcomMunicipality is not null && n.ElcomMunicipality.Id is { Length: > 0 } id && id.All(char.IsAsciiDigit)
                && Sourced(n.ElcomMunicipality.Source), $"ElCom municipality (BFS number) and source of '{n.NodeId}'");
        }
        // One site, one live demand rule: two nodes at the same site must agree.
        Require(Nodes.GroupBy(n => n.SiteId).All(g => g.Select(n => n.LiveDemand).Distinct().Count() == 1
            && g.Select(n => n.ElcomMunicipality.Id).Distinct().Count() == 1), "nodes at one site must share live_demand and municipality");
    }

    public static bool IsLocalHour(string? label) =>
        label is not null && DateTime.TryParseExact(label, LocalHourFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
        && t.Minute == 0;

    /// <summary>Start of a dataset hour in UTC. The dataset has a fixed offset (see data/raw/open-meteo/SOURCE.md).</summary>
    public DateTimeOffset DatasetHourToUtc(string localLabel)
    {
        var local = DateTime.ParseExact(localLabel, LocalHourFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new DateTimeOffset(local, TimeSpan.FromHours(DatasetUtcOffsetHours.Value)).ToUniversalTime();
    }
}
