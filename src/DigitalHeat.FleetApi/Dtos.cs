namespace DigitalHeat.FleetApi;

// JSON contract: web/src/data/types.ts (camelCase via the minimal-API defaults). Units are in the names.

public sealed record RunInfo(string Id, string Scenario, string StartLocal, int Hours, int PeriodHours, int Houses, int Servers,
    double MaxElectricKwPerServer, double HeatRecoveryShare, bool Mock, string Note);

public sealed record HouseSummary(double HeatDemandKwh, double HeatUsedKwh, double HeatDumpedKwh, double HeatBackupKwh,
    double ElectricKwh, double Coverage, double Utilisation, double IncomeChf, double ElectricityCostChf, double HeatValueChf,
    double MarginChf, double? BreakEvenChfPerServerHour, double LateWorkKwh, double Pauses);

public sealed record HouseInfo(string Id, string Municipality, string Canton, string CellId, double Lat, double Lon, int Servers,
    double TankKwh, double ElectricityPriceChfPerKwh, HouseSummary Summary);

public sealed record NetworkHour(int H, double HeatDemandKwh, double HeatProducedKwh, double HeatUsedKwh, double HeatDumpedKwh,
    double HeatBackupKwh, double ElectricKwh, double Load, double IncomeChf, double ElectricityCostChf, double Arrivals,
    double Completions, double Rejections, double Misses, double QueueT1, double QueueT2);

public sealed record HousePeriod(int H, double Load, double TemperatureC, double HeatDemandKwh, double HeatProducedKwh,
    double HeatUsedKwh, double HeatDumpedKwh, double HeatBackupKwh, double ElectricKwh, double TankEndKwh, double IncomeChf,
    double ElectricityCostChf, double LateWorkKwh, double HeatValueChf, double JobsOnTime, double DeadlinesMissed);

public sealed record Snapshot(int H, int PeriodStartH, int PeriodHours, HousePeriod[] Houses);

public sealed record WeatherPoint(string Id, double Lat, double Lon);

public sealed record WeatherHours(int FromH, float[][] TemperatureC);
