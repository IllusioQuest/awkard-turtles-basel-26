using Xunit;

namespace DigitalHeat.Fleet.Tests;

// ------------------------------------------------------------------ fakes

internal sealed record FixedDist(double Value) : IDistribution
{
    public double Sample(Rng rng) => Value;
    public double Mean => Value;
    public string Unit => "x";
    public string Source => "test";
}

internal sealed class FakeForecast(Func<int, int, int, double> f) : IForecastProvider
{
    public string Kind => "fake";
    public double HeatDemandKw(HouseId house, int issuedHour, int targetHour) => f(house.Value, issuedHour, targetHour);
}

internal sealed class FakeHouse(int index, HouseSpec spec, double cap) : IHouseView
{
    public HouseId Id { get; } = new(index);
    public HouseSpec Spec { get; } = spec;
    public double TankCapacityKwh { get; } = cap;
    public double TankKwh { get; set; }
    public double DemandKwNow { get; set; }
    public FakeFleet? Fleet;
    public double ElectricKwNow => Servers.Sum(s => Fleet!.Servers[s.Value].Load) * Fleet!.ServerParams.MaxElectricKw;
    public List<ServerId> ServerIds { get; } = [];
    public IReadOnlyList<ServerId> Servers => ServerIds;
}

internal sealed class FakeServer(int id, int house) : IServerView
{
    public ServerId Id { get; } = new(id);
    public HouseId House { get; } = new(house);
    public double Load { get; set; }
    public ChunkId? Chunk { get; set; }
}

internal sealed class FakeJob(int id, JobRequest req, JobType type) : IJobView
{
    public JobId Id { get; } = new(id);
    public JobRequest Request { get; } = req;
    public JobType Type { get; } = type;
    public JobStatus Status { get; set; } = JobStatus.Waiting;
    public double RemainingUnits { get; set; } = req.WorkUnits;
    public double UnassignedUnits { get; set; } = req.WorkUnits;
    public List<ChunkId> Chunks { get; } = [];
    public IReadOnlyList<ChunkId> ActiveChunks => Chunks;
    public double QuoteChfPerBilledUnit => 0;
}

internal sealed class FakeChunk(FakeFleet fleet, int id, FakeJob job, FakeServer server, double units, double load) : IChunkView
{
    public ChunkId Id { get; } = new(id);
    public JobId Job { get; } = job.Id;
    public ServerId Server { get; } = server.Id;
    public HouseId House { get; } = server.House;
    public double WorkUnits { get; } = units;
    public double UnitsDone => 0;
    public ChunkPhase Phase { get; set; } = ChunkPhase.Working;
    public double Load { get; set; } = load;
    public double UnitsPerSecond => Phase == ChunkPhase.Working
        ? Load * fleet.ServerParams.MaxElectricKw / (fleet.Jobs[Job.Value].Type.EnergyKwhPerWorkUnit.Value * 3600) : 0;
    public long OverheadRemainingMs => 0;
}

/// <summary>A minimal fleet that validates every action like the engine would (throws on invalid ones).</summary>
internal sealed class FakeFleet : IFleetView, IFleetActions
{
    public long NowMs { get; set; }
    public int HourIndex => FleetTime.HourOf(NowMs);
    public long EndMs { get; set; } = 365L * 24 * FleetTime.MsPerHour;
    public int StartHourOfWeek { get; set; }
    public ServerParams ServerParams { get; } = new(1.0, 0.8, 2.5);
    public List<JobType> Types { get; } = [];
    public IReadOnlyList<JobType> JobTypes => Types;
    public List<FakeHouse> HouseList { get; } = [];
    public IReadOnlyList<IHouseView> Houses => HouseList;
    public List<FakeServer> ServerList { get; } = [];
    public IReadOnlyList<IServerView> Servers => ServerList;
    public List<FakeJob> Jobs { get; } = [];
    public List<FakeChunk> Chunks { get; } = [];
    public IReadOnlyCollection<JobId> OpenJobs => Jobs.Where(j => j.Status is JobStatus.Waiting or JobStatus.Active).Select(j => j.Id).ToList();
    public IForecastProvider Forecast { get; set; } = new FakeForecast((_, _, _) => 0);
    public Func<long, double> Committed { get; set; } = _ => 0;
    public double CommittedKwh(long deadlineMs) => Committed(deadlineMs);
    public IJobView Job(JobId id) => Jobs[id.Value];
    public IChunkView Chunk(ChunkId id) => Chunks[id.Value];

    public List<long> Wakeups { get; } = [];
    public List<(int House, double Kwh)> Thresholds { get; } = [];
    public IFleetScheduler? Scheduler;

    public FakeHouse AddHouse(int servers, double cap = 35, string? canton = "BS", double tank = 0, double demandKw = 0)
    {
        int idx = HouseList.Count;
        var spec = new HouseSpec($"h{idx}", 47, 7, "c", null, canton, "w", 10, 25.6, 12, cap, 0, 0.33, null);
        var h = new FakeHouse(idx, spec, cap) { Fleet = this, TankKwh = tank, DemandKwNow = demandKw };
        for (int k = 0; k < servers; k++)
        {
            var s = new FakeServer(ServerList.Count, idx);
            ServerList.Add(s);
            h.ServerIds.Add(s.Id);
        }
        HouseList.Add(h);
        return h;
    }

    public static JobType MakeType(string id, JobClass cls, double kwhPerUnit = 0.1, int maxChunks = 8, double minChunk = 1,
        bool allowPause = true, double minLoad = 0, bool checkpointable = true, double checkpointS = 0,
        string placement = "anywhere", double chfPerUnit = 0.36) => new()
    {
        Id = id, Index = 0, Class = cls, WorkUnit = "u",
        EnergyKwhPerWorkUnit = new Quantity(kwhPerUnit, "kWh", "test"),
        Size = new FixedDist(10), BatchSize = new FixedDist(1), DeadlineS = new FixedDist(3600),
        MaxChunks = maxChunks, MinChunkWorkUnits = minChunk, StartOverheadS = 0,
        Lifecycle = new JobLifecycle(allowPause, minLoad, checkpointable, 0, checkpointS),
        InputMbPerWorkUnit = 0, StateMb = 0,
        Placement = new FleetPlacementPolicy(placement, placement == "country_allowlist" ? ["CH"] : null, "test"),
        Volume = new JobVolume(1, "x", "test"),
        ArrivalShape = new ArrivalShape(new double[168], "f", "c"),
        OriginMix = new OriginMix(0, null),
        Price = cls == JobClass.Type1 ? new JobPrice("fixed", new Quantity(chfPerUnit, "CHF", "test")) : new JobPrice("dynamic", null),
    };

    public JobId AddJob(JobType type, double units, long arrivalMs, long deadlineMs, string origin = "anywhere", string? canton = null)
    {
        if (!Types.Contains(type)) Types.Add(type);
        var req = new JobRequest($"j{Jobs.Count}", type.Id, Types.IndexOf(type), arrivalMs, deadlineMs, units, 1, origin, null, null, null, canton);
        var job = new FakeJob(Jobs.Count, req, type);
        Jobs.Add(job);
        return job.Id;
    }

    // ---- scheduler driving
    public void Send(params FleetEvent[] events) => Scheduler!.OnEvents(this, events, this);
    public void Arrive(JobId j) => Send(new FleetEvent(FleetEventKind.JobArrived, NowMs, Job: j));
    public void HourStart() => Send(new FleetEvent(FleetEventKind.HourStart, NowMs));

    /// <summary>Finishes a pending checkpoint (the engine would send OverheadDone with phase Stopped).</summary>
    public void CompleteCheckpoint(ChunkId c)
    {
        var ch = Chunks[c.Value];
        ch.Phase = ChunkPhase.Stopped;
        ServerList[ch.Server.Value].Chunk = null;
        ServerList[ch.Server.Value].Load = 0;
        var job = Jobs[ch.Job.Value];
        job.Chunks.Remove(c);
        job.UnassignedUnits += ch.WorkUnits;
        Send(new FleetEvent(FleetEventKind.OverheadDone, NowMs, House: ch.House, Chunk: c, Server: ch.Server));
    }

    // ---- actions with the engine's validation rules
    public ChunkId Start(JobId jobId, ServerId serverId, double workUnits, double load)
    {
        var job = Jobs[jobId.Value];
        var server = ServerList[serverId.Value];
        var t = job.Type;
        if (server.Chunk is not null) throw new InvalidFleetActionException("server busy");
        if (workUnits > job.UnassignedUnits + 1e-9) throw new InvalidFleetActionException("too many units");
        if (workUnits < t.MinChunkWorkUnits - 1e-9 && workUnits < job.UnassignedUnits - 1e-9)
            throw new InvalidFleetActionException("chunk too small");
        if (job.Chunks.Count >= t.MaxChunks) throw new InvalidFleetActionException("max chunks");
        CheckPlacement(job, HouseList[server.House.Value]);
        CheckLoad(t, load);
        var chunk = new FakeChunk(this, Chunks.Count, job, server, workUnits, load);
        Chunks.Add(chunk);
        job.Chunks.Add(chunk.Id);
        job.UnassignedUnits -= workUnits;
        job.Status = JobStatus.Active;
        server.Chunk = chunk.Id;
        server.Load = load;
        return chunk.Id;
    }

    private static void CheckPlacement(FakeJob job, FakeHouse house)
    {
        if (job.Type.Placement.Mode == "origin_site" && job.Request.OriginKind == "client_site"
            && house.Spec.Canton != job.Request.OriginCanton)
            throw new InvalidFleetActionException("placement");
    }

    private static void CheckLoad(JobType t, double load)
    {
        if (load < -1e-12 || load > 1 + 1e-12) throw new InvalidFleetActionException("load range");
        if (load <= 1e-12 ? !t.Lifecycle.AllowPause : load < t.Lifecycle.MinLoad - 1e-12)
            throw new InvalidFleetActionException("load below min_load / pause not allowed");
    }

    public void SetLoad(ServerId serverId, double load)
    {
        var server = ServerList[serverId.Value];
        if (server.Chunk is not ChunkId cid) throw new InvalidFleetActionException("no chunk");
        var chunk = Chunks[cid.Value];
        if (chunk.Phase == ChunkPhase.Checkpointing) throw new InvalidFleetActionException("checkpointing");
        CheckLoad(Jobs[chunk.Job.Value].Type, load);
        chunk.Load = load;
        server.Load = load;
    }

    public void Stop(ChunkId c)
    {
        var chunk = Chunks[c.Value];
        if (chunk.Phase == ChunkPhase.Checkpointing) throw new InvalidFleetActionException("checkpointing");
        var job = Jobs[chunk.Job.Value];
        if (job.Type.Lifecycle.Checkpointable && job.Type.Lifecycle.CheckpointOverheadS > 0)
        {
            chunk.Phase = ChunkPhase.Checkpointing;
            return;
        }
        chunk.Phase = ChunkPhase.Stopped;
        var server = ServerList[chunk.Server.Value];
        server.Chunk = null;
        server.Load = 0;
        job.Chunks.Remove(c);
        job.UnassignedUnits += chunk.WorkUnits;
    }

    public void RequestWakeup(long timeMs)
    {
        if (timeMs < NowMs) throw new InvalidFleetActionException("wakeup in the past");
        Wakeups.Add(timeMs);
    }

    public void RegisterTankThreshold(HouseId house, double tankKwh) => Thresholds.Add((house.Value, tankKwh));

    public double[] Loads(int house) => HouseList[house].ServerIds.Select(s => ServerList[s.Value].Load).ToArray();
}

// ------------------------------------------------------------------ server count

public class ServerCountPolicyTests
{
    private static readonly ServerParams P = new(1.0, 0.8, 2.5);
    private static HouseSpec House(int? n = null) =>
        new("h", 47, 7, "c", null, "BS", "w", 10, 25.6, 12, 35, 0, 0.33, n);

    [Fact]
    public void ShareOfPeakRoundsUp()
    {
        var d = new HouseDemandSummary(10, 0, 0);
        Assert.Equal(13, new ShareOfPeakServerCount(1.0, P).ServerCount(House(), d));   // 12.5 -> 13
        Assert.Equal(7, new ShareOfPeakServerCount(0.5, P).ServerCount(House(), d));    // 6.25 -> 7
    }

    [Fact]
    public void ShareOfPeakIgnoresFloatNoise()
    {
        // 2.4000000000000004 / 0.8 = 3.0000000000000004 must stay 3.
        Assert.Equal(3, new ShareOfPeakServerCount(1.0, P).ServerCount(House(), new HouseDemandSummary(2.4000000000000004, 0, 0)));
    }

    [Fact]
    public void FixedAndFromFile()
    {
        var d = new HouseDemandSummary(10, 0, 0);
        Assert.Equal(4, new FixedServerCount(4).ServerCount(House(), d));
        Assert.Equal(6, new FromFileServerCount().ServerCount(House(6), d));
        Assert.Throws<InvalidOperationException>(() => new FromFileServerCount().ServerCount(House(null), d));
    }

    [Fact]
    public void FactoryBuildsPolicies()
    {
        var d = new HouseDemandSummary(10, 0, 0);
        Assert.Equal(3, ServerCountPolicies.FromConfig(new ServersSection { Policy = "fixed", N = 3 }, P).ServerCount(House(), d));
        Assert.Equal(13, ServerCountPolicies.FromConfig(new ServersSection { Policy = "share_of_peak", F = 1 }, P).ServerCount(House(), d));
        Assert.Equal(5, ServerCountPolicies.FromConfig(new ServersSection { Policy = "from_file" }, P).ServerCount(House(5), d));
        Assert.Throws<InvalidDataException>(() => ServerCountPolicies.FromConfig(new ServersSection { Policy = "fixed" }, P));
        Assert.Throws<InvalidDataException>(() => ServerCountPolicies.FromConfig(new ServersSection { Policy = "nope" }, P));
    }
}

// ------------------------------------------------------------------ pricing

public class PricingTests
{
    private static readonly JobType T1 = FakeFleet.MakeType("t1", JobClass.Type1, chfPerUnit: 0.36);
    private static readonly JobType T2 = FakeFleet.MakeType("t2", JobClass.Type2, kwhPerUnit: 1.0);

    private static PricingSection Cfg(double gamma = 1, double sd = 0, double pRef = 0.82, double eps = -0.8) => new()
    {
        PMinChf = 0.226, PMaxChf = 1.20, Gamma = gamma, PRefChf = pRef, PRefNoiseSd = sd, Elasticity = eps,
    };

    /// <summary>2 houses x 4 servers, forecast demand far above server capacity: capacity = 8 kW.</summary>
    private static FakeFleet Fleet(Func<int, int, int, double>? forecast = null)
    {
        var f = new FakeFleet();
        f.AddHouse(4); f.AddHouse(4);
        f.Forecast = new FakeForecast(forecast ?? ((_, _, _) => 100));
        return f;
    }

    private static IJobView Job2(FakeFleet f, double units, long arrivalMs, long deadlineMs)
        => f.Job(f.AddJob(T2, units, arrivalMs, deadlineMs));

    [Fact]
    public void Type1UsesFixedPriceAndIsAccepted()
    {
        var f = Fleet();
        var j = f.Job(f.AddJob(T1, 5, 0, 3_600_000));
        foreach (var pricing in new IPricing[] { new FixedOnlyPricing(Cfg()), new MockLoadPricing(Cfg(), 1) })
        {
            var q = pricing.Quote(j, f);
            Assert.Equal(0.36, q.PriceChfPerBilledUnit);
            Assert.True(q.Accepted);
        }
    }

    [Fact]
    public void FixedOnlyType2IsPMinAccepted()
    {
        var f = Fleet();
        var q = new FixedOnlyPricing(Cfg()).Quote(Job2(f, 5, 0, 3_600_000), f);
        Assert.Equal(0.226, q.PriceChfPerBilledUnit);
        Assert.True(q.Accepted);
    }

    [Fact]
    public void PriceFollowsUtilisationAndGamma()
    {
        var f = Fleet();
        // Window 10 h, capacity 8 kW x 10 h = 80 kWh.
        var job = Job2(f, 0.0, 0, 10 * FleetTime.MsPerHour);
        var p = new MockLoadPricing(Cfg(), 1);

        f.Committed = _ => 0;                       // job of 0 units: u = 0
        Assert.Equal(0.226, p.Quote(job, f).PriceChfPerBilledUnit, 12);
        Assert.Equal(80, p.LastQuote!.CapacityKwh, 9);
        Assert.Equal(0, p.LastQuote.U);

        f.Committed = _ => 40;                      // u = 0.5
        Assert.Equal(0.226 + (1.2 - 0.226) * 0.5, p.Quote(job, f).PriceChfPerBilledUnit, 12);
        f.Committed = _ => 80;                      // u = 1
        Assert.Equal(1.2, p.Quote(job, f).PriceChfPerBilledUnit, 12);
        f.Committed = _ => 500;                     // u capped at 1
        Assert.Equal(1.2, p.Quote(job, f).PriceChfPerBilledUnit, 12);
        Assert.Equal(1, p.LastQuote!.U);

        var p2 = new MockLoadPricing(Cfg(gamma: 2), 1);
        f.Committed = _ => 40;
        Assert.Equal(0.226 + (1.2 - 0.226) * 0.25, p2.Quote(job, f).PriceChfPerBilledUnit, 12);
    }

    [Fact]
    public void RemainingUnitsOfTheJobAreAddedToCommitted()
    {
        var f = Fleet();
        var job = Job2(f, 20, 0, 10 * FleetTime.MsPerHour);   // 20 units x 1 kWh = 20 kWh
        f.Committed = _ => 20;
        var p = new MockLoadPricing(Cfg(), 1);
        p.Quote(job, f);
        Assert.Equal(40, p.LastQuote!.CommittedKwh, 9);
        Assert.Equal(0.5, p.LastQuote.U, 9);
    }

    [Fact]
    public void ZeroCapacityMeansFullUtilisation()
    {
        var f = Fleet((_, _, _) => 0);
        var job = Job2(f, 1, 0, 5 * FleetTime.MsPerHour);
        var p = new MockLoadPricing(Cfg(), 1);
        Assert.Equal(1.2, p.Quote(job, f).PriceChfPerBilledUnit, 12);
        Assert.Equal(1, p.LastQuote!.U);
    }

    [Fact]
    public void AcceptanceProbabilityMatchesElasticity()
    {
        var f = Fleet();
        var job = Job2(f, 1000, 0, 10 * FleetTime.MsPerHour);   // u = 1, price 1.2
        var p = new MockLoadPricing(Cfg(pRef: 0.5), 7);
        int accepted = 0, n = 4000;
        for (int i = 0; i < n; i++) if (p.Quote(job, f).Accepted) accepted++;
        double expected = Math.Pow(1.2 / 0.5, -0.8);
        Assert.Equal(expected, p.LastQuote!.AcceptProbability, 12);
        Assert.InRange(accepted / (double)n, expected - 0.04, expected + 0.04);
    }

    [Fact]
    public void CheapPriceIsAlwaysAccepted()
    {
        var f = Fleet();
        var job = Job2(f, 0, 0, 10 * FleetTime.MsPerHour);       // u = 0, price 0.226 < p_ref
        var p = new MockLoadPricing(Cfg(), 3);
        for (int i = 0; i < 200; i++) Assert.True(p.Quote(job, f).Accepted);
        Assert.Equal(1, p.LastQuote!.AcceptProbability);
    }

    [Fact]
    public void SameSeedSameQuotesDifferentSeedDiffers()
    {
        List<(double, bool)> Run(long seed)
        {
            var f = Fleet();
            f.Committed = _ => 40;
            var p = new MockLoadPricing(Cfg(sd: 0.079, pRef: 0.5), seed);
            var res = new List<(double, bool)>();
            for (int i = 0; i < 300; i++)
            {
                var job = Job2(f, 1, i * 3_600_000L * 3, i * 3_600_000L * 3 + 10 * FleetTime.MsPerHour);
                f.NowMs = job.Request.ArrivalMs;
                var q = p.Quote(job, f);
                res.Add((q.PriceChfPerBilledUnit, q.Accepted));
            }
            return res;
        }
        Assert.Equal(Run(5), Run(5));
        Assert.NotEqual(Run(5), Run(6));
    }

    [Fact]
    public void PRefOfWeekDoesNotDependOnQueryOrder()
    {
        double PRef(long[] hours, long probe)
        {
            var f = Fleet();
            var p = new MockLoadPricing(Cfg(sd: 0.079), 11);
            double result = 0;
            foreach (long h in hours)
            {
                var job = Job2(f, 1, h * FleetTime.MsPerHour, (h + 5) * FleetTime.MsPerHour);
                f.NowMs = job.Request.ArrivalMs;
                p.Quote(job, f);
                if (h == probe) result = p.LastQuote!.PRefWeekChf;
            }
            return result;
        }
        Assert.Equal(PRef([10, 200, 400], 400), PRef([400, 200, 10], 400));
        Assert.Equal(PRef([10, 200, 400], 200), PRef([200, 10, 400], 200));
        Assert.NotEqual(PRef([10, 200, 400], 10), PRef([10, 200, 400], 400));
    }

    [Fact]
    public void WindowCapacityEqualsBruteForce()
    {
        // Forecast depends on lead (as the archived forecast does) and on the house; servers bind in some hours.
        double Fc(int house, int issued, int target)
        {
            int lead = Math.Clamp((int)Math.Ceiling((target - issued) / 24.0), 1, 3);
            return 3.0 + house + 2.5 * Math.Sin(target * 0.37 + house) + 0.4 * lead;
        }
        var f = Fleet(Fc);
        var cfg = Cfg();
        var p = new MockLoadPricing(cfg, 1);
        double maxKw = f.ServerParams.MaxElectricKw, share = f.ServerParams.HeatRecoveryShare;

        double Brute(int issued, long a, long d)
        {
            double sum = 0;
            for (int t = (int)(a / FleetTime.MsPerHour); t * FleetTime.MsPerHour < d; t++)
            {
                double lo = Math.Max(a, t * FleetTime.MsPerHour), hi = Math.Min(d, (t + 1) * FleetTime.MsPerHour);
                double cap = 0;
                for (int h = 0; h < 2; h++) cap += Math.Min(4 * maxKw, Fc(h, issued, t) / share);
                sum += (hi - lo) / FleetTime.MsPerHour * cap;
            }
            return sum;
        }

        (long arrival, long dur)[] cases =
        [
            (0, 1_800_000),                                              // inside one hour
            (3_600_000 * 5 + 900_000, 3_600_000L * 3 + 1_200_000),       // fractional ends, short
            (3_600_000 * 40 + 1_000_000, 3_600_000L * 24 * 3 - 777_000), // 3 days, crosses the 72 h boundary
            (3_600_000 * 41 + 100, 3_600_000L * 24 * 6 + 12_345),        // 6 days, far part dominates
            (3_600_000 * 100, 3_600_000L * 2),                           // exact hour ends
        ];
        foreach (var (arrival, dur) in cases)
        {
            var job = Job2(f, 1, arrival, arrival + dur);
            f.NowMs = arrival;
            p.Quote(job, f);
            double expected = Brute(f.HourIndex, arrival, arrival + dur);
            Assert.Equal(expected, p.LastQuote!.CapacityKwh, 7);
        }
    }
}

// ------------------------------------------------------------------ scheduler

public class MockHeatLedSchedulerTests
{
    private static readonly SchedulerSection Cfg = new()
    {
        LowShare = 0.3, HighShare = 0.9, UrgentMarginS = 3600, ChunkTargetS = 3600,
    };
    private const long H = FleetTime.MsPerHour;

    // 0.1 kWh/unit: one server at load 1 does 10 units per hour, a chunk_target_s chunk is 10 units.
    private static JobType T2(bool checkpointable = true, double checkpointS = 0, double minLoad = 0, bool pause = true,
        int maxChunks = 8, double minChunk = 1, string placement = "anywhere")
        => FakeFleet.MakeType("t2", JobClass.Type2, 0.1, maxChunks, minChunk, pause, minLoad, checkpointable, checkpointS, placement);
    private static JobType T1(int maxChunks = 8) => FakeFleet.MakeType("t1", JobClass.Type1, 0.1, maxChunks);

    private static FakeFleet Make(out MockHeatLedScheduler s, params (int servers, double tank, double demand)[] houses)
    {
        var f = new FakeFleet();
        foreach (var (n, tank, demand) in houses) f.AddHouse(n, 35, "BS", tank, demand);
        s = new MockHeatLedScheduler(Cfg);
        f.Scheduler = s;
        s.Initialize(f, f);
        return f;
    }

    [Fact]
    public void InitializeRegistersThresholds()
    {
        var f = Make(out _, (2, 0, 0));
        Assert.Contains((0, 0.3 * 35), f.Thresholds);
        Assert.Contains((0, 0.9 * 35), f.Thresholds);
    }

    [Fact]
    public void ChargeModePutsAllServersAtFullLoad()
    {
        var f = Make(out _, (4, 0, 0));
        f.HourStart();
        f.Arrive(f.AddJob(T2(), 100, 0, 100 * H));
        Assert.Equal([1.0, 1, 1, 1], f.Loads(0));
        Assert.All(f.Chunks, c => Assert.Equal(10, c.WorkUnits, 9));   // chunk_target_s at load 1
    }

    [Fact]
    public void HoldModeHitsDemandOverRecoveryExactly()
    {
        var f = Make(out _, (8, 35, 3.4));   // tank full -> hold; target 3.4 / 0.8 = 4.25 kW
        f.HourStart();
        f.Arrive(f.AddJob(T2(), 1000, 0, 100 * H));
        var loads = f.Loads(0);
        Assert.Equal(4.25, loads.Sum(), 9);
        Assert.Equal(4, loads.Count(l => l == 1.0));
        Assert.Equal(1, loads.Count(l => Math.Abs(l - 0.25) < 1e-9));
        Assert.Equal(3, loads.Count(l => l == 0.0));
        Assert.Equal(5, f.Chunks.Count);
    }

    [Fact]
    public void HoldTargetIsCappedByServerCapacity()
    {
        var f = Make(out _, (2, 35, 50));
        f.HourStart();
        f.Arrive(f.AddJob(T2(), 1000, 0, 100 * H));
        Assert.Equal([1.0, 1.0], f.Loads(0));
    }

    [Fact]
    public void ModeSwitchesWithHysteresis()
    {
        var f = Make(out _, (4, 35, 0.8));   // full tank: hold, target 1 kW
        f.HourStart();
        f.Arrive(f.AddJob(T2(), 1000, 0, 100 * H));
        Assert.Equal(1.0, f.Loads(0).Sum(), 9);

        f.HouseList[0].TankKwh = 10;          // <= 0.3 x 35 = 10.5: charge
        f.Send(new FleetEvent(FleetEventKind.TankThreshold, 0, House: new HouseId(0), ThresholdKwh: 10.5));
        Assert.Equal(4.0, f.Loads(0).Sum(), 9);

        f.HouseList[0].TankKwh = 20;          // between the thresholds: stays charge
        f.Send(new FleetEvent(FleetEventKind.TankThreshold, 0, House: new HouseId(0), ThresholdKwh: 10.5));
        Assert.Equal(4.0, f.Loads(0).Sum(), 9);

        f.HouseList[0].TankKwh = 31.5;        // >= 0.9 x 35: hold
        f.Send(new FleetEvent(FleetEventKind.TankFull, 0, House: new HouseId(0)));
        Assert.Equal(1.0, f.Loads(0).Sum(), 9);
    }

    [Fact]
    public void QueueOrderIsType1ThenType2ByDeadline()
    {
        var f = Make(out _, (2, 0, 0));
        f.HourStart();
        var t2 = T2(maxChunks: 1);
        var t1 = T1(maxChunks: 1);
        var a = f.AddJob(t2, 10, 0, 1 * H);     // T2, earliest deadline of all
        var b = f.AddJob(t1, 10, 0, 50 * H);
        var c = f.AddJob(t1, 10, 0, 30 * H);
        f.Send(new FleetEvent(FleetEventKind.JobArrived, 0, Job: a), new FleetEvent(FleetEventKind.JobArrived, 0, Job: b),
               new FleetEvent(FleetEventKind.JobArrived, 0, Job: c));
        var running = f.Chunks.Select(ch => ch.Job.Value).OrderBy(x => x).ToArray();
        Assert.Equal([b.Value, c.Value], running);
        Assert.Equal(0, f.Jobs[a.Value].ActiveChunks.Count);
    }

    [Fact]
    public void QueueOrderWithinOneEventBatchAcrossJobs()
    {
        var f = Make(out _, (1, 0, 0));
        f.HourStart();
        var a = f.AddJob(T2(maxChunks: 1), 10, 0, 1 * H);
        var b = f.AddJob(T1(maxChunks: 1), 10, 0, 90 * H);
        f.Send(new FleetEvent(FleetEventKind.JobArrived, 0, Job: a), new FleetEvent(FleetEventKind.JobArrived, 0, Job: b));
        Assert.Single(f.Chunks);
        Assert.Equal(b, f.Chunks[0].Job);
    }

    [Fact]
    public void ChunkSizingFollowsTargetAndMinimum()
    {
        var f = Make(out _, (2, 0, 0));
        f.HourStart();
        // 22 units: 10, then 12 would leave 2 < min 5 after another 10 -> second chunk takes the whole 12.
        f.Arrive(f.AddJob(T2(minChunk: 5), 22, 0, 100 * H));
        Assert.Equal([10.0, 12.0], f.Chunks.Select(c => c.WorkUnits).ToArray());
        Assert.Equal(0, f.Jobs[0].UnassignedUnits, 9);

        // A job smaller than min_chunk is taken whole.
        var f2 = Make(out _, (2, 0, 0));
        f2.HourStart();
        f2.Arrive(f2.AddJob(T2(minChunk: 5), 3, 0, 100 * H));
        Assert.Equal([3.0], f2.Chunks.Select(c => c.WorkUnits).ToArray());
    }

    [Fact]
    public void RespectsMaxChunks()
    {
        var f = Make(out _, (4, 0, 0));
        f.HourStart();
        f.Arrive(f.AddJob(T2(maxChunks: 2), 100, 0, 100 * H));
        Assert.Equal(2, f.Chunks.Count);
        Assert.Equal(2, f.Loads(0).Count(l => l == 1.0));
    }

    [Fact]
    public void PausesInReverseQueueOrderWhenPowerIsTooHigh()
    {
        var f = Make(out _, (4, 0, 0));      // charge: all four at load 1
        f.HourStart();
        var t = T2(maxChunks: 1);
        var jobs = new[] { 40L, 10L, 30L, 20L }.Select(dl => f.AddJob(t, 10, 0, dl * H)).ToArray();
        foreach (var j in jobs) f.Arrive(j);
        Assert.Equal([1.0, 1, 1, 1], f.Loads(0));

        f.HouseList[0].TankKwh = 35;         // hold with demand 2 kW -> target 2.5 kW
        f.HouseList[0].DemandKwNow = 2;
        f.HourStart();
        double LoadOf(JobId j) => f.Chunks.Single(c => c.Job == j).Load;
        Assert.Equal(1.0, LoadOf(jobs[1]));  // deadline 10
        Assert.Equal(1.0, LoadOf(jobs[3]));  // deadline 20
        Assert.Equal(0.5, LoadOf(jobs[2]), 9);   // deadline 30
        Assert.Equal(0.0, LoadOf(jobs[0]));  // deadline 40: paused
    }

    [Fact]
    public void NonPausableJobsOvershootToMinLoad()
    {
        var f = Make(out _, (2, 35, 0.4));   // hold: target 0.5 kW
        f.HourStart();
        f.Arrive(f.AddJob(T2(pause: false, minLoad: 0.3), 100, 0, 100 * H));
        // First chunk 0.5, second cannot start below min_load 0.3 of 0 remaining.
        Assert.Equal([0.5, 0.0], f.Loads(0));
        f.HouseList[0].DemandKwNow = 0.0;    // target 0: the running chunk goes to min_load, not 0
        f.HourStart();
        Assert.Equal(0.3, f.Loads(0)[0], 9);
    }

    [Fact]
    public void SkipsStartBelowMinLoadAndRespectsPlacement()
    {
        var f = Make(out _, (2, 35, 0.16));  // hold: target 0.2 kW
        f.HourStart();
        var strict = T2(minLoad: 0.5);
        f.Arrive(f.AddJob(strict, 100, 0, 100 * H));
        Assert.Empty(f.Chunks);              // 0.2 < min_load 0.5

        var f2 = Make(out _, (1, 0, 0), (1, 0, 0));
        f2.HouseList[1].Spec.GetType();      // houses are BS
        var osType = T2(placement: "origin_site");
        f2.HourStart();
        f2.Arrive(f2.AddJob(osType, 10, 0, 100 * H, origin: "client_site", canton: "ZH"));
        Assert.Empty(f2.Chunks);             // no house in ZH
        f2.Arrive(f2.AddJob(osType, 10, 0, 100 * H, origin: "client_site", canton: "BS"));
        Assert.Single(f2.Chunks);
    }

    [Fact]
    public void FinishedChunkFreesServerForQueuedWork()
    {
        var f = Make(out _, (1, 0, 0));
        f.HourStart();
        var a = f.AddJob(T2(maxChunks: 1), 10, 0, 10 * H);
        var b = f.AddJob(T2(maxChunks: 1), 10, 0, 20 * H);
        f.Arrive(a); f.Arrive(b);
        Assert.Equal(a, f.Chunks.Single().Job);
        // engine: chunk 0 finishes
        var ch = f.Chunks[0];
        ch.Phase = ChunkPhase.Finished;
        f.ServerList[0].Chunk = null; f.ServerList[0].Load = 0;
        f.Jobs[0].Chunks.Clear(); f.Jobs[0].Status = JobStatus.Done; f.Jobs[0].RemainingUnits = 0;
        f.Send(new FleetEvent(FleetEventKind.ChunkFinished, H, House: new HouseId(0), Job: a, Chunk: ch.Id, Server: ch.Server),
               new FleetEvent(FleetEventKind.JobCompleted, H, House: new HouseId(0), Job: a));
        Assert.Equal(2, f.Chunks.Count);
        Assert.Equal(b, f.Chunks[1].Job);
        Assert.Equal(1.0, f.Chunks[1].Load);
    }

    [Fact]
    public void UrgentType1PreemptsCheckpointableType2()
    {
        var f = Make(out _, (2, 0, 0));      // charge: both servers busy with Type 2
        f.HourStart();
        f.Arrive(f.AddJob(T2(maxChunks: 1), 10, 0, 100 * H));
        f.Arrive(f.AddJob(T2(maxChunks: 1), 10, 0, 90 * H));
        Assert.Equal(2, f.Chunks.Count);

        f.NowMs = 1000;
        var urgent = f.AddJob(T1(), 10, 1000, 1000 + 1800 * 1000L);   // 1 h of work, 30 min left
        f.Arrive(urgent);
        Assert.Contains(f.Chunks, c => c.Job == urgent && c.Phase == ChunkPhase.Working && c.Load == 1.0);
        // The stopped Type 2 job is back in the queue with all its units.
        var stopped = f.Chunks.Single(c => c.Phase == ChunkPhase.Stopped);
        Assert.Equal(10, f.Jobs[stopped.Job.Value].UnassignedUnits, 9);
        Assert.Equal([1.0, 1.0], f.Loads(0));
    }

    [Fact]
    public void UrgentWaitsForCheckpointThenStarts()
    {
        var f = Make(out _, (1, 0, 0));
        f.HourStart();
        f.Arrive(f.AddJob(T2(maxChunks: 1, checkpointS: 30), 10, 0, 100 * H));
        f.NowMs = 1000;
        var urgent = f.AddJob(T1(), 10, 1000, 1000 + 1800 * 1000L);
        f.Arrive(urgent);
        Assert.Single(f.Chunks);                              // server reserved, checkpoint running
        Assert.Equal(ChunkPhase.Checkpointing, f.Chunks[0].Phase);
        f.NowMs = 31_000;
        f.CompleteCheckpoint(f.Chunks[0].Id);
        Assert.Equal(2, f.Chunks.Count);
        Assert.Equal(urgent, f.Chunks[1].Job);
        Assert.Equal(1.0, f.Chunks[1].Load);
    }

    [Fact]
    public void UrgentStartsAtFullLoadEvenInHoldMode()
    {
        var f = Make(out _, (2, 35, 0.0));    // hold, demand 0: target 0
        f.HourStart();
        f.NowMs = 1000;
        var urgent = f.AddJob(T1(maxChunks: 1), 10, 1000, 1000 + 1800 * 1000L);
        f.Arrive(urgent);
        Assert.Equal([1.0, 0.0], f.Loads(0));
        f.HourStart();                         // stays at full load though the target is 0
        Assert.Equal(1.0, f.Loads(0)[0]);
    }

    [Fact]
    public void NonUrgentType1WaitsForWakeup()
    {
        var f = Make(out _, (2, 35, 0.0));    // hold, no power wanted
        f.HourStart();
        var j = f.AddJob(T1(maxChunks: 1), 10, 0, 10 * H);   // 1 h of work, 10 h left
        f.Arrive(j);
        Assert.Empty(f.Chunks);
        // due = deadline - margin(1 h) - 1 h of work = 8 h; checks are clamped to hourly boundaries.
        f.NowMs = 8 * H;
        f.HourStart();
        Assert.Single(f.Chunks);
        Assert.Equal(1.0, f.Chunks[0].Load);
    }

    [Fact]
    public void WakeupRequestedWithinTheHour()
    {
        var f = Make(out _, (1, 35, 0.0));
        f.HourStart();
        f.NowMs = 0;
        // due at 0.5 h (deadline 2.5 h, 1 h margin, 1 h of work).
        f.Arrive(f.AddJob(T1(maxChunks: 1), 10, 0, (long)(2.5 * H)));
        Assert.Contains((long)(0.5 * H), f.Wakeups);
        f.NowMs = (long)(0.5 * H);
        f.Send(new FleetEvent(FleetEventKind.Wakeup, f.NowMs));
        Assert.Single(f.Chunks);
    }

    [Fact]
    public void DeficitHousesPickUpLateArrivals()
    {
        var f = Make(out _, (1, 0, 0), (1, 0, 0), (1, 0, 0));
        f.HourStart();                         // empty queue: all houses in deficit
        f.Arrive(f.AddJob(T2(maxChunks: 8), 20, 0, 100 * H));
        Assert.Equal(2, f.Chunks.Count);       // two chunks of 10 on houses 0 and 1
        Assert.Equal([0, 1], f.Chunks.Select(c => c.House.Value).ToArray());
        f.Arrive(f.AddJob(T2(maxChunks: 8), 10, 0, 100 * H));
        Assert.Equal(3, f.Chunks.Count);
        Assert.Equal(2, f.Chunks[2].House.Value);
    }
}
