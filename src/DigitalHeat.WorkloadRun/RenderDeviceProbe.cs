using System.Globalization;
using System.Text.Json;
using DigitalHeat.Orchestration;

internal sealed record RenderDeviceReport(string? VerifiedBackend,string Diagnostic,JsonElement Devices);

/// <summary>Shared by startup and the offline deployment preflight. Never changes scheduler policy.</summary>
internal static class RenderDeviceProbe
{
    // Cold GPU kernel compilation may exceed the Docker control command's 30-second limit.
    // Poll a bounded owned container instead; never leave an untracked probe running on timeout.
    private static readonly TimeSpan RenderTimeout=TimeSpan.FromSeconds(90);
    public static RenderDeviceReport Run(RenderConfig cfg,IDockerCommands docker)
    {
        string Execute(params string[] command)
        {
            string ownership=Guid.NewGuid().ToString("N");
            string name="digitalheat-render-probe-"+ownership;
            var args=new List<string>{"run","-d","--name",name,"--label","digitalheat.render-probe="+ownership,"--pull","never","--network","none","--user","65534",
                "--cpus",cfg.CpuCores.ToString("R",CultureInfo.InvariantCulture),"--memory",cfg.RamMib+"m",
                "--shm-size",ContainerProfile.DefaultSharedMemoryMib+"m","--cap-drop","ALL",
                "--security-opt","no-new-privileges:true"};
            if(cfg.Gpu is {} gpu){
                if(gpu.NvidiaDeviceId is {} nvidia)args.AddRange(["--gpus","device="+nvidia]);
                foreach(var d in gpu.LinuxDevices)args.AddRange(["--device",d]);
                foreach(var bind in gpu.ReadOnlyBinds)args.AddRange(["--volume",bind+":ro"]);
            }
            args.Add(cfg.Image);args.AddRange(command);
            Console.Error.WriteLine("Owned capability probe: "+name);
            try{
                string id=docker.Run(args).Trim();
                if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{64}$"))throw new IOException("Invalid probe container identity");
                var until=DateTimeOffset.UtcNow+RenderTimeout;
                while(docker.Run(["inspect","--format","{{.State.Running}}",id]).Trim()=="true"){
                    if(DateTimeOffset.UtcNow>=until)throw new TimeoutException("GPU capability probe exceeded 90 seconds");
                    Thread.Sleep(500);
                }
                string logs=docker.Run(["logs",id]);
                if(docker.Run(["inspect","--format","{{.State.ExitCode}}",id]).Trim()!="0")
                    throw new InvalidOperationException("Blender capability probe failed: "+logs);
                return logs;
            }finally{
                // Name and random ownership label exist BEFORE creation, including a lost
                // CLI acknowledgement. Never remove a caller-supplied/unlabelled container.
                string[] owned=docker.Run(["ps","-aq","--no-trunc","--filter","name=^/"+name+"$",
                    "--filter","label=digitalheat.render-probe="+ownership]).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                foreach(string id in owned){
                    if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{64}$"))throw new IOException("Invalid owned probe identity");
                    try{docker.Run(["stop","--timeout","2",id]);}
                    finally{docker.Run(["rm","--force","-v",id]);}
                }
            }
        }
        string raw=Execute("--probe");
        string report=raw.Split('\n').FirstOrDefault(l=>l.StartsWith("RENDER_DEVICES="))
            ??throw new InvalidDataException("No Blender device report");
        using var document=JsonDocument.Parse(report["RENDER_DEVICES=".Length..]);
        var devices=document.RootElement.Clone();
        string compiled=devices.TryGetProperty("compiled_backends",out var backends)
            ?string.Join(", ",backends.EnumerateArray().Select(x=>x.GetString())):"not reported by image";
        string diagnostic=$"GPU binding/backend not configured. Image Cycles backends: {compiled}.";
        string? verified=null;
        if(cfg.Gpu is not null && cfg.GpuBackend is {} backend){
            try{
                string output=Execute("--verify-gpu",backend);
                if(!output.Contains("RENDER_GPU_VERIFIED="+backend,StringComparison.Ordinal))
                    throw new InvalidOperationException("Probe did not confirm a completed GPU render");
                verified=backend;diagnostic=backend+" passed an actual Cycles render.";
            }catch(Exception error){diagnostic=$"{backend} verification failed: {error.Message}. Image Cycles backends: {compiled}.";}
        }
        return new(verified,diagnostic,devices);
    }
}
