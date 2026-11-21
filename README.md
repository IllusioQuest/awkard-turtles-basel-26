# Awkward Turtles · Basel GreenTech 2026

**Case: Useful heat from computing (DigitalHeat).**
Can computing power heat a home while doing useful work? We build a demonstrator that schedules
interruptible computing jobs on distributed servers so that their heat arrives where and when homes
need it, over a full year, with transparent and reproducible numbers.

> Status: C# .NET 8 core proposed for the reference-house draft; see
> [decision 0005](docs/decisions/0005-csharp-core.md). The team has not accepted
> the broader architecture yet.

## Start here

- [Case description (verbatim)](docs/case/challenge.md)
- [Key figures and must-have checklist](docs/case/key-figures.md)
- [External references and data sources](docs/case/references.md)
- [Deep-dive notes with the case owner](docs/deep-dive/notes-structured.md) ([raw](docs/deep-dive/notes-verbatim.md))
- [Data and how to fetch it](data/README.md) · [Assumptions](docs/assumptions.md) · [Decisions](docs/decisions/README.md)
- [Instructions for AI agents](AGENTS.md)

## Quick checks

```bash
python3 scripts/fetch_data.py --check   # validate the data against the case's expected values
scripts/deadline-reminder.sh            # time left until our deadlines
```

## C# reference-house core (proposed)

Requires the .NET 8 SDK. From the repository root:

```bash
dotnet restore src/DigitalHeat.sln
dotnet build src/DigitalHeat.sln --no-restore
dotnet test src/DigitalHeat.sln --no-build
dotnet run --project src/DigitalHeat.YearRun -- .
```

The last command reads the committed Basel 2025 hourly weather CSV, checks the
configured electricity price against the ElCom 2026 Basel H4 snapshot, and writes
`results/reference-house/hourly.csv` and `summary.json`. Pass another repository
root as the first argument when running from elsewhere. An optional second argument
selects a sourced finite job scenario; the default is
[`config/job-scenarios/unlimited.json`](config/job-scenarios/unlimited.json).
Results are generated and
git-ignored; rerunning the command reproduces them. The parameter file is
[`config/reference-house.json`](config/reference-house.json): each fixed/scenario
value has a unit and source. Four servers, zero minimum load and the example
computing price are scenario settings from the year explorer, not case mandates or
market-backed income. Change the scenario in JSON, keeping the reference-house
case values fixed for scored comparisons.

`DigitalHeat.Core` exposes `IScheduler.Decide(HeatSignal)`, `IExecutor` and
`SchedulerController.Apply`. `DigitalHeat.YearRun` supplies the simulated executor;
`DigitalHeat.JobRuntime` supplies a Docker executor behind that same interface.
The default output marks missed deadlines as `not modelled yet` because unlimited
work has no finite job count. A finite scenario reports arrivals, completed jobs,
backlog, processed work and the percentage of jobs due within the simulated year
that missed their deadline. See [`docs/model/job-queue.md`](docs/model/job-queue.md).
Sourced job arrivals and throughput from T-01/T-02 are still needed for scored
deadline figures.
Electricity, used-heat value and case break-even exclude hardware and operating
costs, so the result is not a full-cost profitability claim.

The calculation uses the local day's mean temperature to decide whether the case
heating formula applies, then adds 12 kWh/day of hot water evenly across 24 hours.
Each hour, the scheduler requests enough server heat to meet demand and fill remaining
tank space, limited by server count and minimum load. The balance records heat sent
directly to the house, charged to/discharged from the lossless tank, backed up and
dumped. Electricity cost is server kWh × the ElCom price; used-heat value is heat
actually used × that price ÷ 2.5; case break-even is their difference divided by
server-hours sold. For one full-load 1 kW server-hour whose 0.8 kWh recoverable heat
is all used, electricity costs CHF 0.3325, heat is worth CHF 0.1064, and the case
break-even is CHF 0.2261 per server-hour. These inputs come from the case and the
ElCom snapshot, with exact source labels in the JSON file.

## Live control of a containerized job

`DigitalHeat.LiveRun` attaches to an **already running Linux container**. It does not
start or implement a workload. Nikita's webhosting and Ennio's transcription work own
their containers, descriptors and measurements; the descriptor format is
[`config/jobs/job.schema.json`](config/jobs/job.schema.json). It records an ID,
image or container command, supported controls, work unit, optional deadline,
runtime and energy per unit (each number with unit and source), and an optional
host-visible progress file. Missing measurements are `null` with a visible
`TODO(source)` in the job's documentation. The job must be interruptible and support
freeze; throttle is optional. Checkpoint is descriptive until its owner provides
a protocol.

From the repository root, after the owner has started a dedicated container that
the replay may exclusively control:

```bash
dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-01-15 --job config/jobs/<owner-job>.json --container <container-id> --cpu-cores <allowed-cpu-cores> --step-seconds <positive-seconds>
dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-07-15 --job config/jobs/<owner-job>.json --container <container-id> --cpu-cores <allowed-cpu-cores> --step-seconds <positive-seconds> --force-pause-step 12
```

Press `P` during a terminal replay to force one full-tank/zero-demand signal. The
forced step option makes the pause repeatable without a keyboard. At the end,
LiveRun sets the declared CPU allowance and unpauses the container; it does not
restore an arbitrary prior container state. Use
Docker Desktop with Linux containers on Windows/macOS, or Docker Engine on Linux.
`DockerCliContainerControl` is the only class that calls Docker; Nikita's node
runtime may reuse it or implement `IContainerControl` with its own Docker client.
The replay uses the same `GreedyHeatScheduler` and `SchedulerController.Apply`
as YearRun, but each hour uses an independent **reference simulated tank** snapshot,
not a sensor reading. A forced pause does not modify later reference snapshots.
`docker stats` CPU use divided by the allowed CPU cores is shown as observed
CPU load; it does not establish electrical power or recovered heat. See
[`docs/decisions/0009-generic-job-control.md`](docs/decisions/0009-generic-job-control.md).

