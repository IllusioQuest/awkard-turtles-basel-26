using System.Text.Json;
using Xunit;
namespace DigitalHeat.Orchestration.Tests;

public sealed class NativeRenderRuntimeTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"native-render-tests-"+Guid.NewGuid().ToString("N"));
    private readonly RenderConfig config;
    private readonly NativeRenderRuntime runtime;
    public NativeRenderRuntimeTests()
    {
        Directory.CreateDirectory(root);
        foreach(string file in new[]{"native_worker.py","scene_builder.py","scene_settings.json"})File.WriteAllText(Path.Combine(root,file),"trusted fixture");
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
    public void Dispose()
    {
        string full=Path.GetFullPath(root);Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),full);Directory.Delete(full,true);
    }
}
