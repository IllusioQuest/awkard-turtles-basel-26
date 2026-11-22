using System.Text.Json;
using Xunit;
namespace DigitalHeat.Orchestration.Tests;

public sealed class NativeRenderRuntimeTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"native-render-tests-"+Guid.NewGuid().ToString("N"));
    private readonly RenderConfig config;
    private readonly NativeRenderRuntime runtime;
    private readonly List<NativeRenderRuntime> warmRuntimes=[];
    public NativeRenderRuntimeTests()
    {
        Directory.CreateDirectory(root);
        foreach(string file in new[]{"native_worker.py","scene_builder.py","scene_settings.json","warm_worker.py"})File.WriteAllText(Path.Combine(root,file),"trusted fixture");
        config=new("node","unused",root,"host",[],100,1,1536,.8,48,7200,"fixture")
            {PreparedSceneMode=true,Native=new(Environment.ProcessPath!,root,Path.Combine(root,"assets"))};
        runtime=new(config);
    }
    private RuntimeAttempt Attempt(bool gpu=false)=>new("fixture",new string('a',32),PlacementTests.Request("batch","batch",.5) with{Lifecycle=new(false,true,false)},
        new("batch","spa","b","host",gpu?"logical-gpu":null,.8,14),
        new("b","default","none",gpu?[new("logical-gpu",null,[],[])]:[],[]),
        new("native-prepare",runtime.ProfileImage,"65534",["prepare"],null,[],[],[]));
    [Fact] public void NativeLogicalGpuDoesNotNeedFabricatedDockerDevicePaths()
    {
        var attempt=Attempt(true);((IWorkloadRuntime)runtime).ValidateAttempt(attempt);
        Assert.Throws<InvalidDataException>(()=>DockerWorkloadRuntime.Validate(attempt));
        Assert.Throws<InvalidDataException>(()=>((IWorkloadRuntime)new CoordinatorTests.Runtime()).ValidateAttempt(attempt));
    }
    [Fact] public void NeverStartedAttemptCanBeCancelledWithoutStartingAProcess()
    {
        var attempt=Attempt();runtime.Create(attempt);Assert.Equal("created",runtime.Observe(attempt).State);
        runtime.Remove(attempt);Assert.Equal("missing",runtime.Observe(attempt).State);Assert.Empty(runtime.Discover("fixture",[]));
    }
    [Fact] public void AmbiguousSpawnRetainsOwnershipAndCannotRelaunch()
    {
        var attempt=Attempt();runtime.Create(attempt);string path=Path.Combine(root,"native-attempts",attempt.AttemptId,"owned.json");
        var owned=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(path),RenderNodeHost.Json)!;
        NativeRenderRuntime.WriteJson(path,owned with{LaunchRequested=true});
        Assert.Throws<IOException>(()=>runtime.Observe(attempt));Assert.Throws<IOException>(()=>runtime.Start(attempt));
        Assert.Single(runtime.Discover("fixture",[]));
    }
    [Fact] public void ChangedAttemptIdentityCannotStopAnything()
    {
        var attempt=Attempt();runtime.Create(attempt);
        var changed=attempt with{Request=attempt.Request with{RecoverableHeatKwhPerHour=.9}};
        Assert.Throws<InvalidOperationException>(()=>runtime.Stop(changed));
    }
    [Fact] public void RecipeDoesNotDependOnExecutablePathGraphicsApiOrGpuRole()
    {
        string other=Path.Combine(root,"other-platform-executable");File.WriteAllText(other,"not executed");
        var portable=new NativeRenderRuntime(config with{Native=config.Native! with{Executable=other,GraphicsApi="vulkan",EnableGpu=true}});
        Assert.Equal(runtime.RecipeId,portable.RecipeId);
        File.AppendAllText(Path.Combine(root,"scene_settings.json"),"changed");
        Assert.NotEqual(runtime.RecipeId,new NativeRenderRuntime(config).RecipeId);
    }
    [Fact] public void CpuPreparationWorkerCannotRunGpuFrameProfile()
    {
        var a=Attempt();a=a with{Profile=a.Profile with{Command=["1","EEVEE",new string('a',64)]}};
        Assert.Throws<InvalidDataException>(()=>runtime.Preflight(a));
    }
    [Fact] public void ProtectedAttemptCannotBeStoppedDirectly()
    {
        var a=Attempt();a=a with{Request=a.Request with{Lifecycle=new(false,false,false)}};
        Assert.Throws<InvalidOperationException>(()=>runtime.Stop(a));
    }
    [Fact] public void FrameOnlyWorkerCannotPrepareScene()
    {
        using var worker=new NativeRenderRuntime(config with{WorkerRole="render"});
        Assert.Throws<InvalidDataException>(()=>worker.Preflight(Attempt()));
    }
    [Fact] public void AssemblyOnlyWorkerCannotPrepareScene()
    {
        using var worker=new NativeRenderRuntime(config with{WorkerRole="assemble"});
        Assert.Throws<InvalidDataException>(()=>worker.Preflight(Attempt()));
    }
    [Fact] public void MalformedOptionalSceneCacheDoesNotInvalidateOwnership()
    {
        File.WriteAllText(Path.Combine(root,"registered-scene-broken.json"),"{broken");
        var valid=new PreparedScene(new string('a',64),runtime.RecipeId,"fixture","build");
        NativeRenderRuntime.WriteJson(Path.Combine(root,"registered-scene-valid.json"),valid);
        Assert.Equal(valid,Assert.Single(runtime.SavedScenes()));
        var a=Attempt();runtime.Create(a);Assert.Equal("created",runtime.Observe(a).State);
    }
    [Fact] public void ResultsWaitForSuccessfulReleaseAndRebuildAfterRestart()
    {
        var a=Attempt();runtime.Create(a);
        string dir=Path.Combine(root,"native-attempts",a.AttemptId);
        NativeRenderRuntime.WriteJson(Path.Combine(dir,"result.json"),new{stage="frame",frame=1});
        Assert.Empty(runtime.Results()); // A result file alone is not a committed success.
        string path=Path.Combine(dir,"owned.json");
        var owned=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(path),RenderNodeHost.Json)!;
        NativeRenderRuntime.WriteJson(path,owned with{Released=true,ExitCode=0});
        Assert.Equal(a.Request.Id,Assert.Single(runtime.Results()).JobId);
        using var recovered=new NativeRenderRuntime(config);
        Assert.Equal(a.Request.Id,Assert.Single(recovered.Results()).JobId);
    }
    [Fact] public void FailedReleaseNeverPublishesResultMetadata()
    {
        var a=Attempt();runtime.Create(a);
        string dir=Path.Combine(root,"native-attempts",a.AttemptId),path=Path.Combine(dir,"owned.json");
        NativeRenderRuntime.WriteJson(Path.Combine(dir,"result.json"),new{stage="frame",frame=1});
        var owned=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(path),RenderNodeHost.Json)!;
        NativeRenderRuntime.WriteJson(path,owned with{Released=true,ExitCode=1});
        Assert.Empty(runtime.Results());Assert.Empty(runtime.Results());
    }
    private (NativeRenderRuntime Runtime,RuntimeAttempt Attempt,NativeOwnership Owned) WarmFrame()
    {
        var warm=new NativeRenderRuntime(config with{Native=config.Native! with{EnableGpu=true,KeepGpuWarm=true}});
        warmRuntimes.Add(warm);
        byte[] scene=[1,2,3];string hash=NativeRenderRuntime.Hash(scene);
        File.WriteAllBytes(Path.Combine(config.Native!.AssetsDirectory,hash+".blend"),scene);
        var a=Attempt(true);a=a with{Profile=a.Profile with{Image=warm.ProfileImage,Command=["1","EEVEE",hash]}};
        warm.Create(a);string path=Path.Combine(root,"native-attempts",a.AttemptId,"owned.json");
        var owned=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(path),RenderNodeHost.Json)!;
        owned=owned with{LaunchRequested=true,Pid=int.MaxValue,StartedUtc=DateTimeOffset.UtcNow};NativeRenderRuntime.WriteJson(path,owned);
        return(warm,a,owned);
    }
    private void CompleteWarm(RuntimeAttempt a,NativeOwnership owned,string? activation=null)
        =>NativeRenderRuntime.WriteJson(Path.Combine(root,"native-attempts",a.AttemptId,"exit.json"),new{code=0,activation=activation??owned.Activation,recipe_id=runtime.RecipeId});
    [Fact] public void CompletedWarmFrameRemainsSuccessfulAfterWorkerDeath()
    {
        var (warm,a,owned)=WarmFrame();CompleteWarm(a,owned);
        Assert.Equal(0,warm.Observe(a).ExitCode); // nonexistent PID must not erase a committed completion.
    }
    [Fact] public void WarmCompletionWithWrongActivationFailsClosed()
    {
        var (warm,a,owned)=WarmFrame();CompleteWarm(a,owned,"different-frame");
        Assert.Throws<IOException>(()=>warm.Observe(a));
    }
    [Fact] public void DelayedStopOfCompletedFrameCannotKillNewFrameSharingThePool()
    {
        var (warm,a,owned)=WarmFrame();CompleteWarm(a,owned);
        string directory=Path.Combine(root,"native-warm-worker");Directory.CreateDirectory(directory);
        var pool=new NativeWarmOwnership(owned with{SpecHash=warm.RecipeId},"new-frame","new-frame-directory");
        NativeRenderRuntime.WriteJson(Path.Combine(directory,"owned.json"),pool);
        warm.Stop(a);
        Assert.Equal("new-frame",JsonSerializer.Deserialize<NativeWarmOwnership>(File.ReadAllText(Path.Combine(directory,"owned.json")),RenderNodeHost.Json)!.ActiveActivation);
        Assert.Equal(0,warm.Observe(a).ExitCode);
    }
    [Fact] public void StopWithoutActiveWarmFrameOwnershipFailsClosed()
    {
        var (warm,a,owned)=WarmFrame();string directory=Path.Combine(root,"native-warm-worker");Directory.CreateDirectory(directory);
        NativeRenderRuntime.WriteJson(Path.Combine(directory,"owned.json"),new NativeWarmOwnership(owned with{SpecHash=warm.RecipeId},"another-frame","other"));
        Assert.Throws<IOException>(()=>warm.Stop(a));
    }
    public void Dispose()
    {
        runtime.Dispose();foreach(var warm in warmRuntimes)warm.Dispose();
        string full=Path.GetFullPath(root);Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),full);Directory.Delete(full,true);
    }
}
