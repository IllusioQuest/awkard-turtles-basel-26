using System.Text.Json;
using DigitalHeat.Core;

namespace DigitalHeat.VoiceDemo;

/// <summary>
/// The /live dashboard snapshot (same fields as T-72's LiveFleetRun, read by FleetApi
/// /api/live-household) plus a <see cref="VoiceState"/> block. House values are modelled;
/// processStatus, stage, transcript, answer, CPU time and energy are observed from the real container.
/// </summary>
internal sealed class Snapshot
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const double MsPerHour = 3_600_000;

    public string RunId { get; set; } = "voice-demo";
    public string Season { get; set; } = "summer";
    public bool Busy { get; set; }
    public string StartLocal { get; set; } = "";
    public string Status { get; set; } = "waiting";
    public string ProcessStatus { get; set; } = "no request yet";
    public string JobId { get; set; } = "";
    public string? Error { get; set; }
    public double SimulatedMs { get; set; }
    public double DemandKw { get; set; }
    public double TankKwh { get; set; }
    public double CpuCores { get; set; }
    public double TankCapacityKwh { get; set; }
    public double TargetLoad { get; set; }
    public int TasksDone { get; set; }
    public double? LatestEnergyPerSpin { get; set; }
    public int WaitingJobs { get; set; }
    public int RunningJobs { get; set; }
    public int PausedJobs { get; set; }
    public bool EventsTruncated { get; set; }
    public double LagSeconds { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public List<Point> History { get; set; } = [];
    public List<LiveEvent> Events { get; set; } = [];
    public VoiceState Voice { get; set; } = new();

    public sealed record Point(double SimulatedMs, double DemandKw, double HeatProducedKw, double TankKwh, double TargetLoad);
    public sealed record LiveEvent(double SimulatedMs, string Kind, string JobId, bool Observed);
    public sealed class VoiceState
    {
        public string Stage { get; set; } = "";
        public string Transcript { get; set; } = "";
        public string Answer { get; set; } = "";
        public double UsefulHeatKwh { get; set; }
        public double RequestDeclaredHeatKwhPerHour { get; set; }
        public double RequestPowerW { get; set; }
        public double RequestCpuSeconds { get; set; }
        public double? RequestEnergyKwh { get; set; }
        public string HotWaterProfile { get; set; } = "";
        public string Scheduler { get; set; } = "mesh heat-first (DigitalHeat.Orchestration MeshPeer)";
    }

    public static double Ms(double localHour) => localHour * MsPerHour;

    public static Snapshot Create(DemoConfig c, ReferenceParameters p, IReadOnlyList<Point> prefill) => new()
    {
        StartLocal = c.DateLocal + "T00:00", CpuCores = c.RequestCpuCores, TankCapacityKwh = p.TankCapacityKwh.Value,
        History = [.. prefill],
        Voice = { RequestDeclaredHeatKwhPerHour = c.RequestDeclaredHeatKwhPerHour, HotWaterProfile = "EU 814/2013 load profile L, scaled to 12 kWh/day" }
    };

    public static Point PointOf(HouseReplay house, double requestHeatKwhPerHour, double serverCapacityKwhPerHour)
    {
        double heat = house.FillerHeatKwhPerHour + requestHeatKwhPerHour;
        return new(Ms(house.LocalHour), house.DemandKwhPerHour, heat, house.TankKwh, heat / serverCapacityKwhPerHour);
    }

    public void Update(HouseReplay house, string phase, string stage, string transcript, string answer, double requestHeatKwhPerHour,
        double serverCapacityKwhPerHour, ContainerEnergyMeter? meter, double lagSeconds)
    {
        var point = PointOf(house, requestHeatKwhPerHour, serverCapacityKwhPerHour);
        SimulatedMs = point.SimulatedMs; DemandKw = point.DemandKw; TankKwh = point.TankKwh; TargetLoad = point.TargetLoad;
        LagSeconds = lagSeconds;
        ProcessStatus = phase switch
        {
            "running" => "running", "paused" => "frozen", "pausing" => "freezing", "resuming" => "thawing",
            "terminal" or "removing" or "released" => stage == "done" ? "finished" : "exited",
            "proposed" or "accepted" or "reserved" or "creating" or "created" or "starting" => "starting",
            _ => phase
        };
        WaitingJobs = phase is "submitted" or "proposed" ? 1 : 0;
        RunningJobs = phase == "running" ? 1 : 0;
        PausedJobs = phase == "paused" ? 1 : 0;
        Voice.Stage = stage; Voice.Transcript = transcript; Voice.Answer = answer;
        Voice.UsefulHeatKwh = HeatCapacity.RemainingKwh(house.Signal());
        if (meter is not null)
        {
            Voice.RequestPowerW = phase == "running" ? meter.PowerWatts : 0;
            Voice.RequestCpuSeconds = meter.CpuSeconds;
            Voice.RequestEnergyKwh = meter.HasEnergy ? meter.EnergyKwh : null;
        }
        History.Add(point);
    }
}
