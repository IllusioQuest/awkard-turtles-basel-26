using DigitalHeat.Core;
using DigitalHeat.Orchestration;
using Xunit;

namespace DigitalHeat.Orchestration.Tests;

public class CoordinatorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "dhw-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string fleet = "test-" + Guid.NewGuid().ToString("N");
    internal static RuntimeNode Node(string id) => new(id, "fixture-context", "none", [], []);
    internal static readonly ContainerProfile Profile = new("fixture", "local-fixture-image", "65534", ["sleep", "300"], null, [], [], ["true"]) { ShmMib = 16 };
    private static readonly RuntimeNode[] Nodes = [Node("a"), Node("b")];

    internal sealed class Runtime : IWorkloadRuntime
    {
        public Dictionary<string, ContainerObservation> States { get; } = [];
        public int Creates, Starts, Pauses, Stops;
        public bool FailStart, MissingAfterFailure, FailStop, FailCreateAfterDispatch, FailPreflight;
        public void Preflight(RuntimeAttempt a) { DockerWorkloadRuntime.Validate(a); if (FailPreflight) throw new InvalidDataException("fixture preflight rejected"); }
        public IReadOnlyList<string> Discover(string fleet, IReadOnlyList<RuntimeNode> nodes) => States.Where(s => s.Value.State != "missing").Select(s => s.Key).ToArray();
        public ContainerObservation Observe(RuntimeAttempt a) => States.GetValueOrDefault(a.AttemptId) ?? new("missing", null, false);
        public void Create(RuntimeAttempt a) { Creates++; States[a.AttemptId] = new("created", "abcdef123456", false); if (FailCreateAfterDispatch) throw new TimeoutException("uncertain create"); }
        public void Start(RuntimeAttempt a)
        {
            Starts++;
            States[a.AttemptId] = new(MissingAfterFailure ? "missing" : "running", "abcdef123456", true);
            if (FailStart) throw new TimeoutException("uncertain start");
        }
        public void Stop(RuntimeAttempt a) { Stops++; if (!FailStop) States[a.AttemptId] = new("exited", "abcdef123456", false); }
        public void Remove(RuntimeAttempt a) => States[a.AttemptId] = new("missing", null, false);
        public double ApplyBatchLoad(RuntimeAttempt a, double load) { if (load == 0) Pauses++; States[a.AttemptId] = new(load == 0 ? "paused" : "running", "abcdef123456", true); return load; }
    }

    private WorkloadCoordinator Start(Runtime runtime)
    {
        var c = new WorkloadCoordinator(fleet, directory, runtime); c.Update(PlacementTests.Inventory(), PlacementTests.Now); c.Reconcile(Nodes); return c;
    }

    [Fact] public void JournalsBeforeStartAndRetryNeverDuplicates()
    {
        var runtime = new Runtime(); using var c = Start(runtime);
        var request = PlacementTests.Request(); var first = c.TryStart(request, Nodes, Profile, PlacementTests.Now);
        Assert.Equal("running", first!.Phase); Assert.True(c.Observe(request.Id).Ready);
        Assert.Equal(first.Attempt.AttemptId, c.TryStart(request, Nodes, Profile, PlacementTests.Now)!.Attempt.AttemptId);
        Assert.Equal(1, runtime.Creates); Assert.Equal(1, runtime.Starts);
        Assert.Contains(first.Attempt.AttemptId, File.ReadAllText(Path.Combine(directory, "journal.json")));
    }

    [Fact] public void LockIsPerFleetEvenWithDifferentStatePaths()
    {
        using var c = Start(new Runtime());
        Assert.Throws<IOException>(() => new WorkloadCoordinator(fleet, directory + "-other", new Runtime()));
    }

    [Fact] public void CrashAfterUncertainStartReconcilesWithoutRelaunch()
    {
        var runtime = new Runtime { FailStart = true };
        using (var c = Start(runtime))
        {
            Assert.Throws<TimeoutException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
            Assert.False(c.Reconciled); Assert.Single(c.Allocations());
            Assert.Throws<InvalidOperationException>(() => c.TryStart(PlacementTests.Request("new"), Nodes, Profile, PlacementTests.Now));
        }
        using var recovered = Start(runtime);
        Assert.Equal("running", recovered.Attempts().Single().Phase);
        recovered.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now);
        Assert.Equal(1, runtime.Starts);
    }

    [Fact] public void MissingUncertainAttemptRetainsClaimAndBlocksRestartAdmission()
    {
        var runtime = new Runtime { FailStart = true, MissingAfterFailure = true };
        using (var c = Start(runtime)) Assert.Throws<TimeoutException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
        using var recovered = new WorkloadCoordinator(fleet, directory, runtime);
        Assert.Throws<InvalidOperationException>(() => recovered.Reconcile(Nodes));
        Assert.Single(recovered.Allocations()); Assert.Equal(1, runtime.Creates);
    }

    [Fact] public void BatchPausesFirstButRetainsMemoryAndProtectedGamesNeverPause()
    {
        var runtime = new Runtime(); using var c = Start(runtime);
        c.TryStart(PlacementTests.Request("batch", "batch", 2, 200), Nodes, Profile, PlacementTests.Now);
        Assert.Null(c.TryStart(PlacementTests.Request("large", ram: 64), Nodes, Profile, PlacementTests.Now));
        Assert.Equal(0, runtime.Pauses); // Pausing cannot help a RAM deficit.
        Assert.NotNull(c.TryStart(PlacementTests.Request("small", ram: 32), Nodes, Profile, PlacementTests.Now));
        Assert.Equal(1, runtime.Pauses);
        Assert.False(c.ResumeBatch("batch", PlacementTests.Now));
        Assert.Throws<InvalidOperationException>(() => c.PauseBatch("small"));
        c.StopAndRelease("small"); Assert.True(c.ResumeBatch("batch", PlacementTests.Now));
    }

    [Fact] public void SharedHeatSchedulerPausesAndResumesRealExecutorBoundary()
    {
        var runtime = new Runtime(); using var c = Start(runtime);
        c.TryStart(PlacementTests.Request("batch", "batch"), Nodes, Profile, PlacementTests.Now);
        Assert.True(c.ApplyBatchHeat("batch", new(0, 0, 0, 1, 0.8), new GreedyHeatScheduler(0), PlacementTests.Now));
        Assert.True(c.Allocations().Single().Paused);
        Assert.True(c.ApplyBatchHeat("batch", new(1, 0, 0, 1, 0.8), new GreedyHeatScheduler(0), PlacementTests.Now));
        Assert.False(c.Allocations().Single().Paused);
    }

    [Fact] public void UnconfirmedTerminationNeverReleasesCapacity()
    {
        var runtime = new Runtime { FailStop = true }; using var c = Start(runtime);
        c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now);
        Assert.Throws<InvalidOperationException>(() => c.StopAndRelease("game"));
        Assert.Single(c.Allocations()); Assert.False(c.Reconciled);
    }

    [Fact] public void UnknownOwnedContainerBlocksFreshJournal()
    {
        var runtime = new Runtime(); runtime.States["unknown"] = new("running", "abcdef123456", true);
        using var c = new WorkloadCoordinator(fleet, directory, runtime);
        Assert.Throws<InvalidOperationException>(() => c.Reconcile(Nodes));
    }

    [Fact] public void InvalidSubmissionDoesNotPauseOrClaimExistingResources()
    {
        var runtime = new Runtime(); using var c = Start(runtime);
        c.TryStart(PlacementTests.Request("batch", "batch", 2), Nodes, Profile, PlacementTests.Now);
        runtime.FailPreflight = true;
        Assert.Throws<InvalidDataException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
        Assert.Equal(0, runtime.Pauses); Assert.Single(c.Allocations());
    }

    [Fact] public void CrashAfterCreateCanContinueOnceWithoutDuplicateCreate()
    {
        var runtime = new Runtime { FailCreateAfterDispatch = true };
        using (var c = Start(runtime)) Assert.Throws<TimeoutException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
        using var recovered = Start(runtime);
        Assert.Equal("created", recovered.Attempts().Single().Phase);
        Assert.Equal("running", recovered.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now)!.Phase);
        Assert.Equal(1, runtime.Creates); Assert.Equal(1, runtime.Starts);
    }

    [Fact] public void CreatedAttemptCanBeCancelledWithoutPretendingItExited()
    {
        var runtime = new Runtime { FailCreateAfterDispatch = true };
        using (var c = Start(runtime)) Assert.Throws<TimeoutException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
        using var recovered = Start(runtime);
        recovered.StopAndRelease("game");
        Assert.Empty(recovered.Allocations()); Assert.Equal(0, runtime.Stops);
    }

    [Fact] public void CreatedRecoveryRequiresFreshResourcesBeforeStarting()
    {
        var runtime = new Runtime { FailCreateAfterDispatch = true };
        using (var c = Start(runtime)) Assert.Throws<TimeoutException>(() => c.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now));
        using var recovered = Start(runtime);
        Assert.Null(recovered.TryStart(PlacementTests.Request(), Nodes, Profile, PlacementTests.Now.AddMinutes(2)));
        Assert.Equal(0, runtime.Starts); Assert.Single(recovered.Allocations());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConfigAndPreflightAgreeOnPhysicalDomainsPerDockerEngine(bool splitDomains, bool splitContexts)
    {
        var inventory = PlacementTests.Inventory() with { Revision = 2 };
        if (splitDomains)
            inventory = inventory with
            {
                Domains = [inventory.Domains[0], inventory.Domains[0] with { Id = "other-host" }],
                Nodes = [inventory.Nodes[0], inventory.Nodes[1] with { DomainId = "other-host" }]
            };
        RuntimeNode[] nodes = [Nodes[0], splitContexts ? Nodes[1] with { DockerContext = "other-context" } : Nodes[1]];
        var request = PlacementTests.Request();
        var config = new WorkloadConfiguration(fleet, inventory, nodes, [new(request, Profile)]);
        var runtime = new Runtime();
        using var coordinator = Start(runtime);
        coordinator.Update(inventory, PlacementTests.Now);
        string path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(config, WorkloadJson.Options));
        if (splitDomains && !splitContexts)
        {
            Assert.Throws<InvalidDataException>(() => WorkloadConfiguration.Load(path));
            Assert.Throws<InvalidDataException>(() => coordinator.TryStart(request, nodes, Profile, PlacementTests.Now));
            Assert.Equal(0, runtime.Creates);
        }
        else
        {
            Assert.NotNull(WorkloadConfiguration.Load(path));
            Assert.NotNull(coordinator.TryStart(request, nodes, Profile, PlacementTests.Now));
        }
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
