using System.Text.Json;
using Xunit;
namespace DigitalHeat.Orchestration.Tests;

public sealed class NodeShutdownTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"node-shutdown-"+Guid.NewGuid().ToString("N"));
    private readonly PreparedScene scene=new(new string('a',64),"recipe","5.3.0","build");
    private RenderConfig Config()
    {
        string assets=Path.Combine(root,"assets"),target=Path.Combine(root,"gpu3");Directory.CreateDirectory(assets);Directory.CreateDirectory(Path.Combine(target,"native-attempts"));
        var cfg=new RenderConfig("origin","image",Path.Combine(root,"origin"),"host",new(){["origin"]="http://127.0.0.1:1",["gpu3"]="http://127.0.0.1:2"},100,4,1024,.8,12,7200,"fixture")
            {AllowLocalNodeShutdown=true,Native=new(Environment.ProcessPath!,root,assets)};
        NativeRenderRuntime.WriteJson(Path.Combine(target,"config.json"),cfg with{Id="gpu3",StateDirectory=target,WorkerRole="render"});
        NativeRenderRuntime.WriteJson(Path.Combine(root,"processes.json"),new[]{new{node="gpu3",id=int.MaxValue,started_utc=DateTimeOffset.UtcNow}});
        return cfg;
    }
    [Fact] public void LocalFenceRejectsWrongRoleSelfAndChangedManifest()
    {
        var cfg=Config();var power=new LocalNodePowerControl(cfg);var identity=power.Prepare("gpu3");
        Assert.Throws<InvalidOperationException>(()=>power.Prepare("origin"));
        NativeRenderRuntime.WriteJson(Path.Combine(root,"processes.json"),new[]{new{node="gpu3",id=int.MaxValue-1,started_utc=identity.StartedUtc}});
        Assert.Throws<IOException>(()=>power.StopAndConfirm(identity));
        NativeRenderRuntime.WriteJson(Path.Combine(root,"gpu3","config.json"),cfg with{Id="gpu3",StateDirectory=Path.Combine(root,"gpu3"),WorkerRole="prepare"});
        Assert.Throws<InvalidDataException>(()=>power.Prepare("gpu3"));
    }
    [Fact] public void LaunchWithoutDurablePidBlocksFencingAndRetry()
    {
        var cfg=Config();string dir=Path.Combine(root,"gpu3","native-attempts","attempt");Directory.CreateDirectory(dir);
        NativeRenderRuntime.WriteJson(Path.Combine(dir,"owned.json"),new NativeOwnership("fleet","attempt","hash","activation",LaunchRequested:true));
        var power=new LocalNodePowerControl(cfg);
        Assert.Throws<IOException>(()=>power.StopAndConfirm(power.Prepare("gpu3")));
    }
    [Fact] public void AlreadyTerminatedIdentitiesCanBeConfirmedIdempotently()
    {
        var power=new LocalNodePowerControl(Config());var intent=power.Prepare("gpu3");power.StopAndConfirm(intent);power.StopAndConfirm(intent);
    }
    private RenderArtifact Result(string peer,int frame)
        =>new("origin.run-"+frame.ToString("D3"),peer,JsonSerializer.SerializeToElement(new{stage="frame",frame,recipe_id=scene.RecipeId,scene_sha256=scene.SceneSha256,build_hash=scene.BuildHash}));
    private static RenderPeerStatus Peer(string id,double weight,RenderArtifact[] results,AttemptRecord[] attempts)
        =>new(id,"host","image",true,10,"GPU",null,[],attempts,results,null){WorkerRole="render",WorkWeight=weight};
    private static AttemptRecord Inflight(int frame)=>new(new("fleet",Guid.NewGuid().ToString("N"),PlacementTests.Request("origin.run-"+frame.ToString("D3")),
        new("w","site","b","host",null,.8,1),CoordinatorTests.Node("b"),CoordinatorTests.Profile),"running");
    [Fact] public void RetryPlanRetainsCompletedAndInflightFramesBeforeSplittingRemainingHeat()
    {
        var active=new[]{Peer("gpu1",1.5,[Result("gpu1",1)],[Inflight(4)]),Peer("gpu2",1,[Result("gpu2",2)],[Inflight(5)])};
        var plan=RenderFailoverPlan.Create("origin","run",12,[],active,[new OriginJob(PlacementTests.Request("origin.run-006") with{Deadline=new(DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMinutes(5),"fixture",true)},"profile",Target:"gpu3")],[Result("gpu3",3)],"gpu3",new HashSet<string>{"origin.run-006"});
        Assert.Equal(6,plan.Needs["gpu1"]);Assert.Equal(5,plan.Needs["gpu2"]);
        Assert.Equal(12,plan.Needs.Values.Sum()+plan.FencedFrames.Length);
        Assert.Equal(1,plan.Generations[6]);Assert.Single(plan.CancelIds);Assert.Equal("run-retry1-006",RenderFailoverPlan.LocalJob("run",6,1));
        var restored=JsonSerializer.Deserialize<FrameRetryPlan>(JsonSerializer.Serialize(plan,RenderNodeHost.Json),RenderNodeHost.Json)!;
        Assert.Equal(plan.CancelIds,restored.CancelIds);Assert.Equal(plan.Needs,restored.Needs);Assert.Equal(plan.Generations,restored.Generations);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5),restored.Deadlines[6].DueAtUtc);
        // Replaying this saved plan requires no new generation or additional heat allocation.
        // At the hour boundary, keep completed and running survivor contributions in the plan;
        // the shared heat helper deducts only the completed earlier-hour work.
        var hour=DateTimeOffset.UnixEpoch.AddHours(2);var commitments=new HeatCommitment[]{
            new("origin.run-001",.8,[hour.AddHours(-1)],true),
            new("origin.run-004",.8,[hour.AddHours(-1),hour],false)};
        double demand=RenderHeatNeed.DemandKwh(new("origin.run-",restored.Needs["gpu1"],.8),commitments,hour,100);
        Assert.Equal(4*.8,demand-.8,5);
    }
    private static AttemptRecord Killed(int frame)=>Inflight(frame) with{Phase="released"};
    [Fact] public void RejoinSplitsOnlyOpenFramesAndTreatsKilledAttemptsAsOtherHeat()
    {
        // ADR 0073 power-on during a 12-frame render: frames 1-3 done (3 on GPU 3 before power-off), 4-5 running,
        // GPU 3's frame 6 was killed at power-off. The 7 open frames are split 1.5/1/0.5 (4/2/1).
        var peers=new[]{Peer("gpu1",1.5,[Result("gpu1",1)],[Inflight(4)]),Peer("gpu2",1,[Result("gpu2",2)],[Inflight(5)]),Peer("gpu3",.5,[],[Killed(6)])};
        var ids=Enumerable.Range(1,12).Select(f=>"origin.run-"+f.ToString("D3"));
        var needs=RenderFailoverPlan.RejoinNeeds(12,ids,peers,[Result("gpu3",3)],"origin.run-",.8);
        Assert.Equal(6,needs["gpu1"].Frames);Assert.Equal(4,needs["gpu2"].Frames);Assert.Equal(2,needs["gpu3"].Frames);
        Assert.Equal(12,needs.Values.Sum(n=>n.Frames));
        Assert.Equal(new[]{"origin.run-006"},needs["gpu3"].SupersededIds);Assert.Empty(needs["gpu1"].SupersededIds);
        // GPU 3 committed heat this hour for frame 3 (done) and the killed frame 6. With 6 superseded it still has
        // room for exactly one more frame; without the list the killed frame would use up its need.
        var hour=DateTimeOffset.UnixEpoch;var commitments=new HeatCommitment[]{new("origin.run-003",.8,[hour],true),new("origin.run-006",.8,[hour],true)};
        Assert.Equal(.8,RenderHeatNeed.DemandKwh(needs["gpu3"],commitments,hour,100)-1.6,5);
        Assert.Equal(0,RenderHeatNeed.DemandKwh(needs["gpu3"] with{SupersededIds=[]},commitments,hour,100)-1.6,5);
        Assert.Throws<InvalidDataException>(()=>RenderFailoverPlan.RejoinNeeds(2,ids,peers,[Result("gpu3",3)],"origin.run-",.8));
    }
    [Fact] public void PowerOnRequiresAFinishedPowerOffAndTheSameNode()
    {
        var cfg=Config();Directory.CreateDirectory(cfg.StateDirectory);var identity=new LocalNodePowerControl(cfg).Prepare("gpu3");
        foreach(var (stage,peer,phase) in new[]{("planned","gpu3","rendering"),("ready","origin","rendering"),("ready","gpu3","assembling"),("ready","gpu3","failed")}){
            NativeRenderRuntime.WriteJson(Path.Combine(cfg.StateDirectory,"render-session.json"),
                new RenderSessionState("run",phase,true,[]){Shutdown=new(stage,identity,Peer("gpu3",.5,[],[]))});
            using var http=new HttpClient();var session=new RenderSession(cfg,http);
            Assert.Throws<InvalidOperationException>(()=>session.StartupCoreForTest(peer).GetAwaiter().GetResult());
        }
    }
    [Fact] public async Task UnfinishedPowerOnBlocksANewRender()
    {
        // Review of ADR 0073: Start must not drop a "starting"/"started" power-on (the node would stay withdrawn).
        var cfg=Config();Directory.CreateDirectory(cfg.StateDirectory);var identity=new LocalNodePowerControl(cfg).Prepare("gpu3");
        NativeRenderRuntime.WriteJson(Path.Combine(cfg.StateDirectory,"render-session.json"),
            new RenderSessionState("run","complete",true,[]){Shutdown=new("started",identity,Peer("gpu3",.5,[],[]))});
        using var http=new HttpClient();var session=new RenderSession(cfg,http);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(session.Start);Assert.Contains("powering on",error.Message);
    }
    [Fact] public void FencedResultsPublishedAfterLastPollRemainDiscoverableAndCorruptionFailsClosed()
    {
        var cfg=Config();var identity=new LocalNodePowerControl(cfg).Prepare("gpu3");string id=Guid.NewGuid().ToString("N"),dir=Path.Combine(identity.StateDirectory,"native-attempts",id);Directory.CreateDirectory(dir);
        string image=Path.Combine(dir,"frame.png");File.WriteAllBytes(image,[137,80,78,71]);
        var artifact=Result("gpu3",3);var meta=RenderFrameStore.Publish(cfg.Native!.AssetsDirectory,image,artifact.Metadata);
        NativeRenderRuntime.WriteJson(Path.Combine(dir,"owned.json"),new NativeOwnership("landscape.gpu3",id,"hash","activation",ExitCode:0,Released:true));
        NativeRenderRuntime.WriteJson(Path.Combine(dir,"job.json"),new{job_id=artifact.JobId});NativeRenderRuntime.WriteJson(Path.Combine(dir,"result.json"),meta);
        var ids=new HashSet<string>{artifact.JobId};Assert.Single(RenderFrameStore.Recover(identity,cfg.Native.AssetsDirectory,ids,scene));
        File.WriteAllBytes(RenderFrameStore.VerifiedPath(cfg.Native.AssetsDirectory,meta),[0]);
        Assert.Throws<InvalidDataException>(()=>RenderFrameStore.Recover(identity,cfg.Native.AssetsDirectory,ids,scene));
    }
    private sealed class RegistrationHandler:HttpMessageHandler
    {
        public bool Fail=true;public readonly List<string> Registrations=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.RequestUri!.AbsolutePath=="/run-need"){
                Registrations.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                if(Fail){Fail=false;return new(System.Net.HttpStatusCode.ServiceUnavailable);}
            }
            if(request.Method==HttpMethod.Get){
                var peer=new RenderPeerStatus(request.RequestUri.Port==1?"origin":"gpu1","host","image",true,10,"fixture",null,[],[],[],null){WorkerRole=request.RequestUri.Port==1?"prepare":"render"};
                return new(System.Net.HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(peer,RenderNodeHost.Json),System.Text.Encoding.UTF8,"application/json")};
            }
            return new(System.Net.HttpStatusCode.OK);
        }
    }
    [Fact] public async Task PartialRegistrationRecoveryReplaysSavedPlanAndGeneration()
    {
        var cfg=Config() with{Peers=new(){["origin"]="http://127.0.0.1:1",["gpu1"]="http://127.0.0.1:3",["gpu3"]="http://127.0.0.1:2"}};
        Directory.CreateDirectory(cfg.StateDirectory);
        var identity=new LocalNodePowerControl(Config()).Prepare("gpu3");
        var plan=new FrameRetryPlan(new(){[6]=1},["origin.run-006"],new(){["gpu1"]=12},[]);
        // Completed logical work may finish while control holds dispatch; registration still must be replayable.
        var state=new RenderSessionState("run","complete",true,[]){Shutdown=new("planned",identity,Peer("gpu3",.5,[],[]),plan),FrameGenerations=plan.Generations};
        NativeRenderRuntime.WriteJson(Path.Combine(cfg.StateDirectory,"render-session.json"),state);
        using var handler=new RegistrationHandler();using var http=new HttpClient(handler);
        var first=new RenderSession(cfg,http);first.RequestShutdown("gpu3");
        for(int i=0;i<200;i++){
            if(!JsonSerializer.SerializeToElement(first.Snapshot()).GetProperty("node_shutdown_busy").GetBoolean())break;
            await Task.Delay(10);
        }
        var retained=JsonSerializer.Deserialize<RenderSessionState>(File.ReadAllText(Path.Combine(cfg.StateDirectory,"render-session.json")),RenderNodeHost.Json)!;
        Assert.Equal("planned",retained.Shutdown!.Stage);Assert.True(retained.Held);Assert.Equal(1,retained.FrameGenerations[6]);
        var recovered=new RenderSession(cfg,http);recovered.RequestShutdown("gpu3");
        for(int i=0;i<200;i++){
            if(!JsonSerializer.SerializeToElement(recovered.Snapshot()).GetProperty("node_shutdown_busy").GetBoolean())break;
            await Task.Delay(10);
        }
        retained=JsonSerializer.Deserialize<RenderSessionState>(File.ReadAllText(Path.Combine(cfg.StateDirectory,"render-session.json")),RenderNodeHost.Json)!;
        Assert.Equal("ready",retained.Shutdown!.Stage);Assert.Equal(1,retained.FrameGenerations[6]);
        Assert.Equal(2,handler.Registrations.Count);Assert.Equal(handler.Registrations[0],handler.Registrations[1]);
    }
    public void Dispose(){if(Directory.Exists(root)){string full=Path.GetFullPath(root);Assert.StartsWith(Path.GetTempPath(),full);Directory.Delete(full,true);}}
}
