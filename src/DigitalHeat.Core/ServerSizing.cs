namespace DigitalHeat.Core;

/// <summary>One server count of a sizing sweep: the physical year result of the shared year model.</summary>
public sealed record SizingRow(int ServerCount, int Hours, double ServerHours, double MarginalServerHours,
    double UsedHeatKwh, double DumpedHeatKwh, double Coverage, double Utilisation,
    double ElectricityCostChf, double HeatValueChf);

/// <summary>
/// Money inputs of the sizing objective. <see cref="FixedChfPerServerYear"/> is depreciation plus
/// operations per server and year (e.g. <see cref="HardwareProfile"/>); <see cref="IdleElectricKw"/>
/// is the draw of a server while it has no load (0 if unsourced); idle heat is not credited.
/// </summary>
public sealed record SizingEconomics(double ComputePriceChfPerServerHour, double FixedChfPerServerYear,
    double IdleElectricKw, double ElectricityPriceChfPerKwh)
{
    public static SizingEconomics From(HardwareProfile hw, double computePriceChfPerServerHour,
        double electricityPriceChfPerKwh) => new(computePriceChfPerServerHour,
        hw.DepreciationChfPerServerYear + (hw.OpsChfPerServerYear.Value
            ?? throw new InvalidDataException($"{hw.Id} has no ops cost (TODO(source)).")),
        hw.IdleElectricKw.Value ?? 0, electricityPriceChfPerKwh);
}

/// <summary>Annual result of one server count under one set of money inputs.</summary>
public sealed record SizingValue(int ServerCount, double ComputingIncomeChf, double IdleElectricityChf,
    double FixedCostChf, double VariableContributionChf, double ContributionChf,
    double MarginalVariableContributionChf);

/// <summary>
/// Server count per house (T-70). The heat-first year model is run for every count n with
/// unlimited sellable work, so the sold server-hours H(n) are the most the house's heat demand can
/// absorb. The annual contribution is
/// <c>C(n) = H(n)·price + used_heat_value(n) − electricity(n) − idle_electricity(n) − n·fixed·run_hours/8760</c>
/// and the optimum is its argmax. Adding server n pays off iff its marginal variable contribution
/// ΔV(n) = V(n) − V(n−1) exceeds the fixed cost of one server: the screening-curve rule of
/// capacity planning (load-duration curve vs. annual fixed cost).
/// </summary>
public static class ServerSizing
{
    /// <summary>Calendar hours of a 365-day year; used only to pro-rate annual fixed costs to the run.</summary>
    public const double HoursPerYear = 8760;

    /// <summary>
    /// Largest count that can still add heat: in one step the house can take at most its demand plus
    /// a full tank charge, so more than ceil((peak_demand_kwh + tank_kwh) / heat_per_server_step)
    /// servers can never all run.
    /// </summary>
    public static int PhysicalUpperBound(IReadOnlyList<DemandHour> demand, ReferenceParameters p)
    {
        double perServer = p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * p.HoursPerStep.Value;
        return (int)Math.Ceiling((demand.Max(x => x.TotalHeatKwh) + p.TankCapacityKwh.Value) / perServer - 1e-9);
    }

    /// <summary>
    /// Runs <see cref="YearSimulation"/> for n = 0..maxCount with the shared
    /// <see cref="HeatFirstScheduler"/> (no mandatory minimum load: unlimited, pausable work).
    /// Stops early at the first count that covers all heat demand.
    /// </summary>
    public static IReadOnlyList<SizingRow> Sweep(IReadOnlyList<DemandHour> demand, ReferenceParameters p,
        int? maxCount = null)
    {
        int max = maxCount ?? PhysicalUpperBound(demand, p);
        if (max < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
        double total = demand.Sum(x => x.TotalHeatKwh);
        var rows = new List<SizingRow>(max + 1) { new(0, demand.Count, 0, 0, 0, 0, 0, 0, 0, 0) };
        var scheduler = new HeatFirstScheduler(0);
        for (int n = 1; n <= max; n++)
        {
            var s = YearSimulation.Run(demand, WithServerCount(p, n), scheduler, new SimulatedExecutor()).Summary;
            rows.Add(new SizingRow(n, demand.Count, s.ServerHours, s.ServerHours - rows[^1].ServerHours,
                s.ServerHeatUsedKwh, s.DumpedHeatKwh, total > 0 ? s.ServerHeatUsedKwh / total : 0,
                s.Utilisation, s.ElectricityCostChf, s.HeatValueChf));
            // Once all demand is covered, more servers cannot sell another hour (no dump allowed).
            if (rows[^1].Coverage >= 1 - 1e-9) break;
        }
        return rows;
    }

    /// <summary>Annual money of every row; see the class summary for the formula.</summary>
    public static IReadOnlyList<SizingValue> Value(IReadOnlyList<SizingRow> rows, SizingEconomics e,
        double hoursPerStep = 1)
    {
        var result = new List<SizingValue>(rows.Count);
        double previous = 0;
        foreach (var r in rows)
        {
            double income = r.ServerHours * e.ComputePriceChfPerServerHour;
            double idleServerHours = Math.Max(0, r.ServerCount * r.Hours * hoursPerStep - r.ServerHours);
            double idle = idleServerHours * e.IdleElectricKw * e.ElectricityPriceChfPerKwh;
            double variable = income + r.HeatValueChf - r.ElectricityCostChf - idle;
            double fixedCost = r.ServerCount * e.FixedChfPerServerYear * r.Hours * hoursPerStep / HoursPerYear;
            result.Add(new SizingValue(r.ServerCount, income, idle, fixedCost, variable, variable - fixedCost,
                r.ServerCount == 0 ? 0 : variable - previous));
            previous = variable;
        }
        return result;
    }

    /// <summary>Count with the highest contribution; ties go to fewer servers (less capital).</summary>
    public static int OptimalCount(IReadOnlyList<SizingValue> values) =>
        values.Aggregate((best, x) => x.ContributionChf > best.ContributionChf + 1e-9 ? x : best).ServerCount;

    /// <summary>
    /// Sold hours h a server needs per year to cover its fixed cost and the idle draw of its unsold
    /// hours: <c>h·margin = F + idle_kw·elec·(8760 − h)</c>, with
    /// <c>margin = price − kW·elec + kW·recovery·elec/COP</c> (heat used: heat-first, no dump).
    /// Idle is charged in every unsold hour (no power-off; conservative). +∞ if no h ≤ 8760 works.
    /// </summary>
    public static double BreakEvenServerHoursPerYear(SizingEconomics e, ReferenceParameters p)
    {
        double elec = e.ElectricityPriceChfPerKwh;
        double margin = e.ComputePriceChfPerServerHour - p.ServerElectricKw.Value * elec
            + p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * elec / p.HeatPumpEfficiency.Value;
        double idle = e.IdleElectricKw * elec;
        if (margin + idle <= 0) return double.PositiveInfinity;
        double h = (e.FixedChfPerServerYear + idle * HoursPerYear) / (margin + idle);
        return h <= HoursPerYear ? h : double.PositiveInfinity;
    }

    /// <summary>True if every added server adds no more hours than the one before (diminishing returns).</summary>
    public static bool HasDiminishingReturns(IReadOnlyList<SizingRow> rows, double toleranceHours = 1e-6)
    {
        for (int i = 2; i < rows.Count; i++)
            if (rows[i].MarginalServerHours > rows[i - 1].MarginalServerHours + toleranceHours) return false;
        return true;
    }

    /// <summary>The same parameters with another server count.</summary>
    public static ReferenceParameters WithServerCount(ReferenceParameters b, int n) => new()
    {
        HoursPerDay = b.HoursPerDay, HoursPerStep = b.HoursPerStep, DesignHeatingKw = b.DesignHeatingKw,
        BaseTemperatureC = b.BaseTemperatureC, FormulaDivisorC = b.FormulaDivisorC,
        HeatingDayThresholdC = b.HeatingDayThresholdC, HotWaterKwhPerDay = b.HotWaterKwhPerDay,
        TankCapacityKwh = b.TankCapacityKwh, ServerElectricKw = b.ServerElectricKw,
        HeatRecoveryShare = b.HeatRecoveryShare, HeatPumpEfficiency = b.HeatPumpEfficiency,
        ServerCount = b.ServerCount with { Value = n, Source = "ServerSizing sweep value" },
        MinimumLoadShare = b.MinimumLoadShare, ElectricityPriceChfPerKwh = b.ElectricityPriceChfPerKwh,
        ComputePriceChfPerServerHour = b.ComputePriceChfPerServerHour
    };
}
