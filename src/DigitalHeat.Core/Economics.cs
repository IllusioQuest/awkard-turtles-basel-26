using System.Text.Json;

namespace DigitalHeat.Core;

/// <summary>Which heater the server heat replaces (sets the value of one kWh of used heat).</summary>
public enum HeatAlternative { HeatPump, Gas, Oil }

/// <summary>
/// Lever G. <c>HeatLed</c>: servers run only for heat; work that finds no heat demand is routed
/// elsewhere (not earned at this site). <c>AlwaysOn</c>: servers run whenever work is sold and
/// surplus heat is dumped.
/// </summary>
public enum OperatingPolicy { HeatLed, AlwaysOn }

public static class EconomicsNames
{
    public static HeatAlternative ParseAlternative(string name) => name switch
    {
        "heat-pump" => HeatAlternative.HeatPump, "gas" => HeatAlternative.Gas, "oil" => HeatAlternative.Oil,
        _ => throw new InvalidDataException($"Unknown heat alternative '{name}'.")
    };

    public static string Name(HeatAlternative a) => a switch
    {
        HeatAlternative.HeatPump => "heat-pump", HeatAlternative.Gas => "gas", _ => "oil"
    };

    public static OperatingPolicy ParsePolicy(string name) => name switch
    {
        "heat-led" => OperatingPolicy.HeatLed, "always-on" => OperatingPolicy.AlwaysOn,
        _ => throw new InvalidDataException($"Unknown operating policy '{name}'.")
    };

    public static string Name(OperatingPolicy p) => p == OperatingPolicy.HeatLed ? "heat-led" : "always-on";
}

/// <summary>Filler and premium prices for hardware profiles whose id starts with the prefix.</summary>
public sealed class PriceSet
{
    public required string HardwareProfilePrefix { get; init; }
    public required ProfileValue FillerPriceChfPerServerHour { get; init; }
    public required ProfileValue PremiumPriceChfPerServerHour { get; init; }
}

public sealed class EconomicsLevers
{
    public required int[] ServerCounts { get; init; }
    public required double[] PremiumShares { get; init; }
    public required string[] Policies { get; init; }
    public required string[] HeatAlternatives { get; init; }
    public required string Source { get; init; }
}

public sealed class EconomicsGrid
{
    public required string HardwareProfile { get; init; }
    public required int[] ServerCounts { get; init; }
    public required double[] PremiumShares { get; init; }
    public required double[] FillRates { get; init; }
    public required double FillerPriceMinChfPerServerHour { get; init; }
    public required double FillerPriceMaxChfPerServerHour { get; init; }
    public required double FillerPriceStepChfPerServerHour { get; init; }
    public required double ElectricityPriceMinChfPerKwh { get; init; }
    public required double ElectricityPriceMaxChfPerKwh { get; init; }
    public required double ElectricityPriceStepChfPerKwh { get; init; }
    public required string HeatAlternative { get; init; }
    public required string Policy { get; init; }
    public required string Source { get; init; }

    /// <summary>Inclusive axis values min, min + step, ..., max (rounded to avoid float drift).</summary>
    public static double[] Axis(double min, double max, double step)
    {
        if (step <= 0 || max < min) throw new InvalidDataException("Invalid grid axis.");
        int n = (int)Math.Floor((max - min) / step + 1e-9);
        return Enumerable.Range(0, n + 1).Select(i => Math.Round(min + i * step, 10)).ToArray();
    }
}

/// <summary>Economic scenario parameters, <c>config/economics/*.json</c> (T-14, decision 0026).</summary>
public sealed class EconomicsParameters
{
    public required string Description { get; init; }
    public required string HardwareDirectory { get; init; }
    public required string DefaultHardwareProfile { get; init; }
    public required Parameter FillRate { get; init; }
    public required Parameter PremiumShareOfOfferedHours { get; init; }
    public required Parameter WearReplacementSharePerYear { get; init; }
    public required Parameter HeatFeeChfPerKwh { get; init; }
    public required Parameter PlatformCommissionShare { get; init; }
    public required Parameter GasPriceChfPerKwhFuel { get; init; }
    public required Parameter GasBoilerEfficiency { get; init; }
    public required Parameter OilPriceChfPerLitre { get; init; }
    public required Parameter OilEnergyKwhPerLitre { get; init; }
    public required Parameter OilBoilerEfficiency { get; init; }
    public required Parameter InstallationChfPerSite { get; init; }
    public required Parameter OilCo2KgPerKwhFuel { get; init; }
    public required Parameter GasCo2KgPerKwhFuel { get; init; }
    public required EconomicsVariants Variants { get; init; }
    public required IReadOnlyList<PriceSet> PriceSets { get; init; }
    public required EconomicsLevers Levers { get; init; }
    public required EconomicsGrid Grid { get; init; }
    public required Parameter RequiredPremiumShareStep { get; init; }

    private static readonly Dictionary<string, string> Units = new()
    {
        [nameof(FillRate)] = "fraction", [nameof(PremiumShareOfOfferedHours)] = "fraction",
        [nameof(WearReplacementSharePerYear)] = "fraction/year", [nameof(HeatFeeChfPerKwh)] = "CHF/kWh",
        [nameof(PlatformCommissionShare)] = "fraction", [nameof(GasPriceChfPerKwhFuel)] = "CHF/kWh",
        [nameof(GasBoilerEfficiency)] = "kWh heat/kWh fuel", [nameof(OilPriceChfPerLitre)] = "CHF/litre",
        [nameof(OilEnergyKwhPerLitre)] = "kWh/litre", [nameof(OilBoilerEfficiency)] = "kWh heat/kWh fuel",
        [nameof(RequiredPremiumShareStep)] = "fraction", [nameof(InstallationChfPerSite)] = "CHF/site",
        [nameof(OilCo2KgPerKwhFuel)] = "kg CO2/kWh fuel", [nameof(GasCo2KgPerKwhFuel)] = "kg CO2/kWh fuel"
    };

    public static EconomicsParameters Load(string path)
    {
        var e = JsonSerializer.Deserialize<EconomicsParameters>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty economics file.");
        foreach (var (name, unit) in Units)
        {
            var item = (Parameter?)typeof(EconomicsParameters).GetProperty(name)!.GetValue(e);
            if (item is null || !double.IsFinite(item.Value) || item.Value < 0 || item.Unit != unit
                || string.IsNullOrWhiteSpace(item.Source))
                throw new InvalidDataException($"Invalid value, unit or source for {name}.");
        }
        if (e.FillRate.Value > 1 || e.PremiumShareOfOfferedHours.Value > 1 || e.PlatformCommissionShare.Value > 1
            || e.GasBoilerEfficiency.Value <= 0 || e.OilBoilerEfficiency.Value <= 0
            || e.OilEnergyKwhPerLitre.Value <= 0 || e.RequiredPremiumShareStep.Value is <= 0 or > 1)
            throw new InvalidDataException("Economics values are outside valid bounds.");
        (e.Variants ?? throw new InvalidDataException("Missing economics variants.")).Validate();
        foreach (var s in e.PriceSets)
            foreach (var v in new[] { s.FillerPriceChfPerServerHour, s.PremiumPriceChfPerServerHour })
            {
                if (v.Unit != "CHF/server-hour" || string.IsNullOrWhiteSpace(v.Source)
                    || (v.Value is null && !v.Source.StartsWith("TODO(source)", StringComparison.Ordinal))
                    || v.Value is < 0)
                    throw new InvalidDataException($"Invalid price in price set {s.HardwareProfilePrefix}.");
            }
        if (e.PriceSets.Any(s => s.FillerPriceChfPerServerHour.Value is null))
            throw new InvalidDataException("Every price set needs a filler price.");
        if (string.IsNullOrWhiteSpace(e.Levers.Source) || string.IsNullOrWhiteSpace(e.Grid.Source)
            || e.Levers.ServerCounts.Concat(e.Grid.ServerCounts).Any(n => n <= 0)
            || e.Levers.PremiumShares.Concat(e.Grid.PremiumShares).Concat(e.Grid.FillRates).Any(x => x is < 0 or > 1))
            throw new InvalidDataException("Invalid lever or grid axes.");
        foreach (var x in e.Levers.HeatAlternatives.Append(e.Grid.HeatAlternative)) EconomicsNames.ParseAlternative(x);
        foreach (var x in e.Levers.Policies.Append(e.Grid.Policy)) EconomicsNames.ParsePolicy(x);
        return e;
    }

    /// <summary>Price set for a profile: the longest prefix that matches its id, so that e.g. a
    /// refurbished <c>gpu-inference-l4-*</c> profile (T-35) sells at the L4 prices.</summary>
    public PriceSet PriceSetFor(string hardwareProfileId) =>
        PriceSets.Where(s => hardwareProfileId.StartsWith(s.HardwareProfilePrefix, StringComparison.Ordinal))
            .OrderByDescending(s => s.HardwareProfilePrefix.Length).FirstOrDefault()
        ?? throw new InvalidDataException($"No price set for hardware profile {hardwareProfileId}.");

    /// <summary>
    /// Value of one kWh of used server heat = cost of producing it with the replaced heater.
    /// Heat pump: electricity ÷ efficiency (case: price ÷ 2.5, docs/case/challenge.md);
    /// gas: fuel price ÷ boiler efficiency; oil: litre price ÷ (kWh per litre × efficiency).
    /// Running energy only; avoided investment and maintenance are not counted.
    /// </summary>
    public double HeatPriceChfPerKwh(HeatAlternative alternative, double electricityPriceChfPerKwh,
        double heatPumpEfficiency) => alternative switch
    {
        HeatAlternative.HeatPump => electricityPriceChfPerKwh / heatPumpEfficiency,
        HeatAlternative.Gas => GasPriceChfPerKwhFuel.Value / GasBoilerEfficiency.Value,
        _ => OilPriceChfPerLitre.Value / (OilEnergyKwhPerLitre.Value * OilBoilerEfficiency.Value)
    };

    /// <summary>CO₂ avoided per kWh of used server heat: fuel factor ÷ boiler efficiency; 0 for the
    /// heat pump (it uses electricity either way; an electricity emission factor is not modelled).</summary>
    public double Co2KgPerKwhHeat(HeatAlternative alternative) => alternative switch
    {
        HeatAlternative.Gas => GasCo2KgPerKwhFuel.Value / GasBoilerEfficiency.Value,
        HeatAlternative.Oil => OilCo2KgPerKwhFuel.Value / OilBoilerEfficiency.Value,
        _ => 0
    };
}

/// <summary>
/// Optional variants from docs/research/creative-ideas.md (§3.2 own PV, §3.6 CO₂ credits). Never part
/// of the base evaluation; the bundle reports them in a separate file, clearly labelled.
/// </summary>
public sealed class EconomicsVariants
{
    public required Parameter Co2CreditChfPerTonne { get; init; }
    public required Parameter PvSurplusHoursPerServerYear { get; init; }
    public required Parameter PvSurplusElectricityPriceChfPerKwh { get; init; }

    public void Validate()
    {
        foreach (var (item, unit) in new[] { (Co2CreditChfPerTonne, "CHF/t CO2"),
                     (PvSurplusHoursPerServerYear, "hours/server-year"), (PvSurplusElectricityPriceChfPerKwh, "CHF/kWh") })
            if (item is null || !double.IsFinite(item.Value) || item.Value < 0 || item.Unit != unit
                || string.IsNullOrWhiteSpace(item.Source))
                throw new InvalidDataException("Invalid economics variant parameter.");
    }
}

/// <summary>
/// Executor for the year simulation that can only deliver as much load as there is sold work:
/// achieved load = min(target, available share). Demand-side cap, decision 0026.
/// </summary>
public sealed class DemandLimitedExecutor : IExecutor
{
    public DemandLimitedExecutor(double availableLoadPerServer)
    {
        if (!double.IsFinite(availableLoadPerServer) || availableLoadPerServer is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(availableLoadPerServer));
        AvailableLoadPerServer = availableLoadPerServer;
    }

    public double AvailableLoadPerServer { get; }

    public double ApplyTargetLoad(double targetLoadPerServer) => Math.Min(targetLoadPerServer, AvailableLoadPerServer);
}

/// <summary>Physical year of one site, split into premium and filler server-hours.</summary>
public sealed record SiteYearOperation(int ServerCount, double PremiumServerHours, double FillerServerHours,
    double ServerElectricKwh, double HeatUsedKwh, double HeatDemandKwh, double DumpedHeatKwh,
    double Utilisation, double AvailableLoadShare, double UnservedPremiumServerHours)
{
    public double ServerHours => PremiumServerHours + FillerServerHours;
    public double Coverage => HeatDemandKwh > 0 ? HeatUsedKwh / HeatDemandKwh : 0;
}

public static class SiteOperation
{
    /// <summary>
    /// Share of a server's capacity that has paid work: premium share p is served first,
    /// filler fills φ of the rest, a = p + φ·(1 − p) (expected value of F = φ·N, decision 0014).
    /// </summary>
    public static double AvailableLoadShare(double premiumShare, double fillRate)
    {
        if (premiumShare is < 0 or > 1 || fillRate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(premiumShare));
        return premiumShare + fillRate * (1 - premiumShare);
    }

    /// <summary>Runs the unchanged year simulation and scheduler with the demand cap.</summary>
    public static SiteYearOperation Simulate(IReadOnlyList<DemandHour> demand, ReferenceParameters reference,
        int serverCount, OperatingPolicy policy, double premiumShare, double fillRate)
    {
        double minimumLoad = policy == OperatingPolicy.AlwaysOn ? 1 : reference.MinimumLoadShare.Value;
        var p = reference.WithScenario(serverCount, minimumLoad);
        double available = AvailableLoadShare(premiumShare, fillRate);
        var result = YearSimulation.Run(demand, p, new GreedyHeatScheduler(minimumLoad),
            new DemandLimitedExecutor(available));
        return FromYear(result, premiumShare, fillRate, p.HoursPerStep.Value);
    }

    /// <summary>
    /// Premium is served first in every hour: premium load = min(actual load, p), filler = the rest.
    /// Premium capacity p that the heat-led schedule leaves unused in an hour is reported as
    /// unserved: a premium buyer would have to be served elsewhere in that hour (lever G).
    /// </summary>
    public static SiteYearOperation FromYear(YearResult result, double premiumShare, double fillRate,
        double hoursPerStep)
    {
        double available = AvailableLoadShare(premiumShare, fillRate);
        double perLoad = result.Summary.ServerCount * hoursPerStep; // server-hours per unit of load and step
        double hours = result.Summary.ServerHours;
        double premium = result.Hours.Sum(h => Math.Min(h.ActualLoadPerServer, premiumShare)) * perLoad;
        double unserved = result.Hours.Sum(h => premiumShare - Math.Min(h.ActualLoadPerServer, premiumShare)) * perLoad;
        return new SiteYearOperation(result.Summary.ServerCount, premium, hours - premium,
            result.Hours.Sum(x => x.ServerElectricKwh), result.Summary.ServerHeatUsedKwh,
            result.Summary.TotalHeatDemandKwh, result.Summary.DumpedHeatKwh, result.Summary.Utilisation,
            available, unserved);
    }
}

/// <summary>All money inputs for one evaluation; prices per 1 kW server-hour. The last four are
/// installation (TODO(source), 0 in the base case) and the creative-ideas variants (0 = off).</summary>
public sealed record EconomicInputs(double ElectricityPriceChfPerKwh, double HeatPriceChfPerKwh,
    double HeatPumpEfficiency, double PremiumPriceChfPerServerHour, double FillerPriceChfPerServerHour,
    double PurchasePriceChfPerServer, double LifetimeYears, double OpsChfPerServerYear,
    double WearReplacementSharePerYear, double HeatFeeChfPerKwh, double PlatformCommissionShare,
    double InstallationChfPerSite = 0, double Co2CreditChfPerKwhHeat = 0,
    double PvSurplusHoursPerServerYear = 0, double PvSurplusElectricityPriceChfPerKwh = 0);

public sealed record ActorSplit(double HostChf, double DigitalHeatChf)
{
    public bool WinWin => HostChf > 0 && DigitalHeatChf > 0;
}

public sealed record EconomicResult(double PremiumIncomeChf, double FillerIncomeChf, double HeatValueChf,
    double ElectricityCostChf, double DepreciationChf, double WearChf, double OperationsChf,
    double InstallationChf, double Co2CreditChf, double SystemSurplusChf,
    double? CaseBreakEvenChfPerServerHour, double? FullCostBreakEvenChfPerServerHour,
    double? BreakEvenPurchasePriceFactor, double AffordableInstallationChfPerSite,
    ActorSplit OperatorModel, ActorSplit HostOwnedModel)
{
    public double ComputingIncomeChf => PremiumIncomeChf + FillerIncomeChf;
}

public static class Economics
{
    /// <summary>
    /// System surplus per year and its split (decision 0026, docs/model/economics.md):
    /// surplus = premium + filler income + heat value (+ CO₂ credits) − electricity − depreciation
    /// − wear − operations − installation ÷ lifetime.
    /// Operator model: host = heat value − heat fee; DigitalHeat = the rest (it owns hardware and
    /// installation, pays electricity and operations, keeps income and credits).
    /// Host-owned model: DigitalHeat = commission × income − operations; host = the rest.
    /// </summary>
    public static EconomicResult Evaluate(SiteYearOperation op, EconomicInputs x)
    {
        if (x.LifetimeYears <= 0 || x.HeatPumpEfficiency <= 0 || x.PlatformCommissionShare is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x));
        double premium = op.PremiumServerHours * x.PremiumPriceChfPerServerHour;
        double filler = op.FillerServerHours * x.FillerPriceChfPerServerHour;
        double income = premium + filler;
        double heatValue = op.HeatUsedKwh * x.HeatPriceChfPerKwh;
        // Variant own PV: the expected sold kWh in PV-surplus hours (flat demand: share a of each
        // hour is sold) is bought at the forgone feed-in price instead of the retail price.
        double kwhPerServerHour = op.ServerHours > 0 ? op.ServerElectricKwh / op.ServerHours : 0;
        double pvKwh = Math.Min(op.ServerElectricKwh,
            op.AvailableLoadShare * x.PvSurplusHoursPerServerYear * op.ServerCount * kwhPerServerHour);
        double electricity = (op.ServerElectricKwh - pvKwh) * x.ElectricityPriceChfPerKwh
            + pvKwh * x.PvSurplusElectricityPriceChfPerKwh;
        double depreciation = op.ServerCount * x.PurchasePriceChfPerServer / x.LifetimeYears;
        double wear = op.ServerCount * x.PurchasePriceChfPerServer * x.WearReplacementSharePerYear;
        double operations = op.ServerCount * x.OpsChfPerServerYear;
        double installation = x.InstallationChfPerSite / x.LifetimeYears; // straight line, no interest
        double co2 = op.HeatUsedKwh * x.Co2CreditChfPerKwhHeat;
        double surplus = income + heatValue + co2 - electricity - depreciation - wear - operations - installation;
        double hours = op.ServerHours;
        // Case formula (scored): electricity minus heat-pump value of the used heat, per server-hour.
        double? caseBreakEven = hours > 0
            ? (electricity - op.HeatUsedKwh * x.ElectricityPriceChfPerKwh / x.HeatPumpEfficiency) / hours : null;
        double? fullBreakEven = hours > 0
            ? (electricity + depreciation + wear + operations + installation - heatValue - co2) / hours : null;
        // Purchase price P* with surplus(P*) = 0, as a share of the profile's price (lever D).
        double perChfOfPurchase = op.ServerCount * (1 / x.LifetimeYears + x.WearReplacementSharePerYear);
        double? priceFactor = x.PurchasePriceChfPerServer > 0
            ? (income + heatValue + co2 - electricity - operations - installation) / perChfOfPurchase
              / x.PurchasePriceChfPerServer : null;
        // Undiscounted one-off installation budget per site that the surplus pays back over the lifetime.
        double affordableInstallation = (surplus + installation) * x.LifetimeYears;
        double heatFee = op.HeatUsedKwh * x.HeatFeeChfPerKwh;
        var operatorModel = new ActorSplit(heatValue - heatFee, surplus - (heatValue - heatFee));
        double commission = x.PlatformCommissionShare * income;
        var hostOwned = new ActorSplit(surplus - (commission - operations), commission - operations);
        return new EconomicResult(premium, filler, heatValue, electricity, depreciation, wear, operations,
            installation, co2, surplus, caseBreakEven, fullBreakEven, priceFactor, affordableInstallation,
            operatorModel, hostOwned);
    }

    /// <summary>
    /// Smallest premium share on the grid 0, step, ..., 1 for which <paramref name="criterion"/>
    /// holds; null if it never holds. Linear search because the year simulation is not linear in p
    /// (tank, caps).
    /// </summary>
    public static double? RequiredPremiumShare(Func<double, EconomicResult> evaluateAtShare,
        Func<EconomicResult, bool> criterion, double step)
    {
        if (step is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(step));
        int n = (int)Math.Round(1 / step);
        for (int i = 0; i <= n; i++)
        {
            double share = Math.Min(1, Math.Round(i * step, 10));
            if (criterion(evaluateAtShare(share))) return share;
        }
        return null;
    }

    /// <summary>Inputs for one hardware profile from the central economics parameters (base case:
    /// creative-ideas variants are off unless co2Credits / ownPv are set).</summary>
    public static EconomicInputs InputsFor(HardwareProfile hardware, EconomicsParameters e,
        ReferenceParameters reference, HeatAlternative alternative, double electricityPriceChfPerKwh,
        double? fillerPriceOverride = null, bool co2Credits = false, bool ownPv = false)
    {
        var prices = e.PriceSetFor(hardware.Id);
        double ops = hardware.OpsChfPerServerYear.Value
            ?? throw new InvalidDataException($"{hardware.Id}: operations cost is TODO(source); economics needs it.");
        const double KgPerTonne = 1000;
        return new EconomicInputs(electricityPriceChfPerKwh,
            e.HeatPriceChfPerKwh(alternative, electricityPriceChfPerKwh, reference.HeatPumpEfficiency.Value),
            reference.HeatPumpEfficiency.Value, prices.PremiumPriceChfPerServerHour.Value ?? 0,
            fillerPriceOverride ?? prices.FillerPriceChfPerServerHour.Value!.Value,
            hardware.PurchasePriceChfPerServer.Value!.Value, hardware.LifetimeYears.Value!.Value, ops,
            e.WearReplacementSharePerYear.Value, e.HeatFeeChfPerKwh.Value, e.PlatformCommissionShare.Value,
            e.InstallationChfPerSite.Value,
            co2Credits ? e.Co2KgPerKwhHeat(alternative) * e.Variants.Co2CreditChfPerTonne.Value / KgPerTonne : 0,
            ownPv ? e.Variants.PvSurplusHoursPerServerYear.Value : 0,
            ownPv ? e.Variants.PvSurplusElectricityPriceChfPerKwh.Value : 0);
    }
}
