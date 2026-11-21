using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.JobRuntime;
using DigitalHeat.Network;
using DigitalHeat.Network.Lp;

/// <summary>
/// Live demo coordinator (decision 0042). Each step: read the task files, expire late jobs, build the
/// NetworkState, let the unchanged network scheduler decide, move waiting files as its allocations say,
/// and apply each node's load through SchedulerController.Apply → DockerJobExecutor. Task files on disk
/// are the only record of where work is; this class keeps job bookkeeping and an event list for the view.
/// </summary>
sealed class LiveCoordinator
{
    private readonly object gate = new();
    private readonly DemoConfig cfg;
    private readonly LiveCalibration cal;
    private readonly LiveWorkStore files;
    private readonly IExecutor[] executors;
    private readonly string[] ids;
    private readonly double[] targetC;
    private readonly double[] targetLoad;
    private readonly string?[] errors;
    private readonly Dictionary<int, Job> jobs = [];
    private readonly HashSet<string> seenDone = [];
    private readonly List<LiveEvent> events = [];
    private readonly Random rng;
    private INetworkScheduler scheduler;
    private LiveTaskPlanner planner;
    private int step;
    private int nextJob = 1;
    private int nextFiller = 1;
    private int fillerDone;
    private long nextEvent = 1;
    private bool autoSpawn;
    private DateTime lastStepUtc = DateTime.UtcNow;
    private readonly object stepLock = new();
    private bool dirty;
    private (int Done, int Running, int Waiting) signature;

    /// <summary>True when the scheduler should decide again now: a request changed the input, a task finished
    /// or started, or work arrived. Cheap: three directory listings.</summary>
    public bool NeedsDecision()
    {
        lock (gate) return dirty || Signature() != signature;
    }

    private (int, int, int) Signature()
    {
        int running = 0, waiting = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            running += files.List("running", i).Length;
            waiting += files.List("queue", i).Length;
        }
        return (files.DoneFiles("*.json").Length, running, waiting);
    }

    public Func<string, double?>? ActualLoad { get; set; }

    public LiveCoordinator(DemoConfig cfg, LiveCalibration cal, LiveWorkStore files, IExecutor[] executors)
    {
        this.cfg = cfg; this.cal = cal; this.files = files; this.executors = executors;
        ids = cfg.Nodes.Select(n => n.Id).ToArray();
        targetC = ids.Select(_ => (cfg.TargetTemperatureMinC.Value + cfg.TargetTemperatureMaxC.Value) / 2).ToArray();
        targetLoad = new double[ids.Length];
        errors = new string?[ids.Length];
        rng = new Random((int)cfg.Seed.Value);
        scheduler = CreateScheduler(cfg.Scheduler);
        planner = new LiveTaskPlanner(ids.Length, cal.ServerHoursPerTask);
        ResetFiles();
    }

    public static INetworkScheduler CreateScheduler(string id) => id switch
    {
        "B2" => new NetworkGreedyJobsScheduler(),
        "B1" => new LocalGreedyJobsScheduler(),
        "LP" => new RollingHorizonWorkLpScheduler(),
        _ => throw new InvalidDataException($"Unknown live-demo scheduler '{id}' (B1, B2 or LP)."),
    };

    private double HeatShare(int i) => !files.IsOnline(i) ? 0 : Math.Clamp((targetC[i] - cfg.TargetTemperatureMinC.Value)
        / (cfg.TargetTemperatureMaxC.Value - cfg.TargetTemperatureMinC.Value), 0, 1);

    // ---- requests (no Docker call, return at once) -------------------------------------------------

    public bool SetTarget(string id, double temperatureC)
    {
        int i = Array.IndexOf(ids, id);
        if (i < 0) return false;
        lock (gate) { targetC[i] = temperatureC; dirty = true; }
        return true;
    }

    public object? Spawn(string id, int? tasks = null)
    {
        int i = Array.IndexOf(ids, id);
        if (i < 0) return null;
        lock (gate) { dirty = true; return SpawnLocked(i, tasks ?? (int)cfg.TasksPerJob.Value, "button"); }
    }

    public void SetAutoSpawn(bool on)
    {
        lock (gate) { autoSpawn = on; Add("info", null, null, null, on ? "Auto-spawn on: a new job every "
            + cfg.AutoSpawnEverySteps.Value + " steps" : "Auto-spawn off: new jobs only by button"); }
    }

    public void SetScheduler(string id)
    {
        var s = CreateScheduler(id);
        lock (gate)
        {
            scheduler = s;
            planner = new LiveTaskPlanner(ids.Length, cal.ServerHoursPerTask);
            Add("info", null, null, null, $"Scheduler switched to {s.Id}");
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            ResetFiles();
            jobs.Clear(); seenDone.Clear(); events.Clear(); fillerDone = 0;
            planner = new LiveTaskPlanner(ids.Length, cal.ServerHoursPerTask);
            Add("info", null, null, null, "Demo reset: all queues empty");
        }
    }

    private void ResetFiles()
    {
        files.Reset();
    }

    private object SpawnLocked(int node, int count, string how)
    {
        // The technical 32-task scan is 16 temperatures × 2 seeds (docs/jobs/research-sim.md).
        const int ScanTemperatures = 16;
        const double ScanMinTemperature = 1.5;
        const double ScanMaxTemperature = 3.5;
        int id = nextJob++;
        var job = new Job(id, node, step, step + (int)cfg.DeadlineSteps.Value, count);
        jobs[id] = job;
        for (int k = 0; k < count; k++)
        {
            var task = new Dictionary<string, object>
            {
                ["id"] = TaskName(id, k), ["job"] = id, ["origin"] = ids[node], ["spawn_step"] = step,
                ["due_step"] = job.DueStep, ["temperature"] = Math.Round(ScanMinTemperature
                    + (ScanMaxTemperature - ScanMinTemperature) * (k % ScanTemperatures) / (ScanTemperatures - 1), 6),
                ["seed"] = (int)cfg.Seed.Value + id * 100 + k, ["lattice_size"] = (int)cfg.TaskLatticeSize.Value,
                ["sweeps"] = (int)cfg.TaskSweeps.Value, ["thermalisation_sweeps"] = (int)cfg.TaskSweeps.Value / 10,
            };
            files.Put(node, TaskName(id, k) + ".json", JsonSerializer.Serialize(task));
        }
        Add("spawn", id, null, node, $"New job {id} at {Label(node)}: {count} tasks, due in "
            + $"{(int)cfg.DeadlineSteps.Value} steps ({how})");
        return new { job = id, node = ids[node], tasks = count, dueStep = job.DueStep };
    }

    private static string TaskName(int job, int k) => $"j{job:0000}-t{k:00}";

    // ---- the step ----------------------------------------------------------------------------------

    /// <summary>
    /// A scheduler decision. <paramref name="newHour"/> = true on the clock tick: the hour advances, deadlines
    /// are checked, auto-spawn may add a job. Between ticks the same decision is re-run whenever something
    /// changes (job arrives, a server becomes free, a slider moves), like the event-driven admission in
    /// DigitalHeat.Orchestration: the heat budget stays per hour, decisions are not tied to the hour.
    /// Docker calls happen outside the state lock so requests never wait for them.
    /// </summary>
    public void Step(bool newHour = true)
    {
        lock (stepLock)
        {
        NetworkState state;
        NetworkDecision decision;
        lock (gate)
        {
            dirty = false;
            files.RecoverPending();
            if (newHour)
            {
                step++;
                lastStepUtc = DateTime.UtcNow;
            }
            CollectResults();
            if (newHour)
            {
                Expire();
                if (autoSpawn && step % (int)cfg.AutoSpawnEverySteps.Value == 0)
                    SpawnLocked(rng.Next(ids.Length), (int)cfg.TasksPerJob.Value, "auto-spawn");
            }
            var tasks = Observe();
            signature = Signature();
            state = LiveNetwork.BuildState(step, ids, ids.Select((_, i) => HeatShare(i)).ToArray(), tasks, cal);
            decision = scheduler.Decide(state);
            foreach (var m in planner.Plan(tasks, decision.Allocations ?? []))
                Move(m, state);
            for (int i = 0; i < ids.Length; i++) targetLoad[i] = decision.PerSite[i].TargetLoadPerServer;
            TopUpFiller(decision.FillerServerHours);
        }
        // Docker CLI calls are slow on Docker Desktop; apply the nodes in parallel so a step stays short.
        Parallel.For(0, ids.Length, i =>
        {
            var signal = state.States[i].Signal;
            try
            {
                SchedulerController.Apply(signal, new CommittedDecisionScheduler(signal, decision.PerSite[i]), executors[i]);
                lock (gate) errors[i] = null;
            }
            catch (Exception e)
            {
                lock (gate)
                {
                    if (errors[i] != e.Message) Console.Error.WriteLine($"[{ids[i]}] Docker: {e.Message}");
                    errors[i] = e.Message;
                }
            }
        });
        if (newHour)
        {
            // One line per demo hour: planned load comes from the network decision; observed
            // CPU is the latest Docker measurement, not an estimate of delivered heat.
            lock (gate)
            {
                string nodes = string.Join("  ", ids.Select((id, i) =>
                {
                    double? observed = ActualLoad?.Invoke(cfg.Nodes[i].Container);
                    return $"{Label(i)} heat {HeatShare(i):P0} → target {targetLoad[i]:P0} / CPU {(observed is null ? "—" : observed.Value.ToString("P0"))}";
                }));
                Console.WriteLine($"[hour {step:000} · {scheduler.Id}] {nodes}");
            }
        }
        }
    }

    private void Move(LiveMove m, NetworkState state)
    {
        if (!files.MoveQueue(m.FromNode, m.ToNode, m.TaskId + ".json")) return;
        var job = jobs[m.Job];
        double heat = state.States[m.FromNode].Signal.DemandKwh
            / (cal.RecoverableKwhPerStep * cal.ServersPerNode);
        int left = job.DueStep - step;
        var cohort = state.Work!.Pending.First(c => c.Id == LiveNetwork.CohortId(m.Job, m.FromNode, ids.Length));
        string why = scheduler is RollingHorizonWorkLpScheduler
            ? "the LP plan places it there"
            : $"{Label(m.FromNode)} wants {heat:P0} heat, so it can do only {heat * cal.ServersPerNode * left:0.#} of {cohort.RemainingServerHours:0.#} server-hours in the {left} h left";
        Add("move", m.Job, m.FromNode, m.ToNode, $"Job {m.Job}: task {m.TaskId[^2..]} → {Label(m.ToNode)}, because {why}", m.TaskId);
    }

    /// <summary>
    /// Type 2 filler (research tasks, decision 0020): a node the scheduler gives filler server-hours keeps
    /// one waiting filler task per server, so every server it lets run has work; a node without filler
    /// in the plan has its waiting filler removed. Filler is local and never moves; Type 1 tasks go first
    /// (the worker claims by deadline, filler has none).
    /// </summary>
    private void TopUpFiller(IReadOnlyList<double>? fillerServerHours)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (!files.IsOnline(i)) continue;
            try
            {
            var waiting = files.List("queue", i, "f*.json");
            int want = (fillerServerHours?[i] ?? 0) > 1e-9 ? cal.ServersPerNode : 0;
            for (int k = want; k < waiting.Length; k++) files.Delete("queue", i, waiting[k]);
            for (int k = waiting.Length; k < want; k++)
            {
                string name = $"f{nextFiller++:000000}";
                var task = new Dictionary<string, object>
                {
                    ["id"] = name, ["job"] = 0, ["filler"] = true, ["due_step"] = int.MaxValue,
                    ["temperature"] = Math.Round(1.5 + 2.0 * rng.NextDouble(), 4), ["seed"] = (int)cfg.Seed.Value + 1_000_000 + nextFiller,
                    ["lattice_size"] = (int)cfg.TaskLatticeSize.Value, ["sweeps"] = (int)cfg.TaskSweeps.Value,
                    ["thermalisation_sweeps"] = (int)cfg.TaskSweeps.Value / 10,
                };
                files.Put(i, name + ".json", JsonSerializer.Serialize(task));
            }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                Console.Error.WriteLine($"[{ids[i]}] Filler deferred while peer is unavailable: {e.Message}");
            }
        }
    }

    private void CollectResults()
    {
        foreach (var (node, name) in files.DoneFiles("f*.json"))
            if (files.Delete("done", node, name)) fillerDone++;
        foreach (var (location, name) in files.DoneFiles("j*.json"))
        {
            string taskId = Path.GetFileNameWithoutExtension(name);
            if (!seenDone.Add(taskId)) continue;
            int jobId = int.Parse(taskId[1..5]);
            if (!jobs.TryGetValue(jobId, out var job)) continue;
            string? nodeId = null;
            try { nodeId = JsonDocument.Parse(files.Read("done", location, name)).RootElement.GetProperty("node_id").GetString(); }
            catch (Exception) { }
            int node = Array.IndexOf(ids, nodeId);
            job.Done++;
            Add("complete", jobId, node < 0 ? null : node, null,
                $"Job {jobId}: task {taskId[^2..]} done at {(node < 0 ? "?" : Label(node))}", taskId);
            if (job.Done == job.Tasks && !job.Missed)
                Add("finished", jobId, null, job.Origin, $"Job {jobId} finished on time ({job.DueStep - step} steps to spare)");
        }
    }

    private void Expire()
    {
        foreach (var job in jobs.Values.Where(j => !j.Missed && j.Done < j.Tasks && j.DueStep <= step))
        {
            job.Missed = true;
            int removed = 0;
            for (int i = 0; i < ids.Length; i++)
                foreach (var name in files.List("queue", i, $"j{job.Id:0000}-*.json"))
                    if (files.Delete("queue", i, name)) removed++;
            Add("missed", job.Id, null, job.Origin, $"Job {job.Id} missed its deadline: {job.Tasks - job.Done} of {job.Tasks} tasks "
                + $"not done ({removed} waiting removed). No house had enough free heat to take the rest in time.");
        }
    }

    /// <summary>Snapshot of the task files of open jobs (one directory listing per node and kind).</summary>
    private List<LiveTask> Observe()
    {
        var tasks = new List<LiveTask>();
        for (int i = 0; i < ids.Length; i++)
            foreach (var (kind, running) in new[] { ("queue", false), ("running", true) })
                foreach (var name in files.List(kind, i, "j*.json"))
                {
                    string taskId = Path.GetFileNameWithoutExtension(name);
                    if (!int.TryParse(taskId.AsSpan(1, 4), out int jobId) || !jobs.TryGetValue(jobId, out var job) || job.Missed)
                        continue;
                    tasks.Add(new LiveTask(taskId, jobId, i, running, job.SpawnStep, job.DueStep));
                }
        return tasks;
    }

    /// <summary>Share of a running task done, as the worker reports it (progress/&lt;node&gt;/&lt;task&gt;.json); null if unknown.</summary>
    private double? Progress(int node, string taskId)
    {
        try
        {
            var root = JsonDocument.Parse(files.Read("progress", node, taskId + ".json")).RootElement;
            return root.GetProperty("task").GetString() == taskId ? root.GetProperty("fraction").GetDouble() : null;
        }
        catch (Exception) { return null; }
    }

    private string Label(int node) => cfg.Nodes[node].Label;

    private void Add(string kind, int? job, int? from, int? to, string text, string? task = null)
    {
        events.Add(new LiveEvent(nextEvent++, step, kind, job, from is null ? null : ids[from.Value],
            to is null ? null : ids[to.Value], task, jobs.TryGetValue(job ?? -1, out var j) ? ids[j.Origin] : null, text));
        Console.WriteLine($"[hour {step:000} · {kind}] {text}");
        if (events.Count > 400) events.RemoveRange(0, events.Count - 400);
    }

    // ---- view --------------------------------------------------------------------------------------

    public object Read(long since)
    {
        lock (gate)
        {
            var tasks = Observe();
            double msToNext = Math.Max(0, cfg.StepSeconds.Value * 1000 - (DateTime.UtcNow - lastStepUtc).TotalMilliseconds);
            return new
            {
                step, stepSeconds = cfg.StepSeconds.Value, msToNextStep = Math.Round(msToNext),
                scheduler = new { id = scheduler.Id, label = scheduler.Label }, autoSpawn,
                tasksPerJob = (int)cfg.TasksPerJob.Value, deadlineSteps = (int)cfg.DeadlineSteps.Value,
                serverHoursPerTask = cal.ServerHoursPerTask, serversPerNode = cal.ServersPerNode,
                temperatureRange = new[] { cfg.TargetTemperatureMinC.Value, cfg.TargetTemperatureMaxC.Value },
                nodes = ids.Select((id, i) => new
                {
                    id, label = Label(i), position = cfg.Nodes[i].Position, targetTemperatureC = targetC[i],
                    heatShare = HeatShare(i), targetLoad = targetLoad[i], actualLoad = ActualLoad?.Invoke(cfg.Nodes[i].Container),
                    error = files.IsOnline(i) ? errors[i] : "Peer offline",
                    waiting = tasks.Where(t => t.Node == i && !t.Running).OrderBy(t => t.DueStep).ThenBy(t => t.Id)
                        .Select(t => new { id = t.Id, job = t.Job, origin = ids[jobs[t.Job].Origin], dueStep = t.DueStep }).ToArray(),
                    running = tasks.Where(t => t.Node == i && t.Running)
                        .Select(t => new { id = t.Id, job = t.Job, origin = ids[jobs[t.Job].Origin], dueStep = t.DueStep,
                            progress = Progress(i, t.Id) }).ToArray(),
                    fillerWaiting = files.List("queue", i, "f*.json").Length,
                    fillerRunning = files.List("running", i, "f*.json")
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .Select(id => new { id, progress = Progress(i, id) }).ToArray(),
                }).ToArray(),
                jobs = jobs.Values.OrderByDescending(j => j.Id).Take(12).Select(j => new
                {
                    id = j.Id, origin = ids[j.Origin], spawnStep = j.SpawnStep, dueStep = j.DueStep, tasks = j.Tasks,
                    done = j.Done, state = j.Missed ? "missed" : j.Done >= j.Tasks ? "finished" : "open",
                }).ToArray(),
                totals = new
                {
                    finished = jobs.Values.Count(j => !j.Missed && j.Done >= j.Tasks),
                    missed = jobs.Values.Count(j => j.Missed),
                    moves = events.Count(e => e.Kind == "move"),
                    fillerDone,
                },
                events = events.Where(e => e.Seq > since).ToArray(),
            };
        }
    }

    private sealed class Job(int id, int origin, int spawnStep, int dueStep, int tasks)
    {
        public int Id { get; } = id;
        public int Origin { get; } = origin;
        public int SpawnStep { get; } = spawnStep;
        public int DueStep { get; } = dueStep;
        public int Tasks { get; } = tasks;
        public int Done { get; set; }
        public bool Missed { get; set; }
    }
}

sealed record LiveEvent(long Seq, int Step, string Kind, int? Job, string? From, string? To, string? Task,
    string? Origin, string Text);

/// <summary>
/// Docker control for the live loop: CPU readings come from one batched <c>docker stats</c> loop instead of
/// one CLI call per request, and unchanged CPU limits are not re-sent. Pause/unpause pass through.
/// </summary>
sealed class CachedDockerControl(DockerCliContainerControl docker) : IContainerControl
{
    private readonly Dictionary<string, double> cpu = [];
    private readonly Dictionary<string, double> limit = [];

    // Idempotent: a restarted LiveGrid finds containers still paused (or running) from the last run.
    public void Pause(string id) => Tolerate(() => docker.Pause(id), "already paused", "is not running", "is restarting");
    public void Unpause(string id) => Tolerate(() => docker.Unpause(id), "not paused");

    private static void Tolerate(Action a, params string[] harmless)
    {
        try { a(); }
        catch (InvalidOperationException e) when (harmless.Any(h => e.Message.Contains(h, StringComparison.OrdinalIgnoreCase))) { }
    }

    public void SetCpuLimit(string id, double cores)
    {
        lock (limit) if (limit.TryGetValue(id, out double l) && l == cores) return;
        try { docker.SetCpuLimit(id, cores); }
        catch (InvalidOperationException e) when (e.Message.Contains("paused", StringComparison.OrdinalIgnoreCase))
        {
            // Some Docker builds refuse to update a paused container: unpause, then set (runs briefly at the old quota).
            Unpause(id);
            docker.SetCpuLimit(id, cores);
        }
        lock (limit) limit[id] = cores;
    }

    public double ReadCpuPercent(string id) { lock (cpu) return cpu.GetValueOrDefault(id); }

    public double? Load(string id, double cores) { lock (cpu) return cpu.TryGetValue(id, out double p) ? Math.Clamp(p / (100 * cores), 0, 1) : null; }

    public void Observe(IReadOnlyList<string> ids)
    {
        var read = docker.ReadCpuPercents(ids);
        lock (cpu) foreach (var (k, v) in read) cpu[k] = v;
    }
}
