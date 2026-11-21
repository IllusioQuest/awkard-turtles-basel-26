using DigitalHeat.Core;
using DigitalHeat.Network;

namespace DigitalHeat.Gaming.Monitor;

/// <summary>
/// Everything loaded once at start: the configuration, the network scenario sites the nodes sit at
/// (via the existing <see cref="NetworkScenarioLoader"/>), the case reference parameters and the
/// runtime profiles. Builds replay hours from the scenario's hourly demand.
/// </summary>
public sealed class MonitorSetup
{
    public required string RepositoryRoot { get; init; }
    public required MonitorConfig Config { get; init; }
    public required IReadOnlyDictionary<string, NetworkSite> NetworkSites { get; init; }
    public required ReferenceParameters Reference { get; init; }
    public required global::DigitalHeat.Gaming.RuntimeProfiles Profiles { get; init; }
    public required JobPlacementPolicy PlayerPolicy { get; init; }
    /// <summary>Node ids whose declared hardware lacks the application's required capabilities.</summary>
    public required IReadOnlyDictionary<string, string> IncompatibleNodes { get; init; }
    /// <summary>Time axis of the scenario (identical for all sites, 8,760 local hours).</summary>
    public required IReadOnlyList<string> Axis { get; init; }

    public static MonitorSetup Load(string repositoryRoot, string configPath)
    {
        var config = MonitorConfig.Load(configPath);
        string Full(string relative) => Path.GetFullPath(Path.Combine(repositoryRoot, relative));
        var scenario = NetworkScenarioLoader.Load(Full(config.Scenario), repositoryRoot);
        var byId = scenario.NetworkSites.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var siteIds = config.Nodes.Select(n => n.SiteId).Append(config.PlayerOriginSiteId).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var id in siteIds)
            if (!byId.ContainsKey(id)) throw new InvalidDataException($"Site '{id}' is not in scenario {config.Scenario}.");
        var monitored = config.Nodes.Select(n => n.SiteId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => byId[id], StringComparer.Ordinal);

        var axis = monitored.Values.First().Demand.Select(h => h.LocalTime).ToArray();
        foreach (var site in monitored.Values)
            if (!site.Demand.Select(h => h.LocalTime).SequenceEqual(axis))
                throw new InvalidDataException($"Site '{site.Id}' has a different time axis.");
        foreach (var label in new[] { config.ReplayStartLocal, config.WinterJumpLocal, config.SummerJumpLocal })
            if (Array.IndexOf(axis, label) < 0) throw new InvalidDataException($"Hour '{label}' is not in the scenario year.");

        var profiles = global::DigitalHeat.Gaming.RuntimeProfiles.Load(Full(config.RuntimeProfiles));
        var app = profiles.Applications.SingleOrDefault(a => a.Id == config.ApplicationId)
            ?? throw new InvalidDataException($"Application '{config.ApplicationId}' is not in {config.RuntimeProfiles}.");
        var incompatible = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in config.Nodes)
        {
            var profile = profiles.Nodes.SingleOrDefault(p => p.Id == node.NodeId)
                ?? throw new InvalidDataException($"Node '{node.NodeId}' is not in {config.RuntimeProfiles}.");
            var hardware = profiles.HardwareProfiles.Single(h => h.Id == profile.HardwareProfileId);
            var missing = app.RequiredCapabilities.Except(hardware.Capabilities, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0) incompatible[node.NodeId] = "lacks " + string.Join(", ", missing);
        }

        var originIndex = scenario.Sites.ToList().FindIndex(s => s.Id == config.PlayerOriginSiteId);
        return new MonitorSetup
        {
            RepositoryRoot = repositoryRoot,
            Config = config,
            NetworkSites = monitored,
            Reference = ReferenceParameters.Load(Full(config.ReferenceParameters)),
            Profiles = profiles,
            PlayerPolicy = scenario.Sites[originIndex].Placement,
            IncompatibleNodes = incompatible,
            Axis = axis,
        };
    }

    public HeatMonitorEngine CreateEngine() => new(
        NetworkSites.Values.Select(s => new MonitorSite(s.Id, s.Name, s.CountryCode, s.ServerCount,
            s.RecoverableKwhPerServerHour, s.TankCapacityKwh)),
        Config.Nodes.Select(n => new MonitorNodeInfo(n.NodeId, n.SiteId, n.SessionSlots)),
        Config.SessionHeatKwhPerHour.Value, Config.PlayerOriginSiteId, PlayerPolicy);

    public int IndexOf(string localLabel)
    {
        int i = Axis is string[] a ? Array.IndexOf(a, localLabel) : Axis.ToList().IndexOf(localLabel);
        if (i < 0) throw new ArgumentException($"Hour '{localLabel}' is not in the scenario year.");
        return i;
    }

    /// <summary>Offline replay of one scenario hour (default mode, deterministic).</summary>
    public HourInputs ReplayHour(int index)
    {
        if (index < 0 || index >= Axis.Count) throw new ArgumentOutOfRangeException(nameof(index));
        string label = Axis[index];
        var sites = NetworkSites.Values.ToDictionary(s => s.Id, s =>
        {
            var d = s.Demand[index];
            return new SiteHour(d, "weather-derived model, " + label[..4] + " (" + Path.GetFileName(Config.Scenario) + ")",
                double.IsFinite(d.TemperatureC) ? d.TemperatureC : null, s.ElectricityPriceChfPerKwh,
                s.HeatValueChfPerKwh, "scenario (committed ElCom snapshot)");
        }, StringComparer.Ordinal);
        return new HourInputs("replay", label, Config.DatasetHourToUtc(label), sites);
    }

    /// <summary>Index of the scenario hour with the same month, day and hour (29 Feb → 28 Feb).</summary>
    public int SameCalendarHourIndex(string localLabel)
    {
        string monthDayHour = localLabel[5..13];
        if (monthDayHour.StartsWith("02-29", StringComparison.Ordinal)) monthDayHour = "02-28" + monthDayHour[5..];
        for (int i = 0; i < Axis.Count; i++)
            if (string.CompareOrdinal(Axis[i], 5, monthDayHour, 0, monthDayHour.Length) == 0) return i;
        throw new ArgumentException($"No scenario hour for {localLabel}.");
    }

    /// <summary>Finds the repository root (the folder with config/reference-house.json).</summary>
    public static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (DirectoryInfo? d = new(start); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found; pass --root <path>.");
    }
}
