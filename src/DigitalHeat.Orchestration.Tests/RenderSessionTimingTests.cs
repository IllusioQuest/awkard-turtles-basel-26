using Xunit;
namespace DigitalHeat.Orchestration.Tests;
public class RenderSessionTimingTests
{
    private static readonly DateTimeOffset Start=new(2026,11,21,12,0,0,TimeSpan.Zero);
    [Fact] public void StoppedRunDoesNotKeepAccumulatingPauseTime()
    {
        var state=new RenderSessionState("run","cancelled",true,[]){StartedUtc=Start,FinishedUtc=Start.AddSeconds(120),PauseStartedUtc=Start.AddSeconds(90),PausedSeconds=10};
        Assert.Equal((120d,40d,80d),RenderSession.Timing(state,Start.AddHours(2)));
    }
    [Fact] public void ActiveTimeFreezesWhilePaused()
    {
        var state=new RenderSessionState("run","rendering",true,[]){StartedUtc=Start,PauseStartedUtc=Start.AddSeconds(90)};
        Assert.Equal(90,RenderSession.Timing(state,Start.AddSeconds(120)).Active);
        Assert.Equal(90,RenderSession.Timing(state,Start.AddSeconds(150)).Active);
    }
    [Fact] public void PreparationPauseIsNotSubtractedFromGpuRenderTime()
    {
        var state=new RenderSessionState("run","preparing",true,[]){PauseStartedUtc=Start};
        Assert.Equal((0d,0d,0d),RenderSession.Timing(state,Start.AddSeconds(30)));
    }
}
