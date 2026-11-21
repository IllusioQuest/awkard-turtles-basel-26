internal sealed record AssemblyManifest(string ManifestSha256);
internal sealed record AssemblySubmit(string JobId,string ManifestSha256);
internal static class RenderGpuLayout
{
    internal static void Validate(RenderConfig cfg,RenderPeerStatus[] peers)
    {
        var gpu=peers.Where(p=>p.PhysicalGpuId is not null).ToArray();
        foreach(var group in gpu.GroupBy(p=>p.PhysicalGpuId)){
            if(group.Select(p=>p.PhysicalHostId).Distinct().Count()!=1)throw new InvalidDataException("GPU identity advertised on different physical hosts");
        }
        foreach(var host in gpu.GroupBy(p=>p.PhysicalHostId)){
            if(host.Count()==1)continue;
            if(cfg.SharedGpuLimit<=1||cfg.SharedGpuGroup is null||host.Count()>cfg.SharedGpuLimit||
               host.Select(p=>p.PhysicalGpuId).Distinct().Count()!=1||host.Any(p=>!p.FrameBoundaryPause||p.SharedGpuGroup!=cfg.SharedGpuGroup||p.SharedGpuLimit!=cfg.SharedGpuLimit))
                throw new InvalidDataException("Shared GPU aliases require matching explicit local group, host, device and concurrency limit");
        }
    }
}
