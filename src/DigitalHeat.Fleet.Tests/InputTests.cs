using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace DigitalHeat.Fleet.Tests;

public class InputTests(ITestOutputHelper output)
{
    private static readonly string Root = Scenario.FindRepoRoot(AppContext.BaseDirectory);
    private static string P(string rel) => Path.Combine(Root, rel);

    private static readonly string[] TypeFiles =
    [
        "config/fleet/job-types/transcription-whisper-large-v3.json",
        "config/fleet/job-types/llm-inference-apertus-8b.json",
        "config/fleet/job-types/research-sim-montecarlo.json"
    ];

    private static JobType LoadType(int fileIndex, int index) => JobTypeLoader.Load(P(TypeFiles[fileIndex]), index, Root);

    private static HouseSpec ReferenceHouse() => HouseFiles.Read(P("config/fleet/houses/reference.json")).Houses.Single();

    // ------------------------------------------------------------------ calendar

    [Fact]
    public void CalendarMapsRunWindowToDataRows()
    {
        var full = RunCalendar.From(new RunSection());
        Assert.Equal(0, full.OffsetHour);
        Assert.Equal(8760, full.RunHours);
        Assert.Equal(8760L * FleetTime.MsPerHour, full.EndMs);
        Assert.Equal(2 * 24, full.StartHourOfWeek); // 2025-01-01 is a Wednesday, Monday = 0

        var feb = RunCalendar.From(new RunSection { StartLocal = "2025-02-03T06:00", EndLocal = "2025-02-04T00:00" });
        Assert.Equal(33 * 24 + 6, feb.OffsetHour); // Feb 3 is day 33 of the year (0-based), a Monday
        Assert.Equal(18, feb.RunHours);
        Assert.Equal(6, feb.StartHourOfWeek);

        Assert.Throws<ArgumentException>(() => RunCalendar.From(new RunSection { EndLocal = "2026-01-01T01:00" }));
        Assert.Throws<ArgumentException>(() => RunCalendar.From(new RunSection { StartLocal = "2024-12-31T00:00" }));
        Assert.Throws<ArgumentException>(() => RunCalendar.From(new RunSection { StartLocal = "2025-03-01T00:00", EndLocal = "2025-03-01T00:00" }));
    }

    // ------------------------------------------------------------------ case values

    [Fact]
    public void ReferenceHouseMatchesCaseValues()
    {
        var weather = new WeatherStore(Root);
        var builder = new HouseDemandBuilder(Root, weather);
        var demand = builder.Build(ReferenceHouse(), RunCalendar.From(new RunSection()));
        Assert.Equal(206, demand.HeatingDays);
        Assert.Equal(26850.5, demand.SpaceHeatKw.Sum(), 1);            // hourly steps: kW = kWh per step
        var p = builder.Baseline;
        double coldestServers = demand.SpaceHeatKw.Max() / (p.ServerElectricKw.Value * p.HeatRecoveryShare.Value);
        Assert.Equal(12.5, coldestServers, 6);
        Assert.Equal(19.2, 24 * p.ServerElectricKw.Value * p.HeatRecoveryShare.Value, 9);
        Assert.Equal(demand.HotWaterKw.Select((w, i) => w + demand.SpaceHeatKw[i]), demand.TotalKw);
    }

    [Fact]
    public void EqualHousesShareOneDemandInstance()
    {
        var weather = new WeatherStore(Root);
        var builder = new HouseDemandBuilder(Root, weather);
        var cal = RunCalendar.From(new RunSection { EndLocal = "2025-02-01T00:00" });
        var h = ReferenceHouse();
        var all = builder.BuildAll([h, h with { HouseId = "b" }, h with { HouseId = "c", DesignHeatingKw = 8 }], cal);
        Assert.Same(all[0], all[1]);
        Assert.NotSame(all[0], all[2]);
        Assert.Equal(all[0].SpaceHeatKw.Sum() * 0.8, all[2].SpaceHeatKw.Sum(), 6);
        Assert.Equal(31 * 24, all[0].TotalKw.Length);
    }

    [Fact]
    public void WindowSliceMatchesFullYear()
    {
        var weather = new WeatherStore(Root);
        var builder = new HouseDemandBuilder(Root, weather);
        var h = ReferenceHouse();
        var full = builder.Build(h, RunCalendar.From(new RunSection()));
        var cal = RunCalendar.From(new RunSection { StartLocal = "2025-01-10T05:00", EndLocal = "2025-01-12T00:00" });
        var part = builder.Build(h, cal);
        Assert.Equal(full.TotalKw.Skip(cal.OffsetHour).Take(cal.RunHours), part.TotalKw);
        Assert.Equal(2, part.HeatingDays); // the window touches Jan 10 (from 05:00) and Jan 11, both heating days
    }

    // ------------------------------------------------------------------ forecasts

    [Fact]
    public void ForecastProviders()
    {
        var sw = Stopwatch.StartNew();
        var weather = new WeatherStore(Root);
        var builder = new HouseDemandBuilder(Root, weather);
        var cal = RunCalendar.From(new RunSection());
        var h = ReferenceHouse();
        var houses = new[] { h };
        var demands = builder.BuildAll(houses, cal);
        var archived = Fleet.ForecastProviders.Create("archived", houses, cal, demands, weather, builder.Baseline);
        output.WriteLine($"archive + forecast gz load for 1 point: {sw.ElapsedMilliseconds} ms");
        var perfect = Fleet.ForecastProviders.Create("perfect", houses, cal, demands, weather, builder.Baseline);
        var persistence = Fleet.ForecastProviders.Create("persistence", houses, cal, demands, weather, builder.Baseline);

        int differing = 0;
        for (int t = 24; t < 24 * 60; t++)
        {
            Assert.Equal(demands[0].TotalKw[t], perfect.HeatDemandKw(new HouseId(0), t - 5, t));
            if (Math.Abs(archived.HeatDemandKw(new HouseId(0), t - 5, t) - demands[0].TotalKw[t]) > 1e-9) differing++;
            Assert.Equal(demands[0].TotalKw[t - 24], persistence.HeatDemandKw(new HouseId(0), t - 5, t));
        }
        Assert.True(differing > 100, $"archived lead-1 forecast should differ from actual (differing hours: {differing})");
        Assert.Equal(1, Fleet.ForecastProviders.LeadDays(10, 10));
        Assert.Equal(1, Fleet.ForecastProviders.LeadDays(10, 34));
        Assert.Equal(2, Fleet.ForecastProviders.LeadDays(10, 35));
        Assert.Equal(3, Fleet.ForecastProviders.LeadDays(10, 500));
        // beyond the data: clamps to the last hour instead of throwing
        Assert.True(archived.HeatDemandKw(new HouseId(0), 8700, 20000) >= 0);
        Assert.Equal(demands[0].TotalKw[^1], perfect.HeatDemandKw(new HouseId(0), 8700, 20000));
    }

    [Fact]
    public void GridWeatherLoadsRequestedPointsOnly()
    {
        var sw = Stopwatch.StartNew();
        var a = GridWeather.LoadArchive(P(GridWeather.ArchivePath), ["cap-bs", "g0000"]);
        output.WriteLine($"archive gz, 2 points: {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        var f = GridWeather.LoadForecast(P(GridWeather.ForecastPath), ["cap-bs"]);
        output.WriteLine($"forecast gz, 1 point: {sw.ElapsedMilliseconds} ms");
        Assert.Equal(2, a.Count);
        Assert.Equal(8760, a["cap-bs"].TemperatureC.Length);
        Assert.Equal("2025-01-01T00:00", a["cap-bs"].TimeLocal[0]);
        Assert.Equal(8760, f["cap-bs"].PreviousDay3C.Length);
        var grid = GridWeather.ReadGrid(P(GridWeather.GridPath));
        Assert.Equal(435, grid.Count);
        Assert.Equal("BS", grid["cap-bs"].Canton);
    }

    // ------------------------------------------------------------------ distributions

    [Fact]
    public void PoissonMeanAndVariance()
    {
        foreach (double lambda in new[] { 0.3, 4.0, 29.5, 30.0, 150.0 })
        {
            var rng = new Rng(7);
            const int n = 60_000;
            double sum = 0, sum2 = 0;
            for (int i = 0; i < n; i++) { double k = Poisson.Sample(rng, lambda); sum += k; sum2 += k * k; }
            double mean = sum / n, var = sum2 / n - mean * mean;
            Assert.InRange(mean, lambda - 5 * Math.Sqrt(lambda / n), lambda + 5 * Math.Sqrt(lambda / n));
            Assert.InRange(var, lambda * 0.93, lambda * 1.07);
        }
        Assert.Equal(0, Poisson.Sample(new Rng(1), 0));
    }

    [Fact]
    public void LogFactorialMatchesDirectSum()
    {
        double direct = 0;
        for (int i = 2; i <= 200; i++) { direct += Math.Log(i); if (i is 21 or 50 or 200) Assert.Equal(direct, Poisson.LogFactorial(i), 9); }
    }

    [Fact]
    public void QuantileSamplerHitsMedianAndMean()
    {
        var t = LoadType(1, 1);
        var q = Assert.IsType<QuantileDistribution>(t.Size);
        Assert.Equal(973, q.Inverse(0.5), 9);              // 932 in + 41 out at the median (llm-token-quantiles.csv)
        Assert.Equal(7387, q.Inverse(0.995), 9);           // capped at the 0.99 quantile
        Assert.Equal(1, q.Inverse(0), 9);                  // q = 0 -> min
        var rng = new Rng(3);
        var samples = Enumerable.Range(0, 40_000).Select(_ => q.Sample(rng)).OrderBy(x => x).ToArray();
        Assert.InRange(samples[samples.Length / 2], 973 * 0.97, 973 * 1.03);
        Assert.InRange(samples.Average(), q.Mean * 0.97, q.Mean * 1.03);
        // the config's min chunk (one request) is the derived mean tokens per request
        Assert.Equal(q.Mean, t.MinChunkWorkUnits, 0);
    }

    [Fact]
    public void HistogramSamplerAndMean()
    {
        var t = LoadType(2, 2);
        var h = Assert.IsType<HistogramDistribution>(t.Size);
        var rng = new Rng(5);
        const int n = 200_000;
        double sum = 0; int below1min = 0;
        double tasksPerSecond = 1 / (t.EnergyKwhPerWorkUnit.Value * FleetTime.SecondsPerHour);
        for (int i = 0; i < n; i++) { double x = h.Sample(rng); sum += x; if (x / tasksPerSecond < 60) below1min++; }
        Assert.InRange(sum / n, h.Mean * 0.9, h.Mean * 1.1); // heavy tail: loose bound
        Assert.InRange(below1min / (double)n, 0.1533 * 0.95, 0.1533 * 1.05); // share of "<1 min" in job-durations.csv
        output.WriteLine($"research mean job size {h.Mean:F0} tasks = {h.Mean * t.EnergyKwhPerWorkUnit.Value:F2} server-hours");
    }

    [Fact]
    public void DistributionParsingRejectsMissingSourceOrUnit()
    {
        var ctx = new DistributionContext(Root);
        Assert.Throws<InvalidDataException>(() => Distribution.Parse(JsonDocument.Parse("""{"dist":"fixed","value":1,"unit":"x","source":""}""").RootElement, ctx, "t"));
        Assert.Throws<InvalidDataException>(() => Distribution.Parse(JsonDocument.Parse("""{"dist":"fixed","value":1,"source":"s"}""").RootElement, ctx, "t"));
        var u = Distribution.Parse(JsonDocument.Parse("""{"dist":"uniform","min":2,"max":4,"unit":"s","source":"s"}""").RootElement, ctx, "t");
        Assert.Equal(3, u.Mean);
        var c = Distribution.Parse(JsonDocument.Parse("""{"dist":"choice","values":[1,3],"weights":[3,1],"unit":"s","source":"s"}""").RootElement, ctx, "t");
        Assert.Equal(1.5, c.Mean, 12);
        var l = Distribution.Parse(JsonDocument.Parse("""{"dist":"lognormal","median":2,"sigma":0.5,"unit":"s","source":"s"}""").RootElement, ctx, "t");
        Assert.Equal(2 * Math.Exp(0.125), l.Mean, 12);
    }

    // ------------------------------------------------------------------ job types

    [Fact]
    public void JobTypesLoadWithResolvedVolumes()
    {
        var types = TypeFiles.Select((f, i) => JobTypeLoader.Load(P(f), i, Root)).ToList();
        foreach (var t in types)
        {
            output.WriteLine($"{t.Id}: {t.Volume.JobsPerDay:F3} jobs/day | {t.Volume.Derivation}");
            Assert.False(string.IsNullOrWhiteSpace(t.RawJson));
            Assert.Equal(168, t.ArrivalShape.HourOfWeek.Length);
            Assert.Equal(1.0, t.ArrivalShape.HourOfWeek.Average(), 9);
        }
        Assert.Equal(428, types[0].Volume.JobsPerDay, 9);
        Assert.Equal(["CH"], types[0].Placement.AllowedCountryCodes!);
        Assert.Equal(JobClass.Type2, types[2].Class);
        // 194e12 * 0.01 * 0.01 / 7 / mean tokens per request / 1000 requests per job
        Assert.Equal(194e12 * 1e-4 / 7 / types[1].Size.Mean / 1000, types[1].Volume.JobsPerDay, 6);
        // research: jobs/day x mean server-hours per job x 365 = 80,000 server-hours per year
        Assert.Equal(80_000, types[2].Volume.JobsPerDay * 365 * types[2].Size.Mean * types[2].EnergyKwhPerWorkUnit.Value, 3);
        Assert.Equal(2 * 428, JobTypeLoader.Load(P(TypeFiles[0]), 0, Root, 2.0).Volume.JobsPerDay, 9);
    }

    [Fact]
    public void JobTypeLoaderRejectsEmptySource()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var broken = File.ReadAllText(P(TypeFiles[0])).Replace("\"source\": \"scenario value: reference volume, no scaling\"", "\"source\": \"\"");
        var path = Path.Combine(dir, "broken.json");
        File.WriteAllText(path, broken);
        Assert.Throws<InvalidDataException>(() => JobTypeLoader.Load(path, 0, Root));
    }

    // ------------------------------------------------------------------ houses

    [Fact]
    public void ReferenceHouseFileIsTheCaseHouse()
    {
        var h = ReferenceHouse();
        Assert.Equal(("reference", 47.56, 7.59, "cap-bs", 2701, "BS"), (h.HouseId, h.LatDeg, h.LonDeg, h.CellId, h.BfsMunicipalityNr, h.Canton));
        Assert.Equal((10.0, 25.6, 12.0, 35.0, 0.0, 0.3325, (int?)null), (h.DesignHeatingKw, h.FormulaDivisorC, h.HotWaterKwhPerDay, h.TankKwh, h.TankInitialKwh, h.ElectricityPriceChfPerKwh, h.ServerCount));
    }

    private static JsonElement Spec(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void GeneratorIsDeterministicAndFiltersCantons()
    {
        var spec = Spec(File.ReadAllText(P("config/fleet/houses/basel-region-100.spec.json")));
        var (a, resolved) = HouseGenerator.Generate(spec, Root);
        var (b, _) = HouseGenerator.Generate(spec, Root);
        Assert.Equal(100, a.Count);
        Assert.Equal(a, b);
        Assert.All(a, h => Assert.Contains(h.Canton, new[] { "BS", "BL" }));
        Assert.Equal("h00001", a[0].HouseId);
        Assert.All(a, h => Assert.InRange(h.ElectricityPriceChfPerKwh, 0.1, 0.8));
        Assert.All(a, h => Assert.Equal(h.CellId, h.WeatherRef));
        Assert.Equal(100, resolved.GetProperty("count").GetInt32());
        output.WriteLine("basel-region-100 resolved: " + resolved.GetProperty("resolved"));

        var (c, _) = HouseGenerator.Generate(Spec("""{"count":100,"cantons":["BS","BL"],"seed":1}"""), Root);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void GeneratorCellMedianDesignReproducesCellMedianSpaceHeat()
    {
        var (houses, _) = HouseGenerator.Generate(Spec("""{"count":20,"cantons":["ZH"],"seed":3}"""), Root);
        var heat = CsvTable.Read(P(HouseGenerator.HeatDemandPath));
        var weather = new WeatherStore(Root);
        var builder = new HouseDemandBuilder(Root, weather);
        var demands = builder.BuildAll(houses, RunCalendar.From(new RunSection()));
        for (int i = 0; i < houses.Count; i++)
        {
            double median = heat.Num(heat.Rows.Single(r => heat.Str(r, "point_id") == houses[i].CellId), "single_family_heat_median_kwh_per_yr");
            Assert.Equal(median, demands[i].SpaceHeatKw.Sum(), 3);
        }
        // fixed design, fixed price, spread
        var (fixedHouses, _) = HouseGenerator.Generate(Spec("""{"count":50,"design_heating_kw":{"fixed":10},"electricity_price":{"fixed":0.3325},"design_heating_spread":0.2,"seed":4}"""), Root);
        Assert.All(fixedHouses, h => Assert.Equal(0.3325, h.ElectricityPriceChfPerKwh));
        Assert.InRange(fixedHouses.Average(h => h.DesignHeatingKw), 8.5, 11.5);
        Assert.True(fixedHouses.Select(h => h.DesignHeatingKw).Distinct().Count() > 40);
        // positions stay within a cell (±5 km ≈ ±0.045 deg latitude)
        var grid = GridWeather.ReadGrid(P(GridWeather.GridPath));
        Assert.All(fixedHouses, h => Assert.InRange(Math.Abs(h.LatDeg - grid[h.CellId].LatDeg), 0, HouseGenerator.HalfCellKm / HouseGenerator.KmPerDegreeLatitude + 1e-9));
    }

    [Fact]
    public void HouseFilesRoundTripJsonAndCsv()
    {
        var (houses, spec) = HouseGenerator.Generate(Spec("""{"count":30,"cantons":["BL"],"seed":9}"""), Root);
        var dir = Directory.CreateTempSubdirectory().FullName;
        var json = Path.Combine(dir, "h.json");
        HouseFiles.Write(json, houses, spec, 9);
        var rj = HouseFiles.Read(json);
        Assert.Equal(houses, rj.Houses);
        Assert.Equal(9, rj.Seed);
        var csv = Path.Combine(dir, "h.csv");
        HouseFiles.Write(csv, houses, spec, 9);
        var rc = HouseFiles.Read(csv);
        Assert.Equal(houses, rc.Houses);
        Assert.True(File.Exists(csv + ".spec.json"));
        Assert.Equal(30, rc.Spec!.Value.GetProperty("count").GetInt32());
    }

    // ------------------------------------------------------------------ demand

    private static RunCalendar Month() => RunCalendar.From(new RunSection { StartLocal = "2025-01-01T00:00", EndLocal = "2025-01-15T00:00" });

    [Fact]
    public void DemandIsDeterministicAndSorted()
    {
        var types = TypeFiles.Select((f, i) => JobTypeLoader.Load(P(f), i, Root)).ToList();
        var a = DemandGenerator.Generate(types, Month(), 20261121, Root);
        var b = DemandGenerator.Generate(types, Month(), 20261121, Root);
        Assert.Equal(a, b);
        Assert.NotEmpty(a);
        for (int i = 1; i < a.Count; i++)
            Assert.True(a[i - 1].ArrivalMs < a[i].ArrivalMs || (a[i - 1].ArrivalMs == a[i].ArrivalMs && a[i - 1].TypeIndex <= a[i].TypeIndex));
        foreach (var t in types)
        {
            var mine = a.Where(j => j.TypeIndex == t.Index).ToList();
            double expected = t.Volume.JobsPerDay * 14;
            output.WriteLine($"{t.Id}: {mine.Count} jobs in 14 days (expected about {expected:F0})");
            Assert.InRange(mine.Count, expected * 0.9, expected * 1.1);
            Assert.All(mine, j => Assert.True(j.DeadlineMs > j.ArrivalMs && j.ArrivalMs < Month().EndMs));
        }
        var tr = a.Where(j => j.TypeIndex == 0).ToList();
        Assert.All(tr, j => { Assert.Equal("client_site", j.OriginKind); Assert.NotNull(j.ClientId); Assert.Equal(6.5, j.WorkUnits); Assert.Equal(1, j.BatchSize); });
        Assert.StartsWith("transcription-whisper-large-v3-0000001", tr[0].JobKey);
        Assert.All(a.Where(j => j.TypeIndex == 1), j => { Assert.Equal("anywhere", j.OriginKind); Assert.Equal(1000, j.BatchSize); });
        var different = DemandGenerator.Generate(types, Month(), 1, Root);
        Assert.NotEqual(a, different);
    }

    [Fact]
    public void DemandStreamsAreIndependentPerType()
    {
        var all = TypeFiles.Select((f, i) => JobTypeLoader.Load(P(f), i, Root)).ToList();
        var two = DemandGenerator.Generate(all.Take(2).ToList(), Month(), 20261121, Root);
        var three = DemandGenerator.Generate(all, Month(), 20261121, Root);
        Assert.Equal(two, three.Where(j => j.TypeIndex < 2).ToList());
    }

    [Fact]
    public void JobsInputCsvRoundTripsExactly()
    {
        var types = TypeFiles.Select((f, i) => JobTypeLoader.Load(P(f), i, Root)).ToList();
        var jobs = DemandGenerator.Generate(types, Month(), 20261121, Root);
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "jobs-input.csv");
        JobsInputCsv.Write(path, jobs);
        var back = JobsInputCsv.Read(path);
        Assert.Equal(jobs, back);
        JobsInputCsv.Write(path + "2", back);
        Assert.Equal(File.ReadAllText(path), File.ReadAllText(path + "2"));
    }

    [Fact]
    public void ClientsFileReadsCantonCapitals()
    {
        var c = ClientsFile.Read(P("config/fleet/clients/canton-capitals.csv"));
        Assert.Equal(26, c.Clients.Count);
        Assert.Contains(c.Clients, x => x.ClientId == "cap-bs" && x.Canton == "BS" && x.Weight > 0);
        var rng = new Rng(1);
        var drawn = Enumerable.Range(0, 5000).Select(_ => c.Draw(rng).Canton).ToList();
        Assert.True(drawn.Count(x => x == "BE") > drawn.Count(x => x == "UR")); // weight proxy: BE 137,601 vs UR 5,973
    }
}
