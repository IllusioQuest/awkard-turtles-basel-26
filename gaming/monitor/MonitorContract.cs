using System.Text.Json;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>Transport contract, independent of UI, HTTP, files and Docker commands.</summary>
public sealed record MonitorInputs
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumReservations = 100; // Existing console's bounded interactive request limit.
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string? SelectedHourLocal { get; init; }
    public required int InitialReservations { get; init; }
    public required bool ObserveDocker { get; init; }
    public required bool BypassCapabilities { get; init; }
    public required bool ShowDetails { get; init; }
    public static JsonSerializerOptions JsonOptions => RuntimeProfiles.JsonOptions;

    public static MonitorInputs Default(MonitorConfig config) => new()
    {
        SchemaVersion = CurrentSchemaVersion, Mode = "replay", SelectedHourLocal = config.ReplayStartLocal,
        InitialReservations = 0, ObserveDocker = false, BypassCapabilities = false, ShowDetails = false
    };

    public void Validate(IReadOnlyList<string> axis)
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new ArgumentException("Unsupported schema_version.");
        if (Mode is not ("replay" or "live")) throw new ArgumentException("mode must be replay or live.");
        if (InitialReservations is < 0 or > MaximumReservations) throw new ArgumentException($"initial_reservations must be 0..{MaximumReservations}.");
        if (Mode == "live" && SelectedHourLocal is not null) throw new ArgumentException("Live mode requires selected_hour_local=null.");
        if (Mode == "replay" && (!MonitorConfig.IsLocalHour(SelectedHourLocal) || !axis.Contains(SelectedHourLocal!)))
            throw new ArgumentException("Replay requires selected_hour_local from the committed scenario hours.");
    }
}

public sealed record MonitorMetric(double? Value, string Unit, string Kind, string Meaning, string Source);
public sealed record MonitorNodeSnapshot(string NodeId, string SiteId, string ObservationStatus,
    bool? ContainerRunning, bool? ApplicationRunning, string ObservationDetail, DateTimeOffset? ObservedAtUtc,
    string? CpuPercent, string? MemoryUsedAndLimit, bool ReadyForNewClaims, string ReadinessReason,
    MonitorMetric PlannedSessions, MonitorMetric SessionSlots, MonitorMetric PlannedHeatKwhPerHour)
{
    public DateTimeOffset? ResourcesObservedAtUtc { get; init; }
    public double? CpuSampleIntervalSeconds { get; init; }
    public string ObservationSource { get; init; } = "unavailable";
}
public sealed record MonitorSiteSnapshot(string SiteId, string Name, MonitorMetric HeatNeededKwh,
    MonitorMetric PlannedHeatKwh, MonitorMetric TankStoredKwh, MonitorMetric TankCapacityKwh,
    MonitorMetric HeatHeadroomKwh, MonitorMetric WastedHeatKwh, MonitorMetric BackupHeatKwh,
    MonitorMetric TemperatureC, MonitorMetric ElectricityPriceChfPerKwh,
    MonitorMetric HeatValueChfPerKwh, MonitorMetric PotentialHeatValueChfPerHour);
public sealed record MonitorSnapshot(int SchemaVersion, MonitorInputs Inputs, string Mode, string LocalHour,
    string TimeBasis, string ApplicationId, DateTimeOffset GeneratedAtUtc, string ScenarioSource,
    MonitorMetric SessionHeatKwhPerHour, int PlacedReservations, IReadOnlyList<string> Warnings,
    IReadOnlyList<MonitorNodeSnapshot> Nodes, IReadOnlyList<MonitorSiteSnapshot> Sites);

public static class MonitorSnapshotBuilder
{
    public static MonitorSnapshot Build(MonitorSetup setup, MonitorInputs inputs, HourInputs hour,
        IReadOnlyDictionary<string, DockerNodeState> observations, DateTimeOffset? observedAtUtc,
        DateTimeOffset generatedAtUtc, IEnumerable<string>? sourceWarnings = null)
    {
        inputs.Validate(setup.Axis);
        if (hour.Mode != inputs.Mode || (inputs.Mode == "replay" && hour.LocalLabel != inputs.SelectedHourLocal))
            throw new ArgumentException("Resolved hour must match the requested mode/hour.");
        var engine = setup.CreateEngine();
        engine.Begin(hour);
        var reasons = new Dictionary<string, string>();
        foreach (var node in setup.Config.Nodes)
        {
            observations.TryGetValue(node.NodeId, out var observed);
            bool capable = !setup.IncompatibleNodes.ContainsKey(node.NodeId);
            bool available = !inputs.ObserveDocker || observed?.ContainerRunning == true;
            bool ready = available && (capable || inputs.BypassCapabilities);
            engine.SetReady(node.NodeId, ready);
            reasons[node.NodeId] = !available ? "Docker availability not confirmed" : !capable && !inputs.BypassCapabilities
                ? "Application setup missing: " + setup.IncompatibleNodes[node.NodeId]
                : inputs.BypassCapabilities ? "Setup check bypassed (what-if); heat and slots still checked"
                : "Container/setup prechecks passed; heat and slots still checked";
            if (!inputs.ObserveDocker && ready) reasons[node.NodeId] = "Container assumed available (no Docker observation); " + reasons[node.NodeId];
        }
        for (int i = 0; i < inputs.InitialReservations; i++) engine.Reserve();
        var warnings = new List<string>
        {
            "One-hour stateless preview: tanks start empty for each request; no cumulative history.",
            "Heat and reservations are model estimates, not power/heat measurements or observed players. No games are launched.",
            "Scope is configured application desktops, not the Minecraft world server or the complete Docker engine. All lab containers share one laptop."
        };
        if (!inputs.ObserveDocker) warnings.Add("Docker observation disabled: container availability is assumed, CPU/RAM and processes are unavailable.");
        if (inputs.BypassCapabilities) warnings.Add("WHAT-IF: application compatibility checks bypassed.");
        if (engine.Sessions.Count < inputs.InitialReservations) warnings.Add($"Placed {engine.Sessions.Count} of {inputs.InitialReservations} requested claims; setup, locality, heat or slots constrain placement.");
        if (sourceWarnings is not null) warnings.AddRange(sourceWarnings);
        MonitorMetric Metric(double? value, string unit, string meaning, string source, string kind = "model") => new(value, unit, kind, meaning, source);
        var nodes = engine.NodeViews().Select(n =>
        {
            observations.TryGetValue(n.Node.NodeId, out var d);
            bool error = d?.Detail.StartsWith("docker error:", StringComparison.Ordinal) == true || d?.Detail == "ambiguous";
            bool observed = inputs.ObserveDocker && d is not null && !error;
            return new MonitorNodeSnapshot(n.Node.NodeId, n.Node.SiteId,
                !inputs.ObserveDocker ? "disabled" : d is null ? "unavailable" : error ? "error" : "observed",
                observed ? d!.ContainerRunning : null, observed ? d!.ApplicationRunning : null,
                inputs.ObserveDocker ? d?.Detail ?? "No observation available" : "Docker disabled",
                inputs.ObserveDocker ? d?.ObservedAtUtc ?? observedAtUtc : null, observed ? d!.CpuPercent : null, observed ? d!.MemoryUsage : null,
                n.Ready, reasons[n.Node.NodeId],
                Metric(n.Sessions.Count, "sessions", "Planned in-memory claims; not connected players", "Existing SessionPlacement"),
                Metric(n.Node.SessionSlots, "slots", "Configured simultaneous desktop slots, not measured throughput", setup.Config.Nodes.Single(x => x.NodeId == n.Node.NodeId).SessionSlotsSource, "assumption"),
                Metric(n.GenerationKwh, "kWh/h", "Planned recoverable heat rate, not measured heat", setup.Config.SessionHeatKwhPerHour.Source))
            { ResourcesObservedAtUtc = observed ? d!.ResourcesObservedAtUtc : null, CpuSampleIntervalSeconds = observed ? d!.CpuSampleIntervalSeconds : null,
                ObservationSource = inputs.ObserveDocker ? d?.ObservationSource ?? "unavailable" : "disabled" };
        }).ToArray();
        var sites = engine.SiteViews().Select(v => new MonitorSiteSnapshot(v.Site.Id, v.Site.Name,
            Metric(v.Hour.Demand.TotalHeatKwh, "kWh", "Space heating plus hot water for this hour", v.Hour.DemandSource),
            Metric(v.GenerationKwh, "kWh", "Heat from claims over one full hour", setup.Config.SessionHeatKwhPerHour.Source),
            Metric(v.TankStoredKwh, "kWh", "Stored energy at hour start; empty in this stateless preview", "Initial empty-tank model state"),
            Metric(v.Site.TankCapacityKwh, "kWh", "Maximum stored energy; not litres or temperature", setup.Config.Scenario, "assumption"),
            Metric(v.FreeCapacityKwh, "kWh", "max(0, heat needed + empty tank - planned heat); not CPU/GPU capacity", "Shared Core HeatCapacity.RemainingKwh"),
            Metric(v.Preview.DumpedHeatKwh, "kWh", "Excess heat that cannot be used or stored", "Shared Core HeatBalance.Step"),
            Metric(v.Preview.BackupHeatKwh, "kWh", "Heat another heater must supply; not a backup server", "Shared Core HeatBalance.Step"),
            Metric(v.Hour.TemperatureC, "degC", "Outside temperature; live mode uses forecast, not a local sensor", v.Hour.TemperatureSource ?? $"Committed scenario weather series: {setup.Config.Scenario}", "weather_input"),
            Metric(v.Hour.ElectricityPriceChfPerKwh, "CHF/kWh", "Annual household tariff; non-household proxy, not spot price", v.Hour.PriceSource, "source_input"),
            Metric(v.Hour.HeatValueChfPerKwh, "CHF/kWh", "Value of displaced heating, not computing revenue", v.Hour.PriceSource, "source_input"),
            Metric(v.UsefulValueChfPerHour, "CHF/h", "Value of direct heat use plus tank charging; not profit or realised savings", "Existing engine view: heat value times direct use plus tank charging"))).ToArray();
        return new(MonitorInputs.CurrentSchemaVersion, inputs, hour.Mode, hour.LocalLabel,
            hour.Mode == "live" ? "Europe/Zurich" : $"Dataset fixed UTC{setup.Config.DatasetUtcOffsetHours.Value:+0;-0;0}",
            setup.Config.ApplicationId, generatedAtUtc, setup.Config.Scenario,
            Metric(engine.SessionHeatKwhPerHour, "kWh/h per session", "Assumed session recoverable heat, not measured game/laptop power", setup.Config.SessionHeatKwhPerHour.Source, "assumption"),
            engine.Sessions.Count, warnings, nodes, sites);
    }
}
