namespace DigitalHeat.Orchestration;

/// <summary>Operator-selected frame mix, not a utilization prediction or placement policy.
/// Heat-first placement still selects the eligible target for each CPU/GPU request.</summary>
public static class RenderDeviceAllocation
{
    public static int GpuFrames(int frames,double gpuShare,bool gpuAvailable)
    {
        if(frames<1||!double.IsFinite(gpuShare)||gpuShare<0||gpuShare>1)throw new ArgumentOutOfRangeException(nameof(gpuShare));
        return gpuAvailable?(int)Math.Round(frames*gpuShare,MidpointRounding.AwayFromZero):0;
    }
    public static string Backend(int frame,int frames,double gpuShare,string? gpuBackend)
    {
        if(frame<1||frame>frames)throw new ArgumentOutOfRangeException(nameof(frame));
        int cpuFrames=frames-GpuFrames(frames,gpuShare,gpuBackend is not null);
        // Evenly spread exactly the rounded CPU count rather than sending all CPU frames last.
        return (long)frame*cpuFrames/frames>(long)(frame-1)*cpuFrames/frames?"CPU":gpuBackend!;
    }
}
