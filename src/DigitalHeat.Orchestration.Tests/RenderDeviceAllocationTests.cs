using Xunit;
namespace DigitalHeat.Orchestration.Tests;
public class RenderDeviceAllocationTests
{
    [Fact] public void Approved48FrameMixHas38GpuAnd10CpuFrames()
    {
        var backends=Enumerable.Range(1,48).Select(f=>RenderDeviceAllocation.Backend(f,48,.8,"CUDA")).ToArray();
        Assert.Equal(38,backends.Count(b=>b=="CUDA"));Assert.Equal(10,backends.Count(b=>b=="CPU"));
        Assert.Contains("CPU",backends.Take(5));Assert.Contains("CUDA",backends.Take(5));
    }
    [Fact] public void MissingGpuIsExplicitAllCpuFallback()
        =>Assert.All(Enumerable.Range(1,48),f=>Assert.Equal("CPU",RenderDeviceAllocation.Backend(f,48,.8,null)));
    [Theory][InlineData(-.1)][InlineData(1.1)][InlineData(double.NaN)]
    public void InvalidRatiosAreRejected(double share)=>Assert.Throws<ArgumentOutOfRangeException>(()=>RenderDeviceAllocation.GpuFrames(48,share,true));
}
