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
    private readonly string work;
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
    private long nextEvent = 1;
    private bool autoSpawn;
    private DateTime lastStepUtc = DateTime.UtcNow;

    public Func<string, double?>? ActualLoad { get; set; }

    public LiveCoordinator(DemoConfig cfg, LiveCalibration cal, string work, IExecutor[] executors)
    {
        this.cfg = cfg; this.cal = cal; this.work = work; this.executors = executors;
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

    private double HeatShare(int i) => Math.Clamp((targetC[i] - cfg.TargetTemperatureMinC.Value)
        / (cfg.TargetTemperatureMaxC.Value - cfg.TargetTemperatureMinC.Value), 0, 1);

    private string Dir(string kind, int node) => Path.Combine(work, kind, ids[node]);

    // ---- requests (no Docker call, return at once) -------------------------------------------------

    public bool SetTarget(string id, double temperatureC)
    {
        int i = Array.IndexOf(ids, id);
        if (i < 0) return false;
        lock (gate) targetC[i] = temperatureC;
        return true;
    }

    public object? Spawn(string id, int? tasks = null)
    {
        int i = Array.IndexOf(ids, id);
        if (i < 0) return null;
        lock (gate) return SpawnLocked(i, tasks ?? (int)cfg.TasksPerJob.Value, "button");
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
            jobs.Clear(); seenDone.Clear(); events.Clear();
            planner = new LiveTaskPlanner(ids.Length, cal.ServerHoursPerTask);
            Add("info", null, null, null, "Demo reset: all queues empty");
        }
    }

    private void ResetFiles()
    {
        foreach (var kind in new[] { "queue", "running" })
            for (int i = 0; i < ids.Length; i++)
            {
                Directory.CreateDirectory(Dir(kind, i));
                foreach (var f in Directory.GetFiles(Dir(kind, i), "*.json")) TryDelete(f);
            }
        Directory.CreateDirectory(Path.Combine(work, "done"));
        foreach (var f in Directory.GetFiles(Path.Combine(work, "done"))) TryDelete(f);
    }

    private object SpawnLocked(int node, int count, string how)
    {
        int id = nextJob++;
        var job = new Job(id, node, step, step + (int)cfg.DeadlineSteps.Value, count);
        jobs[id] = job;
        for (int k = 0; k < count; k++)
        {
            var task = new Dictionary<string, object>
            {
                ["id"] = TaskName(id, k), ["job"] = id, ["origin"] = ids[node], ["spawn_step"] = step,
                ["due_step"] = job.DueStep, ["temperature"] = Math.Round(1.5 + 2.0 * rng.NextDouble(), 4),
                ["seed"] = (int)cfg.Seed.Value + id * 100 + k, ["lattice_size"] = (int)cfg.TaskLatticeSize.Value,
                ["sweeps"] = (int)cfg.TaskSweeps.Value, ["thermalisation_sweeps"] = (int)cfg.TaskSweeps.Value / 10,
            };
            string path = Path.Combine(Dir("queue", node), TaskName(id, k) + ".json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(task));
            File.Move(path + ".tmp", path);
        }
        Add("spawn", id, null, node, $"New job {id} at {Label(node)}: {count} tasks, due in "
            + $"{(int)cfg.DeadlineSteps.Value} steps ({how})");
        return new { job = id, node = ids[node], tasks = count, dueStep = job.DueStep };
    }

    private static string TaskName(int job, int k) => $"j{job:0000}-t{k:00}";

    // ---- the step ----------------------------------------------------------------------------------

    /// <summary>One scheduler hour. Docker calls happen outside the lock so requests never wait for them.</summary>
    public void Step()
    {
        NetworkState state;
        NetworkDecision decision;
        lock (gate)
        {
            step++;
            lastStepUtc = DateTime.UtcNow;
            CollectResults();
            Expire();
            if (autoSpawn && step % (int)cfg.AutoSpawnEverySteps.Value == 0)
                SpawnLocked(rng.Next(ids.Length), (int)cfg.TasksPerJob.Value, "auto-spawn");
            var tasks = Observe();
            state = LiveNetwork.BuildState(step, ids, ids.Select((_, i) => HeatShare(i)).ToArray(), tasks, cal);
            decision = scheduler.Decide(state);
            foreach (var m in planner.Plan(tasks, decision.Allocations ?? []))
                Move(m, state);
            for (int i = 0; i < ids.Length; i++) targetLoad[i] = decision.PerSite[i].TargetLoadPerServer;
        }
        for (int i = 0; i < ids.Length; i++)
        {
            var signal = state.States[i].Signal;
            try
            {
                SchedulerController.Apply(signal, new CommittedDecisionScheduler(signal, decision.PerSite[i]), executors[i]);
                lock (gate) errors[i] = null;
            }
            catch (Exception e) { lock (gate) errors[i] = e.Message; }
        }
    }

    private void Move(LiveMove m, NetworkState state)
    {
        string from = Path.Combine(Dir("queue", m.FromNode), m.TaskId + ".json");
        string to = Path.Combine(Dir("queue", m.ToNode), m.TaskId + ".json");
        try { File.Move(from, to); }
        catch (FileNotFoundException) { return; }      // the worker claimed it first: it stays there
        catch (DirectoryNotFoundException) { return; }
        var job = jobs[m.Job];
        double heat = state.States[m.FromNode].Signal.DemandKwh / cal.RecoverableKwhPerStep;
        int left = job.DueStep - step;
        var cohort = state.Work!.Pending.First(c => c.Id == LiveNetwork.CohortId(m.Job, m.FromNode, ids.Length));
        string why = scheduler is RollingHorizonWorkLpScheduler
            ? "the LP plan places it there"
            : $"{Label(m.FromNode)} wants {heat:P0} heat, so it can do only {heat * left:0.#} of {cohort.RemainingServerHours:0.#} server-hours in the {left} h left";
        Add("move", m.Job, m.FromNode, m.ToNode, $"Job {m.Job}: task {m.TaskId[^2..]} → {Label(m.ToNode)}, because {why}", m.TaskId);
    }

    private void CollectResults()
    {
        foreach (var f in Directory.GetFiles(Path.Combine(work, "done"), "j*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            if (!seenDone.Add(name)) continue;
            int jobId = int.Parse(name[1..5]);
            if (!jobs.TryGetValue(jobId, out var job)) continue;
            string? nodeId = null;
            try { nodeId = JsonDocument.Parse(File.ReadAllText(f)).RootElement.GetProperty("node_id").GetString(); }
            catch (Exception) { }
            int node = Array.IndexOf(ids, nodeId);
            job.Done++;
            Add("complete", jobId, node < 0 ? null : node, null,
                $"Job {jobId}: task {name[^2..]} done at {(node < 0 ? "?" : Label(node))}", name);
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
                foreach (var f in Directory.GetFiles(Dir("queue", i), $"j{job.Id:0000}-*.json"))
                    if (TryDelete(f)) removed++;
            Add("missed", job.Id, null, job.Origin, $"Job {job.Id} missed its deadline: {job.Tasks - job.Done} of {job.Tasks} tasks "
                + $"not done ({removed} waiting removed; a running task finishes but does not count)");
        }
    }

    /// <summary>Snapshot of the task files of open jobs (one directory listing per node and kind).</summary>
    private List<LiveTask> Observe()
    {
        var tasks = new List<LiveTask>();
        for (int i = 0; i < ids.Length; i++)
            foreach (var (kind, running) in new[] { ("queue", false), ("running", true) })
                foreach (var f in Directory.GetFiles(Dir(kind, i), "j*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (!int.TryParse(name.AsSpan(1, 4), out int jobId) || !jobs.TryGetValue(jobId, out var job) || job.Missed)
                        continue;
                    tasks.Add(new LiveTask(name, jobId, i, running, job.SpawnStep, job.DueStep));
                }
        return tasks;
    }

    private static bool TryDelete(string f)
    {
        try { File.Delete(f); return true; } catch (IOException) { return false; }
    }

    private string Label(int node) => cfg.Nodes[node].Label;

    private void Add(string kind, int? job, int? from, int? to, string text, string? task = null)
    {
        events.Add(new LiveEvent(nextEvent++, step, kind, job, from is null ? null : ids[from.Value],
            to is null ? null : ids[to.Value], task, jobs.TryGetValue(job ?? -1, out var j) ? ids[j.Origin] : null, text));
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
                serverHoursPerTask = cal.ServerHoursPerTask,
                temperatureRange = new[] { cfg.TargetTemperatureMinC.Value, cfg.TargetTemperatureMaxC.Value },
                nodes = ids.Select((id, i) => new
                {
                    id, label = Label(i), position = cfg.Nodes[i].Position, targetTemperatureC = targetC[i],
                    heatShare = HeatShare(i), targetLoad = targetLoad[i], actualLoad = ActualLoad?.Invoke(cfg.Nodes[i].Container),
                    error = errors[i],
                    waiting = tasks.Where(t => t.Node == i && !t.Running).OrderBy(t => t.DueStep).ThenBy(t => t.Id)
                        .Select(t => new { id = t.Id, job = t.Job, origin = ids[jobs[t.Job].Origin], dueStep = t.DueStep }),
                    running = tasks.Where(t => t.Node == i && t.Running)
                        .Select(t => new { id = t.Id, job = t.Job, origin = ids[jobs[t.Job].Origin], dueStep = t.DueStep }),
                }),
                jobs = jobs.Values.OrderByDescending(j => j.Id).Take(12).Select(j => new
                {
                    id = j.Id, origin = ids[j.Origin], spawnStep = j.SpawnStep, dueStep = j.DueStep, tasks = j.Tasks,
                    done = j.Done, state = j.Missed ? "missed" : j.Done >= j.Tasks ? "finished" : "open",
                }),
                totals = new
                {
                    finished = jobs.Values.Count(j => !j.Missed && j.Done >= j.Tasks),
                    missed = jobs.Values.Count(j => j.Missed),
                    moves = events.Count(e => e.Kind == "move"),
                },
                events = events.Where(e => e.Seq > since),
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
    public void Pause(string id) => Tolerate(() => docker.Pause(id), "already paused");
    public void Unpause(string id) => Tolerate(() => docker.Unpause(id), "not paused");

    private static void Tolerate(Action a, string harmless)
    {
        try { a(); }
        catch (InvalidOperationException e) when (e.Message.Contains(harmless, StringComparison.OrdinalIgnoreCase)) { }
    }

    public void SetCpuLimit(string id, double cores)
    {
        lock (limit) if (limit.TryGetValue(id, out double l) && l == cores) return;
        docker.SetCpuLimit(id, cores);
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
