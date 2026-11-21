using DigitalHeat.Core;
using DigitalHeat.YearRun;
using Xunit;

namespace DigitalHeat.Core.Tests;

/// <summary>Economics per actor (T-14, decision 0026): hand-checkable cases and case cross-checks.</summary>
public sealed class EconomicsTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "config", "reference-house.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static ReferenceParameters Reference() =>
        ReferenceParameters.Load(Path.Combine(Root(), "config", "reference-house.json"));

    private static EconomicsParameters Config() =>
        EconomicsParameters.Load(Path.Combine(Root(), "config", "economics", "reference-house.json"));

    private static IReadOnlyList<DemandHour> Demand() => HeatDemand.Build(
        WeatherCsv.Read(Path.Combine(Root(), "data", "raw", "open-meteo", "basel-2025-hourly.csv")), Reference());

    // One server, 100 premium + 200 filler server-hours, 300 kWh, 240 kWh heat used.
    private static readonly SiteYearOperation Op = new(1, 100, 200, 300, 240, 1000, 0, 0.5, 0.5, 0);

    private static readonly EconomicInputs In = new(ElectricityPriceChfPerKwh: 0.3, HeatPriceChfPerKwh: 0.12,
        HeatPumpEfficiency: 2.5, PremiumPriceChfPerServerHour: 3, FillerPriceChfPerServerHour: 1,
        PurchasePriceChfPerServer: 6000, LifetimeYears: 6, OpsChfPerServerYear: 100,
        WearReplacementSharePerYear: 0.01, HeatFeeChfPerKwh: 0.02, PlatformCommissionShare: 0.1,
        InstallationChfPerSite: 600);

    [Fact]
    public void SurplusMatchesHandCalculation()
    {
        var r = Economics.Evaluate(Op, In);
        // income 300 + 200; heat 240 × 0.12 = 28.8; electricity 90; depreciation 1000; wear 60;
        // operations 100; installation 600 / 6 = 100 → 500 + 28.8 − 90 − 1000 − 60 − 100 − 100 = −821.2
        Assert.Equal(500, r.ComputingIncomeChf, 9);
        Assert.Equal(28.8, r.HeatValueChf, 9);
        Assert.Equal(90, r.ElectricityCostChf, 9);
        Assert.Equal(1000, r.DepreciationChf, 9);
        Assert.Equal(60, r.WearChf, 9);
        Assert.Equal(100, r.InstallationChf, 9);
        Assert.Equal(-821.2, r.SystemSurplusChf, 9);
    }

    [Fact]
    public void SplitIsHandCheckableAndSumsToSurplus()
    {
        var r = Economics.Evaluate(Op, In);
        // Operator: host = 28.8 − 240 × 0.02 = 24; DigitalHeat = −821.2 − 24.
        Assert.Equal(24, r.OperatorModel.HostChf, 9);
        Assert.Equal(-845.2, r.OperatorModel.DigitalHeatChf, 9);
        // Host-owned: DigitalHeat = 0.1 × 500 − 100 = −50; host = the rest.
        Assert.Equal(-50, r.HostOwnedModel.DigitalHeatChf, 9);
        Assert.Equal(-771.2, r.HostOwnedModel.HostChf, 9);
        foreach (var split in new[] { r.OperatorModel, r.HostOwnedModel })
            Assert.Equal(r.SystemSurplusChf, split.HostChf + split.DigitalHeatChf, 9);
        Assert.False(r.OperatorModel.WinWin);
    }

    [Fact]
    public void BreakEvensAreHandCheckable()
    {
        var r = Economics.Evaluate(Op, In);
        Assert.Equal((90 - 240 * 0.3 / 2.5) / 300, r.CaseBreakEvenChfPerServerHour!.Value, 9); // 0.204
        Assert.Equal((90 + 1000 + 60 + 100 + 100 - 28.8) / 300, r.FullCostBreakEvenChfPerServerHour!.Value, 9);
        // Purchase price that zeroes the surplus: 238.8 / (1/6 + 0.01) = 1351.7 CHF = 0.2253 × 6000.
        double factor = r.BreakEvenPurchasePriceFactor!.Value;
        var atFactor = Economics.Evaluate(Op, In with { PurchasePriceChfPerServer = 6000 * factor });
        Assert.Equal(0, atFactor.SystemSurplusChf, 6);
        Assert.Equal((-821.2 + 100) * 6, r.AffordableInstallationChfPerSite, 9);
    }

    [Fact]
    public void FullCostBreakEvenPriceZeroesTheSurplus()
    {
        var noPremium = Op with { PremiumServerHours = 0, FillerServerHours = 300 };
        var r = Economics.Evaluate(noPremium, In);
        var atBreakEven = Economics.Evaluate(noPremium,
            In with { FillerPriceChfPerServerHour = r.FullCostBreakEvenChfPerServerHour!.Value });
        Assert.Equal(0, atBreakEven.SystemSurplusChf, 9);
    }

    [Fact]
    public void VariantsAreOffByDefaultAndHandCheckable()
    {
        var baseCase = Economics.Evaluate(Op, In);
        Assert.Equal(0, baseCase.Co2CreditChf);
        // CO₂: 240 kWh × 0.001 CHF/kWh = 0.24. PV: a 0.5 × 100 h × 1 server × 1 kWh = 50 kWh at 0.06.
        var v = Economics.Evaluate(Op, In with { Co2CreditChfPerKwhHeat = 0.001, PvSurplusHoursPerServerYear = 100,
            PvSurplusElectricityPriceChfPerKwh = 0.06 });
        Assert.Equal(0.24, v.Co2CreditChf, 9);
        Assert.Equal(250 * 0.3 + 50 * 0.06, v.ElectricityCostChf, 9);
    }

    [Fact]
    public void DemandCapAndPremiumFirstSplit()
    {
        Assert.Equal(0.25 + 0.35 * 0.75, SiteOperation.AvailableLoadShare(0.25, 0.35), 12);
        Assert.Equal(0.4, new DemandLimitedExecutor(0.4).ApplyTargetLoad(0.9), 12);
        Assert.Equal(0.1, new DemandLimitedExecutor(0.4).ApplyTargetLoad(0.1), 12);
        // Two hours at load 0.6 and 0.1, two servers, p = 0.2: premium = (0.2 + 0.1) × 2 = 0.6,
        // all work = 1.4, filler = 0.8, unserved premium = (0 + 0.1) × 2 = 0.2.
        HourResult H(double load) => new("t", 0, 0, 0, load, load, 0, 0, 0, 0, 0, 0, 0, load * 2);
        var summary = new YearSummary(2, 0, 2, 0, 0, 0, 0, 0, 0, 1.4, 0.35, 0, 0, 0, 0, null, "", "", "");
        var op = SiteOperation.FromYear(new YearResult([H(0.6), H(0.1)], summary), 0.2, 0.35, 1);
        Assert.Equal(0.6, op.PremiumServerHours, 12);
        Assert.Equal(0.8, op.FillerServerHours, 12);
        Assert.Equal(0.2, op.UnservedPremiumServerHours, 12);
    }

    [Fact]
    public void UncappedRunReproducesTheScoredCaseFigures()
    {
        var p = Reference();
        var demand = Demand();
        var scored = YearSimulation.Run(demand, p, new GreedyHeatScheduler(p.MinimumLoadShare.Value),
            new SimulatedExecutor()).Summary;
        // Fill rate 1 means "unlimited jobs": the demand cap never binds.
        var op = SiteOperation.Simulate(demand, p, (int)p.ServerCount.Value, OperatingPolicy.HeatLed, 0, 1);
        Assert.Equal(scored.ServerHours, op.ServerHours, 9);
        Assert.Equal(scored.ServerHeatUsedKwh, op.HeatUsedKwh, 9);
        var e = Config();
        var hw = HardwareProfile.Load(Path.Combine(Root(), "config", "hardware", "gpu-inference-l4.json"));
        var r = Economics.Evaluate(op, Economics.InputsFor(hw, e, p, HeatAlternative.HeatPump,
            p.ElectricityPriceChfPerKwh.Value));
        Assert.Equal(scored.BreakEvenChfPerServerHour!.Value, r.CaseBreakEvenChfPerServerHour!.Value, 9);
        Assert.InRange(r.CaseBreakEvenChfPerServerHour!.Value, 0.2255, 0.2265); // case: ≈ 0.226
        Assert.Equal(scored.HeatValueChf, r.HeatValueChf, 9);
    }

    [Fact]
    public void HeatAlternativePricesFollowTheirFormulas()
    {
        var e = Config();
        Assert.Equal(0.3325 / 2.5, e.HeatPriceChfPerKwh(HeatAlternative.HeatPump, 0.3325, 2.5), 12);
        Assert.Equal(0.1398 / 0.9, e.HeatPriceChfPerKwh(HeatAlternative.Gas, 0.3325, 2.5), 12);
        Assert.Equal(1.5394 / (10.08 * 0.9), e.HeatPriceChfPerKwh(HeatAlternative.Oil, 0.3325, 2.5), 12);
        Assert.Equal(0, e.Co2KgPerKwhHeat(HeatAlternative.HeatPump));
        Assert.Equal(0.2653 / 0.9, e.Co2KgPerKwhHeat(HeatAlternative.Oil), 12);
    }

    [Fact]
    public void PriceSetsMatchByLongestPrefixSoRefurbishedProfilesPlugIn()
    {
        var e = Config();
        Assert.Equal("gpu-inference-l4", e.PriceSetFor("gpu-inference-l4").HardwareProfilePrefix);
        Assert.Equal("gpu-inference-l4", e.PriceSetFor("gpu-inference-l4-refurbished").HardwareProfilePrefix);
        Assert.Throws<InvalidDataException>(() => e.PriceSetFor("unknown-profile"));
        Assert.Null(e.TryPriceSetFor("unknown-profile"));
        foreach (string id in new[] { "cpu-server", "gpu-consumer-rtx4090", "gpu-inference-l4" })
            Assert.NotNull(e.PriceSetFor(id));
    }

    [Fact]
    public void RequiredPremiumShareFindsTheFirstPassingStep()
    {
        static EconomicResult Fake(double s) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, s - 0.137, null, null, null, 0,
            new ActorSplit(1, s - 0.137), new ActorSplit(1, s - 0.137));
        Assert.Equal(0.14, Economics.RequiredPremiumShare(Fake, r => r.SystemSurplusChf > 0, 0.01)!.Value, 12);
        Assert.Null(Economics.RequiredPremiumShare(Fake, r => r.SystemSurplusChf > 5, 0.01));
    }

    [Fact]
    public void BundleWritesLeverTableRequiredSharesAndGrid()
    {
        string output = Path.Combine(Path.GetTempPath(), "dh-economics-" + Guid.NewGuid().ToString("N"));
        try
        {
            EconomicsBundle.Write(output, Root(), Path.Combine(Root(), "config", "economics", "reference-house.json"),
                Demand(), Reference());
            foreach (string f in new[] { "run.json", "parameters.json", "levers.csv", "required-premium.csv", "win-win.csv" })
                Assert.True(File.Exists(Path.Combine(output, f)), f);
            var grid = File.ReadAllLines(Path.Combine(output, "win-win.csv"));
            var g = Config().Grid;
            int cells = EconomicsGrid.Axis(g.FillerPriceMinChfPerServerHour, g.FillerPriceMaxChfPerServerHour, g.FillerPriceStepChfPerServerHour).Length
                * EconomicsGrid.Axis(g.ElectricityPriceMinChfPerKwh, g.ElectricityPriceMaxChfPerKwh, g.ElectricityPriceStepChfPerKwh).Length;
            Assert.Equal(1 + cells * g.ServerCounts.Length * g.PremiumShares.Length * g.FillRates.Length, grid.Length);
            Assert.StartsWith("hardware_profile_id,", grid[0]);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }
}
