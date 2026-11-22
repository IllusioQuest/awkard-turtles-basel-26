using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class DeadlineTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "deadline-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string fleet = "deadline-" + Guid.NewGuid().ToString("N");
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = PlacementTests.Now;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }
    private sealed class Runtime : IWorkloadRuntime
    {
        public readonly Dictionary<string, ContainerObservation> States = [];
        public int Creates, Starts, Stops;
        public bool FailStop, CompleteOnStart;
        public Action? OnCreate, OnStart, OnPreflight;
        public void Preflight(RuntimeAttempt a) { DockerWorkloadRuntime.Validate(a); OnPreflight?.Invoke(); }
        public IReadOnlyList<string> Discover(string fleet, IReadOnlyList<RuntimeNode> nodes) => States.Where(p => p.Value.State != "missing").Select(p => p.Key).ToArray();
        public ContainerObservation Observe(RuntimeAttempt a) => States.GetValueOrDefault(a.AttemptId) ?? new("missing", null, false);
        public void Create(RuntimeAttempt a) { Creates++; States[a.AttemptId] = new("created", "abcdef123456", false); OnCreate?.Invoke(); }
        public void Start(RuntimeAttempt a) { Starts++; States[a.AttemptId] = new(CompleteOnStart ? "exited" : "running", "abcdef123456", !CompleteOnStart) { ExitCode = CompleteOnStart ? 0 : null }; OnStart?.Invoke(); }
        public void Stop(RuntimeAttempt a) { Stops++; if (!FailStop) States[a.AttemptId] = new("exited", "abcdef123456", false) { ExitCode = 143 }; }
        public void Remove(RuntimeAttempt a) => States[a.AttemptId] = new("missing", null, false);
        public double ApplyBatchLoad(RuntimeAttempt a, double load) { States[a.AttemptId] = new(load == 0 ? "paused" : "running", "abcdef123456", load != 0); return load; }
    }
    private static readonly RuntimeNode[] Nodes = [CoordinatorTests.Node("a"), CoordinatorTests.Node("b")];
    private WorkloadCoordinator Start(Runtime runtime, Clock clock)
    {
        var c = new WorkloadCoordinator(fleet, directory, runtime, clock);
        c.Update(PlacementTests.Inventory(), clock.Now); c.Reconcile(Nodes); return c;
    }
    private static WorkloadRequest Request(double seconds = 5, bool stop = true) => PlacementTests.Request("timed", "batch") with
    {
        Deadline = new(PlacementTests.Now, PlacementTests.Now.AddSeconds(seconds), "Synthetic deadline test policy", true),
        Lifecycle = new(true, stop, false)
    };
    [Fact] public void ExpiryIsExclusiveAndFutureSubmissionsDoNotStart()
    {
        var r = new Runtime(); var clock = new Clock(); using var c = Start(r, clock);
        clock.Advance(5); Assert.Null(c.TryStart(Request(), Nodes, CoordinatorTests.Profile, PlacementTests.Now));
        Assert.Equal(0, r.Creates); Assert.Empty(c.Allocations());
        var future = Request() with { Deadline = new(clock.Now.AddSeconds(1), clock.Now.AddSeconds(10), "Synthetic", true) };
        Assert.Null(c.TryStart(future, Nodes, CoordinatorTests.Profile, PlacementTests.Now));
    }
    [Fact] public void ExpiryDuringPreflightPreventsCreate()
    {
        var clock = new Clock(); var r = new Runtime { OnPreflight = () => clock.Advance(6) }; using var c = Start(r, clock);
        Assert.Null(c.TryStart(Request(), Nodes, CoordinatorTests.Profile, PlacementTests.Now)); Assert.Equal(0, r.Creates);
    }
    [Fact] public void ExpiryDuringCreatePreventsStartAndCleansConfirmedCreatedContainer()
    {
        var clock = new Clock(); var r = new Runtime { OnCreate = () => clock.Advance(6) }; using var c = Start(r, clock);
        var result = c.TryStart(Request(), Nodes, CoordinatorTests.Profile, PlacementTests.Now)!;
        Assert.Equal(0, r.Starts); Assert.Equal("released", result.Phase); Assert.Equal("missed", result.Deadline!.Status); Assert.Empty(c.Allocations());
    }
    [Fact] public void ExpiryDuringBlockingStartIsRecordedBeforeStop()
    {
        var clock = new Clock(); var r = new Runtime { OnStart = () => clock.Advance(6) }; using var c = Start(r, clock);
        var result = c.TryStart(Request(), Nodes, CoordinatorTests.Profile, PlacementTests.Now)!;
        Assert.Equal("missed", result.Deadline!.Status); Assert.True(result.Deadline.StopIntentPersisted); Assert.Equal(1, r.Stops); Assert.Equal("released", result.Phase);
    }
    private static void Enforce(WorkloadCoordinator c,bool snapshot)
    { if(snapshot)c.EnforceDeadlines();else c.EnforceDeadlinePolicies(); }
    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void RunningAndPausedOverdueJobsStopButNeverBecomeSuccess(bool pause,bool snapshot)
    {
        var clock = new Clock(); var r = new Runtime();
        using (var c = Start(r, clock))
        {
            c.TryStart(Request(), Nodes, CoordinatorTests.Profile, clock.Now);
            if (pause) c.PauseBatch("timed"); clock.Advance(5); Enforce(c,snapshot);
            Assert.Equal("missed", c.Attempts().Single().Deadline!.Status); Assert.Equal(1, r.Stops); Assert.Empty(c.Allocations());
        }
        using var recovered = new WorkloadCoordinator(fleet, directory, r, clock); recovered.Reconcile(Nodes);
        Assert.Equal("missed", recovered.Attempts().Single().Deadline!.Status); Assert.Equal("released", recovered.Attempts().Single().Phase);
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void StopRequiresLifecyclePermissionAndLatenessPersists(bool snapshot)
    {
        var clock = new Clock(); var r = new Runtime(); using var c = Start(r, clock);
        c.TryStart(Request(stop: false), Nodes, CoordinatorTests.Profile, clock.Now); clock.Advance(10); Enforce(c,snapshot);
        Assert.Equal(0, r.Stops); Assert.Single(c.Allocations()); Assert.Equal("missed", c.Attempts().Single().Deadline!.Status);
        Assert.False(c.ResumeBatch("timed", PlacementTests.Now));
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void UncertainStopRetainsResourcesAndResumesCleanupAfterRecovery(bool snapshot)
    {
        var clock = new Clock(); var r = new Runtime { FailStop = true };
        using (var c = Start(r, clock))
        {
            c.TryStart(Request(), Nodes, CoordinatorTests.Profile, clock.Now); clock.Advance(6);
            Assert.Throws<InvalidOperationException>(() => Enforce(c,snapshot)); Assert.Single(c.Allocations()); Assert.False(c.Reconciled);
            Assert.True(c.Attempts().Single().Deadline!.StopIntentPersisted);
        }
        r.FailStop = false; using var recovered = new WorkloadCoordinator(fleet, directory, r, clock);
        recovered.Reconcile(Nodes); Enforce(recovered,snapshot); Assert.Empty(recovered.Allocations()); Assert.Equal("missed", recovered.Attempts().Single().Deadline!.Status);
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void SuccessfulEarlyCompletionSurvivesRestartPastDue(bool snapshot)
    {
        var clock = new Clock(); var r = new Runtime { CompleteOnStart = true };
        using (var c = Start(r, clock))
        {
            var result = c.TryStart(Request(), Nodes, CoordinatorTests.Profile, clock.Now)!;
            Assert.Equal("terminal", result.Phase); Assert.Equal("on_time", result.Deadline!.Status);
        }
        clock.Advance(10); using var recovered = new WorkloadCoordinator(fleet, directory, r, clock); recovered.Reconcile(Nodes); Enforce(recovered,snapshot);
        Assert.Equal("on_time", recovered.Attempts().Single().Deadline!.Status); Assert.Equal(0, r.Stops);
    }
    [Theory] [InlineData(false)] [InlineData(true)] public void FirstTerminalObservationAfterDueCannotClaimOnTimeFromRemoteClock(bool snapshot)
    {
        var clock = new Clock(); var r = new Runtime(); using var c = Start(r, clock);
        var attempt = c.TryStart(Request(), Nodes, CoordinatorTests.Profile, clock.Now)!.Attempt;
        r.States[attempt.AttemptId] = new("exited", "abcdef123456", false) { ExitCode = 0, ReportedFinishedAtUtc = clock.Now.AddSeconds(1) };
        clock.Advance(10); Enforce(c,snapshot); Assert.Equal("unknown", c.Attempts().Single().Deadline!.Status); Assert.Equal(0, r.Stops);
    }
    [Fact] public void RetriesCannotExtendDeadlineAndGamesCannotAcquireOne()
    {
        var clock = new Clock(); var r = new Runtime(); using var c = Start(r, clock);
        c.TryStart(Request(), Nodes, CoordinatorTests.Profile, clock.Now);
        Assert.Throws<InvalidDataException>(() => c.TryStart(Request(10), Nodes, CoordinatorTests.Profile, clock.Now));
        Assert.Throws<InvalidDataException>(() => WorkloadPlacement.ValidateRequest(Request() with { Kind = "interactive", Lifecycle = new(false, true, false) }));
        Assert.Equal(1, r.Starts);
    }
    [Fact] public void LegacyJournalRetainsExactPreDeadlineContainerHash()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/legacy-deadline-journal.json")));
        string journal = doc.RootElement.GetProperty("journal").GetRawText();
        var state = JsonSerializer.Deserialize<CoordinatorState>(journal, WorkloadJson.Options)!;
        string identity = WorkloadJson.Identity(state.Attempts.Single().Attempt);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        Assert.Equal(doc.RootElement.GetProperty("expected_spec_sha256").GetString(), hash); Assert.DoesNotContain("\"deadline\"", identity);
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "journal.json"), journal);
        var r = new Runtime(); r.States[state.Attempts[0].Attempt.AttemptId] = new("running", "abcdef123456", true);
        using var c = new WorkloadCoordinator(state.FleetId, directory, r, new Clock()); c.Reconcile(Nodes); Assert.Null(c.Attempts().Single().Deadline);
    }
    [Fact] public void ProfilesHaveMinuteTargetsAndRejectLargerOrWrongWorkloads()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "config/live-job-deadlines.json"))) root = root.Parent;
        Assert.NotNull(root);
        var p = LiveDeadlineProfiles.Load(Path.Combine(root!.FullName, "config/live-job-deadlines.json"));
        var small = p.For("small-summary", "llm-summary-apertus-8b", 15, PlacementTests.Now);
        Assert.Equal(TimeSpan.FromMinutes(5), small.DueAtUtc - small.SubmittedAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(10), p.For("finite-research", "research-sim-montecarlo", 320, PlacementTests.Now).DueAtUtc - PlacementTests.Now);
        Assert.Throws<ArgumentException>(() => p.For("small-summary", "llm-summary-apertus-8b", 16, PlacementTests.Now));
        Assert.Throws<ArgumentException>(() => p.For("small-summary", "wrong", 1, PlacementTests.Now));
        Assert.Throws<ArgumentException>(() => p.For("finite-research", "research-sim-montecarlo", 321, PlacementTests.Now));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
