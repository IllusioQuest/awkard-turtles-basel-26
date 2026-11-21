using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.YearRun;
using Xunit;

public sealed class ResultBundleTests
{
    [Fact]
    public void ExportPreservesModelValuesAndSourcesWithSnakeCaseKeys()
    {
        string root = FindRoot();
        var p = ReferenceParameters.Load(Path.Combine(root, "config/reference-house.json"));
        string scenarioPath = Path.Combine(root, "config/job-scenarios/unlimited.json");
        var scenario = JobScenario.Load(scenarioPath);
        var result = YearSimulation.Run(
            HeatDemand.Build(Enumerable.Range(0, (int)p.HoursPerDay.Value)
                .Select(hour => new WeatherHour($"2025-01-01T{hour:00}:00", 0)).ToArray(), p),
            p, new HeatFirstScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor(), scenario);
        string output = Path.Combine(Path.GetTempPath(), "digitalheat-export-" + Guid.NewGuid());
        try
        {
            ResultBundle.Write(output, root, scenarioPath, result, p, scenario);
            using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "summary.json")));
            var site = summary.RootElement.GetProperty("sites")[0];
            Assert.Equal("reference-house", site.GetProperty("site_id").GetString());
            Assert.Equal(result.Summary.TotalHeatDemandKwh,
                site.GetProperty("summary").GetProperty("total_heat_demand_kwh").GetDouble());
            using var inputs = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "parameters.json")));
            Assert.Equal(p.ServerElectricKw.Source, inputs.RootElement.GetProperty("reference_parameters")
                .GetProperty("server_electric_kw").GetProperty("source").GetString());
            Assert.False(inputs.RootElement.GetProperty("job_scenario").TryGetProperty("filler", out _));
            foreach (string path in Directory.GetFiles(output, "*.json"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                AssertSnakeCase(doc.RootElement);
            }
            using var run = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "run.json")));
            Assert.Equal("not_modelled", run.RootElement.GetProperty("flows_status").GetString());
            Assert.Equal(JsonValueKind.Null, run.RootElement.GetProperty("seed").ValueKind);
            Assert.All(run.RootElement.GetProperty("inputs").EnumerateArray(), item =>
                Assert.Matches("^[a-f0-9]{64}$", item.GetProperty("sha256").GetString()!));
            Assert.Single(File.ReadAllLines(Path.Combine(output, "flows.csv")));
            Assert.Throws<IOException>(() => ResultBundle.Write(output, root, scenarioPath, result, p, scenario));
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public void CsvUsesInvariantDecimalsAndEscapesText()
    {
        string path = Path.GetTempFileName();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
            ResultBundle.WriteHourly(path, new[] { new HourResult("label,\"quoted\"", 1.25,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) });
            Assert.StartsWith("\"label,\"\"quoted\"\"\",1.25,", File.ReadAllLines(path)[1]);
        }
        finally { CultureInfo.CurrentCulture = previous; File.Delete(path); }
    }

    [Fact]
    public void MixedExportAddsPerTypeReportWithoutChangingVersionOneSummary()
    {
        string root = FindRoot();
        var p = ReferenceParameters.Load(Path.Combine(root, "config/reference-house.json"));
        string scenarioPath = Path.Combine(root, "config/job-scenarios/mixed-basel-technical.json");
        var scenario = JobScenario.Load(scenarioPath);
        var weather = Enumerable.Range(0, 24)
            .Select(hour => new WeatherHour($"2025-01-01T{hour:00}:00", 0)).ToArray();
        var result = YearSimulation.Run(HeatDemand.Build(weather, p), p,
            new HeatFirstScheduler(p.MinimumLoadShare.Value), new SimulatedExecutor(), scenario);
        string output = Path.Combine(Path.GetTempPath(), "digitalheat-mixed-export-" + Guid.NewGuid());
        try
        {
            ResultBundle.Write(output, root, scenarioPath, result, p, scenario);
            using var jobs = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "jobs.json")));
            Assert.Equal("mixed", jobs.RootElement.GetProperty("mode").GetString());
            Assert.Equal(result.Summary.ServerHours,
                jobs.RootElement.GetProperty("total_server_hours").GetDouble(), 8);
            Assert.Equal(result.Summary.ComputingIncomeChf,
                jobs.RootElement.GetProperty("total_computing_income_chf").GetDouble(), 8);
            Assert.Equal("research-simulation",
                jobs.RootElement.GetProperty("filler").GetProperty("id").GetString());
            AssertSnakeCase(jobs.RootElement);
            using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "summary.json")));
            Assert.Equal(1, summary.RootElement.GetProperty("schema_version").GetInt32());
            Assert.False(summary.RootElement.GetProperty("sites")[0].GetProperty("summary")
                .TryGetProperty("filler", out _));
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    private static void AssertSnakeCase(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                Assert.Matches("^[a-z][a-z0-9_]*$", property.Name);
                AssertSnakeCase(property.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) AssertSnakeCase(item);
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "config/reference-house.json"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }
}
