using DigitalHeat.Core;
using DigitalHeat.Gaming.Monitor;
using DigitalHeat.Network;
using Xunit;

public class HeatMonitorTests
{
    private static readonly DateTimeOffset Hour0 = new(2025, 1, 15, 17, 0, 0, TimeSpan.Zero);
    private static readonly JobPlacementPolicy Swiss = new("country-allowlist", "Synthetic test policy", ["CH"]);

    // Synthetic sites: values chosen for readable arithmetic, not sourced figures.
    private static readonly MonitorSite Home = new("home", "Home", "CH", 4, 0.8, 2);
    private static readonly MonitorSite Hotel = new("hotel", "Hotel", "CH", 28, 0.8, 10);

    private static HeatMonitorEngine Engine(int slots = 2) => new([Home, Hotel],
        [new("node-home", "home", slots), new("node-hotel", "hotel", slots)], 0.8, "home", Swiss);

    private static HourInputs Hour(DateTimeOffset start, double homeNeed, double hotelNeed) => new("replay",
        start.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture), start, new Dictionary<string, SiteHour>
        {
            ["home"] = new(new DemandHour("t", 0, true, homeNeed, 0), "test", null, 0.3, 0.12, "test"),
            ["hotel"] = new(new DemandHour("t", 0, true, hotelNeed, 0), "test", null, 0.3, 0.12, "test"),
        });

    private static HeatMonitorEngine Started(double homeNeed, double hotelNeed, int slots = 2)
    {
        var e = Engine(slots);
        e.Begin(Hour(Hour0, homeNeed, hotelNeed));
        e.SetReady("node-home", true);
        e.SetReady("node-hotel", true);
        return e;
    }

    [Fact] public void ReservesAtSiteWithLargestFreeHeatShareAndCountsGenerationPerNode()
    {
        // Decision 0053. Free: home 1+2=3, hotel 5+10=15 kWh. Both start at share 1.0, so the tie goes to
        // more free kWh (hotel). Then hotel has 14.2 / 15 = 0.95 free and home 1.0, so home is next.
        var e = Started(homeNeed: 1, hotelNeed: 5);
        Assert.Equal("node-hotel", e.Reserve()!.NodeId);
        Assert.Equal("node-home", e.Reserve()!.NodeId);
        var hotel = e.NodeViews().Single(n => n.Node.NodeId == "node-hotel");
        Assert.Equal(0.8, hotel.GenerationKwh, 9);
        Assert.Equal(new[] { "player-1" }, hotel.Sessions);
        Assert.Equal(15 - 0.8, hotel.SiteFreeCapacityKwh, 9);
        Assert.Equal(0.8, e.NodeViews().Single(n => n.Node.NodeId == "node-home").GenerationKwh, 9);
    }

    [Fact] public void NotReadyNodesGetNoNewSessionsButKeepHeldOnes()
    {
        var e = Started(1, 5);
        Assert.Equal("node-hotel", e.Reserve()!.NodeId);
        e.SetReady("node-hotel", false);
        Assert.Equal("node-home", e.Reserve()!.NodeId);
        Assert.Equal(0.8, e.NodeViews().Single(n => n.Node.NodeId == "node-hotel").GenerationKwh, 9);
    }

    [Fact] public void AdvanceUsesCoreHeatBalanceAndKeepsSessionsPinned()
    {
        var e = Started(homeNeed: 0.5, hotelNeed: 0, slots: 1);
        e.SetReady("node-hotel", false);
        Assert.Equal("node-home", e.Reserve()!.NodeId);     // 0.8 produced, 0.5 needed, 0.3 to tank
        var expected = HeatBalance.Step(0.8, 0.5, 0, Home.TankCapacityKwh);
        var steps = e.Advance(Hour(Hour0.AddHours(1), 0, 50));
        Assert.Equal(expected, steps["home"]);
        var home = e.SiteViews().Single(s => s.Site.Id == "home");
        Assert.Equal(0.3, home.TankStoredKwh, 9);
        Assert.Equal(0.8, home.GenerationKwh, 9);           // not moved to the hotel despite its demand
        Assert.Equal(1, home.Totals.Hours);
        Assert.Equal(0.8, home.Totals.UsefulKwh, 9);
    }

    [Fact] public void TankFullDumpsExcessInPreview()
    {
        var e = Started(homeNeed: 0, hotelNeed: 0, slots: 5);
        e.SetReady("node-hotel", false);
        for (int i = 0; i < 2; i++) Assert.NotNull(e.Reserve()); // 1.6 kWh into a 2 kWh tank
        Assert.Null(e.Reserve());                                 // 2.4 > 2 kWh capacity: refused
        e.Advance(Hour(Hour0.AddHours(1), 0, 0));
        var home = e.SiteViews().Single(s => s.Site.Id == "home");
        Assert.Equal(1.6, home.TankStoredKwh, 9);
        Assert.Equal(1.2, home.Preview.DumpedHeatKwh, 9);          // held sessions stay; 0.4 fits, 1.2 dumped
        Assert.Equal(0, home.FreeCapacityKwh);
    }

    [Fact] public void AdvanceRefusesToGoBackInTime()
    {
        var e = Started(1, 1);
        Assert.Throws<InvalidOperationException>(() => e.Advance(Hour(Hour0, 1, 1)));
    }

    [Fact] public void BeginReplacesPlayersInOrderAndReportsDropped()
    {
        var e = Started(homeNeed: 1, hotelNeed: 1, slots: 1);
        Assert.NotNull(e.Reserve());
        Assert.NotNull(e.Reserve());
        var memory = e.Memory;
        var dropped = e.Begin(Hour(Hour0.AddHours(-5), homeNeed: 0, hotelNeed: 0.5), memory); // home 0+2, hotel 0.5+10
        Assert.Empty(dropped);
        Assert.Equal(new[] { "player-1", "player-2" }, e.Sessions);
        e.SetReady("node-home", false);
        dropped = e.Begin(Hour(Hour0, 1, 1));
        Assert.Equal(new[] { "player-2" }, dropped);                // only one ready slot left
        Assert.Equal(new[] { "player-1" }, e.Sessions);
    }

    [Fact] public void ReleaseLastFreesGeneration()
    {
        var e = Started(1, 5);
        e.Reserve(); e.Reserve();
        Assert.Equal("player-2", e.ReleaseLast());
        Assert.Equal(0.8, e.SiteViews().Sum(s => s.GenerationKwh), 9);
        Assert.Equal("player-3", e.Reserve()!.SessionId);  // ids are never reused within a run
    }

    // ---- configuration and real repository data ----

    private static string Root() => MonitorSetup.FindRepositoryRoot();
    private static string ConfigPath() => Path.Combine(Root(), "gaming", "deploy", "heat-monitor.json");

    [Fact] public void CheckedInSetupMapsNodesToScenarioSitesAndCapabilities()
    {
        var setup = MonitorSetup.Load(Root(), ConfigPath());
        Assert.Equal(new[] { "basel-reference-house-case-1", "basel-mfh-1", "basel-hotel-spa-1" }, setup.NetworkSites.Keys);
        Assert.Equal(0.8, setup.Config.SessionHeatKwhPerHour.Value);
        // Runtime profiles: only session-c declares Xonotic (docs/gaming-profiles.md).
        Assert.True(setup.IncompatibleNodes.ContainsKey("session-a"));
        Assert.True(setup.IncompatibleNodes.ContainsKey("session-b"));
        Assert.False(setup.IncompatibleNodes.ContainsKey("session-c"));
    }

    [Fact] public void ReplayHourMatchesScenarioDemandFile()
    {
        var setup = MonitorSetup.Load(Root(), ConfigPath());
        var csv = NetworkScenarioLoader.ReadDemandCsv(Path.Combine(Root(), "data", "derived", "network",
            "switzerland-test", "basel-hotel-spa-1-demand.csv"));
        int i = setup.IndexOf("2025-07-15T18:00");
        var hour = setup.ReplayHour(i);
        Assert.Equal(csv[i].TotalHeatKwh, hour.Sites["basel-hotel-spa-1"].Demand.TotalHeatKwh, 9);
        Assert.Equal(new DateTimeOffset(2025, 7, 15, 17, 0, 0, TimeSpan.Zero), hour.IntervalStartUtc);
        Assert.Equal(i, setup.SameCalendarHourIndex("2026-07-15T18:00"));
    }

    [Fact] public void CheckedInSetupStartsAndPlacesOnTheOnlyXonoticNode()
    {
        var setup = MonitorSetup.Load(Root(), ConfigPath());
        var e = setup.CreateEngine();
        e.Begin(setup.ReplayHour(setup.IndexOf(setup.Config.ReplayStartLocal)));
        foreach (var n in setup.Config.Nodes) e.SetReady(n.NodeId, !setup.IncompatibleNodes.ContainsKey(n.NodeId));
        Assert.Equal("session-c", e.Reserve()!.NodeId);
        Assert.Null(e.Reserve());                           // one desktop, one session slot
    }

    [Fact] public void ConfigRejectsUnsourcedSessionHeat()
    {
        var json = File.ReadAllText(ConfigPath()).Replace("docs/case/challenge.md: 1 kW", "TODO(source) 1 kW");
        var path = Path.Combine(Path.GetTempPath(), $"heat-monitor-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try { Assert.Throws<InvalidDataException>(() => MonitorConfig.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact] public void ConfigRejectsUnknownFields()
    {
        var json = File.ReadAllText(ConfigPath()).Replace("\"application_id\"", "\"unexpected\": 1, \"application_id\"");
        var path = Path.Combine(Path.GetTempPath(), $"heat-monitor-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try { Assert.ThrowsAny<Exception>(() => MonitorConfig.Load(path)); }
        finally { File.Delete(path); }
    }
}
