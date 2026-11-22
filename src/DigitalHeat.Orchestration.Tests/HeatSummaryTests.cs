using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
namespace DigitalHeat.Orchestration.Tests;

public sealed class HeatSummaryTests(ITestOutputHelper output):IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"heat-summary-"+Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Hour=new(2026,11,22,10,0,0,TimeSpan.Zero);
    private WorkloadCoordinator Open(int count)
    {
        Directory.CreateDirectory(root);string fleet=Path.GetFileName(root);
        var rows=Enumerable.Range(0,count).Select(i=>new AttemptRecord(new(fleet,Guid.NewGuid().ToString("N"),
            PlacementTests.Request("frame-"+i),new("frame-"+i,"spa","b","host",null,.8,1),CoordinatorTests.Node("b"),CoordinatorTests.Profile),
            i%3==0?"running":i%3==1?"terminal":"released")
            {CommittedHeatIntervalsUtc=i%2==0?[Hour]:[Hour.AddHours(-1),Hour]}).ToArray();
        File.WriteAllText(Path.Combine(root,"journal.json"),JsonSerializer.Serialize(new CoordinatorState(fleet,rows){RetainHourlyHeat=true},WorkloadJson.Options));
        return new(fleet,root,new CoordinatorTests.Runtime(),retainHourlyHeat:true);
    }
    [Fact] public void SummaryPreservesAllHeatPhasesOrderAndDetachedIntervals()
    {
        using var c=Open(12);var full=c.Attempts();var compact=c.HeatCommitments();
        Assert.Equal(full.Length,compact.Length);
        for(int i=0;i<full.Length;i++){
            Assert.Equal(full[i].Attempt.Request.Id,compact[i].WorkloadId);
            Assert.Equal(full[i].Attempt.Reservation.RecoverableHeatKwhPerHour,compact[i].HeatKwhPerHour);
            Assert.Equal(full[i].CommittedHeatIntervalsUtc,compact[i].HourStartsUtc);
            Assert.Equal(full[i].Phase is "released" or "terminal",compact[i].Finished);
        }
        compact[0].HourStartsUtc[0]=Hour.AddDays(1);
        Assert.Equal(Hour,c.HeatCommitments()[0].HourStartsUtc[0]);
        foreach(var hour in new[]{Hour.AddHours(-1),Hour,Hour.AddHours(1)})
            Assert.Equal(full.Where(a=>a.CommittedHeatIntervalsUtc!.Contains(hour)).Sum(a=>a.Attempt.Reservation.RecoverableHeatKwhPerHour),c.CommittedHeatKwh(hour));
        Assert.False(c.Reconciled); // These reads do not reconcile or authorize new work.
    }
    [Theory][InlineData(100)][InlineData(500)]
    public void QueryAllocationBenchmark(int history)
    {
        using var c=Open(history);
        double Old()=>c.Attempts().Where(a=>a.CommittedHeatIntervalsUtc!.Contains(Hour)).Sum(a=>a.Attempt.Reservation.RecoverableHeatKwhPerHour);
        double Summary()=>c.HeatCommitments().Where(a=>a.HourStartsUtc.Contains(Hour)).Sum(a=>a.HeatKwhPerHour);
        (double Milliseconds,long Bytes,double Value) Measure(Func<double> query){
            for(int i=0;i<5;i++)query();
            long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();double value=0;
            for(int i=0;i<50;i++)value+=query();
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            return(Stopwatch.GetElapsedTime(start).TotalMilliseconds,bytes,value);
        }
        var old=Measure(Old);var summary=Measure(Summary);var scalar=Measure(()=>c.CommittedHeatKwh(Hour));
        Assert.Equal(old.Value,summary.Value,9);Assert.Equal(old.Value,scalar.Value,9);
        Assert.True(summary.Bytes<old.Bytes);Assert.True(scalar.Bytes<summary.Bytes);
        output.WriteLine(JsonSerializer.Serialize(new{history,queries=50,old_ms=old.Milliseconds,old_bytes=old.Bytes,
            summary_ms=summary.Milliseconds,summary_bytes=summary.Bytes,scalar_ms=scalar.Milliseconds,scalar_bytes=scalar.Bytes}));
    }
    public void Dispose(){if(Directory.Exists(root)){string path=Path.GetFullPath(root);Assert.StartsWith(Path.GetTempPath(),path);Directory.Delete(path,true);}}
}
