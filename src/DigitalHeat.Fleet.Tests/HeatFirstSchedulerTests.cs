using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class HeatFirstSchedulerTests
{
    private static FakeFleet Create(params (double Tank, double Demand)[] houses)
    {
        var fleet = new FakeFleet();
        foreach (var h in houses) fleet.AddHouse(1, cap: 1, tank: h.Tank, demandKw: h.Demand);
        var scheduler = new HeatFirstFleetScheduler(new() { ChunkTargetS = 3600 });
        fleet.Scheduler = scheduler; scheduler.Initialize(fleet, fleet); fleet.HourStart();
        return fleet;
    }
    private static JobType Batch() => FakeFleet.MakeType("batch", JobClass.Type2, maxChunks: 1);

    [Fact] public void HigherUsefulHeatWinsInsteadOfFirstHouse()
    {
        var f = Create((1, .2), (0, 2));
        f.Arrive(f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour));
        Assert.Equal(1, Assert.Single(f.Chunks).House.Value);
    }
    [Fact] public void HeatTieUsesStableHouseId()
    {
        var f = Create((0, 1), (0, 1));
        f.Arrive(f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour));
        Assert.Equal(0, Assert.Single(f.Chunks).House.Value);
    }
    [Fact] public void NoHeatNeverStartsEvenAnUrgentJob()
    {
        var f = Create((1, 0));
        f.Arrive(f.AddJob(FakeFleet.MakeType("urgent", JobClass.Type1), 10, 0, 1));
        Assert.Empty(f.Chunks);
    }
    [Fact] public void PendingJobsUseDeadlineBeforeType()
    {
        var f = Create((0, 1));
        var early = f.AddJob(Batch(), 10, 0, 1000);
        var later = f.AddJob(FakeFleet.MakeType("later", JobClass.Type1), 10, 0, 2000);
        f.Send(new FleetEvent(FleetEventKind.JobArrived, 0, Job: later), new FleetEvent(FleetEventKind.JobArrived, 0, Job: early));
        Assert.Equal(early, Assert.Single(f.Chunks).Job);
    }
    [Fact] public void HeatLossPausesBatchAndReturnResumesIt()
    {
        var f = Create((0, 1));
        f.Arrive(f.AddJob(Batch(), 100, 0, 100000));
        f.HouseList[0].TankKwh = 1; f.HouseList[0].DemandKwNow = 0;
        f.Send(new FleetEvent(FleetEventKind.TankFull, 0, House: new(0)));
        Assert.Equal(0, f.Chunks[0].Load);
        f.HouseList[0].DemandKwNow = 1;
        f.Send(new FleetEvent(FleetEventKind.TankEmpty, 0, House: new(0)));
        Assert.Equal(1, f.Chunks[0].Load);
        Assert.Single(f.Chunks);
    }
    [Fact] public void NoninterruptibleWorkContinuesAfterHeatLoss()
    {
        var f = Create((0, 1));
        f.Arrive(f.AddJob(FakeFleet.MakeType("interactive", JobClass.Type1, allowPause: false), 100, 0, 100000));
        f.HouseList[0].TankKwh = 1; f.HouseList[0].DemandKwNow = 0;
        f.Send(new FleetEvent(FleetEventKind.TankFull, 0, House: new(0)));
        Assert.Equal(1, f.Chunks[0].Load);
    }
    [Fact] public void RepeatedCompletionEventsDoNotReopenHourlyHeat()
    {
        var f = Create((1, .8));
        var first = f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour);
        f.Arrive(first);
        f.Chunks[0].Phase = ChunkPhase.Stopped; f.ServerList[0].Chunk = null; f.ServerList[0].Load = 0;
        f.Jobs[first.Value].Chunks.Clear(); f.Jobs[first.Value].Status = JobStatus.Done;
        var second = f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour);
        f.Arrive(second);
        f.Send(new FleetEvent(FleetEventKind.ChunkFinished, 0, House: new(0), Chunk: new(0)));
        Assert.Single(f.Chunks);
    }
    [Fact] public void CheckpointOverheadContinuesAndOccupiesItsServer()
    {
        var f = Create((0, 1));
        f.Arrive(f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour));
        f.Chunks[0].Phase = ChunkPhase.Checkpointing;
        f.HouseList[0].TankKwh = 1; f.HouseList[0].DemandKwNow = 0;
        f.Arrive(f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour));
        f.Send(new FleetEvent(FleetEventKind.TankFull, 0, House: new(0)));
        Assert.Equal(1, f.Chunks[0].Load);
        Assert.Single(f.Chunks);
    }
    [Fact] public void TickWithoutChangesDoesNotScanWaitingJobs()
    {
        var f = Create((1, 0));
        for (int i = 0; i < 100; i++) f.Arrive(f.AddJob(Batch(), 10, 0, FleetTime.MsPerHour));
        int before = f.JobLookups;
        f.Send(new FleetEvent(FleetEventKind.Tick, 1));
        Assert.Equal(before, f.JobLookups);
    }
}

