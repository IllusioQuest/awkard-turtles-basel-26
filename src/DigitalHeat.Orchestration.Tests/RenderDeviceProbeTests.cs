using Xunit;
namespace DigitalHeat.Orchestration.Tests;

public class RenderDeviceProbeTests
{
    private static RenderConfig Config(bool gpu=false)=>new("node","image","state","host",[],100,1,1536,.8,48,7200,"lab")
        {Gpu=gpu?new("gpu0","GPU-test",[],[]):null,GpuBackend=gpu?"CUDA":null};
    private sealed class Docker : IDockerCommands
    {
        public readonly List<string[]> Calls=[];
        public bool LoseAcknowledgement,StopFails,MissingConfirmation;
        private int runs;
        public string Run(IReadOnlyList<string> arguments)
        {
            string[] a=arguments.ToArray();Calls.Add(a);
            if(a[0]=="run"){
                runs++;
                Assert.Contains("--name",a);Assert.Contains("--label",a);
                if(LoseAcknowledgement)throw new IOException("Lost create acknowledgement");
                return new string('a',64);
            }
            if(a[0]=="ps"){
                Assert.Contains(a,x=>x.StartsWith("name=^/digitalheat-render-probe-"));
                Assert.Contains(a,x=>x.StartsWith("label=digitalheat.render-probe="));
                return new string('a',64);
            }
            if(a[0]=="inspect")return a[2].Contains("Running")?"false":"0";
            if(a[0]=="logs")return runs==1?"RENDER_DEVICES={\"compiled_backends\":[\"CUDA\"],\"gpus\":[]}":MissingConfirmation?"No verified render":"RENDER_GPU_VERIFIED=CUDA";
            if(a[0]=="stop"&&StopFails)throw new IOException("Stop failed");
            return "";
        }
    }
    [Fact] public void CpuProbeDoesNotAdvertiseGpuAndRemovesItsVolume()
    {
        var docker=new Docker();var report=RenderDeviceProbe.Run(Config(),docker);
        Assert.Null(report.VerifiedBackend);
        Assert.Single(docker.Calls,a=>a[0]=="run");
        Assert.Contains(docker.Calls,a=>a.SequenceEqual(new[]{"rm","--force","-v",new string('a',64)}));
    }
    [Fact] public void GpuRequiresCompletedRenderWithSameBindingsAndLimits()
    {
        var docker=new Docker();Assert.Equal("CUDA",RenderDeviceProbe.Run(Config(true),docker).VerifiedBackend);
        var runs=docker.Calls.Where(a=>a[0]=="run").ToArray();Assert.Equal(2,runs.Length);
        foreach(var a in runs){Assert.Contains("device=GPU-test",a);Assert.Contains("65534",a);Assert.Contains("1536m",a);Assert.Contains("no-new-privileges:true",a);}
    }
    [Fact] public void EnumerationAloneCannotCertifyGpu()
    {
        var docker=new Docker{MissingConfirmation=true};
        Assert.Null(RenderDeviceProbe.Run(Config(true),docker).VerifiedBackend);
    }
    [Fact] public void LostCreateAcknowledgementAndFailedStopStillAttemptOwnedForceRemoval()
    {
        var docker=new Docker{LoseAcknowledgement=true,StopFails=true};
        Assert.Throws<IOException>(()=>RenderDeviceProbe.Run(Config(),docker));
        Assert.Contains(docker.Calls,a=>a[0]=="rm"&&a.Contains("--force")&&a.Contains("-v"));
    }
}
