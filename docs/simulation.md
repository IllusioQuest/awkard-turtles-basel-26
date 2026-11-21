# Fleet simulation: data, inputs, parameters

What the fleet simulation (`src/DigitalHeat.Fleet`) reads, where it comes from, how it becomes
scheduler input and what can be tuned. Design and algorithms: [`model/fleet-simulation.md`](model/fleet-simulation.md).

## 1. Data used

| Data | Source (link) | Period / granularity | Repo file | Used for |
|---|---|---|---|---|
| Weather, reference house | Open-Meteo Historical Weather API, https://open-meteo.com/en/docs/historical-weather-api; 47.56 N 7.59 E | 2025-01-01 – 2025-12-31, hourly, Europe/Zurich (24 values per local day) | `data/raw/open-meteo/basel-2025-hourly.csv` | heat demand of `reference` |
| Weather, Swiss grid | same API, 409 points on a 10 km LV95 grid + 26 canton capitals (`cap-bs` = case point) | 2025, hourly, Europe/Zurich | `data/raw/swiss-grid/weather-2025-hourly.csv.gz`, `grid-10km.csv` | heat demand per generated house |
| Grid points, municipality | geo.admin.ch identify / search API, https://api3.geo.admin.ch/rest/services/api/MapServer/identify | current boundaries (retrieved 2026-11-21) | `data/raw/swiss-grid/grid-10km.csv` | cell → BFS municipality, canton |
| Temperature forecasts | Open-Meteo Previous Runs API, https://open-meteo.com/en/docs/previous-runs-api | 2025, hourly, lead 1/2/3 days | `data/raw/swiss-grid/forecast-2025-hourly-lead0-3.csv.gz` | forecasts for scheduler and pricing |
| Electricity price, reference | ElCom, https://www.strompreis.elcom.admin.ch; IWB Basel, H4, total | tariff year 2026, one flat price | `data/raw/elcom/iwb-basel-h4-2026.json` | 0.3325 CHF/kWh |
| Electricity price, all municipalities | same API, categories H1–H8 | tariff year 2026, per municipality | `data/raw/elcom/municipalities-2026/*.json` | price per generated house |
| Buildings | BFS building register GWR, https://www.housing-stat.ch/ (extract https://public.madd.bfs.admin.ch/ch.zip) | export 2026-11-20, per building | not committed (`data/external/`) | house locations, single-family counts |
| Residential heat demand | BFE heat demand map, https://www.bfe.admin.ch/waerme-kaeltenachfrage-wohnen-und-dl | base data 2014 (layer 2022), per hectare, MWh/a | not committed (`data/external/`) | design heating power per cell |
| LLM request trace | Azure LLM inference trace 2024, https://github.com/Azure/AzurePublicDataset/blob/master/AzureLLMInferenceDataset2024.md | 2024-05-12 – 05-18 (one week), per request | not committed | arrival shape (LLM, transcription proxy), tokens per request |
| GPU cluster trace | Alibaba PAI 2020, https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020 | July–Aug 2020 (9.9 weeks), per task | not committed | arrival shape and job sizes (research) |
| Physicians, Basel-Stadt | Grosser Rat BS, https://grosserrat.bs.ch/dokumente/100406/000000406995.pdf | Jan 2024 | `config/fleet/job-types/transcription-whisper-large-v3.json` | transcription volume |
| Dictation length and volume | Bax et al. 2011, https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3402968/ | study value | same | 6.5 min per dictation, 66.71 min per physician-month |
| LLM token volume | OpenRouter, via https://fourweekmba.com/ai-openrouters-run-rate-tops-10-quadrillion-tokens-up-6-7x/ | week ending 2026-10-26 | `data/raw/market/compute-demand.csv` (`cd-t1-openrouter-week`) | LLM volume |
| Research compute volume | sciCORE Basel, `docs/research/demand.md` | per year | `data/raw/market/compute-demand.csv` (`cd-d-scicore-sh-energy`) | research volume |
| Energy per work unit | L4 Whisper benchmark (`config/hardware/gpu-inference-l4.json`); L4 LLM throughput 25 M tokens/server-h (`gpu-inference-l4.json` `LlmTokensPerServerHour`, `docs/research/l4-throughput.md`); own laptop measurement 2026-11-21 (`config/jobs/research-sim-montecarlo.json`, CPU placeholder) | per unit | job-type files | work rate of a server |
| Type 1 prices | Infomaniak / kvant Whisper V3; Public AI Apertus 8B (`docs/research/compute-demand-traces.md` §3) | 2026 list prices | job-type files | income |
| Type 2 reference price, elasticity | CSCS, Baughman 2019, Jin 2026 et al. (`docs/research/compute-price-elasticity.md`) | — | scenario `pricing` | Type 2 acceptance |
| Case constants | `docs/case/challenge.md` | — | `config/reference-house.json` | 1 kW, 0.8, 10 kW, 25.6, 12 °C, 12 kWh/day, 35 kWh, ÷ 2.5 |

Solar irradiance is downloaded but not used.

## 2. From data to scheduler input

1. **Houses.** `reference`: the case house, hand-written (`config/fleet/houses/reference.json`).
   Generated fleets (`generate-houses`): grid cells drawn multinomially, weighted by GWR single-family
   count per cell; position = cell centre ± 5 km. Per house:
   - `design_heating_kw` (`cell_median`) = 10 kW × (median modelled single-family heat demand of the cell,
     BFE split onto GWR buildings by floor area) ÷ (case-formula space heat of a 10 kW house on the cell's weather).
   - `electricity_price_chf_per_kwh` = ElCom 2026 H4 total of the cell's municipality (coverage-weighted mean over operators).
   - `formula_divisor_c` 25.6, hot water 12 kWh/day, tank 35 kWh (start 0): case values.
2. **Heat demand.** Core `HeatDemand.Build` (the same code as the year simulation): space heat
   `max(0, design_kW × (20 − T) ÷ 25.6)` on days with mean T < 12 °C, plus 12 ÷ 24 kWh hot water per hour.
   Hourly values, held constant within the hour.
3. **Servers.** `servers.policy` sets the count; each server is one load in [0, 1]:
   electricity = load × 1 kW, recoverable heat = 0.8 × electricity.
4. **Jobs** (generated before the run, seeded, written to `jobs-input.csv`):
   - Jobs/day: transcription 428 (1,269 physicians × 66.71 min/month × 12/365 ÷ 6.5 min);
     LLM 194 × 10¹² tokens/week × Swiss share × market share ÷ mean tokens per request ÷ 1,000 requests
     per job (≈ 1,566); research 80,000 server-h/yr ÷ mean job size (≈ 135). Each × `volume_scale`.
   - Per hour: count ~ Poisson(jobs/day ÷ 24 × hour-of-week shape), times uniform within the hour.
   - Per job: size (transcription 6.5 min fixed; LLM empirical Azure tokens; research Alibaba duration
     at 1 kW), deadline uniform 2 h – 3 d, origin (transcription: a canton capital, weighted by
     single-family count, must stay in CH).
5. **Pricing on arrival.** Type 1: fixed price, always accepted. Type 2: price = p_min + (p_max − p_min) × u^γ,
   u = committed work ÷ forecast heat-led capacity in the job's window; accepted with probability
   min(1, (price ÷ p_ref)^ε), p_ref with weekly noise.
6. **Forecasts.** Every hour, per house: forecast temperature = archive temperature + (lead-N forecast − lead-0),
   N = 1–3 days → same demand formula.
7. **What the scheduler sees** (`IFleetView`): time; per house current demand kW, tank kWh, servers with
   load and chunk; forecast demand; queued and running jobs with size, deadline, origin, lifecycle
   rules. It reacts to events (hour start, arrival, chunk end, tank thresholds, deadlines).
8. **Heat balance** per segment: Core `HeatBalance.Step` (direct → tank → dump; deficit from tank, then
   backup; details in §5). Heat value = used heat × house price ÷ 2.5.

## 3. Tunable parameters and defaults

Scenario file (`config/fleet/scenarios/*.json`); defaults = code defaults (`Contracts/Scenario.cs`):

| Key | Default | Meaning |
|---|---|---|
| `run.start_local` / `end_local` | 2025-01-01T00:00 / 2026-01-01T00:00 | window, Europe/Zurich |
| `run.mode`, `tick_ms` | `event`, 1000 | exact event engine, or fixed ticks |
| `run.seed` | 20261121 | all random draws |
| `houses.file` / `houses.generate` | — (required) | house list or generator spec |
| `servers.policy` | `fixed` (`n`: 4 in all shipped scenarios) | or `share_of_peak` (`f`), `from_file` |
| `servers.hardware_profile` | `config/hardware/gpu-inference-l4.json` in all shipped scenarios | source of the GPU job types' energy per unit (checked by a test) and of capex/ops in `post` |
| `servers.max_electric_kw`, `heat_recovery_share` | 1.0, 0.8 (case) | per server |
| `demand.volume_scale` | 1 per type (`reference`: 0.1) | multiplies job volume |
| `demand.from_file` | null | replay a `jobs-input.csv` |
| `pricing.p_min_chf` | 0.226 CHF/server-h | case break-even |
| `pricing.p_max_chf` | 1.20 | |
| `pricing.gamma` | 1 | price curve shape |
| `pricing.p_ref_chf`, `p_ref_noise_sd` | 0.82, 0.079 | competitor price, weekly relative noise |
| `pricing.elasticity` | −0.8 | |
| `forecast.kind` | `archived` | or `perfect`, `persistence` |
| `scheduler.id` | `mesh-heat-first` | the only scheduler (`HeatFirstFleetScheduler`); no tank hysteresis, no urgent override (the old `low_share`, `high_share`, `urgent_margin_s` keys were removed in commit b1b7803). Sites are ranked by the free share of their hourly heat budget, then free kWh, then ID (`HeatShareRank`, decision 0056); a site that can run a server at full load goes before one with only a partial budget |
| `scheduler.chunk_target_s` | 3600 | chunk size at load 1 |
| `logging.house_timeseries` | `hourly` | `none` / `daily` / `hourly` / `events` |

House generator spec (`config/fleet/houses/*.spec.json`): `count` (100), `cantons` (all; `basel-region-100`: BS, BL; only BL cells exist, so all its houses are in BL),
`distribution` `proportional_to_single_family`, `design_heating_kw` `cell_median` (or `fixed: 10`),
`design_heating_spread` 0, `electricity_price` `per_municipality` H4 (or `fixed`), `seed` 20261121.

Job types (`config/fleet/job-types/*.json`):

| | transcription (T1) | LLM (T1) | research (T2) |
|---|---|---|---|
| energy per unit | 3.55e-5 kWh/audio min | 4.0e-8 kWh/token | 4.09e-6 kWh/task (CPU placeholder) |
| price | CHF 0.006/audio min | CHF 8.9e-8/token | dynamic |
| batch, `max_chunks` | 1, 1 | 1,000 requests, 8 | 1, 4 |
| volume knobs | 428 jobs/day | Swiss share 0.01, market share 0.01 | 80,000 server-h/yr |
| deadline | uniform 2 h – 3 d | same | same |
| placement | CH only, origin canton capitals | anywhere | anywhere |
| pause / checkpoint / `min_load` / overheads | yes / yes / 0 / 0 s (`TODO(source)`) | same | same |

## 4. Run

```bash
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/reference   # full cost, same hardware profile
```

`margin_chf` in `houses-summary.csv` is income + heat value − electricity, **without hardware**; the
net result after capex and ops is `net_after_hardware_chf` in `houses-fullcost.csv` (from `post`).

Output in `data/derived/fleet/<scenario>/` (`run.json` records the resolved scenario, seeds and commit).

## 5. Heat storage (tank): when heat goes in and out

The case house has one water tank, about 35 kWh usable, no losses (`docs/case/challenge.md`,
"Heat storage"; `config/reference-house.json` `TankCapacityKwh`). All three simulation paths settle
the tank with the same function, Core `HeatBalance.Step` (`src/DigitalHeat.Core/HeatBalance.cs:15-25`).
For one period with produced server heat P, demand D, tank content S and capacity C (all kWh):

1. **Direct use first:** direct = min(P, D).
2. **Surplus into the tank:** charge = min(P − direct, C − S).
3. **Tank full → dump:** dump = P − direct − charge. The balance never throttles servers; that is the scheduler's job.
4. **Deficit from the tank:** discharge = min(S, D − direct).
5. **Tank empty → backup:** backup = D − direct − discharge.
6. S′ = S + charge − discharge, 0 ≤ S′ ≤ C. No standing losses, no minimum level; charge and discharge are never both above 0.

**What the scheduler sees.** The hourly heat budget is `HeatCapacity.RemainingKwh` = D + (C − S)
− heat already committed this hour (`src/DigitalHeat.Core/HeatCapacity.cs:42-44`). `HeatFirstScheduler`
sets total load = clamp(budget ÷ 0.8 kWh per server-hour, `MinimumLoadShare` × N, N)
(`src/DigitalHeat.Core/Scheduling.cs:53-57`). Consequences:

- Free tank space lets servers **run ahead of demand**: whenever servers can produce more than D, they fill the tank.
- With `MinimumLoadShare` 0 (`config/reference-house.json`), P ≤ D + (C − S), so heat is **never dumped**.
  Dump needs a forced minimum load (Core, Network) or work that cannot pause (Fleet, see below).
- Stored heat does **not** lower the load: servers only slow down once the tank is full. So the tank
  discharges only in hours where all servers (or all available work) produce less than D.

| | Core year (`YearSimulation`) | Network (`NetworkSimulation`) | Fleet (event engine) |
|---|---|---|---|
| Tank state | `tankKwh` (`src/DigitalHeat.Core/YearSimulation.cs:32`) | `tanks[i]` per site (`src/DigitalHeat.Network/NetworkSimulation.cs:40`) | `HouseState.Tank` (`src/DigitalHeat.Fleet/Engine/EngineState.cs:35`) |
| Capacity | `TankCapacityKwh` 35 | `tankCapacityKwh` per site (`config/network/reference-only.json`: 35) | `tank_kwh` per house (`config/fleet/houses/reference.json`: 35) |
| Start | 0, in code | 0, in code | `tank_initial_kwh` (0) |
| Budget | each hour, tank at hour start (`YearSimulation.cs:44-46`) | each hour per site; jobs go to the site with most remaining heat (`HeatFirstNetworkScheduler.cs:19,36`) | once per hour at `HourStart` (`src/DigitalHeat.Fleet/Policies/HeatFirstFleetScheduler.cs:52-59`); claims are not released within the hour |
| Settlement | one step per hour (`YearSimulation.cs:49`) | one step per site and hour, on actual demand (`NetworkSimulation.cs:66`) | one step per constant-rate segment, never across an hour (`src/DigitalHeat.Fleet/Engine/FleetEngine.cs:448-528`) |
| Tank fills | no reaction within the hour; the plan already fits the free space | same as Core | `TankFull` event at the exact crossing (`FleetEngine.cs:574-609`); `Refresh` throttles pausable chunks to D ÷ 0.8 (`HeatFirstFleetScheduler.cs:105-130`); non-pausable or checkpointing chunks keep running and their surplus is dumped |
| Tank empties | backup for the rest of the hour | same | `TankEmpty` event; backup from that moment; the budget is not raised |
| Outputs | hourly `tank_*`, `dumped_heat_kwh`, `backup_heat_kwh`; no final-tank field in the summary (last hourly row) | summary `FinalTankKwh` (`NetworkSimulation.cs:95`) | `tank_final_kwh` (`houses-summary.csv`), `tank_end_kwh` per row |
| Heat used | D − backup, incl. discharge (`YearSimulation.cs:58-60`) | same (`NetworkSimulation.cs:89-90`) | same (`src/DigitalHeat.Fleet/Contracts/Results.cs:60`) |

The paths agree on the rule and give the same totals when the load is constant within an hour. Fleet
differs only when the load changes within an hour (the tank can hit full or empty part-way). The
unlimited-work year run (`YearRun`) uses `SingleSiteUnlimitedNetworkScheduler` → `HeatFirstNetworkScheduler`
→ `HeatFirstScheduler`, so Core and Network compute the same reference loads. `LiveRun` takes the
tank from the previous hour of the year run and forces a pause with D = 0 and a full tank, i.e.
budget 0 (`src/DigitalHeat.LiveRun/Program.cs:48-56`). `HeatLedLp` has the same tank constraints but
is only used in tests.

**Worked example** (Core, reference house). 4 servers (`ServerCount`, a comparison scenario, not a
case value) × 1 kW × 0.8 = 3.2 kWh per hour at full load. D = 10 × (20 − T) ÷ 25.6 on heating days
(daily mean < 12 °C) + 12 ÷ 24 = 0.5 kWh hot water (`config/reference-house.json`, case values).
T from `data/raw/open-meteo/basel-2025-hourly.csv`; all other values from
`dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/unlimited.json --output <dir>`
(`sites/reference-house/hourly.csv`, commit 6737bd5).

| Hour (2025) | T °C | D | Tank before | Budget D + (C − S) | Load/server | P | Direct | Charge | Discharge | Backup | Tank after |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 10-02 04:00, cold (day mean 8.8 °C) | 5.0 | 5.859 + 0.5 = 6.359 | 3.452 | 37.908 | 1 (capped at N) | 3.2 | 3.2 | 0 | 3.159 | 0 | 0.292 |
| 10-02 05:00, cold | 4.7 | 6.477 | 0.292 | 41.18 | 1 | 3.2 | 3.2 | 0 | 0.292 | 2.984 | 0 |
| 04-04 08:00, tank fills (day mean 12.6 °C) | — | 0.5 | 33.106 | 0.5 + 1.894 = 2.394 | 2.394 ÷ 3.2 = 0.748 | 2.394 | 0.5 | 1.894 | 0 | 0 | 35 |
| 07-15 12:00, summer | 24.2 | 0.5 | 35 | 0.5 | 0.5 ÷ 3.2 = 0.156 | 0.5 | 0.5 | 0 | 0 | 0 | 35 |

Dump is 0 in all rows. In Fleet, the 05:00 hour would fire `TankEmpty` after 0.292 ÷ (6.477 − 3.2)
= 0.089 h (5.4 min) and then draw backup at 3.277 kW for 0.911 h = 2.984 kWh, the same total.
Over the year (same run) the tank charges and discharges 665 kWh each (3.7 % of 18,129 kWh used
server heat), is full in 3,680 of 8,760 hours (it stays full from spring to autumn, so summer
servers run only at hot-water demand), dumps 0 kWh and ends the year empty.

**Open points** (not fixed; code unchanged):

- The start tank of 0 is hard-coded in Core (`YearSimulation.cs:32`) and Network
  (`NetworkSimulation.cs:40`), not taken from `config/`; Fleet reads `tank_initial_kwh`. Same value today.
- Heat left in the tank at the end of a run counts as neither used nor dumped, but its electricity is
  paid. 0 kWh in the reference year; a run that ends with a full tank (e.g. a summer window) has
  produced = used + dumped + final tank. The Core summary does not report the final tank.
- Fleet pricing capacity (`src/DigitalHeat.Fleet/Policies/Pricing.cs:30-31`) uses forecast demand only,
  not free tank space, while the scheduler budget includes it; Type 2 utilisation `u` is therefore
  slightly overstated against what the scheduler can run.
- `HeatFirstFleetScheduler` registers a tank threshold at capacity (`HeatFirstFleetScheduler.cs:26`);
  the engine skips thresholds at capacity (`FleetEngine.cs:590`) because `TankFull` covers them. Harmless.
- `docs/model/fleet-simulation.md` §9.2 still describes the removed tank-hysteresis mock scheduler.
