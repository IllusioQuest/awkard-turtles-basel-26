using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.YearRun;

/// <summary>
/// T-70 server-count report for the reference house: one physical sweep (Core ServerSizing), then
/// the optimum for every hardware profile in config/hardware at each of its sourced price points
/// plus the team-adopted case price. Variant only: the scored figures keep the case server.
/// </summary>
public static class SizingReport
{
    public static void Write(string path, string root, IReadOnlyList<DemandHour> demand, ReferenceParameters p)
    {
        var rows = ServerSizing.Sweep(demand, p);
        double elec = p.ElectricityPriceChfPerKwh.Value;
        var scenarios = new List<object>();
        foreach (var hw in HardwareProfile.LoadDirectory(Path.Combine(root, "config", "hardware")))
        {
            if (hw.ServerElectricKw.Value != p.ServerElectricKw.Value || hw.HeatRecoveryShare.Value != p.HeatRecoveryShare.Value)
                throw new InvalidDataException($"{hw.Id}: sizing assumes the case's 1 kW, 0.8 recovery server.");
            if (hw.OpsChfPerServerYear.Value is null) continue;
            var prices = new (string Label, double? Value, string Source)[]
            {
                ("adopted", p.ComputePriceChfPerServerHour.Value, p.ComputePriceChfPerServerHour.Source),
                ("spot", hw.SpotPriceChfPerServerHour.Value, hw.SpotPriceChfPerServerHour.Source),
                ("home_host", hw.HomeHostPriceChfPerServerHour.Value, hw.HomeHostPriceChfPerServerHour.Source),
                ("on_demand", hw.OnDemandPriceChfPerServerHour.Value, hw.OnDemandPriceChfPerServerHour.Source),
            };
            foreach (var (label, price, source) in prices)
            {
                if (price is not { } value) continue;
                var e = SizingEconomics.From(hw, value, elec);
                var values = ServerSizing.Value(rows, e, p.HoursPerStep.Value);
                int n = ServerSizing.OptimalCount(values);
                scenarios.Add(new
                {
                    hardware_profile = hw.Id, price_label = label, price_chf_per_server_hour = value,
                    price_source = source, fixed_chf_per_server_year = e.FixedChfPerServerYear,
                    idle_electric_kw = e.IdleElectricKw,
                    break_even_server_hours_per_year = double.IsFinite(ServerSizing.BreakEvenServerHoursPerYear(e, p))
                        ? ServerSizing.BreakEvenServerHoursPerYear(e, p) : (double?)null,
                    optimal_server_count = n, contribution_at_optimum_chf = values[n].ContributionChf,
                    contribution_at_4_servers_chf = values.Count > 4 ? values[4].ContributionChf : (double?)null,
                    values
                });
            }
        }
        var report = new
        {
            ticket = "T-70", method = "docs/results/server-sizing.md",
            note = "Variant analysis. Unlimited sellable, pausable work (upper bound on sold hours); fixed cost = purchase / lifetime + ops per server-year; idle draw charged at the electricity price when sourced, idle heat not credited. Not a full-cost claim: installation, heating integration and the host/operator split are excluded.",
            inputs = new
            {
                weather = "data/raw/open-meteo/basel-2025-hourly.csv", config = "config/reference-house.json",
                hardware = "config/hardware/*.json", scheduler = "Core HeatFirstScheduler, minimum load 0"
            },
            diminishing_returns = ServerSizing.HasDiminishingReturns(rows),
            rows, scenarios
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) + "\n");
        Console.WriteLine($"Wrote sizing sweep n = 0..{rows[^1].ServerCount} and {scenarios.Count} price scenarios to {path}");
        foreach (dynamic s in scenarios)
            Console.WriteLine($"  {s.hardware_profile,-30} {s.price_label,-10} {s.price_chf_per_server_hour,6:F2} CHF/h -> n* = {s.optimal_server_count,2}, CHF {s.contribution_at_optimum_chf,9:F0}/yr");
    }
}
