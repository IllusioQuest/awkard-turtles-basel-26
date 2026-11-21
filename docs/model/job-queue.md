# Finite job supply in the year simulation (T-05)

Status: implemented input boundary; no sourced finite demand scenario is committed yet. The
reference-house run remains an explicitly unlimited-work comparison, as allowed by the case.

## Input and handoff to T-01

`YearRun` reads `config/job-scenarios/unlimited.json` by default. A second command argument can
select a finite JSON scenario. Each `jobTypes` entry needs `id`, `workUnit`, `interruptible`, and
four `{value, unit, source}` quantities:

| Field | Unit | Meaning |
|---|---|---|
| `arrivalsPerDay` | `jobs/day` | Nonnegative integer jobs arriving at each local midnight |
| `workUnitsPerJob` | `work units/job` | Work contained in one job |
| `workUnitsPerServerHour` | `work units/server-hour` | Throughput on the case's abstract server; T-01/T-02 must supply a measurement and scaling method |
| `deadlineHours` | `hours` | Positive whole number of simulation steps from arrival |

The scenario also records `mode` (`finite` or `unlimited`), `excessHeatPolicy` (`wait` or
`run-and-dump`) and a `source` explaining the scenario choice. A finite run rejects missing or
`TODO(source)` quantities and noninterruptible job types. T-01 should map its catalog into this
input object or generate a scenario file; a measured value should live once in the catalog, not
be copied into two maintained JSON files. No job arrival rate or throughput was invented here.

## Calculation

At each local midnight, add the stated integer arrivals. Required work for one job is
`workUnitsPerJob / workUnitsPerServerHour` server-hours. Its exclusive deadline boundary is
`arrival step + deadlineHours / hoursPerStep`. Before each hour, unfinished jobs at that boundary
are marked missed exactly once. Work is assigned earliest deadline first. The existing scheduler
requests a heat-led load; `JobQueueExecutor` limits the **achieved** load to queued work and
debits only achieved server-hours. The unchanged heat balance uses this achieved load.

With `wait`, no heat demand and a full tank leave work queued. With `run-and-dump`, available
work may run at full capacity and surplus heat is recorded as dumped. Moving jobs between sites
belongs to T-10/T-12; neither policy is asserted to be the team's business choice.

`missedDeadlinePercent = 100 × missed / dueWithinHorizon`. The denominator counts jobs whose
deadline falls within the run, including jobs completed before that deadline. Jobs with later
deadlines remain completed or backlog counts but are excluded from this percentage. If no jobs
are due, the percentage is `null`. `summary.json` gives totals and per-type completed, missed,
backlog, processed work units and remaining work units. Work units are not summed across types.

The synthetic two-day regression test uses 10 jobs/day, 4 work units/job, 1 work unit/server-hour
and a 24-hour deadline. Day one supplies 40 server-hours of work; day two starts with the tank
almost full, then has insufficient heat-led runtime. Ten of the 20 due jobs miss their deadline,
so the fixture reports 50%. These values are **test inputs**, not a demand forecast or scored
reference-house figures.

## Reproduction

```bash
dotnet test src/DigitalHeat.sln
dotnet run --project src/DigitalHeat.YearRun -- .
dotnet run --project src/DigitalHeat.YearRun -- . <path-to-sourced-finite-scenario.json>
```

The default unlimited run must retain 58.0% coverage, zero dumped heat, 22,661 server-hours
and about CHF 0.2261/server-hour case break-even. Its missed-deadline percentage is undefined;
the submitted finite result waits for sourced T-01 job inputs.
