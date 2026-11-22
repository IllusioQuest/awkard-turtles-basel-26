using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;
using DigitalHeat.VoiceDemo;

// Live pitch demo (T-78): a spoken or typed request becomes one real container (Whisper + LLM) that the
// mesh heat-first scheduler (MeshPeer, the same code as the Linux peer mesh) admits, freezes and thaws
// from the modelled summer reference house. The house is simulated; the container and its energy are real.
try
{
    string root = Paths.FindRepoRoot();
    var demo = new DemoConfig(Path.Combine(root, "config", "voice-demo.json"));
    var p = ReferenceParameters.Load(Path.Combine(root, "config", "reference-house.json"));
    string voiceDir = Path.Combine(root, "data", "external", "voice-demo");
    string liveDir = Path.Combine(root, "data", "external", "live-household");
    Directory.CreateDirectory(liveDir);
    using var owner = new FileStream(Path.Combine(liveDir, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var app = new VoiceDemoApp(root, voiceDir, Path.Combine(liveDir, "current.json"), demo, p, cancellation.Token);
    app.Prepare();
    app.Run();
}
catch (OperationCanceledException) { Console.WriteLine("Stopped."); }
catch (Exception e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }

internal sealed class VoiceDemoApp(string root, string voiceDir, string snapshotPath, DemoConfig c, ReferenceParameters p, CancellationToken cancel)
{
    private const string SiteId = "reference-house";
    private const string StorageId = "voice-demo";
    private const int MaxHistoryPoints = 600, MaxEvents = 100;
    private readonly IDockerCommands docker = new DockerCommands();
    private readonly ConcurrentQueue<(string From, PeerMessage Message)> loopback = new();
    private string inbox => Path.Combine(voiceDir, "inbox");
    private string work => Path.Combine(voiceDir, "work");
    private IReadOnlyList<double> demandByHour = [];
    private MeshPeer? peer;
    private ContainerProfile? profile;
    private RuntimeNode[] nodes = [];
    private long revision, clockTicks = 100;
    private string sessionId = "";
    private int requestNumber;

    private double RecoverablePerServer => p.ServerElectricKw.Value * p.HeatRecoveryShare.Value * p.HoursPerStep.Value;
    private double ServerCapacityKwhPerHour => RecoverablePerServer * p.ServerCount.Value;

    public void Prepare()
    {
        sessionId = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        Log.Clock = () => c.ReplayStartLocalHour;
        demandByHour = BuildDemand();
        Directory.CreateDirectory(inbox); Directory.CreateDirectory(work);
        double preload = PrepareModels();
        docker.Run(["image", "inspect", c.Image]);
        string imageId = docker.Run(["image", "inspect", "--format", "{{.Id}}", c.Image]).Trim();
        EnsureVolume();
        RemoveStaleDemoContainers();
        // Linux reports CPU time per container (cgroup v2) and CPU energy (RAPL); elsewhere (macOS/Windows Docker
        // Desktop) the meter is off and the demo still runs. The container user is the host user on Linux.
        clockTicks = OperatingSystem.IsLinux() ? long.Parse(Shell("getconf", "CLK_TCK").Trim(), CultureInfo.InvariantCulture) : 100;
        string uid = OperatingSystem.IsWindows() ? "1000" : Shell("id", "-u").Trim();
        profile = new ContainerProfile("voice-assistant-" + imageId.Replace("sha256:", "", StringComparison.Ordinal)[..12], imageId, uid,
            [((int)c.RequestCpuCores).ToString(CultureInfo.InvariantCulture), ((int)c.MaxNewTokens).ToString(CultureInfo.InvariantCulture),
             c.Language, c.WhisperModel, c.LlmModel, c.SystemPrompt], null, [], [], []);
        nodes = [new RuntimeNode(SiteId + "-worker", "default", "none", [], [new VolumeBinding(StorageId, c.DockerVolume, "/data")])];
        string state = Path.Combine(voiceDir, "mesh-state", sessionId);
        Directory.CreateDirectory(state);
        peer = new MeshPeer("voice-demo-" + sessionId, SiteId, state, Inventory(NewHouse(out _).Signal()), nodes,
            new Dictionary<string, ContainerProfile> { [profile.Id] = profile }, [SiteId],
            new Loopback(loopback), new DockerWorkloadRuntime(docker), TimeProvider.System, TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(c.MinimumFreezeSeconds));
        Log.Line("INFO", "site", $"{SiteId} {c.DateLocal}  {p.ServerCount.Value:0}×{p.ServerElectricKw.Value:0} kW  tank {p.TankCapacityKwh.Value:0} kWh  hot water EU profile L");
        Log.Line("INFO", "models", $"whisper + llm in RAM ({preload:0.0} s)");
    }

    /// <summary>Space heat from the case formula (Core HeatDemand, Basel weather) plus the case's 12 kWh/day of
    /// hot water distributed over the day by EU 814/2013 load profile L (config/hot-water-profile-l.json).</summary>
    private double[] BuildDemand()
    {
        var weather = WeatherCsv.Read(Path.Combine(root, "data", "raw", "open-meteo", "basel-2025-hourly.csv"));
        var day = HeatDemand.Build(weather, p).Where(h => h.LocalTime.StartsWith(c.DateLocal, StringComparison.Ordinal)).ToArray();
        if (day.Length != (int)p.HoursPerDay.Value) throw new InvalidDataException($"Expected 24 demand hours for {c.DateLocal}.");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, c.HotWaterProfile)));
        var tappings = doc.RootElement.GetProperty("tappings").EnumerateArray()
            .Select(t => (int.Parse(t.GetProperty("time").GetString()![..2], CultureInfo.InvariantCulture), t.GetProperty("qtap_kwh").GetDouble())).ToArray();
        double qref = doc.RootElement.GetProperty("qref_kwh").GetDouble();
        if (Math.Abs(tappings.Sum(t => t.Item2) - qref) > 1e-6) throw new InvalidDataException("Tapping profile does not sum to its Qref.");
        return HouseReplay.Demand(tappings, qref, p.HotWaterKwhPerDay.Value, day.Select(h => h.SpaceHeatKwh).ToArray());
    }

    /// <summary>The house from midnight (tank full) to the replay start, with only the heat-first background
    /// servers running; returns the house at the start and its history for the chart.</summary>
    private HouseReplay NewHouse(out List<Snapshot.Point> history) => NewHouse(c.ReplayStartLocalHour, out history);

    private HouseReplay NewHouse(double startHour, out List<Snapshot.Point> history)
    {
        var house = new HouseReplay(new HouseReplaySettings(demandByHour, 0, p.TankCapacityKwh.Value, p.TankCapacityKwh.Value,
            p.ServerCount.Value, RecoverablePerServer));
        history = [];
        while (house.LocalHour < startHour - 1e-9)
        {
            house.Advance(Math.Min(0.25, startHour - house.LocalHour), 0);
            history.Add(Snapshot.PointOf(house, 0, ServerCapacityKwhPerHour));
        }
        return house;
    }

    public void Run()
    {
        NewHouse(out var prefill);
        var snapshot = Snapshot.Create(c, p, prefill);
        Write(snapshot);
        StartTypedRequests();
        while (true)
        {
            Log.Clock = () => c.ReplayStartLocalHour;
            Log.Line("INFO", "inbox", "waiting for a request (/speak or type here)");
            var request = WaitForRequest();
            try { snapshot = RunRequest(request); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A failed request is reported on /live and the console; the demo stays ready for the next one.
                Log.Line("ERROR", "inbox", "request failed: " + e.Message);
                snapshot.Status = "failed"; snapshot.Error = e.Message;
                Write(snapshot);
                if (Directory.Exists(request)) Directory.Delete(request, true);
            }
        }
    }

    /// <summary>Fallback when the phone fails: a line typed into this terminal becomes a text request in
    /// the same inbox, so it takes the same scheduler path as a spoken one (only Whisper is skipped).</summary>
    private void StartTypedRequests()
    {
        new Thread(() =>
        {
            while (!cancel.IsCancellationRequested)
            {
                string? line = Console.ReadLine();
                if (line is null) return;
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line.Length > 500) line = line[..500];
                string dir = Path.Combine(inbox, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-typed");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "text.txt"), line);
                File.WriteAllText(Path.Combine(dir, "ready"), "");
            }
        }) { IsBackground = true, Name = "typed-requests" }.Start();
    }

    private string WaitForRequest()
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            var ready = Directory.GetDirectories(inbox).Where(d => File.Exists(Path.Combine(d, "ready")))
                .Order(StringComparer.Ordinal).FirstOrDefault();
            if (ready is not null) return ready;
            Thread.Sleep(200);
        }
    }

    private Snapshot RunRequest(string inboxDir)
    {
        string jobId = $"voice-{++requestNumber:D3}";
        // One request at a time owns the work folder; the container reads it at /data/work.
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        Directory.Move(inboxDir, Path.Combine(work, "request"));
        File.Delete(Path.Combine(work, "request", "ready"));
        var input = new DirectoryInfo(Path.Combine(work, "request")).GetFiles().First();
        bool spoken = input.Name.StartsWith("audio.", StringComparison.Ordinal);

        double startHour = c.ReplayStartLocalHour; // same simulated moment for every request
        var house = NewHouse(startHour, out var prefill);
        var s = Snapshot.Create(c, p, prefill);
        s.Status = "running"; s.JobId = jobId; s.ProcessStatus = "submitted";
        Log.Clock = () => house.LocalHour;
        void Event(string what) { s.Events.Add(new(Snapshot.Ms(house.LocalHour), what, jobId, true)); if (s.Events.Count > MaxEvents) { s.Events.RemoveAt(0); s.EventsTruncated = true; } }
        string Free() => $"free {HeatCapacity.RemainingKwh(house.Signal()):0.000}";
        string need = $"need {c.RequestDeclaredHeatKwhPerHour:0.000} kWh";

        Log.Line("INFO", "inbox", $"{jobId} {(spoken ? "audio" : "text")} {input.Length / 1024.0:0.0} kB");
        Event("received");
        var now = DateTimeOffset.UtcNow;
        var request = new WorkloadRequest(jobId, "batch", SiteId, new("anywhere", c.Source, null),
            new(c.RequestCpuCores, (long)c.RequestRamMib, 1), c.Source, c.RequestDeclaredHeatKwhPerHour, c.Source,
            [], null, SiteId + "-worker", StorageId, [], new(true, true, false))
            { Deadline = new(now, now.AddSeconds(c.RequestDeadlineSeconds), c.Source, true) };
        string globalId = peer!.Submit(request, profile!.Id);

        var clock = Stopwatch.StartNew();
        double lastHours = 0, span = c.ReplayEndLocalHour - startHour, requestHeat = 0;
        string lastPhase = "submitted", lastStage = "", container = "";
        int streamed = 0;
        bool done = false, failed = false; double? doneAtHour = null;
        ContainerEnergyMeter? meter = null;
        bool meterUnavailable = false;
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            double hours = Math.Min(span, clock.Elapsed.TotalSeconds / c.SecondsPerSimulatedHour);
            house.Advance(hours - lastHours, requestHeat);
            lastHours = hours;
            try { peer.Update(Inventory(house.Signal())); peer.Tick(); Deliver(); }
            catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException or InvalidDataException)
            { Log.Line("WARN", "sched", "deferred: " + e.Message); }

            var attempt = peer.Attempts().Where(a => a.Attempt.Request.Id == globalId).LastOrDefault();
            string phase = attempt?.Phase ?? (peer.Jobs().Single(j => j.Request.Id == globalId).Status ?? "submitted");
            if (attempt is not null && container.Length == 0) container = DockerWorkloadRuntime.Name(attempt.Attempt)[..12];
            if (meter is null && !meterUnavailable && attempt is not null && phase == "running")
            {
                string id = docker.Run(["inspect", "--format", "{{.Id}}", DockerWorkloadRuntime.Name(attempt.Attempt)]).Trim();
                try { meter = new ContainerEnergyMeter(id, clockTicks); }
                catch (Exception e) when (e is FileNotFoundException or IOException or UnauthorizedAccessException)
                { meterUnavailable = true; Log.Line("WARN", "meter", "no cgroup v2 CPU counter for the container: CPU time and energy not measured"); }
            }
            if (meter is not null && !done && !failed) meter.Sample();
            // The request's heat into the house: 80 % of its measured electrical power (case heat recovery).
            requestHeat = phase == "running"
                ? (meter is { HasEnergy: true } ? meter.PowerWatts / 1000 * p.HeatRecoveryShare.Value : c.RequestDeclaredHeatKwhPerHour)
                : 0;

            if (phase != lastPhase)
            {
                switch (phase)
                {
                    case "running" when lastPhase is "paused" or "resuming":
                        Log.Line("INFO", "sched", $"thaw   {jobId}  {Free()} ≥ {need}"); Event("resumed"); break;
                    case "running":
                        Log.Line("INFO", "sched", $"admit  {jobId}  {Free()} ≥ {need}");
                        Log.Line("INFO", "docker", $"start  {container}  cpus={c.RequestCpuCores:0}"); Event("started"); break;
                    case "paused":
                        Log.Line("WARN", "sched", $"freeze {jobId}  {Free()} < {need}"); Event("paused"); break;
                    case "terminal" or "removing" or "released" when !done && !failed:
                        if (ReadText("stage") == "done")
                        {
                            done = true; doneAtHour = house.LocalHour; Event("completed");
                            string energy = meter is { HasEnergy: true } ? $"  {meter.EnergyKwh:0.000000} kWh" : "";
                            Log.Line("INFO", "docker", $"exit 0 {container}  cpu {meter?.CpuSeconds ?? 0:0} s{energy}");
                        }
                        else { failed = true; s.Status = "failed"; s.Error = "The container ended before the answer was written."; Log.Line("ERROR", "docker", $"exit {container}: no answer"); Event("failed"); }
                        break;
                }
                lastPhase = phase;
            }

            string stage = ReadText("stage");
            if (stage != lastStage && stage == "answering" && spoken) Log.Output(container, "» " + ReadText("transcript.txt"), newLine: true);
            lastStage = stage;
            if (stage is "answering" or "done")
            {
                // The real job output as it is written (like `docker logs -f`); it stops while the container is frozen.
                string answer = ReadRaw("answer.txt").Replace("[end of text]", "").TrimEnd();
                if (answer.Length > streamed) { Log.Output(container, answer[streamed..].ReplaceLineEndings(" ")); streamed = answer.Length; }
            }

            s.Update(house, phase, stage, ReadText("transcript.txt"), ReadText("answer.txt"), requestHeat, ServerCapacityKwhPerHour, meter,
                Math.Max(0, clock.Elapsed.TotalSeconds - lastHours * c.SecondsPerSimulatedHour));
            if (s.History.Count > MaxHistoryPoints) s.History.RemoveAt(prefill.Count);
            if (failed || done && house.LocalHour >= doneAtHour + 0.5 || lastHours >= span)
            {
                if (!done && !failed) { s.Status = "failed"; s.Error = "Replay ended before the request finished."; Log.Line("ERROR", "site", "replay ended before the job finished"); }
                else if (done) s.Status = "completed";
                Write(s);
                return s;
            }
            Write(s);
            Thread.Sleep(TimeSpan.FromSeconds(c.ControlIntervalSeconds));
        }
    }

    private void Deliver()
    {
        // Single-site mesh: proposals and replies addressed to this peer are delivered in order.
        for (int i = 0; i < 100 && loopback.TryDequeue(out var m); i++) peer!.Receive(m.From, m.Message);
    }

    private WorkloadInventory Inventory(HeatSignal heat)
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var resource = new ResourceAmount(c.RequestCpuCores, (long)c.RequestRamMib, 1);
        return new(++revision, start, start.AddHours(1), c.Source,
            [new(SiteId, "CH", heat, 0)],
            [new(SiteId + "-physical", resource, [])],
            [new(SiteId + "-worker", SiteId, SiteId + "-physical", true, true, resource, [], [], [StorageId], [])], [])
            { ResourceObservedAtUtc = now, ResourceValidUntilUtc = now.AddSeconds(30) };
    }

    private string ReadRaw(string name)
    {
        try { string path = Path.Combine(work, name); return File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch (IOException) { return ""; }
    }

    private string ReadText(string name) => ReadRaw(name).Trim();

    private void Write(Snapshot s)
    {
        s.UpdatedUtc = DateTimeOffset.UtcNow;
        s.Voice.Console = Log.Lines();
        string tmp = snapshotPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Snapshot.Json));
        File.Move(tmp, snapshotPath, true);
    }

    /// <summary>Hard-links the models into the bound folder and reads them once into RAM (page cache).</summary>
    private double PrepareModels()
    {
        string models = Path.Combine(voiceDir, "models");
        Directory.CreateDirectory(models);
        var watch = Stopwatch.StartNew();
        foreach (var name in new[] { c.WhisperModel, c.LlmModel })
        {
            string target = Path.Combine(models, name);
            if (!File.Exists(target))
            {
                string? source = new[] { Path.Combine(root, "data", "external", "transcription", "models", name),
                    Path.Combine(root, "data", "external", "llm", "models", name) }.FirstOrDefault(File.Exists);
                if (source is null) throw new FileNotFoundException($"Model {name} not found; see {c.ModelSource(name)}.");
                // A hard link costs no disk space; Docker cannot follow symlinks out of the bound folder.
                Shell("ln", source, target);
            }
            using var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            var buffer = new byte[1 << 22];
            while (stream.Read(buffer, 0, buffer.Length) > 0) cancel.ThrowIfCancellationRequested();
        }
        return watch.Elapsed.TotalSeconds;
    }

    private void EnsureVolume()
    {
        // Named volume bound to data/external/voice-demo, mounted through the mesh's existing
        // node-local volume binding (VolumeBinding/StorageId).
        try
        {
            string device = docker.Run(["volume", "inspect", "--format", "{{index .Options \"device\"}}", c.DockerVolume]).Trim();
            if (device != voiceDir) throw new InvalidDataException($"Docker volume {c.DockerVolume} points to {device}, expected {voiceDir}. Remove it and restart.");
        }
        catch (InvalidOperationException)
        {
            docker.Run(["volume", "create", "--driver", "local", "--opt", "type=none", "--opt", "o=bind", "--opt", "device=" + voiceDir, c.DockerVolume]);
        }
    }

    private void RemoveStaleDemoContainers()
    {
        // Only containers labelled by an earlier voice-demo session (fleet "voice-demo-…") are removed.
        foreach (string line in docker.Run(["ps", "-a", "--filter", "label=digitalheat.fleet", "--format", "{{.ID}} {{.Label \"digitalheat.fleet\"}}"])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', 2);
            if (parts.Length == 2 && parts[1].StartsWith("voice-demo-", StringComparison.Ordinal))
            { docker.Run(["rm", "-f", parts[0]]); Log.Line("WARN", "docker", "removed stale " + parts[0]); }
        }
    }

    private static string Shell(string file, params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var a in args) process.StartInfo.ArgumentList.Add(a);
        process.Start();
        string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{file} failed: {error.Trim()}");
        return output;
    }

    private sealed class Loopback(ConcurrentQueue<(string, PeerMessage)> queue) : IPeerMeshTransport
    {
        public void Send(string from, string to, PeerMessage message) => queue.Enqueue((from, message));
    }
}

internal static class Paths
{
    public static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "config"))) return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository.");
    }
}

/// <summary>Compact scheduler log for half a screen: simulated site time, level, component, message.
/// Job output is prefixed with its container name, like `docker logs`.</summary>
internal static class Log
{
    public static Func<double> Clock { get; set; } = () => 0;
    private static bool midLine;
    private static readonly object Gate = new();
    private const int MaxLines = 120;
    // The same lines as the terminal, kept for the live snapshot (pitch mode shows them as a console).
    private static readonly List<Snapshot.ConsoleLine> Recent = [];
    public static Snapshot.ConsoleLine[] Lines() { lock (Gate) return [.. Recent]; }
    private static void Keep(Snapshot.ConsoleLine line)
    {
        Recent.Add(line);
        if (Recent.Count > MaxLines) Recent.RemoveAt(0);
    }
    private static string Stamp() { int min = (int)Math.Floor(Clock() * 60 + 1e-6); return $"{min / 60:00}:{min % 60:00}"; }

    public static void Line(string level, string component, string message)
    {
        lock (Gate)
        {
            if (midLine) { Console.WriteLine(); midLine = false; }
            string lvl = level.PadRight(5);
            lvl = level switch { "WARN" => Ansi.Yellow(lvl), "ERROR" => Ansi.Red(lvl), _ => Ansi.Green(lvl) };
            Console.WriteLine($"{Ansi.Dim(Stamp())} {lvl} {component,-6} {message}");
            Keep(new(level.ToLowerInvariant(), $"{Stamp()} {level,-5} {component,-6} {message}"));
        }
    }

    public static void Output(string container, string text, bool newLine = false)
    {
        lock (Gate)
        {
            string name = (container.Length > 0 ? container : "job") + " | ";
            string prefix = Ansi.Cyan((container.Length > 0 ? container : "job") + " |") + " ";
            if (newLine) { if (midLine) Console.WriteLine(); Console.WriteLine(prefix + text); midLine = false; Keep(new("out", name + text)); return; }
            if (!midLine) { Console.Write(prefix); midLine = true; Keep(new("out", name + text)); }
            else Recent[^1] = Recent[^1] with { Text = Recent[^1].Text + text };
            Console.Write(text);
        }
    }
}

internal static class Ansi
{
    private static readonly bool On = !Console.IsOutputRedirected;
    private static string Wrap(string code, string text) => On ? $"\u001b[{code}m{text}\u001b[0m" : text;
    public static string Dim(string t) => Wrap("2", t);
    public static string Green(string t) => Wrap("1;32", t);
    public static string Yellow(string t) => Wrap("1;33", t);
    public static string Cyan(string t) => Wrap("1;36", t);
    public static string Red(string t) => Wrap("1;31", t);
}
