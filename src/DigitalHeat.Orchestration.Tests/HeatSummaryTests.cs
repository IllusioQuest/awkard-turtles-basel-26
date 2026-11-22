using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
namespace DigitalHeat.Orchestration.Tests;

public sealed class HeatSummaryTests(ITestOutputHelper output):IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"heat-summary-"+Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Hour=new(2026,11,22,10,0,0,TimeSpan.Zero);
    private WorkloadCoordinator Open(int count,int? activeCount=null,bool allPhases=false)
    {
        Directory.CreateDirectory(root);string fleet=Path.GetFileName(root);
        var rows=Enumerable.Range(0,count).Select(i=>new AttemptRecord(new(fleet,Guid.NewGuid().ToString("N"),
            PlacementTests.Request("frame-"+i,"batch"),new("frame-"+i,"spa","b","host",null,.8,1),CoordinatorTests.Node("b"),CoordinatorTests.Profile),
            allPhases?new[]{"reserved","creating","created","starting","running","paused","pausing","resuming","stopping","terminal","removing","released"}[i%12]:activeCount is {} active?(i<active?"running":"released"):i%3==0?"running":i%3==1?"terminal":"released")
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
    [Fact] public void ActiveSnapshotKeepsUncertainPhasesAndDoesNotEraseRetainedHeat()
    {
        using var c=Open(12,allPhases:true);var full=c.Attempts();var active=c.ActiveAttempts();
        Assert.Equal(JsonSerializer.Serialize(full.Where(r=>r.Phase!="released")),JsonSerializer.Serialize(active));
        active[0].Attempt.Profile.Command[0]="changed";active[0].CommittedHeatIntervalsUtc![0]=Hour.AddDays(1);
        Assert.Equal("sleep",c.ActiveAttempts()[0].Attempt.Profile.Command[0]);
        Assert.Equal(full.Sum(r=>r.Attempt.Reservation.RecoverableHeatKwhPerHour),c.CommittedHeatKwh(Hour));
        Assert.Equal(12,c.Attempts().Length);Assert.False(c.Reconciled);
    }
    [Theory][InlineData(100)][InlineData(500)]
    public void ReleasedHistoryBookkeepingAllocationBenchmark(int history)
    {
        using var c=Open(history,activeCount:0);c.Reconcile([]);
        (long Bytes,double Ms) Measure(Action query){
            for(int i=0;i<5;i++)query();long bytes=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            for(int i=0;i<50;i++)query();
            return(GC.GetAllocatedBytesForCurrentThread()-bytes,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        var oldActive=Measure(()=>{_ = c.Attempts().Where(r=>r.Phase!="released").ToArray();});
        var active=Measure(()=>{_ = c.ActiveAttempts();});
        var oldDeadline=Measure(()=>{_ = c.EnforceDeadlines();});
        var deadline=Measure(c.EnforceDeadlinePolicies);
        Assert.True(active.Bytes<oldActive.Bytes);Assert.True(deadline.Bytes<oldDeadline.Bytes);
        Assert.Equal(history,c.Attempts().Length);Assert.Equal(history*.8,c.CommittedHeatKwh(Hour),8);
        output.WriteLine(JsonSerializer.Serialize(new{history,queries=50,old_active_bytes=oldActive.Bytes,active_bytes=active.Bytes,
            old_active_ms=oldActive.Ms,active_ms=active.Ms,old_deadline_bytes=oldDeadline.Bytes,deadline_bytes=deadline.Bytes,
            old_deadline_ms=oldDeadline.Ms,deadline_ms=deadline.Ms}));
    }
    public void Dispose(){if(Directory.Exists(root)){string path=Path.GetFullPath(root);Assert.StartsWith(Path.GetTempPath(),path);Directory.Delete(path,true);}}
}
