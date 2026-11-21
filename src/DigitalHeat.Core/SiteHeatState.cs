using System.Globalization;

namespace DigitalHeat.Core;

public enum HeatForecastKind { PerfectForesight, SyntheticError }

/// <summary>Gross site demand for a nominal local hour, in kWh. Not net of tank discharge.</summary>
public sealed record ForecastHour(string LocalTime, double NeededHeatKwh);

/// <summary>Immutable forecast; element zero is the hour after CurrentLocalTime.</summary>
public sealed class HeatForecast
{
    public HeatForecastKind Kind { get; }
    public string SourceSeriesId { get; }
    public int SourceIndex { get; }
    public string CurrentLocalTime { get; }
    public double CurrentDemandKwh { get; }
    public IReadOnlyList<ForecastHour> Hours { get; }

    private HeatForecast(HeatForecastKind kind, string sourceSeriesId, int sourceIndex,
        string currentLocalTime, double currentDemandKwh, ForecastHour[] hours)
    {
        Kind = kind;
        SourceSeriesId = sourceSeriesId;
        SourceIndex = sourceIndex;
        CurrentLocalTime = currentLocalTime;
        CurrentDemandKwh = currentDemandKwh;
        Hours = Array.AsReadOnly(hours);
    }

    /// <summary>
    /// Copies future source demand. Optional synthetic errors use actual * (1 + error),
    /// with error >= -1. Local labels follow the dataset's 24 nominal hours/day convention;
    /// this deliberately does not convert DST transitions to elapsed UTC hours. See ADR 0008.
    /// </summary>
    public static HeatForecast Build(IReadOnlyList<DemandHour> source, string sourceSeriesId,
        int currentIndex, int horizonHours, IReadOnlyList<double>? fractionalErrors = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSeriesId);
        if (currentIndex < 0 || currentIndex >= source.Count || horizonHours < 1 ||
            horizonHours > source.Count - currentIndex - 1)
            throw new ArgumentOutOfRangeException(nameof(horizonHours), "A complete future horizon is required.");
        if (fractionalErrors is not null && fractionalErrors.Count != horizonHours)
            throw new ArgumentException("Error count must match the horizon.", nameof(fractionalErrors));
        var current = source[currentIndex];
        var start = ParseHour(current.LocalTime);
        ValidateDemand(current);
        var hours = new ForecastHour[horizonHours];
        for (int i = 0; i < horizonHours; i++)
        {
            var hour = source[currentIndex + i + 1];
            if (ParseHour(hour.LocalTime) != start.AddHours(i + 1))
                throw new ArgumentException("Source must contain consecutive nominal local hours.", nameof(source));
            ValidateDemand(hour);
            double error = fractionalErrors?[i] ?? 0;
            if (!double.IsFinite(error) || error < -1)
                throw new ArgumentOutOfRangeException(nameof(fractionalErrors));
            double predicted = hour.TotalHeatKwh * (1 + error);
            if (!double.IsFinite(predicted))
                throw new ArgumentOutOfRangeException(nameof(fractionalErrors), "Forecast arithmetic overflowed.");
            hours[i] = new ForecastHour(hour.LocalTime, Math.Max(0, predicted));
        }
        return new HeatForecast(fractionalErrors is null ? HeatForecastKind.PerfectForesight : HeatForecastKind.SyntheticError,
            sourceSeriesId, currentIndex, current.LocalTime, current.TotalHeatKwh, hours);
    }

    private static DateTime ParseHour(string label)
    {
        if (!DateTime.TryParseExact(label, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var value) || value.Minute != 0)
            throw new ArgumentException("Expected a nominal local hour label yyyy-MM-ddTHH:00.", nameof(label));
        return value;
    }

    private static void ValidateDemand(DemandHour hour)
    {
        if (!double.IsFinite(hour.SpaceHeatKwh) || hour.SpaceHeatKwh < 0 ||
            !double.IsFinite(hour.HotWaterKwh) || hour.HotWaterKwh < 0 || !double.IsFinite(hour.TotalHeatKwh))
            throw new ArgumentException("Demand components and their sum must be finite and nonnegative.");
    }
}

/// <summary>One building's hourly state. Do not duplicate its demand for each server.</summary>
public sealed class SiteHeatState
{
    public string SiteId { get; }
    public HeatSignal Signal { get; }
    public HeatForecast Forecast { get; }
    public string CurrentLocalTime => Forecast.CurrentLocalTime;
    public double NeededHeatKwh => Signal.DemandKwh;
    public double MaxHeatProducedKwh => Signal.ServerCount * Signal.RecoverableKwhPerServerAtFullLoad;

    public SiteHeatState(string siteId, HeatSignal signal, HeatForecast forecast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(forecast);
        if (!double.IsFinite(signal.DemandKwh) || signal.DemandKwh < 0 ||
            !double.IsFinite(signal.TankStoredKwh) || signal.TankStoredKwh < 0 ||
            !double.IsFinite(signal.TankCapacityKwh) || signal.TankCapacityKwh < signal.TankStoredKwh ||
            !double.IsFinite(signal.ServerCount) || signal.ServerCount <= 0 ||
            !double.IsFinite(signal.RecoverableKwhPerServerAtFullLoad) || signal.RecoverableKwhPerServerAtFullLoad <= 0 ||
            !double.IsFinite(signal.ServerCount * signal.RecoverableKwhPerServerAtFullLoad))
            throw new ArgumentOutOfRangeException(nameof(signal));
        if (signal.DemandKwh != forecast.CurrentDemandKwh)
            throw new ArgumentException("Forecast source current demand must match the site's signal.", nameof(forecast));
        SiteId = siteId;
        Signal = signal;
        Forecast = forecast;
    }
}

public interface ISiteHeatScheduler
{
    SchedulerDecision Decide(SiteHeatState state);
}

/// <summary>Preserves an existing scheduler's policy; forecasts are available but unused by this baseline.</summary>
public sealed class CurrentHeatSchedulerAdapter : ISiteHeatScheduler
{
    private readonly IScheduler scheduler;
    public CurrentHeatSchedulerAdapter(IScheduler scheduler) =>
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    public SchedulerDecision Decide(SiteHeatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return scheduler.Decide(state.Signal);
    }
}
