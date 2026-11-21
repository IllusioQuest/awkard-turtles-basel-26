# Fleet simulation: implementation spec

> **Status:** agreed with Ennio 2026-11-21, not built. Ticket T-52, branch `feat/T-52-fleet-simulation`.
> This file is the decision record for T-52 (design reviewed by Ennio 2026-11-21; no separate ADR).
> Everything below is in scope for the first build. Values marked `TODO(source)` stay visible
> parameters; never replace them with invented numbers.

## 1. Goal

Simulate many households with DigitalHeat servers over a period (default: the year 2025). Compute
jobs arrive from customers. A pluggable **scheduler** (a mock for now) decides which job chunk runs on
which server, at which load, and when. A pluggable **pricing function** quotes Type 2 jobs. The
outputs are per house, per job and per network: heat demand, heat used/dumped/backup, power and
utilisation, missed deadlines, electricity cost, heat value, computing income and break-even. The
server count per house is a function, so it can be optimised later.

## 2. Code layout and reuse

- New projects in `src/`, added to `DigitalHeat.sln`: `DigitalHeat.Fleet` (library: engine, models,
  generators, mock scheduler, mock pricing), `DigitalHeat.FleetRun` (CLI), `DigitalHeat.Fleet.Tests`
  (xUnit, as the other test projects).
- Reuse from `DigitalHeat.Core`, do not copy: `HeatDemand.Build` (case formula),
  `HeatBalance.Step` (tank balance), `ReferenceParameters` / `config/reference-house.json`,
  `WeatherCsv`. No second heat model.
- Do not change `DigitalHeat.Network` (T-12, Jona) or `DigitalHeat.Orchestration`. The real scheduler
  is adapted to `IFleetScheduler` later.
- Config: `config/fleet/` (scenarios, job types, clients). Output:
  `data/derived/fleet/<run-id>/` (gitignored except small reference runs).
- Units in all names (`_kw`, `_kwh`, `_s`, `_chf_per_kwh`, ...).

## 3. Engine: event-driven, exact

- **Clock:** `long` milliseconds since run start. Hour index `h = t / 3_600_000` maps to row `h` of
  every hourly series (local time Europe/Zurich, 24 rows per local day, as the CSVs).
- **Event queue:** a binary heap ordered by `(time_ms, sequence)`. The sequence number is
  monotonically increasing, which makes ties deterministic. Stale events (e.g. a tank crossing
  computed before the load changed) are dropped lazily via a per-house/per-chunk version counter.
- **Invariant:** between two events every rate is constant (house demand kW, every server's load,
  every chunk's progress rate). For each house, the segment `[t0, t1)` is settled with
  `HeatBalance.Step(produced_kwh, demand_kwh, tank_kwh, cap_kwh)`. This is exact, because with
  constant rates the tank moves in one direction and is clamped at most once. Houses are settled
  lazily: only when one of their rates changes, or at hour start and at the end of the run.
- **Loop:** pop all events with the same `time_ms` → settle the touched houses → apply engine state
  changes (chunk finished, overhead done, ...) → call `scheduler.OnEvents(batch)` once → validate and
  apply the actions → compute new crossing events (tank full/empty/thresholds, chunk finish) for the
  touched houses and chunks → continue.
- **Tick mode** (`run.mode = "tick"`, `run.tick_ms`): the same engine, plus a `Tick` event every
  `tick_ms`. All non-tick events are delivered to the scheduler only at the next tick (buffered).
  Used for validation (§12) and small runs.
- **End of run:** settle all houses at `run.end`. Unfinished jobs are logged with status `open`.

## 4. Houses and servers

### 4.1 House spec

A house file (`.json` or `.csv`, one row per house) holds: `house_id, lat_deg, lon_deg, cell_id,
bfs_municipality_nr, weather_ref, design_heating_kw, formula_divisor_c, hot_water_kwh_per_day,
tank_kwh, tank_initial_kwh, electricity_price_chf_per_kwh, server_count`.
`server_count` may be empty, in which case the server-count policy decides (§4.4).

Hourly demand per house: `HeatDemand.Build` with the house's weather and the parameters above.
Houses that share `weather_ref` and parameters share one demand array (memory).

### 4.2 House generator (`FleetRun generate-houses <spec.json> -o <file>`)

| Spec key | Meaning | Default and source |
|---|---|---|
| `count` | N houses (adopters) | scenario value, labelled |
| `distribution` | `proportional_to_single_family`: multinomial draw over the grid cells of `data/derived/buildings/grid-10km-heat-demand.csv`, weight `n_single_family`; position = cell centre + uniform offset within ±5 km | T-51 |
| `cantons` | optional filter, e.g. `["BS","BL"]` | all |
| `weather` | cell weather `data/raw/swiss-grid/weather-2025-hourly.csv.gz` (`point_id` = cell) | T-51 |
| `design_heating_kw` | `cell_median`: the value at which the case formula on the cell's weather gives `single_family_heat_median_kwh_per_yr` space heat (linear in design kW, so one division). Alternatively `fixed: 10` | T-51 / case |
| `design_heating_spread` | relative SD of a lognormal factor per house | 0; `TODO(source)` if > 0 |
| `formula_divisor_c` | 25.6 | case (Basel constant, as `config/network/README.md`) |
| `hot_water_kwh_per_day` | 12 | case |
| `tank_kwh`, `tank_initial_kwh` | 35, 0 | case; initial 0 as Core |
| `electricity_price` | `per_municipality`: `data/derived/elcom/price-per-municipality-2026.csv`, `category` (default H4), joined on the cell's `bfs_municipality_nr`; or `fixed: 0.3325` | ElCom 2026 |
| `seed` | deterministic draw | 20261121 |

The generated file stores the spec and seed in its header (JSON) or in a sidecar `.spec.json` (CSV).
`config/fleet/houses/reference.json` is the case house, unchanged: Basel CSV
`data/raw/open-meteo/basel-2025-hourly.csv`, 10 kW, 25.6, 12 kWh/day, 35 kWh, CHF 0.3325.

### 4.3 Servers: one load value

All servers are GPU servers, modelled abstractly as in the case. No GPU, RAM, idle or boot details.

- Each server has `max_electric_kw` = 1.0 (case) and `heat_recovery_share` = 0.8 (case), both
  scenario parameters.
- A server's state is one number, `load ∈ [0, 1]`, set by the scheduler. Electric power
  `P = load × max_electric_kw`; heat `= heat_recovery_share × P`. `load = 0` draws nothing.
- A server runs **at most one chunk** at a time; the chunk's load is the server's load. A paused
  chunk stays on its server at load 0 (the server is blocked until it is resumed or stopped).
- **Work rate of a chunk:** `units_per_s = load × max_electric_kw ÷ (energy_kwh_per_work_unit × 3600)`,
  and 0 during overhead phases (which still draw `load × max_electric_kw`). Assumption (labelled):
  the energy per work unit does not depend on the load.

### 4.4 Server count per house

`IServerCountPolicy { int ServerCount(HouseSpec house, HouseDemandSummary demand); }`. The demand
summary holds the peak hour kW, annual kWh and mean kW. Built-in policies, selectable in config:

- `fixed: n`
- `share_of_peak: f` → `ceil(f × peak_heat_kw ÷ (max_electric_kw × heat_recovery_share))`
- `from_file`: the `server_count` column

Library entry point:
`FleetSimulation.Run(Scenario scenario, IServerCountPolicy? servers = null, IFleetScheduler? scheduler = null, IPricing? pricing = null)`,
so an optimiser can pass any function, e.g. `h => ...`, and run repeatedly without the CLI.

## 5. Jobs

### 5.1 Job type (`config/fleet/job-types/<id>.json`)

Every number is `{value, unit, source}`. A distribution is
`{dist: "fixed"|"uniform"|"lognormal"|"empirical"|"choice", ...params, unit, source}`.

| Field | Meaning |
|---|---|
| `id`, `class` | `type1` (fixed price, served first) or `type2` (dynamic price, filler) |
| `work_unit` | e.g. `audio_minute`, `token`, `simulation_task` |
| `energy_kwh_per_work_unit` | on a GPU server, with source; sets the work rate (§4.3) |
| `size` | distribution of work units per request |
| `batch_size` | distribution of requests per job (1 = single request) |
| `deadline_s` | distribution, relative to arrival; a few hours to a few days (team 2026-11-21) |
| `max_chunks` | maximum chunks in parallel (1 = not splittable) |
| `min_chunk_work_units` | smallest chunk (split granularity, e.g. one request or one simulation task) |
| `start_overhead_s` | per chunk start: power drawn at the set load, no progress (container start, model load) |
| `lifecycle` | `allow_pause`, `min_load` (lowest load other than 0 the job can run at; 1 = no throttling), `checkpointable` (stop keeps progress), `resume_overhead_s`, `checkpoint_overhead_s` |
| `input_mb_per_work_unit`, `state_mb` | data per chunk and checkpoint size. **Not used by the engine**; passed to the scheduler for locality and transfer decisions |
| `placement_policy` | `anywhere` / `origin_site` / `country_allowlist` (as `JobPlacementPolicy`, decision 0013) |
| `max_rtt_ms` | optional latency limit to origin, passed to the scheduler |
| `volume` | §6 |
| `arrival_shape` | hour-of-week CSV + column (mean normalised to 1) |
| `origin_mix` | `{client_site_share, clients_file}`; the rest is `anywhere` |
| `price` | `fixed` (CHF per work unit) or `dynamic` (§7) |

### 5.2 Initial job types (configs to create; values from the repo)

| | T1 `transcription-whisper-large-v3` | T1 `llm-inference-apertus-8b` | T2 `research-sim-montecarlo` |
|---|---|---|---|
| work unit | audio minute | token | simulation task |
| energy per unit (GPU) | 1 kWh ÷ (469.9 audio h × 60) = 3.55e-5 kWh per audio min (`config/hardware/gpu-inference-l4.json` `TranscriptionAudioHoursPerServerHour`, whisper-large-v3-turbo on L4, low confidence) | 0.21 J per token = 5.83e-8 kWh (RTX 4090, vLLM batched, `pe-rtx4090-vllm-iog-qps8`, conservative reading, `docs/research/profitability-inputs.md`) | `TODO(source)`: no GPU measurement; the CPU laptop value 4.09e-6 kWh per task (`config/jobs/research-sim-montecarlo.json`) is the labelled placeholder |
| size | lognormal, median 6.5 audio min (Bax 2011); spread `TODO(source)` | empirical tokens per request, `data/derived/demand/llm-token-quantiles.csv` (Azure `conv`) | empirical runtime, `job-durations.csv` (Alibaba 2020), converted to tasks at load 1 |
| batch size | distribution, configurable | distribution, configurable | 1 |
| deadline | uniform 2 h–3 d (team range) | uniform 2 h–3 d | uniform 2 h–3 d |
| splitting | per dictation (`min_chunk` = 1 request) | per request | per task, `max_chunks` configurable |
| overheads | start: model load `TODO(source)` (the T-02 runner reloads a 3 GB model per chunk) | start: model load `TODO(source)` | start `TODO(source)`; checkpoint per task (atomic file, `docs/jobs/research-sim.md`) |
| lifecycle | pause, `min_load` `TODO(source)` | pause, `min_load` `TODO(source)` | pause, `min_load` `TODO(source)`, checkpoint |
| price | CHF 0.36 per audio hour (Infomaniak / kvant Whisper V3, `compute-demand-traces.md` §3) | Public AI Apertus 8B CHF 0.084 in / 0.168 out per 1M tokens (same) | dynamic (§7) |

## 6. Demand generation (before the run, seeded)

- **Volume (absolute):**
  - Transcription: 428 jobs/day, Basel-Stadt physicians (`config/job-scenarios/transcription-basel.json`) × `volume_scale`.
  - LLM: an order-of-magnitude estimate, configurable: OpenRouter 194 × 10¹² tokens/week
    (`cd-t1-openrouter-week`) × `swiss_share` × `market_share` ÷ mean tokens per request.
    `swiss_share` and `market_share` are labelled scenario values.
  - Research: sciCORE ≈ 80,000 server-h/yr (`cd-d-scicore-sh-energy`) × `volume_scale`, turned into
    jobs/day with the mean job size.
- **Arrivals:** for each hour `h`, the count ~ Poisson(`jobs_per_day ÷ 24 × shape[hour_of_week(h)]`),
  with arrival times uniform within the hour. Shapes: LLM and transcription use Azure `conv`
  (`llm-azure2024-hour-of-week.csv`; for transcription a proxy, labelled); research uses Alibaba
  (`research-alibaba2020-hour-of-week.csv`).
- **Per job:** draw batch size, sizes, deadline and origin. For a `client_site` origin, draw a client
  from `config/fleet/clients/<file>.csv` (`client_id, lat_deg, lon_deg, weight`). The default file
  places clients at the T-51 canton capitals (`cap-*` points), weighted by their cells'
  single-family counts (a labelled proxy for population).
- One RNG stream per job type (seed = run seed + type index), so adding a type does not change the
  others' jobs.
- Write all jobs to `jobs-input.csv`. A run can read it back (`demand.from_file`) for exact reruns
  and A/B tests between schedulers.

## 7. Pricing

`IPricing { Quote Quote(Job job, IFleetView view); }` → `{price_chf_per_billed_unit, accepted}`.
It is called once when a job arrives. A rejected job is logged (`status = rejected_price`) and never
enters the queue. Pricing is pluggable and separate from the scheduler.

- **Type 1:** the fixed price from the job type; always accepted.
- **Type 2 billing unit:** server-hours delivered = `work_units × energy_kwh_per_work_unit ÷ 1 kW`.
  This is independent of load and overheads.
- **Mock Type 2 pricing** (`MockLoadPricing`, our first function; replaceable):
  - `u = min(1, committed_kwh ÷ capacity_kwh)` over the job's window `[arrival, deadline]`:
    - `capacity_kwh` = Σ over houses and forecast hours of
      `min(servers × max_electric_kw, forecast_heat_kw ÷ heat_recovery_share)`. It is kept as a
      network-wide hourly array with prefix sums, updated whenever a forecast is issued, so each
      quote costs O(1).
    - `committed_kwh` = remaining work of the queued and running jobs whose deadline lies in the
      window, plus this job.
  - `price = p_min + (p_max − p_min) × u^γ`:
    - `p_min` = 0.226 CHF per server-hour: the case break-even (electricity minus heat value).
    - `p_max` = 1.20 CHF per server-hour: the top of the CPU range, `docs/research/demand.md` §1.
    - `γ`: a scenario parameter.
  - Acceptance probability `= min(1, (price ÷ p_ref)^ε)` (constant-elasticity demand
    `Q = Q₀ × (p ÷ p_ref)^ε`, capped at the sourced volume `Q₀`), Bernoulli with the pricing RNG
    stream. `p_ref` is the competitor reference price, `ε` the price elasticity (§7.1).

### 7.1 Competitor price and elasticity (`docs/research/compute-price-elasticity.md`)

| Parameter | Default | Range | Source |
|---|---|---|---|
| `p_ref_chf` | 0.82 CHF per 1 kW server-hour | 0.46–1.80 | CSCS academic pay-per-use CHF 0.87 per node-hour (10k–50k node-h) ÷ 1.06 kW per node; low end CSCS subscription 0.49; high end cheapest Swiss spot (repo A2) |
| `p_ref_noise_sd` | 0.079 (relative, weekly) | 0–0.10 | Baughman et al. 2019: AWS spot price SD 7.9 % of mean after 2017; no daily/weekly profile |
| `elasticity` (ε) | −0.8 | −0.5 to −1.2 | Jin, Peng, Wang 2026 (0.63–0.94, VM groups); Brand et al. 2025 (≈ 0.5, spot); Jiang et al. 2016 (1.2, early EC2); all via Hull & Sattath 2026, secondary, low confidence |

The node-to-kW conversion uses the method in `config/computing-price-method.json` (256 vCPU at 3.74 W
plus an assumed 256 GB of RAM at 0.392 W per GB). The 256 GB is an assumption. Interruptible capacity
should be priced below CSCS (CSCS jobs are not interrupted).

## 8. Forecasts (given to the scheduler and to pricing)

`IForecastProvider.HeatDemandKw(house, issued_hour, target_hour)` returns the forecast space heat
and hot water demand, built from forecast temperature with the same case formula. The heating-day
rule uses the forecast daily mean.

- `archived` (**default**): T-51 `forecast-2025-hourly-lead0-3.csv.gz` of the house's cell.
  - Lead `N = clamp(ceil((target − issued) ÷ 24 h), 1, 3)`.
  - Forecast temperature = archive temperature + (`temperature_2m_previous_dayN` − `temperature_2m`),
    i.e. the forecast error is applied to the archive weather (`data/raw/swiss-grid/SOURCE.md` caveat).
  - Beyond 72 h: lead 3. For the Basel reference house: the nearest grid point.
- `perfect`: the actual demand.
- `persistence`: the same hour of the previous day.

A new forecast is issued every hour (`ForecastIssued` event, which is the same as `HourStart`).

## 9. Scheduler

### 9.1 Interface

```csharp
public interface IFleetScheduler
{
    string Id { get; }
    void Initialize(IFleetView view, IFleetActions act);
    void OnEvents(IFleetView view, IReadOnlyList<FleetEvent> events, IFleetActions act);
}

public interface IFleetActions
{
    ChunkId Start(JobId job, ServerId server, double workUnits, double load);  // server must be free
    void SetLoad(ServerId server, double load);   // 0 = pause (needs allow_pause), else >= min_load
    void Stop(ChunkId chunk);                     // frees the server; keeps progress only if checkpointable
    void RequestWakeup(long timeMs);
    void RegisterTankThreshold(HouseId house, double tankKwh);
}
```

- `IFleetView` (read-only): the current time; houses (demand kW now, tank kWh, servers with load and
  chunk); jobs (waiting, running and paused, with origin, deadline,
  remaining units, chunks and all type fields from §5.1); forecasts (§8); price quotes.
- **Events:** `HourStart`, `JobArrived` (accepted jobs only), `OverheadDone`, `ChunkFinished`,
  `JobCompleted`, `DeadlinePassed`, `TankFull`, `TankEmpty`, `TankThreshold`, `Wakeup`,
  `Tick` (tick mode).
- **Engine checks every action** (server free, lifecycle permission, `max_chunks`,
  `min_chunk_work_units`, placement policy, load range). An invalid action throws;
  the engine never silently fixes it.
- The engine does not model data transfer or latency; locality is entirely the scheduler's task.
  Moving work = `Stop` (checkpointable) + `Start` elsewhere, and both are counted.

### 9.2 Mock scheduler (`MockHeatLedScheduler`)

Parameters: `low_share` (0.3), `high_share` (0.9), `urgent_margin_s` (3600), `chunk_target_s` (3600).

1. **Heat target per house.** Mode `charge` while tank < `high_share × cap`: target electric kW = all
   servers at full power. Mode `hold` once tank ≥ high: target = `demand_kw ÷ heat_recovery_share`.
   Switch back to `charge` when tank ≤ `low_share × cap`. The thresholds are registered with
   `RegisterTankThreshold`.
2. **Fill to target**, queue order: Type 1 earliest deadline first, then Type 2 earliest deadline
   first.
   - Chunk size = `min(remaining, units for chunk_target_s at load 1)`, within `max_chunks` and
     `min_chunk_work_units`.
   - Loads are set so the house hits its target exactly: full servers at load 1, one server at the
     remainder (if ≥ its job's `min_load`, else 0).
   - Too much power: lower loads, then pause chunks in reverse queue order.
3. **Urgent:** a Type 1 job with slack (`deadline − now − remaining ÷ (active chunk rate)`) <
   `urgent_margin_s` is started on the first house (by id) with a free server, regardless of heat.
   If none is free, it stops a checkpointable Type 2 chunk there (its progress is kept) and uses that
   server.
4. **Cost per event is O(change):** keep the set of houses with power below target. `HourStart`
   re-evaluates all houses (N × 8,760 per year).
5. It ignores origin, latency, data size and forecasts.

## 10. Accounting (per house, integrated per segment)

- **Energy:** electric kWh split into overhead / Type 1 work / Type 2 work. Produced heat
  kWh = recovery × electric. Then, from `HeatBalance.Step`: direct used, tank charge/discharge,
  dumped, backup.
- `heat_used_kwh = demand − backup`. `heat_value_chf = heat_used_kwh × price ÷ 2.5` (case).
- `electricity_cost_chf = electric_kwh × electricity_price_chf_per_kwh`.
- `compute_income_chf`: Type 1 = units done × fixed price; Type 2 = billed server-hours of the units
  done × the job's quote. Credited to the house where the work ran.
- `utilisation = mean(load)` over the installed servers and time. `busy_share` = the share of
  server-time with a chunk at load > 0.
- **Case break-even** = `(electricity_cost − heat_value) ÷ (electric_kwh ÷ max_electric_kw)` CHF per
  server-hour.
- **Missed deadlines:** recorded, never netted into income. Per job: `deadline_missed`,
  `lateness_s`, units done after the deadline. Per house: units done for late jobs.
- **Post-run only** (`FleetRun post <run-dir> --hardware config/hardware/gpu-inference-l4.json`):
  `houses-fullcost.csv` with `hardware_chf_per_year` (purchase ÷ lifetime + ops),
  `net_after_hardware_chf`, `payback_years`. Never used during the run.

## 11. Output (`data/derived/fleet/<run-id>/`)

| File | Rows | Columns (minimum) |
|---|---|---|
| `run.json` | 1 | full resolved scenario, seeds, git commit, start/end, event count, wall time, versions of the house and job input files |
| `jobs-input.csv` | job | job_id, type, arrival_ms, deadline_ms, work_units, batch_size, origin_kind, origin_lat, origin_lon, client_id |
| `jobs.csv` | job | + status (`done`/`open`/`rejected_price`), quote_chf, accepted, first_start_ms, finish_ms, deadline_missed, lateness_s, chunks, pauses, stops, moves, houses_used, units_late, income_chf, energy_kwh |
| `houses-summary.csv` | house | house_id, cell_id, servers, space/hot-water/total demand kWh, electric kWh (overhead/T1/T2), produced kWh, used kWh, dumped kWh, backup kWh, coverage, final tank kWh, utilisation, busy_share, server-hours T1/T2, units for late jobs, pauses, electricity CHF, heat value CHF, income CHF T1/T2, margin CHF (income + heat value − electricity), case break-even |
| `network-hourly.csv` | hour | demand, produced, used, dumped, backup kWh; electric kW mean; running chunks; queue length and remaining kWh T1/T2; arrivals, rejections, completions, misses; mean T2 quote |
| `houses-timeseries.csv` | house × period | as summary, per period; `logging.house_timeseries` = `none`/`daily`/`hourly`/`events` |

## 12. Scenario file and CLI

```jsonc
// config/fleet/scenarios/<name>.json
{
  "run":       { "start_local": "2025-01-01T00:00", "end_local": "2026-01-01T00:00",
                 "mode": "event", "tick_ms": 1000, "seed": 20261121 },
  "houses":    { "file": "config/fleet/houses/ch-1000.csv" },        // or "generate": { §4.2 spec }
  "servers":   { "policy": "fixed", "n": 4 },                         // or share_of_peak / from_file
  "jobTypes":  ["config/fleet/job-types/transcription-whisper-large-v3.json",
                "config/fleet/job-types/llm-inference-apertus-8b.json",
                "config/fleet/job-types/research-sim-montecarlo.json"],
  "demand":    { "from_file": null },                                 // or a jobs-input.csv
  "pricing":   { "id": "mock-load", "p_min_chf": 0.226, "p_max_chf": 1.20, "gamma": 1,
                 "p_ref_chf": 0.82, "p_ref_noise_sd": 0.079, "elasticity": -0.8 },
  "forecast":  { "kind": "archived" },
  "scheduler": { "id": "mock-heat-led", "low_share": 0.3, "high_share": 0.9,
                 "urgent_margin_s": 3600, "chunk_target_s": 3600 },
  "logging":   { "house_timeseries": "hourly", "out_dir": "data/derived/fleet" }
}
```

CLI (`dotnet run --project src/DigitalHeat.FleetRun --`): `generate-houses <spec> -o <file>`,
`run <scenario>`, `post <run-dir> --hardware <file>`.

Scenarios to ship:
- `reference.json`: the case house, 4 servers, cross-check.
- `basel-region-100.json`: BS+BL, 100 houses, tick-vs-event test.
- `ch-1000.json`: Switzerland, 1,000 houses.

## 13. Tests (`DigitalHeat.Fleet.Tests`)

1. **Case values:** reference house has 206 heating days, space heat 26,850.5 kWh (Core), 12.5
   servers needed in the coldest hour, 19.2 kWh per server-day.
2. **Cross-check against Core:** `reference.json` with a Type 2 backlog that always fills, the mock
   scheduler, `min_load` = 0 (continuous load, as Core). Server-hours and heat used must agree with Core's
   22,661 h / 18,129 kWh within a tolerance stated in the test with its reason (hysteresis vs.
   Core's per-hour greedy).
3. **Event = tick:** 1 month, 20 houses, the same `jobs-input.csv`, event mode vs. tick mode at
   `tick_ms` = 1000. Per house and per job the energy and work agree within a bound of
   `tick_ms × number of events × max power`, which the test computes.
4. **Exactness of crossings:** a single house with constant rates. The tank-full time equals
   `(cap − T) ÷ (p − d)` to the millisecond; dumped kWh = analytic value.
5. **Conservation:**
   - per house: produced = direct + charge + dumped; demand = direct + discharge + backup;
     Δtank = charge − discharge.
   - per job: Σ chunk units = size.
   - per server: `0 ≤ load ≤ 1`, at most one chunk.
6. **Action validation:** every invalid action type throws.
7. **Determinism:** the same scenario gives byte-identical output files (except wall time in
   `run.json`).
8. **Performance (reported, not asserted):** wall time and events per second for `ch-1000.json` in
   `run.json`.

## 14. Data sources used

| Data | Use |
|---|---|
| `data/raw/open-meteo/basel-2025-hourly.csv` | reference house |
| `data/raw/swiss-grid/weather-2025-hourly.csv.gz`, `grid-10km.csv` | weather and position per cell |
| `data/raw/swiss-grid/forecast-2025-hourly-lead0-3.csv.gz` | archived forecasts (default) |
| `data/derived/buildings/grid-10km-heat-demand.csv` | house draw, design heat |
| `data/derived/elcom/price-per-municipality-2026.csv` | electricity price |
| `data/derived/demand/*hour-of-week.csv`, `job-durations.csv`, `llm-token-quantiles.csv` | arrival shapes, sizes |
| `data/derived/demand/gpu-price-history.csv` | optional `p_ref(t)` |
| `config/hardware/gpu-inference-l4.json`, `docs/research/profitability-inputs.md` | GPU energy per work unit; post-run costs |
| `config/jobs/research-sim-montecarlo.json` | placeholder energy per task (CPU) |
| `config/job-scenarios/transcription-basel.json`, `docs/research/demand.md`, `compute-demand-traces.md` | volumes, prices |

Not used: solar irradiance (no PV yet), T-12 synthetic sites.

## 15. Known simplifications (state them with every result)

- Servers are abstract 1 kW GPU servers with one load value; energy per work unit is from GPU
  benchmarks (research simulation: CPU placeholder) and independent of load.
- No outages, no network or bandwidth model (transfer is the scheduler's task).
- Hot water is flat; heat demand uses the case's linear formula; the tank is lossless.
- Arrivals are Poisson (real LLM traffic is burstier, BurstGPT CV 2.2 per minute).
- Job sizes are known when a job arrives.
- The mock scheduler and mock pricing are placeholders; the results describe them, not our real
  policy.
