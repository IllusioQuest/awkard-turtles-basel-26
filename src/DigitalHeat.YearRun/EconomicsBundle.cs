using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DigitalHeat.Core;

namespace DigitalHeat.YearRun;

/// <summary>
/// Writes the economics bundle (T-14, decision 0025) in the T-13 conventions: lever table,
/// required premium shares and win-win grid. Every number comes from Core.Economics and the
/// unchanged year simulation; this class only loops over scenarios and writes files.
/// </summary>
public static class EconomicsBundle
{
    public const int SchemaVersion = 1;

    private sealed class Runner(IReadOnlyList<DemandHour> demand, ReferenceParameters reference)
    {
        private readonly Dictionary<(int, OperatingPolicy, double, double), SiteYearOperation> cache = new();

        public SiteYearOperation Operation(int n, OperatingPolicy policy, double premium, double fill)
        {
            var key = (n, policy, premium, fill);
            if (!cache.TryGetValue(key, out var op))
                cache[key] = op = SiteOperation.Simulate(demand, reference, n, policy, premium, fill);
            return op;
        }
    }

    public static void Write(string output, string root, string economicsPath, IReadOnlyList<DemandHour> demand,
        ReferenceParameters reference)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Export directory must be empty; choose a new run directory.");
        var e = EconomicsParameters.Load(economicsPath);
        string hardwareDir = Path.Combine(root, e.HardwareDirectory);
        var profiles = HardwareProfile.LoadDirectory(hardwareDir);
        var runner = new Runner(demand, reference);
        double electricity = reference.ElectricityPriceChfPerKwh.Value;
        double fill = e.FillRate.Value;
        // Provenance is read before any output exists, so an export inside the repository stays clean.
        string? revision = ResultBundle.Git(root, "rev-parse", "HEAD");
        string? status = ResultBundle.Git(root, "status", "--porcelain", "--untracked-files=normal");
        Directory.CreateDirectory(output);

        var levers = new StringBuilder(LeverHeader + "\n");
        var required = new StringBuilder("hardware_profile_id,server_count,policy,heat_alternative,variant,fill_rate," +
            "required_premium_share_system_surplus,required_premium_share_operator_win_win," +
            "required_premium_share_host_owned_win_win,break_even_purchase_price_factor_without_premium\n");
        foreach (var hw in profiles)
        {
            bool hasPremium = e.PriceSetFor(hw.Id).PremiumPriceChfPerServerHour.Value is not null;
            foreach (int n in e.Levers.ServerCounts)
            foreach (var policy in e.Levers.Policies.Select(EconomicsNames.ParsePolicy))
            foreach (var alt in e.Levers.HeatAlternatives.Select(EconomicsNames.ParseAlternative))
            {
                var inputs = Economics.InputsFor(hw, e, reference, alt, electricity);
                foreach (double p in e.Levers.PremiumShares.Where(p => hasPremium || p == 0))
                {
                    var op = runner.Operation(n, policy, p, fill);
                    levers.AppendLine(LeverRow(hw.Id, n, policy, alt, p, fill, inputs, op, Economics.Evaluate(op, inputs)));
                }
                // "base" is the model; the other rows are creative-ideas variants (labelled, decision 0025).
                foreach (var (variant, co2, pv) in Variants)
                {
                    var vin = Economics.InputsFor(hw, e, reference, alt, electricity, null, co2, pv);
                    EconomicResult At(double p) => Economics.Evaluate(runner.Operation(n, policy, p, fill), vin);
                    double step = e.RequiredPremiumShareStep.Value;
                    double? Need(Func<EconomicResult, bool> c) => hasPremium ? Economics.RequiredPremiumShare(At, c, step)
                        : c(At(0)) ? 0 : null;
                    required.AppendLine(string.Join(",", hw.Id, n.ToString(CultureInfo.InvariantCulture),
                        EconomicsNames.Name(policy), EconomicsNames.Name(alt), variant, F(fill),
                        F(Need(r => r.SystemSurplusChf > 0)), F(Need(r => r.OperatorModel.WinWin)),
                        F(Need(r => r.HostOwnedModel.WinWin)), F(At(0).BreakEvenPurchasePriceFactor)));
                }
            }
        }
        File.WriteAllText(Path.Combine(output, "levers.csv"), levers.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "required-premium.csv"), required.ToString(), new UTF8Encoding(false));

        var g = e.Grid;
        var gridHw = profiles.Single(x => x.Id == g.HardwareProfile);
        var gridPolicy = EconomicsNames.ParsePolicy(g.Policy);
        var gridAlt = EconomicsNames.ParseAlternative(g.HeatAlternative);
        var grid = new StringBuilder("hardware_profile_id,server_count,policy,heat_alternative,premium_share," +
            "fill_rate,filler_price_chf_per_server_hour,electricity_price_chf_per_kwh,system_surplus_chf," +
            "operator_host_chf,operator_digitalheat_chf,operator_win_win,host_owned_host_chf," +
            "host_owned_digitalheat_chf,host_owned_win_win,full_cost_break_even_chf_per_server_hour\n");
        foreach (int n in g.ServerCounts)
        foreach (double p in g.PremiumShares)
        foreach (double phi in g.FillRates)
        {
            var op = runner.Operation(n, gridPolicy, p, phi);
            foreach (double price in EconomicsGrid.Axis(g.FillerPriceMinChfPerServerHour, g.FillerPriceMaxChfPerServerHour, g.FillerPriceStepChfPerServerHour))
            foreach (double el in EconomicsGrid.Axis(g.ElectricityPriceMinChfPerKwh, g.ElectricityPriceMaxChfPerKwh, g.ElectricityPriceStepChfPerKwh))
            {
                var r = Economics.Evaluate(op, Economics.InputsFor(gridHw, e, reference, gridAlt, el, price));
                grid.AppendLine(string.Join(",", gridHw.Id, n.ToString(CultureInfo.InvariantCulture), g.Policy,
                    g.HeatAlternative, F(p), F(phi), F(price), F(el), F(r.SystemSurplusChf),
                    F(r.OperatorModel.HostChf), F(r.OperatorModel.DigitalHeatChf), B(r.OperatorModel.WinWin),
                    F(r.HostOwnedModel.HostChf), F(r.HostOwnedModel.DigitalHeatChf), B(r.HostOwnedModel.WinWin),
                    F(r.FullCostBreakEvenChfPerServerHour)));
            }
        }
        File.WriteAllText(Path.Combine(output, "win-win.csv"), grid.ToString(), new UTF8Encoding(false));

        ResultBundle.WriteJson(Path.Combine(output, "parameters.json"), new
        {
            SchemaVersion, ReferenceParameters = reference, Economics = e, HardwareProfiles = profiles
        });
        string economicsRelative = ResultBundle.RelativeInput(root, economicsPath);
        var inputPaths = new[] { "config/reference-house.json", economicsRelative,
                "data/raw/open-meteo/basel-2025-hourly.csv", "data/raw/elcom/iwb-basel-h4-2026.json" }
            .Concat(Directory.GetFiles(hardwareDir, "*.json").Where(x => !x.EndsWith(".schema.json", StringComparison.Ordinal))
                .OrderBy(x => x, StringComparer.Ordinal).Select(x => ResultBundle.RelativeInput(root, x)))
            .Distinct(StringComparer.Ordinal);
        ResultBundle.WriteJson(Path.Combine(output, "run.json"), new
        {
            SchemaVersion, Kind = "economics", Timezone = "Europe/Zurich",
            StepHours = reference.HoursPerStep.Value,
            GitCommit = revision, GitDirty = status is null ? (bool?)null : status.Length != 0,
            CodeProvenance = revision is null || status is null ? "unknown"
                : status.Length != 0 ? "dirty_not_reproducible_from_commit" : "clean_commit",
            Seed = (int?)null, Randomness = "none",
            Command = new[] { "dotnet", "run", "--project", "src/DigitalHeat.YearRun", "--", ".",
                "--economics", economicsRelative, "--output", "results/economics" },
            CommandWorkingDirectory = "repository_root",
            Inputs = inputPaths.Select(path => new
            {
                Path = path,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path)))).ToLowerInvariant()
            }).ToArray(),
            Files = new[] { "levers.csv", "required-premium.csv", "win-win.csv", "parameters.json" },
            Note = "Variant economics (decision 0025). Scored case figures are unchanged; case_break_even uses the case formula. Premium demand is TODO(source): premium shares are a sweep, not a forecast."
        });
    }

    private static readonly (string Name, bool Co2, bool Pv)[] Variants =
        [("base", false, false), ("variant-co2-credit", true, false), ("variant-own-pv", false, true),
         ("variant-co2-credit-own-pv", true, true)];

    private const string LeverHeader = "hardware_profile_id,server_count,policy,heat_alternative,premium_share," +
        "fill_rate,electricity_price_chf_per_kwh,heat_price_chf_per_kwh,premium_price_chf_per_server_hour," +
        "filler_price_chf_per_server_hour,server_hours,premium_server_hours,filler_server_hours," +
        "unserved_premium_server_hours,utilisation," +
        "coverage,heat_used_kwh,dumped_heat_kwh,premium_income_chf,filler_income_chf,heat_value_chf," +
        "electricity_cost_chf,depreciation_chf,wear_chf,operations_chf,installation_chf,system_surplus_chf," +
        "case_break_even_chf_per_server_hour,full_cost_break_even_chf_per_server_hour," +
        "break_even_purchase_price_factor,affordable_installation_chf_per_site,operator_host_chf,operator_digitalheat_chf,operator_win_win," +
        "host_owned_host_chf,host_owned_digitalheat_chf,host_owned_win_win";

    private static string LeverRow(string id, int n, OperatingPolicy policy, HeatAlternative alt, double p,
        double fill, EconomicInputs x, SiteYearOperation op, EconomicResult r) => string.Join(",",
        id, n.ToString(CultureInfo.InvariantCulture), EconomicsNames.Name(policy), EconomicsNames.Name(alt),
        F(p), F(fill), F(x.ElectricityPriceChfPerKwh), F(x.HeatPriceChfPerKwh), F(x.PremiumPriceChfPerServerHour),
        F(x.FillerPriceChfPerServerHour), F(op.ServerHours), F(op.PremiumServerHours), F(op.FillerServerHours),
        F(op.UnservedPremiumServerHours), F(op.Utilisation), F(op.Coverage), F(op.HeatUsedKwh), F(op.DumpedHeatKwh),
        F(r.PremiumIncomeChf), F(r.FillerIncomeChf), F(r.HeatValueChf), F(r.ElectricityCostChf),
        F(r.DepreciationChf), F(r.WearChf), F(r.OperationsChf), F(r.InstallationChf), F(r.SystemSurplusChf),
        F(r.CaseBreakEvenChfPerServerHour), F(r.FullCostBreakEvenChfPerServerHour),
        F(r.BreakEvenPurchasePriceFactor), F(r.AffordableInstallationChfPerSite), F(r.OperatorModel.HostChf), F(r.OperatorModel.DigitalHeatChf),
        B(r.OperatorModel.WinWin), F(r.HostOwnedModel.HostChf), F(r.HostOwnedModel.DigitalHeatChf),
        B(r.HostOwnedModel.WinWin));

    /// <summary>Round-trip number, empty field for undefined (T-13 CSV convention uses empty = null).</summary>
    private static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    private static string B(bool v) => v ? "true" : "false";
}
