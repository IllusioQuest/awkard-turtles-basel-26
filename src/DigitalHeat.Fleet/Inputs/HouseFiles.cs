using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalHeat.Fleet;

/// <summary>A house file: the houses plus the generator spec and seed that made them (§4.2), if known.</summary>
public sealed record HouseFile(IReadOnlyList<HouseSpec> Houses, JsonElement? Spec, long? Seed);

/// <summary>Reads and writes house files (§4.1): <c>.json</c> ({spec, seed, houses}) and <c>.csv</c> (+ sidecar &lt;file&gt;.spec.json).</summary>
public static class HouseFiles
{
    public const string SidecarSuffix = ".spec.json";

    public static readonly string[] CsvColumns =
    [
        "house_id", "lat_deg", "lon_deg", "cell_id", "bfs_municipality_nr", "canton", "weather_ref",
        "design_heating_kw", "formula_divisor_c", "hot_water_kwh_per_day", "tank_kwh", "tank_initial_kwh",
        "electricity_price_chf_per_kwh", "server_count"
    ];

    private sealed class HouseDto
    {
        [JsonPropertyName("house_id")] public string HouseId { get; set; } = "";
        [JsonPropertyName("lat_deg")] public double LatDeg { get; set; }
        [JsonPropertyName("lon_deg")] public double LonDeg { get; set; }
        [JsonPropertyName("cell_id")] public string CellId { get; set; } = "";
        [JsonPropertyName("bfs_municipality_nr")] public int? BfsMunicipalityNr { get; set; }
        [JsonPropertyName("canton")] public string? Canton { get; set; }
        [JsonPropertyName("weather_ref")] public string WeatherRef { get; set; } = "";
        [JsonPropertyName("design_heating_kw")] public double DesignHeatingKw { get; set; }
        [JsonPropertyName("formula_divisor_c")] public double FormulaDivisorC { get; set; }
        [JsonPropertyName("hot_water_kwh_per_day")] public double HotWaterKwhPerDay { get; set; }
        [JsonPropertyName("tank_kwh")] public double TankKwh { get; set; }
        [JsonPropertyName("tank_initial_kwh")] public double TankInitialKwh { get; set; }
        [JsonPropertyName("electricity_price_chf_per_kwh")] public double ElectricityPriceChfPerKwh { get; set; }
        [JsonPropertyName("server_count")] public int? ServerCount { get; set; }

        public static HouseDto From(HouseSpec h) => new()
        {
            HouseId = h.HouseId, LatDeg = h.LatDeg, LonDeg = h.LonDeg, CellId = h.CellId, BfsMunicipalityNr = h.BfsMunicipalityNr,
            Canton = h.Canton, WeatherRef = h.WeatherRef, DesignHeatingKw = h.DesignHeatingKw, FormulaDivisorC = h.FormulaDivisorC,
            HotWaterKwhPerDay = h.HotWaterKwhPerDay, TankKwh = h.TankKwh, TankInitialKwh = h.TankInitialKwh,
            ElectricityPriceChfPerKwh = h.ElectricityPriceChfPerKwh, ServerCount = h.ServerCount
        };

        public HouseSpec ToSpec() => new(HouseId, LatDeg, LonDeg, CellId, BfsMunicipalityNr, Canton, WeatherRef, DesignHeatingKw,
            FormulaDivisorC, HotWaterKwhPerDay, TankKwh, TankInitialKwh, ElectricityPriceChfPerKwh, ServerCount);
    }

    private sealed class FileDto
    {
        [JsonPropertyName("spec")] public JsonElement? Spec { get; set; }
        [JsonPropertyName("seed")] public long? Seed { get; set; }
        [JsonPropertyName("houses")] public List<HouseDto> Houses { get; set; } = [];
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static HouseFile Read(string path)
    {
        if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return ReadCsv(path);
        var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options) ?? throw new InvalidDataException($"Empty {path}.");
        var houses = dto.Houses.Select(h => h.ToSpec()).ToList();
        Validate(houses, path);
        return new HouseFile(houses, dto.Spec?.Clone(), dto.Seed);
    }

    public static void Write(string path, IReadOnlyList<HouseSpec> houses, JsonElement? spec = null, long? seed = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) { WriteCsv(path, houses, spec, seed); return; }
        var dto = new FileDto { Spec = spec, Seed = seed, Houses = houses.Select(HouseDto.From).ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, Options) + "\n");
    }

    private static string R(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    private static void WriteCsv(string path, IReadOnlyList<HouseSpec> houses, JsonElement? spec, long? seed)
    {
        var lines = new List<string> { string.Join(',', CsvColumns) };
        foreach (var h in houses)
            lines.Add(string.Join(',', h.HouseId, R(h.LatDeg), R(h.LonDeg), h.CellId,
                h.BfsMunicipalityNr?.ToString(CultureInfo.InvariantCulture) ?? "", h.Canton ?? "", h.WeatherRef,
                R(h.DesignHeatingKw), R(h.FormulaDivisorC), R(h.HotWaterKwhPerDay), R(h.TankKwh), R(h.TankInitialKwh),
                R(h.ElectricityPriceChfPerKwh), h.ServerCount?.ToString(CultureInfo.InvariantCulture) ?? ""));
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        if (spec is not null || seed is not null)
            File.WriteAllText(path + SidecarSuffix, JsonSerializer.Serialize(new FileDto { Spec = spec, Seed = seed, Houses = [] }, Options) + "\n");
    }

    private static HouseFile ReadCsv(string path)
    {
        var t = CsvTable.Read(path);
        var houses = new List<HouseSpec>();
        foreach (var r in t.Rows)
        {
            string bfs = t.Str(r, "bfs_municipality_nr"), servers = t.Str(r, "server_count");
            string canton = t.Has("canton") ? t.Str(r, "canton") : "";
            houses.Add(new HouseSpec(t.Str(r, "house_id"), t.Num(r, "lat_deg"), t.Num(r, "lon_deg"), t.Str(r, "cell_id"),
                bfs.Length == 0 ? null : int.Parse(bfs, CultureInfo.InvariantCulture), canton.Length == 0 ? null : canton,
                t.Str(r, "weather_ref"), t.Num(r, "design_heating_kw"), t.Num(r, "formula_divisor_c"), t.Num(r, "hot_water_kwh_per_day"),
                t.Num(r, "tank_kwh"), t.Num(r, "tank_initial_kwh"), t.Num(r, "electricity_price_chf_per_kwh"),
                servers.Length == 0 ? null : int.Parse(servers, CultureInfo.InvariantCulture)));
        }
        Validate(houses, path);
        JsonElement? spec = null; long? seed = null;
        if (File.Exists(path + SidecarSuffix))
        {
            var side = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path + SidecarSuffix), Options);
            spec = side?.Spec?.Clone(); seed = side?.Seed;
        }
        return new HouseFile(houses, spec, seed);
    }

    private static void Validate(IReadOnlyList<HouseSpec> houses, string path)
    {
        if (houses.Select(h => h.HouseId).Distinct().Count() != houses.Count) throw new InvalidDataException($"{path}: duplicate house_id.");
        foreach (var h in houses)
            if (h.DesignHeatingKw < 0 || h.FormulaDivisorC <= 0 || h.HotWaterKwhPerDay < 0 || h.TankKwh < 0
                || h.TankInitialKwh < 0 || h.TankInitialKwh > h.TankKwh || h.ElectricityPriceChfPerKwh < 0
                || string.IsNullOrWhiteSpace(h.WeatherRef) || string.IsNullOrWhiteSpace(h.CellId))
                throw new InvalidDataException($"{path}: house {h.HouseId} has invalid values.");
    }
}
