using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.Fleet;

/// <summary>
/// House generator (§4.2): multinomial draw over the T-51 grid cells weighted by single-family building count,
/// position = cell centre + uniform offset within ±5 km, design power from the cell's median single-family heat
/// demand, electricity price per municipality (ElCom 2026).
/// </summary>
public static class HouseGenerator
{
    public const string HeatDemandPath = "data/derived/buildings/grid-10km-heat-demand.csv";
    public const string ElcomPath = "data/derived/elcom/price-per-municipality-2026.csv";
    public const string MunicipalityPath = "data/derived/buildings/municipality-buildings-heat.csv";
    public const long DefaultSeed = 20261121;
    public const string DefaultElectricityCategory = "H4";
    /// <summary>Half the grid spacing: cells are 10 km apart (data/raw/swiss-grid/SOURCE.md, regular grid every 10 km).</summary>
    public const double HalfCellKm = 5.0;
    /// <summary>IUGG mean Earth radius (km); one degree of latitude = R × π ÷ 180 ≈ 111.195 km (spherical approximation).</summary>
    public const double EarthMeanRadiusKm = 6371.0088;
    public const double KmPerDegreeLatitude = EarthMeanRadiusKm * Math.PI / 180.0;
    /// <summary>Tank starts empty as in Core (HeatBalance reference run, docs/model/fleet-simulation.md §4.2).</summary>
    public const double DefaultTankInitialKwh = 0;
    public const string DistributionProportional = "proportional_to_single_family";

    private sealed record Cell(string PointId, double LatDeg, double LonDeg, int Bfs, string Canton, double SingleFamily, double? MedianKwhPerYr);

    public static (IReadOnlyList<HouseSpec> Houses, JsonElement ResolvedSpec) Generate(JsonElement spec, string repoRoot)
    {
        var baseline = ReferenceParameters.Load(Path.Combine(repoRoot, HouseDemandBuilder.ReferenceHouseConfig));
        int count = (int)Num(spec, "count");
        if (count <= 0) throw new InvalidDataException("spec.count must be > 0.");
        string distribution = Str(spec, "distribution", DistributionProportional);
        if (distribution != DistributionProportional) throw new InvalidDataException($"Unknown distribution '{distribution}'.");
        long seed = spec.TryGetProperty("seed", out var sd) ? sd.GetInt64() : DefaultSeed;
        string[]? cantons = spec.TryGetProperty("cantons", out var cs) && cs.ValueKind == JsonValueKind.Array
            ? cs.EnumerateArray().Select(x => x.GetString()!).ToArray() : null;
        double spread = spec.TryGetProperty("design_heating_spread", out var sp) ? sp.GetDouble() : 0;
        if (spread < 0) throw new InvalidDataException("design_heating_spread must be >= 0.");
        double divisor = Opt(spec, "formula_divisor_c", baseline.FormulaDivisorC.Value);
        double hotWater = Opt(spec, "hot_water_kwh_per_day", baseline.HotWaterKwhPerDay.Value);
        double tank = Opt(spec, "tank_kwh", baseline.TankCapacityKwh.Value);
        double tankInitial = Opt(spec, "tank_initial_kwh", DefaultTankInitialKwh);

        double? fixedDesign = null;
        if (spec.TryGetProperty("design_heating_kw", out var dk))
        {
            if (dk.ValueKind == JsonValueKind.Number) fixedDesign = dk.GetDouble();
            else if (dk.ValueKind == JsonValueKind.Object) fixedDesign = Num(dk, "fixed");
            else if (dk.GetString() != "cell_median") throw new InvalidDataException("design_heating_kw: 'cell_median' or {fixed: x}.");
        }
        double? fixedPrice = null; string category = DefaultElectricityCategory;
        if (spec.TryGetProperty("electricity_price", out var ep))
        {
            if (ep.ValueKind == JsonValueKind.Number) fixedPrice = ep.GetDouble();
            else if (ep.ValueKind == JsonValueKind.Object)
            {
                if (ep.TryGetProperty("fixed", out var fx)) fixedPrice = fx.GetDouble();
                else category = Str(ep, "category", DefaultElectricityCategory);
            }
            else if (ep.GetString() != "per_municipality") throw new InvalidDataException("electricity_price: 'per_municipality' or {fixed: x}.");
        }

        // cells
        var grid = GridWeather.ReadGrid(Path.Combine(repoRoot, GridWeather.GridPath));
        var heat = CsvTable.Read(Path.Combine(repoRoot, HeatDemandPath));
        var cells = new List<Cell>();
        foreach (var r in heat.Rows)
        {
            string id = heat.Str(r, "point_id");
            double sf = heat.Num(r, "n_single_family");
            if (sf <= 0 || !grid.TryGetValue(id, out var g)) continue;
            if (cantons is not null && !cantons.Contains(g.Canton)) continue;
            // A cell without a median (no heat-demand estimate) cannot get a cell_median design power.
            string med = heat.Str(r, "single_family_heat_median_kwh_per_yr");
            double? median = med.Length == 0 ? null : heat.Num(r, "single_family_heat_median_kwh_per_yr");
            if (median is null && fixedDesign is null) continue;
            cells.Add(new Cell(id, g.LatDeg, g.LonDeg, g.BfsMunicipalityNr, g.Canton, sf, median));
        }
        if (cells.Count == 0) throw new InvalidDataException("No grid cell matches the cantons filter.");
        double total = cells.Sum(c => c.SingleFamily), acc = 0;
        var cumulative = cells.Select(c => acc += c.SingleFamily / total).ToArray();

        // electricity
        Func<Cell, double> priceOf;
        int fallbackCount = 0;
        if (fixedPrice is { } fp) priceOf = _ => fp;
        else
        {
            var (byBfs, cantonMedian) = LoadElectricity(repoRoot, category);
            priceOf = c =>
            {
                if (byBfs.TryGetValue(c.Bfs, out var p)) return p;
                fallbackCount++;
                return cantonMedian.TryGetValue(c.Canton, out var m) ? m
                    : throw new InvalidDataException($"No ElCom price for municipality {c.Bfs} and no canton median for {c.Canton}.");
            };
        }

        // draw, then design power for the cells that were drawn
        var draws = Draw(new Rng(seed), cells, cumulative, count, spread);
        var designByCell = new Dictionary<string, double>();
        if (fixedDesign is null)
        {
            var store = new WeatherStore(repoRoot);
            store.Preload(draws.Select(d => cells[d.Cell].PointId).Distinct().ToList(), []);
            // Space heat is linear in the design power, so one division gives the design power that yields the median.
            var unit = HouseDemandBuilder.ParametersFor(baseline, 1.0, divisor, 0, 0);
            foreach (var ci in draws.Select(d => d.Cell).Distinct())
            {
                var c = cells[ci];
                var w = store.Actual(c.PointId);
                var hours = HeatDemand.Build(w.TimeLocal.Zip(w.TemperatureC, (t, x) => new WeatherHour(t, x)).ToList(), unit);
                double spaceKwhPerKw = hours.Sum(h => h.SpaceHeatKwh);
                if (spaceKwhPerKw <= 0) throw new InvalidDataException($"Cell {c.PointId} has no space heat demand.");
                designByCell[c.PointId] = c.MedianKwhPerYr!.Value / spaceKwhPerKw;
            }
        }
        return Finish(draws, cells, designByCell, fixedDesign, priceOf, divisor, hotWater, tank, tankInitial, count, seed, cantons,
            spread, category, fixedPrice, () => fallbackCount);
    }

    private sealed record DrawResult(int Cell, double LatDeg, double LonDeg, double SpreadFactor);

    private static List<DrawResult> Draw(Rng rng, List<Cell> cells, double[] cumulative, int count, double spread)
    {
        double sigmaLn = Math.Sqrt(Math.Log(1 + spread * spread)); // lognormal with mean 1 and relative SD = spread
        var result = new List<DrawResult>(count);
        for (int i = 0; i < count; i++)
        {
            double u = rng.NextDouble();
            int ci = Array.FindIndex(cumulative, c => u < c);
            if (ci < 0) ci = cells.Count - 1;
            var c = cells[ci];
            double dyKm = rng.Uniform(-HalfCellKm, HalfCellKm), dxKm = rng.Uniform(-HalfCellKm, HalfCellKm);
            double lat = c.LatDeg + dyKm / KmPerDegreeLatitude;
            double lon = c.LonDeg + dxKm / (KmPerDegreeLatitude * Math.Cos(c.LatDeg * Math.PI / 180.0));
            double factor = spread > 0 ? Math.Exp(sigmaLn * rng.Normal() - sigmaLn * sigmaLn / 2) : 1;
            result.Add(new DrawResult(ci, lat, lon, factor));
        }
        return result;
    }

    private static (IReadOnlyList<HouseSpec>, JsonElement) Finish(List<DrawResult> draws, List<Cell> cells,
        Dictionary<string, double> designByCell, double? fixedDesign, Func<Cell, double> priceOf, double divisor, double hotWater,
        double tank, double tankInitial, int count, long seed, string[]? cantons, double spread, string category,
        double? fixedPrice, Func<int> fallbackCount)
    {
        var houses = new List<HouseSpec>(draws.Count);
        for (int i = 0; i < draws.Count; i++)
        {
            var d = draws[i];
            var c = cells[d.Cell];
            double design = (fixedDesign ?? designByCell[c.PointId]) * d.SpreadFactor;
            houses.Add(new HouseSpec("h" + (i + 1).ToString("D5", CultureInfo.InvariantCulture), d.LatDeg, d.LonDeg, c.PointId, c.Bfs,
                c.Canton, c.PointId, design, divisor, hotWater, tank, tankInitial, priceOf(c), null));
        }
        var resolved = new Dictionary<string, object?>
        {
            ["count"] = count,
            ["distribution"] = DistributionProportional,
            ["cantons"] = cantons,
            ["weather"] = GridWeather.ArchivePath,
            ["design_heating_kw"] = fixedDesign is { } f ? new Dictionary<string, object> { ["fixed"] = f } : "cell_median",
            ["design_heating_spread"] = spread,
            ["formula_divisor_c"] = divisor,
            ["hot_water_kwh_per_day"] = hotWater,
            ["tank_kwh"] = tank,
            ["tank_initial_kwh"] = tankInitial,
            ["electricity_price"] = fixedPrice is { } p
                ? new Dictionary<string, object> { ["fixed"] = p }
                : new Dictionary<string, object> { ["per_municipality"] = ElcomPath, ["category"] = category },
            ["seed"] = seed,
            ["resolved"] = new Dictionary<string, object>
            {
                ["cells_available"] = cells.Count,
                ["cells_used"] = draws.Select(x => x.Cell).Distinct().Count(),
                ["electricity_canton_median_fallbacks"] = fallbackCount()
            }
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(resolved));
        return (houses, doc.RootElement.Clone());
    }

    /// <summary>Price per BFS municipality of the category, and the median price per canton code (fallback).</summary>
    private static (Dictionary<int, double> ByBfs, Dictionary<string, double> CantonMedian) LoadElectricity(string repoRoot, string category)
    {
        var t = CsvTable.Read(Path.Combine(repoRoot, ElcomPath));
        var byBfs = new Dictionary<int, double>();
        foreach (var r in t.Rows)
            if (t.Str(r, "category") == category)
                byBfs[int.Parse(t.Str(r, "bfs_municipality_nr"), CultureInfo.InvariantCulture)] = t.Num(r, "price_chf_per_kwh");
        if (byBfs.Count == 0) throw new InvalidDataException($"No ElCom rows for category {category}.");
        var muni = CsvTable.Read(Path.Combine(repoRoot, MunicipalityPath));
        var perCanton = new Dictionary<string, List<double>>();
        foreach (var r in muni.Rows)
        {
            int bfs = int.Parse(muni.Str(r, "bfs_municipality_nr"), CultureInfo.InvariantCulture);
            if (!byBfs.TryGetValue(bfs, out var price)) continue;
            string canton = muni.Str(r, "canton");
            if (!perCanton.TryGetValue(canton, out var list)) perCanton[canton] = list = [];
            if (muni.Str(r, "building_class") == "all_residential") list.Add(price);
        }
        return (byBfs, perCanton.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => Median(kv.Value)));
    }

    private static double Median(List<double> v)
    {
        var s = v.OrderBy(x => x).ToList();
        int n = s.Count;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
    }

    private static double Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble()
            : throw new InvalidDataException($"spec: '{name}' must be a number.");

    private static double Opt(JsonElement e, string name, double dflt) => e.TryGetProperty(name, out var v) ? v.GetDouble() : dflt;

    private static string Str(JsonElement e, string name, string dflt) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : dflt;
}
