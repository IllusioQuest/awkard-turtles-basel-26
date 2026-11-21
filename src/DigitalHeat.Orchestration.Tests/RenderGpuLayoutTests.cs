using Xunit;
namespace DigitalHeat.Orchestration.Tests;
public class RenderGpuLayoutTests
{
    private static RenderConfig Config()=>new("cpu","recipe","state","host",[],100,4,1536,.8,48,7200,"Synthetic test") {SharedGpuGroup="lab",SharedGpuLimit=3};
    private static RenderPeerStatus Peer(string id)=>new(id,"host","recipe",true,100,"GPU",null,[],[],[],null){PhysicalGpuId="shared-gpu",FrameBoundaryPause=true,SharedGpuGroup="lab",SharedGpuLimit=3};
    [Fact] public void ExplicitMatchingThreeWorkerLabSharesOneGpu()=>RenderGpuLayout.Validate(Config(),[Peer("a"),Peer("b"),Peer("c")]);
    [Fact] public void DefaultStillRejectsAliasedGpu()=>Assert.Throws<InvalidDataException>(()=>RenderGpuLayout.Validate(Config() with{SharedGpuGroup=null,SharedGpuLimit=1},[Peer("a"),Peer("b")]));
    [Fact] public void MismatchedGroupsAreRejected()=>Assert.Throws<InvalidDataException>(()=>RenderGpuLayout.Validate(Config(),[Peer("a"),Peer("b") with{SharedGpuGroup="other"}]));
    [Fact] public void SameGpuCannotAppearOnDifferentHosts()=>Assert.Throws<InvalidDataException>(()=>RenderGpuLayout.Validate(Config(),[Peer("a"),Peer("b") with{PhysicalHostId="other"}]));
    [Fact] public void NativeMultiAdapterSelectionIsNotInvented()=>Assert.Throws<InvalidDataException>(()=>RenderGpuLayout.Validate(Config(),[Peer("a"),Peer("b") with{PhysicalGpuId="other-gpu"}]));
    [Fact] public void ConcurrencyLimitCannotBeExceeded()=>Assert.Throws<InvalidDataException>(()=>RenderGpuLayout.Validate(Config(),[Peer("a"),Peer("b"),Peer("c"),Peer("d")]));
}
