using System.Text.Json;
using DigitalHeat.Gaming.Monitor;
using Xunit;

public class MonitorContractTests
{
    private static MonitorSetup Setup()
    {
        string root = MonitorSetup.FindRepositoryRoot();
        return MonitorSetup.Load(root, Path.Combine(root, "gaming/tests/Fixtures/monitor-heat.json"));
    }
    [Fact] public void InputsRoundTripAndRejectUnknownFieldsOrMissingRequiredFlags()
    {
        var setup = Setup();
        var input = MonitorInputs.Default(setup.Config);
        string json = JsonSerializer.Serialize(input, MonitorInputs.JsonOptions);
        Assert.Contains("\"selected_hour_local\"", json);
        Assert.DoesNotContain("SelectedHourLocal", json);
        Assert.Equal(input, JsonSerializer.Deserialize<MonitorInputs>(json, MonitorInputs.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MonitorInputs>(json.Replace("{", "{\"command\":\"stop\","), MonitorInputs.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MonitorInputs>("{}", MonitorInputs.JsonOptions));
    }
    [Fact] public void InvalidVersionsCountsModesAndHoursFailClosed()
    {
        var setup = Setup();
        var input = MonitorInputs.Default(setup.Config);
        foreach (var invalid in new[] { input with { SchemaVersion = 2 }, input with { InitialReservations = -1 },
            input with { InitialReservations = MonitorInputs.MaximumReservations + 1 }, input with { Mode = "other" },
            input with { Mode = "live" }, input with { SelectedHourLocal = null },
            input with { SelectedHourLocal = "2025-07-15T18:30" }, input with { SelectedHourLocal = "1999-01-01T00:00" } })
            Assert.Throws<ArgumentException>(() => invalid.Validate(setup.Axis));
        (input with { Mode = "live", SelectedHourLocal = null }).Validate(setup.Axis);
    }
    [Fact] public void SnapshotUsesExistingViewsAndDoesNotTurnAbsentObservationsIntoZero()
    {
        var setup = Setup();
        var input = MonitorInputs.Default(setup.Config) with { InitialReservations = 3, BypassCapabilities = true };
        var hour = setup.ReplayHour(setup.IndexOf(input.SelectedHourLocal!));
        var snapshot = MonitorSnapshotBuilder.Build(setup, input, hour, new Dictionary<string, DockerNodeState>(), null, DateTimeOffset.UnixEpoch);
        var engine = setup.CreateEngine(); engine.Begin(hour);
        foreach (var node in setup.Config.Nodes) engine.SetReady(node.NodeId, true);
        for (int i = 0; i < input.InitialReservations; i++) engine.Reserve();
        Assert.Equal(engine.Sessions.Count, snapshot.PlacedReservations);
        foreach (var view in engine.SiteViews())
        {
            var site = snapshot.Sites.Single(s => s.SiteId == view.Site.Id);
            Assert.Equal(view.Preview.BackupHeatKwh, site.BackupHeatKwh.Value);
            Assert.Equal(view.FreeCapacityKwh, site.HeatHeadroomKwh.Value);
            Assert.Equal(view.GenerationKwh, site.PlannedHeatKwh.Value);
            Assert.Equal("kWh", site.PlannedHeatKwh.Unit);
            Assert.Equal("model", site.PlannedHeatKwh.Kind);
        }
        Assert.All(snapshot.Nodes, n => { Assert.Null(n.ContainerRunning); Assert.Null(n.CpuPercent); Assert.Null(n.ObservedAtUtc); });
        Assert.Equal("assumption", snapshot.SessionHeatKwhPerHour.Kind);
        Assert.Contains(snapshot.Warnings, w => w.Contains("empty"));
        string json = JsonSerializer.Serialize(snapshot, MonitorInputs.JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<MonitorSnapshot>(json, MonitorInputs.JsonOptions)!;
        Assert.Equal(snapshot.Sites[0].HeatHeadroomKwh.Value, roundTrip.Sites[0].HeatHeadroomKwh.Value);
        Assert.DoesNotContain("[bold]", json);
    }
    [Fact] public void DockerErrorIsUnknownAndDoesNotAdmitClaims()
    {
        var setup = Setup(); var input = MonitorInputs.Default(setup.Config) with { ObserveDocker = true, BypassCapabilities = true, InitialReservations = 1 };
        var unavailable = setup.Config.Nodes.ToDictionary(n => n.NodeId, _ => new DockerNodeState(false, null, "docker error: fixture"));
        var hour = setup.ReplayHour(setup.IndexOf(input.SelectedHourLocal!));
        var snapshot = MonitorSnapshotBuilder.Build(setup, input, hour, unavailable, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Equal(0, snapshot.PlacedReservations);
        Assert.All(snapshot.Nodes, n => { Assert.Null(n.ContainerRunning); Assert.Equal("error", n.ObservationStatus); Assert.False(n.ReadyForNewClaims); });
    }
    [Fact] public void SnapshotRejectsResolvedHourThatDoesNotMatchRequest()
    {
        var setup = Setup(); var input = MonitorInputs.Default(setup.Config);
        Assert.Throws<ArgumentException>(() => MonitorSnapshotBuilder.Build(setup, input,
            setup.ReplayHour(setup.IndexOf(input.SelectedHourLocal!) + 1), new Dictionary<string, DockerNodeState>(), null, DateTimeOffset.UnixEpoch));
    }
    private sealed class FailedProcessProbe : DigitalHeat.Gaming.IDockerCommandRunner
    {
        public string Run(IReadOnlyList<string> args)
        {
            if (args.Contains("inspect")) return "true false";
            if (args.Contains("top")) throw new InvalidOperationException("Synthetic process probe failure");
            if (args.Contains("stats")) return "";
            return "aaaaaaaaaaaa";
        }
    }
    [Fact] public void FailedProcessProbeIsUnknownRatherThanConfirmedAbsent()
    {
        var result = new DockerProbe(Setup(), new FailedProcessProbe()).Probe();
        Assert.True(result["session-c"].ContainerRunning);
        Assert.Null(result["session-c"].ApplicationRunning);
        Assert.Contains("application probe unavailable", result["session-c"].Detail);
    }
}
