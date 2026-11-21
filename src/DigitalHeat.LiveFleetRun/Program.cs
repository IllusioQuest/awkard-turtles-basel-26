using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DigitalHeat.Fleet;
using DigitalHeat.JobRuntime;

// A separate local process, not a simulation executor: the Fleet scheduler's actions are forwarded
// to one container created by this run. The displayed house quantities remain modelled values.
try
{
    if (args.Length is < 1 or > 2 || args[0] is not ("winter" or "summer") || (args.Length == 2 && args[1] != "--busy"))
        throw new ArgumentException("Usage: dotnet run --project src/DigitalHeat.LiveFleetRun -- winter|summer [--busy]");
    string root = Scenario.FindRepoRoot(Environment.CurrentDirectory);
    string season = args[0];
    bool busy = args.Length == 2;
    string liveDir = Path.Combine(root, "data", "external", "live-household");
    Directory.CreateDirectory(liveDir);
    using var owner = new FileStream(Path.Combine(liveDir, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "config", "live-household-demo.json")));
    var c = config.RootElement;
    double quantity(string key) => c.GetProperty(key).GetProperty("value").GetDouble();
    double cpu = quantity("cpu_cores"), secondsPerHour = quantity("seconds_per_simulated_hour");
    if (!double.IsFinite(cpu) || cpu < 0.01 || !double.IsFinite(secondsPerHour) || secondsPerHour <= 0)
        throw new InvalidDataException("Invalid demo CPU or replay speed.");
    string image = c.GetProperty("image").GetString() ?? throw new InvalidDataException("Missing workload image.");
    string runId = Guid.NewGuid().ToString("N");
    string workDir = Path.Combine(liveDir, runId);
    Directory.CreateDirectory(workDir);
    string script = Path.Combine(root, "jobs", "research-sim", "live_request.py");
    string scenarioPath = Path.Combine(root, "config", "fleet", "scenarios", $"reference-{season}-jobs.json");
    var scenario = Scenario.Load(scenarioPath);
    // The live request is the first of the day's jobs. Other winter jobs remain in the offline run.
    var selected = JobsInputCsv.Read(scenario.Resolve(scenario.Demand.FromFile!)).Take(1).ToList();
    if (selected.Count != 1) throw new InvalidDataException("The scenario needs a live request.");
    int extraJobs = checked((int)quantity("simulated_queue_jobs"));
    if (extraJobs is < 0 or > 1000) throw new InvalidDataException("Invalid simulated queue size.");
    if (busy)
        for (int i = 0; i < extraJobs; i++)
            selected.Add(selected[0] with { JobKey = $"{season}-simulated-{i + 1:D4}" });
    string jobsFile = Path.Combine(workDir, "jobs-input.csv");
    JobsInputCsv.Write(jobsFile, selected);
    scenario.Demand.FromFile = jobsFile;
    scenario.Logging.JobEvents = false;
    // Fail immediately if the configured offline image is absent; do not implicitly pull one.
    Docker.Run("image", "inspect", image);
    string created = Docker.Run("create", "--name", "dh-household-" + runId, "--label", "digitalheat.live=" + runId,
        "--cpus", cpu.ToString("R", CultureInfo.InvariantCulture), "--entrypoint", "python",
        "-e", "TEMPERATURE=" + quantity("temperature").ToString("R", CultureInfo.InvariantCulture),
        "-e", "BASE_SEED=" + quantity("seed").ToString("0", CultureInfo.InvariantCulture),
        "-e", "LATTICE_SIZE=" + quantity("lattice_size").ToString("0", CultureInfo.InvariantCulture),
        "-e", "SWEEPS=" + quantity("sweeps").ToString("0", CultureInfo.InvariantCulture),
        "-e", "THERMALISATION_SWEEPS=" + quantity("thermalisation_sweeps").ToString("0", CultureInfo.InvariantCulture),
        "-v", workDir + ":/work", "-v", script + ":/live_request.py:ro", image, "-u", "/live_request.py").Trim();
    if (!Regex.IsMatch(created, "^[a-f0-9]{64}$")) throw new InvalidDataException("Docker returned an invalid container ID.");
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var session = new LiveSession(season, runId, scenario, workDir, Path.Combine(liveDir, "current.json"), created, cpu, secondsPerHour, busy, cancellation.Token);
    try
    {
        Console.WriteLine($"Live {season} request; dashboard: http://127.0.0.1:5173/live . Press Ctrl+C to stop.");
        session.Publish();
        var run = FleetSimulation.Run(scenario, scheduler: session);
        session.Finish(run);
    }
    catch (OperationCanceledException) { session.Fail("cancelled", "Stopped by operator."); }
    catch (Exception e) { session.Fail("failed", e.Message); Environment.ExitCode = 1; }
    finally
    {
        // Only the ID returned by this run's docker create is ever removed.
        try { Docker.Run("rm", "-f", created); }
        catch (Exception e) { session.Fail("failed", "Owned-container cleanup failed: " + e.Message); Environment.ExitCode = 1; }
    }
}
catch (Exception e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }

internal sealed class LiveSession : IFleetScheduler
{
    private const int MaxHistoryPoints = 512;
    private const int MaxEvents = 100;
    private readonly HeatFirstFleetScheduler inner;
    private readonly string workDir, snapshotPath, containerId;
    private readonly double secondsPerHour;
    private readonly CancellationToken cancellation;
    private readonly Stopwatch clock = new();
    private readonly DockerJobExecutor executor;
    private readonly LiveState state;
    private IFleetView? view;
    private bool started, finished;
    private ServerId? realServer;
    private long lastSampleTicks;

    public LiveSession(string season, string runId, Scenario scenario, string workDir, string snapshotPath,
        string containerId, double cpu, double secondsPerHour, bool busy, CancellationToken cancellation)
    {
        inner = new HeatFirstFleetScheduler(scenario.Scheduler);
        this.workDir = workDir; this.snapshotPath = snapshotPath; this.containerId = containerId;
        this.secondsPerHour = secondsPerHour; this.cancellation = cancellation;
        executor = new DockerJobExecutor(new DockerCliContainerControl(), containerId, cpu, supportsThrottle: false);
        state = new LiveState { RunId = runId, Season = season, StartLocal = scenario.Run.StartLocal,
            SecondsPerSimulatedHour = secondsPerHour, CpuCores = cpu, Busy = busy,
            JobId = JobsInputCsv.Read(scenario.Resolve(scenario.Demand.FromFile!))[0].JobKey };
    }

    public string Id => inner.Id;
    public void Initialize(IFleetView view, IFleetActions actions)
    {
        this.view = view;
        clock.Start();
        inner.Initialize(view, new Actions(this, actions));
    }

    public void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions actions)
    {
        this.view = view;
        WaitUntil(view.NowMs);
        state.SimulatedMs = view.NowMs;
        state.LagSeconds = Math.Max(0, clock.Elapsed.TotalSeconds - view.NowMs / 3_600_000.0 * secondsPerHour);
        var house = view.Houses.Single();
        state.DemandKw = house.DemandKwNow; state.TankKwh = house.TankKwh; state.TankCapacityKwh = house.TankCapacityKwh;
        foreach (var completed in events.Where(e => e.Kind == FleetEventKind.JobCompleted && e.Job is not null))
        {
            string jobId = view.Job(completed.Job!.Value).Request.JobKey;
            if (jobId == state.JobId) CompleteRealRequest();
            else Event("completed", jobId, false);
        }
        inner.OnEvents(view, events, new Actions(this, actions));
        state.WaitingJobs = 0; state.RunningJobs = 0; state.PausedJobs = 0;
        foreach (var id in view.OpenJobs)
        {
            var job = view.Job(id);
            if (job.Status == JobStatus.Waiting) state.WaitingJobs++;
            else if (job.ActiveChunks.Any(chunk => view.Chunk(chunk).Load > 0)) state.RunningJobs++;
            else state.PausedJobs++;
        }
        state.HeatProducedKw = house.ElectricKwNow * view.ServerParams.HeatRecoveryShare;
        AddHistory();
        Publish();
    }

    private void WaitUntil(long simMs)
    {
        double target = simMs / 3_600_000.0 * secondsPerHour;
        while (clock.Elapsed.TotalSeconds < target)
        {
            cancellation.ThrowIfCancellationRequested();
            if (clock.ElapsedMilliseconds - lastSampleTicks >= 1000)
            {
                lastSampleTicks = clock.ElapsedMilliseconds;
                if (started && !finished && Docker.Run("inspect", "--format", "{{.State.Status}}", containerId).Trim() == "exited")
                    throw new InvalidOperationException("Real request exited before the simulated scheduler finished.");
                Publish();
            }
            cancellation.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Min(200, Math.Max(1, (target - clock.Elapsed.TotalSeconds) * 1000))));
        }
    }

    private void StartReal(ServerId server, double load)
    {
        if (started) throw new InvalidOperationException("This live demo supports one unsplit job.");
        if (load != 1) throw new InvalidOperationException("The live workload requires full load.");
        Docker.Run("start", containerId);
        started = true;
        realServer = server;
        executor.ApplyTargetLoad(load);
        state.TargetLoad = load; state.ProcessStatus = "running";
        Event("started", state.JobId, true);
    }

    private void SetRealLoad(double load)
    {
        if (!started || finished || load is not (0 or 1)) throw new InvalidOperationException("Unexpected live job load.");
        if (state.TargetLoad == load) return;
        if (load == 0) ReadProgress();
        executor.ApplyTargetLoad(load);
        if (load == 0) ReadProgress(); // Flush any task completed while Docker was acknowledging the pause.
        state.TargetLoad = load; state.ProcessStatus = load == 0 ? "paused" : "running";
        Event(load == 0 ? "paused" : "resumed", state.JobId, true);
    }

    private void CompleteRealRequest()
    {
        if (!started || finished) throw new InvalidOperationException("Unexpected simulated completion.");
        if (state.TargetLoad == 0) throw new InvalidOperationException("Cannot complete a paused real request.");
        // The real request is explicitly time-bounded by the replay. SIGTERM asks it to finish its
        // current task and exit; actual output and exit status are checked independently.
        Docker.Run("stop", "--time", "10", containerId);
        string actual = Docker.Run("inspect", "--format", "{{.State.ExitCode}}", containerId).Trim();
        ReadProgress();
        if (actual != "0" || state.TasksDone < 1) throw new InvalidOperationException("Real request did not finish cleanly with output.");
        finished = true; realServer = null; state.TargetLoad = 0; state.ProcessStatus = "completed";
        Event("completed", state.JobId, true);
    }

    public void Finish(FleetRun run)
    {
        if (!finished) throw new InvalidOperationException("The simulated day ended before the real request completed.");
        WaitUntil(run.Calendar.EndMs);
        state.SimulatedMs = run.Calendar.EndMs;
        state.TankKwh = run.Result.Houses.Single().TankFinalKwh;
        AddHistory();
        state.Status = "completed"; Publish();
        Console.WriteLine("Live request complete; the saved output is in " + workDir);
    }

    public void Fail(string status, string message)
    {
        state.Status = status; state.Error = message; state.ProcessStatus = status;
        Publish(); Console.Error.WriteLine(message);
    }

    private void Event(string kind, string jobId, bool observed)
    {
        if (state.Events.Count == MaxEvents) { state.Events.RemoveAt(0); state.EventsTruncated = true; }
        state.Events.Add(new LiveEvent(state.SimulatedMs, kind, jobId, observed));
        Console.WriteLine($"{state.SimulatedMs / 3_600_000.0:F2} simulated h: {jobId} {kind} ({(observed ? "real" : "simulated")})");
    }

    private void ReadProgress()
    {
        string path = Path.Combine(workDir, "progress.json");
        try
        {
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            int done = doc.RootElement.GetProperty("tasks_done").GetInt32();
            double energy = doc.RootElement.GetProperty("latest_result").GetProperty("mean_energy_per_spin").GetDouble();
            if (done > state.TasksDone)
                Console.WriteLine($"Real result: {done} tasks, mean energy/spin {energy:F4}");
            state.TasksDone = done;
            state.LatestEnergyPerSpin = energy;
        }
        catch (IOException) { /* A concurrently replaced progress file can be read on the next sample. */ }
    }

    private void AddHistory()
    {
        if (state.History.Count == MaxHistoryPoints) state.History.RemoveAt(0);
        state.History.Add(new LivePoint(state.SimulatedMs, state.DemandKw, state.HeatProducedKw, state.TankKwh, state.TargetLoad));
    }

    public void Publish()
    {
        ReadProgress(); state.UpdatedUtc = DateTimeOffset.UtcNow;
        string tmp = snapshotPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        File.Move(tmp, snapshotPath, true);
    }

    private sealed class Actions(LiveSession session, IFleetActions inner) : IFleetActions
    {
        public ChunkId Start(JobId job, ServerId server, double workUnits, double load)
        {
            var result = inner.Start(job, server, workUnits, load);
            string id = session.view!.Job(job).Request.JobKey;
            if (id == session.state.JobId) session.StartReal(server, load);
            else session.Event("started", id, false);
            return result;
        }
        public void SetLoad(ServerId server, double load)
        {
            var v = session.view!;
            double before = v.Servers[server.Value].Load;
            var chunk = v.Servers[server.Value].Chunk ?? throw new InvalidOperationException("Missing active chunk.");
            string id = v.Job(v.Chunk(chunk).Job).Request.JobKey;
            inner.SetLoad(server, load);
            if (session.realServer == server) session.SetRealLoad(load);
            else if (before > 0 && load == 0) session.Event("paused", id, false);
            else if (before == 0 && load > 0) session.Event("resumed", id, false);
        }
        public void Stop(ChunkId chunk)
        {
            var v = session.view!;
            var c = v.Chunk(chunk);
            string id = v.Job(c.Job).Request.JobKey;
            if (session.realServer == c.Server) throw new InvalidOperationException("Unexpected scheduler stop for the real request.");
            inner.Stop(chunk);
            session.Event("stopped", id, false);
        }
        public void RequestWakeup(long timeMs) => inner.RequestWakeup(timeMs);
        public void RegisterTankThreshold(HouseId house, double tankKwh) => inner.RegisterTankThreshold(house, tankKwh);
    }
}

internal sealed class LiveState
{
    public string RunId { get; set; } = "";
    public string Season { get; set; } = "";
    public bool Busy { get; set; }
    public string StartLocal { get; set; } = "";
    public string Status { get; set; } = "running";
    public string ProcessStatus { get; set; } = "waiting";
    public string JobId { get; set; } = "";
    public string? Error { get; set; }
    public double SecondsPerSimulatedHour { get; set; }
    public double CpuCores { get; set; }
    public long SimulatedMs { get; set; }
    public double DemandKw { get; set; }
    public double HeatProducedKw { get; set; }
    public double TankKwh { get; set; }
    public double TankCapacityKwh { get; set; }
    public double TargetLoad { get; set; }
    public double LagSeconds { get; set; }
    public int TasksDone { get; set; }
    public int WaitingJobs { get; set; }
    public int RunningJobs { get; set; }
    public int PausedJobs { get; set; }
    public bool EventsTruncated { get; set; }
    public double? LatestEnergyPerSpin { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public List<LivePoint> History { get; } = [];
    public List<LiveEvent> Events { get; } = [];
}
internal sealed record LivePoint(long SimulatedMs, double DemandKw, double HeatProducedKw, double TankKwh, double TargetLoad);
internal sealed record LiveEvent(long SimulatedMs, string Kind, string JobId, bool Observed);

internal static class Docker
{
    public static string Run(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15_000)) { process.Kill(true); throw new TimeoutException("Docker command timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException("Docker " + args[0] + " failed: " + error.GetAwaiter().GetResult().Trim());
        return output.GetAwaiter().GetResult();
    }
}
