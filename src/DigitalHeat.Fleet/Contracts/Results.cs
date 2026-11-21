namespace DigitalHeat.Fleet;

// Engine inputs and results (§10, §11). The engine (Engine/) fills these; Output/ writes them.

public enum RunMode { Event, Tick }

/// <summary>House timeseries granularity (§11): none / daily / hourly / events (one row per segment).</summary>
public enum TimeseriesMode { None, Daily, Hourly, Events }

public sealed record EngineHouse(HouseSpec Spec, int ServerCount, HouseDemand Demand);

/// <summary>Everything the engine needs; built by FleetSimulation from a Scenario.</summary>
public sealed class EngineInputs
{
    public required long EndMs { get; init; }
    public required int StartHourOfWeek { get; init; }
    public required RunMode Mode { get; init; }
    public required long TickMs { get; init; }
    public required ServerParams ServerParams { get; init; }
    public required IReadOnlyList<EngineHouse> Houses { get; init; }
    public required IReadOnlyList<JobType> JobTypes { get; init; }
    /// <summary>Sorted by (ArrivalMs, TypeIndex, JobKey); JobId = index.</summary>
    public required IReadOnlyList<JobRequest> Jobs { get; init; }
    public required IFleetScheduler Scheduler { get; init; }
    public required IPricing Pricing { get; init; }
    public required IForecastProvider Forecast { get; init; }
    public required TimeseriesMode Timeseries { get; init; }
}

/// <summary>
/// Additive energy and money account (§10). Used per house (whole run), per house and period
/// (timeseries) and network-wide per hour. All kWh, CHF, server-hours.
/// </summary>
public sealed class EnergyAccount
{
    public double SpaceHeatKwh, HotWaterKwh, DemandKwh;
    public double ElectricOverheadKwh, ElectricT1Kwh, ElectricT2Kwh;
    public double ProducedKwh, DirectKwh, ChargeKwh, DischargeKwh, DumpedKwh, BackupKwh;
    /// <summary>Σ load × hours over installed servers (utilisation numerator).</summary>
    public double LoadServerHours;
    /// <summary>Σ hours with a chunk at load &gt; 0 (busy_share numerator).</summary>
    public double BusyServerHours;
    /// <summary>Σ hours of installed servers (utilisation denominator).</summary>
    public double InstalledServerHours;
    public double IncomeT1Chf, IncomeT2Chf;
    /// <summary>Units / kWh of work done after the job's deadline (mixed units summed as reported).</summary>
    public double UnitsLate, LateWorkKwh;
    public int Pauses;

    public double ElectricKwh => ElectricOverheadKwh + ElectricT1Kwh + ElectricT2Kwh;
    /// <summary>heat_used = demand − backup (case, §10): includes tank discharge.</summary>
    public double UsedKwh => DemandKwh - BackupKwh;

    public void Add(EnergyAccount o)
    {
        SpaceHeatKwh += o.SpaceHeatKwh; HotWaterKwh += o.HotWaterKwh; DemandKwh += o.DemandKwh;
        ElectricOverheadKwh += o.ElectricOverheadKwh; ElectricT1Kwh += o.ElectricT1Kwh; ElectricT2Kwh += o.ElectricT2Kwh;
        ProducedKwh += o.ProducedKwh; DirectKwh += o.DirectKwh; ChargeKwh += o.ChargeKwh;
        DischargeKwh += o.DischargeKwh; DumpedKwh += o.DumpedKwh; BackupKwh += o.BackupKwh;
        LoadServerHours += o.LoadServerHours; BusyServerHours += o.BusyServerHours;
        InstalledServerHours += o.InstalledServerHours;
        IncomeT1Chf += o.IncomeT1Chf; IncomeT2Chf += o.IncomeT2Chf;
        UnitsLate += o.UnitsLate; LateWorkKwh += o.LateWorkKwh; Pauses += o.Pauses;
    }
}

public sealed record HouseResult(
    HouseSpec Spec,
    int ServerCount,
    int HeatingDays,
    EnergyAccount Account,
    double TankFinalKwh)
{
    public double ElectricityChf => Account.ElectricKwh * Spec.ElectricityPriceChfPerKwh;
    /// <summary>Case: heat value = heat used × electricity price ÷ heat-pump efficiency.</summary>
    public double HeatValueChf(ServerParams p) => Account.UsedKwh * Spec.ElectricityPriceChfPerKwh / p.HeatPumpEfficiency;
    public double IncomeChf => Account.IncomeT1Chf + Account.IncomeT2Chf;
    public double MarginChf(ServerParams p) => IncomeChf + HeatValueChf(p) - ElectricityChf;
    public double Coverage => Account.DemandKwh > 0 ? Account.UsedKwh / Account.DemandKwh : 0;
    public double Utilisation => Account.InstalledServerHours > 0 ? Account.LoadServerHours / Account.InstalledServerHours : 0;
    public double BusyShare => Account.InstalledServerHours > 0 ? Account.BusyServerHours / Account.InstalledServerHours : 0;
    /// <summary>Case break-even (§10): (electricity − heat value) ÷ (electric kWh ÷ max kW), CHF per server-hour.</summary>
    public double? BreakEvenChfPerServerHour(ServerParams p) => Account.ElectricKwh > 0
        ? (ElectricityChf - HeatValueChf(p)) / (Account.ElectricKwh / p.MaxElectricKw) : null;
}

public sealed record JobResult(
    JobRequest Request,
    string Status,            // done | open | rejected_price
    double QuoteChf,          // per billed unit
    bool Accepted,
    long? FirstStartMs,
    long? FinishMs,
    bool DeadlineMissed,      // finished after the deadline, or open at run end with deadline < end
    double LatenessS,         // finish − deadline (open: end − deadline), ≥ 0
    int Chunks,
    int Pauses,
    int Stops,
    int Moves,
    int HousesUsed,
    double UnitsDone,
    double UnitsLate,
    double IncomeChf,
    double EnergyKwh);        // electric kWh incl. overheads

public sealed record NetworkHourRow(
    int Hour,
    EnergyAccount Account,
    double ElectricKwMean,
    int RunningChunks,        // at the end of the hour, phase Overhead/Working/Checkpointing with load > 0
    int QueueLengthT1, int QueueLengthT2,       // open jobs at the end of the hour
    double RemainingKwhT1, double RemainingKwhT2,
    int Arrivals, int Rejections, int Completions, int Misses,
    double? MeanT2QuoteChf);

/// <summary>One timeseries row: house, period [StartMs, EndMs), its account and tank at EndMs.</summary>
public sealed record HouseTimeseriesRow(int HouseIndex, long StartMs, long EndMs, EnergyAccount Account, double TankEndKwh);

public sealed class FleetRunResult
{
    public required IReadOnlyList<HouseResult> Houses { get; init; }
    public required IReadOnlyList<JobResult> Jobs { get; init; }
    public required IReadOnlyList<NetworkHourRow> NetworkHours { get; init; }
    public required IReadOnlyList<HouseTimeseriesRow> HouseTimeseries { get; init; }
    public required long EventCount { get; init; }
    public required long SchedulerCalls { get; init; }
    public required double WallTimeS { get; init; }
}
