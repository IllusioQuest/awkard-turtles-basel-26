# T-31: Mixed job queue in the C# year simulation

**Status:** technical example, not the pitch business model. Decision 0020 defines public-document
LLM as Type 1 and research simulation as Type 2. No sourced finite public-document arrival rate
exists yet. The Basel medical-dictation counts in the example input exercise queue priority and
deadlines only; health records are never loaded or run on a home site. The scored unlimited run
remains the comparison baseline.

## Run it

From the repository root:

```bash
dotnet test src/DigitalHeat.sln
dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/mixed-basel-technical.json --output results/t31-mixed-basel
dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/mixed-basel-35pct-technical.json --output results/t31-mixed-basel-35pct
```

Open `results/t31-mixed-basel/jobs.json` for per-type server-hours, income and missed jobs;
`summary.json` and `sites/reference-house/hourly.csv` retain the case key figures. The 35% run
is a demand sensitivity, not a forecast. Choose new empty output folders if rerunning. Each
bundle's `run.json` records the command, Git state and input hashes.

## Input and method

`mode: mixed` adds one `filler` descriptor to the existing finite `jobTypes`. Each type has a
`priceChfPerServerHour` with value, unit and source. The filler has a sourced
`soldCapacityShare` (fraction of server capacity sold each hour). Its price is the team's pinned
CHF 0.66/server-hour; 1.0 sold share is the case-permitted all-work-sold comparison, while 0.35
is the central marketplace sensitivity in `docs/research/filler-demand.md`. The finite job
arrival and throughput values come unchanged from `transcription-basel.json` and its cited
research and T-02 laptop measurement. No new arrival number was estimated.

For each hour, with `C = server_count × hours_per_step` server-hours, `t` the target load per
server from the **existing** heat scheduler, `F` available finite work in server-hours and `s`
the sold filler share:

```text
offered_server_hours = min(t × C, F + s × C)
actual_load_per_server = achieved_server_hours / C
Type 1 gets achieved hours first, earliest deadline first
Type 2 gets max(0, achieved hours − Type 1 hours), at most s × C
income_chf = Σ(type_server_hours × type_price_chf_per_server_hour)
```

The executor still calls the same `IExecutor.ApplyTargetLoad` path as other runs. The hourly
heat, tank and case break-even equations are unchanged. Only achieved, sold hours consume
electricity and produce heat; the backup heater covers the shortfall. Type 2 can never use the
`run-and-dump` policy. A missed Type 1 deadline has no penalty in the current economic formula;
the value of partially processed jobs that later miss a deadline remains an open contract issue.

**Worked synthetic hour:** four servers, one hour, heat target 100%, one server-hour of finite
work, filler sold share 0.35. The offered work is `min(4, 1 + 0.35 × 4) = 2.4` server-hours.
Type 1 gets 1.0 and Type 2 gets 1.4, so actual load is 60% per server. At the case's 0.8 kWh
recoverable heat per server-hour, they produce 1.92 kWh. Against the test's 3.2 kWh heat demand,
backup heat is 1.28 kWh and dumped heat is zero. This hour is a **test fixture**, not a Basel
measurement.

## Observed full-year results on the technical fixture

Inputs: fixed Basel 2025 reference house and weather (`config/reference-house.json`,
`data/raw/open-meteo/basel-2025-hourly.csv`), IWB H4 2026 electricity snapshot, 428 technical
dictation arrivals/day from `docs/research/job-demand.md`, and the two stated filler shares.
The annual quantities below are produced by the commands above, not independent estimates.

| Sold filler share | Type 1 hours | Type 2 hours | Total hours | Heat coverage | Dumped heat | Computing income |
|---:|---:|---:|---:|---:|---:|---:|
| 100% | 38.79 | 22,622.24 | 22,661.03 | 58.05% | 0 kWh | CHF 14,956.28 |
| 35% | 38.79 | 9,720.56 | 9,759.35 | 25.00% | 0 kWh | CHF 6,441.17 |

The technical Type 1 fixture completed 156,220 jobs with zero missed deadlines in both runs.
At 100% filler share, the physical result equals the original unlimited run: 58.0% rounded
coverage, zero dumped heat, 22,661 server-hours and CHF 0.226/server-hour case break-even. The
mixed income is priced from the scenario's CHF 0.66 for both job types; the old unlimited
reference config still contains its illustrative CHF 0.30 and is not silently changed here.
Per-type income in the 100% run is CHF 25.60 (technical Type 1) plus CHF 14,930.68 (filler).

The `jobs.json` file is an additive report. The existing versioned `summary.json` schema remains
v1, so current UI consumers can keep reading the scored reference-house fields. A future
sourced LLM-demand scenario can replace the technical finite input without changing the queue.
