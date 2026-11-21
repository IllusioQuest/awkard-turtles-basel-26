using Xunit;

namespace DigitalHeat.Fleet.Tests;

// ------------------------------------------------------------------ fakes

// FixedDist (test distribution) is shared with EngineTests.cs.

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
    public int JobLookups { get; private set; }
    public IJobView Job(JobId id) { JobLookups++; return Jobs[id.Value]; }
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
