# Fleet simulation outputs

Written by `FleetRun run <scenario.json>` to `data/derived/fleet/<scenario name>/` (or `--out <dir>`, `--run-id <id>`).
Run outputs are git-ignored; only this README is committed. Spec: [`docs/model/fleet-simulation.md`](../../../docs/model/fleet-simulation.md).

## Commands

```bash
dotnet run -c Release --project src/DigitalHeat.FleetRun -- generate-houses config/fleet/houses/ch-1000.spec.json -o config/fleet/houses/ch-1000.csv
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/reference --hardware config/hardware/gpu-inference-l4.json
```

Scenarios: `config/fleet/scenarios/{reference,basel-region-100,ch-1000}.json`. The 10 km grid has no cell inside
Basel-Stadt, so the "Basel region" houses are all in Basel-Landschaft. `reference.json` scales every job type to 0.1
of its sourced volume (one house cannot absorb the whole regional volume; at 1.0 the open queue grows without bound).

## Files

Same scenario, same inputs: byte-identical files, except the timing fields, `git_commit` and the `phase_wall_time` block of `run.json`.
Doubles use the round-trip "R" format, invariant culture, `\n` line endings. All energies kWh, money CHF, times ms (`_ms`) or s (`_s`) since run start.

| File | Rows | Content |
|---|---|---|
| `run.json` | 1 | resolved scenario, run seed and stream seeds (per job type, pricing), git commit, start/end local, run hours, events, scheduler calls, wall time, events per second, SHA-256 of house file, job-type files and `jobs-input.csv`, resolved jobs/day with derivation per job type, known simplifications |
| `jobs-input.csv` | job | job_id, type, type_index, arrival_ms, deadline_ms, work_units, batch_size, origin_kind, origin_lat, origin_lon, client_id, origin_canton. Can be fed back with `demand.from_file` |
| `jobs.csv` | job | the input columns + status (`done`/`open`/`rejected_price`), quote_chf (per billed unit), accepted, first_start_ms, finish_ms, deadline_missed, lateness_s, chunks, pauses, stops, moves, houses_used, units_done, units_late, income_chf, energy_kwh |
| `houses-summary.csv` | house | house_id, cell_id, servers, heating_days, space/hot-water/total demand, electric overhead/T1/T2/total, heat produced/direct/tank charge/tank discharge/used/dumped/backup, units_late, late_work_kwh, pauses, income_t1/t2_chf, coverage (used ÷ demand), tank_final_kwh, utilisation (mean load), busy_share, server_hours_t1/t2 (electric work kWh ÷ max kW), electricity_cost_chf, heat_value_chf (used × price ÷ 2.5), income_chf, margin_chf (income + heat value − electricity), break_even_chf_per_server_hour ((electricity − heat value) ÷ server-hours) |
| `network-hourly.csv` | run hour | demand, produced, direct, charge, discharge, used, dumped, backup kWh; electric_kw_mean; running_chunks; queue length and remaining kWh T1/T2; arrivals, rejections, completions, misses; mean_t2_quote_chf |
| `houses-timeseries.csv` | house × period | house_id, period_start_ms, period_end_ms, the account columns of `houses-summary.csv`, tank_end_kwh. Absent when `logging.house_timeseries` is `none` |
| `houses-fullcost.csv` | house | only from `post`: servers, margin_chf, annual_margin_chf, hardware_chf_per_year, net_after_hardware_chf, payback_years |

`houses-fullcost.csv` (never used during the run): `hardware_chf_per_year = servers × (purchase ÷ lifetime + ops per server-year)` from the
hardware profile; the margin is annualised as `margin × 8760 ÷ run hours`; `net_after_hardware_chf = annual margin − hardware_chf_per_year`;
`payback_years = servers × purchase ÷ (annual margin − servers × ops)` when positive, else empty. The first line is a comment stating this.

## Caveat (known simplifications, §15 of the spec; repeated in `run.json`)

- Servers are abstract 1 kW GPU servers with one load value; energy per work unit is from GPU benchmarks (research simulation: CPU placeholder) and independent of load.
- No outages, no network or bandwidth model (transfer is the scheduler's task).
- Hot water is flat; heat demand uses the case's linear formula; the tank is lossless.
- Arrivals are Poisson (real LLM traffic is burstier, BurstGPT CV 2.2 per minute).
- Job sizes are known when a job arrives.
- The mock scheduler and mock pricing are placeholders; the results describe them, not our real policy.

`units_late` sums work units of mixed job types (tokens, audio minutes, tasks) and is only meaningful per job.
