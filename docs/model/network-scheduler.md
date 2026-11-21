# Network scheduler engine (T-12, decision 0024)

**Status:** step 1 done (2026-11-21). Engine, baselines B0/B1, rolling-horizon LP and full-year upper
bound run on the reference house. Steps 2 (jobs with deadlines) and 3 (Swiss multi-site dataset, map
export) are next.

## Projects

| Project | What | Dependencies |
|---|---|---|
| `src/DigitalHeat.Core` | `HeatBalance.Step` (the one heat balance), `WeatherCsv`, heat demand, scheduler interfaces | none |
| `src/DigitalHeat.Network` | `NetworkSite`, `NetworkState`, `NetworkDecision`, `INetworkScheduler`, `NetworkSimulation`, forecasts (perfect foresight, AR(1) error), B0 always on, B1 local greedy | Core only |
| `src/DigitalHeat.Network.Lp` | `HeatLedLp` (lexicographic LP over a window), `RollingHorizonLpScheduler`, `FullYearUpperBound` | Network, Google.OrTools 9.15.6755 (GLOP) |
| `src/DigitalHeat.NetworkRun` | strategy comparison on the reference house | Network.Lp |
| `src/DigitalHeat.Network.Tests` | 7 tests (see below) | Network.Lp |

Each hour, `NetworkSimulation` builds every site's signal and forecast and asks the scheduler once. It
then actuates every site through the unchanged `SchedulerController.Apply` → `IExecutor` path. The
adapter `CommittedDecisionScheduler` throws if the signal differs from the one the decision was made
on. Finally `HeatBalance.Step` settles the hour on **actual** demand.

## The LP (heat-led mode)

Per site and window hour (index 0 = current hour, 1..H = forecast):
`a·u = direct + charge + dump`, `direct + discharge + backup = D`,
`S_t = S_(t−1) + charge − discharge`, `0 ≤ S ≤ capacity`, `μN ≤ u ≤ N`.
Solved lexicographically in three stages:

1. Minimise dump. If the optimum is zero, dump is fixed to exactly 0.
2. Maximise used heat (direct + discharge).
3. Maximise server-hours.

Each later stage keeps the earlier optimum within 10⁻⁹ (relative) + 10⁻⁹ kWh. The rolling horizon
uses H = 48 h and commits only the current hour (decision 0014 §6.2).

Forecast error (decision 0024 §7): `e_k = ρ·e_(k−1) + σ_k·ξ_k`, with `σ_k = σ24·√(k/24)` and
standard-normal ξ, seeded per site and hour. **σ24 and ρ are `TODO(source)`**, so they are shown as a
sweep (ρ = 0.8 is a placeholder too).

## Result on the reference house

`dotnet run --project src/DigitalHeat.NetworkRun -- .` (4 servers, 35 kWh tank, Basel 2025):

| Strategy | Information | Coverage | Dumped | Server-hours | Utilisation |
|---|---|---:|---:|---:|---:|
| B0 always on | current hour | 58.05 % | **9,903 kWh** | 35,040 | 100 % |
| B1 local greedy (the scored scheduler) | current hour | 58.05 % | 0 | 22,661.0 | 64.67 % |
| LP rolling horizon | perfect foresight (bound) | 58.05 % | 0 | 22,661.0 | 64.67 % |
| LP rolling horizon | forecast error σ24 = 5 % | 58.03 % | 0 | 22,654.7 | 64.65 % |
| LP rolling horizon | forecast error σ24 = 10 % | 57.99 % | 0 | 22,639.1 | 64.61 % |
| LP rolling horizon | forecast error σ24 = 20 % | 57.82 % | 0 | 22,572.9 | 64.42 % |
| UB full-year LP | perfect foresight, whole year (bound only) | 58.05 % | 0 | 22,661.0 | 64.67 % |

**How to read it:**

- **B1 reaches the upper bound.** On the reference house (minimum load 0, lossless tank, unlimited
  work) the greedy rule is optimal, as decision 0024 argues, and this is now a regression test.
- **B0 has the same coverage but dumps 9,903 kWh.** Four servers are the limit in cold hours whatever
  the strategy; always-on only adds dumped heat in mild hours.
- **Planning ahead on a wrong forecast costs a little.** The LP plans the current hour for the
  forecast world, so its plan can be slightly worse in reality. The mechanism still needs a closer
  look. More intelligence pays off only with deadlines and several sites (steps 2–3), not on one
  house.

Runtime: about 45 s per rolling-horizon year (8,760 solves of three stages), 2 s for the full-year LP.

## Step 2: work model and network strategies (decision 0025)

- **Type 1** cohorts: the LLM batch jobs of one origin site and day (arrive 00:00, deadline 24 h,
  1,090 documents per 1 kW server-hour from T-33). Volume is a **labelled scenario**: a share of the
  origin's yearly heat-led server-hours (no sourced arrival rate exists, decision 0020).
- **Type 2** filler: research simulation, no deadline, available φ × servers per hour (φ = 1 is the
  case-permitted "unlimited jobs"; φ = 0.35 from `docs/research/demand.md`).
- One ledger attributes the achieved load: Type 1 earliest deadline first, filler last. Moves are
  simulated flows only (no data transfer delay or cost yet, `TODO(source)`).
- Strategies (all heat-led hard: no dumping):
  - **B1-jobs:** local greedy load, own Type 1 first, then filler, no moves.
  - **B2:** local greedy load; Type 1 that its origin cannot finish before the deadline (persistence
    estimate) moves to the eligible site with the most free heat-led capacity.
  - **B1-reserve / B2-reserve** (decision 0029, control): as B1/B2, but filler keeps tank room free
    for the next Type 1 cohort (persistence only).
  - **LP:** rolling horizon over sites × hours with cohort variables, lexicographic stages: dump ↓,
    used heat ↑, missed Type 1 ↓, server-hours ↑, moved server-hours ↓. Knows arrived work only;
    plans on the heat forecast (perfect foresight unless `--sigma24` is given).

## Step 3: validation and experiments on the Swiss test network

**Data:** `config/network/switzerland-test.json`: 33 sites in 9 Swiss cities (reference house, single-
and multi-family houses, 9 hotels with spa, 5 indoor pools). Locations and sizes are **synthetic test
data**; heat demand comes from the case formula (houses) or sourced profiles (see the scenario file).
All results below are **scenarios**, not forecasts of a real network.

**Reproduce:** `python3 scripts/network_experiments.py` (greedy, ≈ 1 min) and
`python3 scripts/network_experiments.py --lp --jobs 4` (adds full-year LP runs). Outputs:
`data/derived/network/experiments/*.csv` (one row per run, with the exact command).

### A. Type 1 volume (φ = 1)

| Type 1 share | B1 missed | B2 missed | B2 moved server-h | B2-reserve moved | LP missed / moved |
|---:|---:|---:|---:|---:|---|
| 10 % | 0 % | 0 % | 7,725 | 7,725 | pending |
| 30 % | 15.69 % | 0 % | 102,754 | 64,230 | pending |
| 50 % | 25.19 % | 0 % | 205,267 | 161,968 | pending |
| 70 % | 29.47 % | 0 % | 309,325 | 265,260 | pending |

Coverage is 83.42 % and dumped heat 0 kWh in every run (B2-reserve 83.40 %): Type 1 volume does not
change how much heat is produced, only which work makes it.

### B. Filler availability φ (Type 1 share 30 %)

| φ | B1 coverage | B2 coverage | B1 missed | B2 missed |
|---:|---:|---:|---:|---:|
| 1.0 | 83.42 % | 83.42 % | 15.69 % | 0 % |
| 0.6 | 54.21 % | 54.71 % | 15.54 % | 0 % |
| 0.35 | 34.58 % | 35.38 % | 14.93 % | 0 % |
| 0.2 | 21.41 % | 22.34 % | 14.82 % | 0 % |

### D. Network mix (Type 1 share 30 %, φ = 1)

| Network | Sites | B1 missed | B2 missed | B2 moved server-h | Coverage |
|---|---:|---:|---:|---:|---:|
| all sites | 33 | 15.69 % | **0 %** | 102,754 | 83.42 % |
| residential only (no hotels, no pools) | 19 | 15.69 % | **13.80 %** | 63,333 | 63.18 % |

### E. Transfer delay (decision 0030, assumption sweep; Type 1 share 30 %, φ = 1)

"Data staged at arrival": moved work may run elsewhere only from arrival + d (a lower bound on friction).

| d | B2 missed | B2 moved server-h | moved per avoided job | B2-reserve moved | B2-reserve per avoided job |
|---:|---:|---:|---:|---:|---:|
| 0 h | 0 % | 102,754 | 1.57 | 64,230 | 0.98 |
| 1 h | 0 % | 102,771 | 1.57 | 64,230 | 0.98 |
| 4 h | 0 % | 76,680 | 1.17 | 60,527 | 0.93 |
| 12 h | 0 % | 61,727 | 0.95 | 58,775 | 0.90 |

"Avoided jobs" = B1's missed jobs minus the strategy's missed jobs (65,352 at every d). Even 12 h of
delay costs no deadline here, because the year-round sinks have enough free summer capacity to finish
the excess in the remaining 12 h. B2 even moves less with a delay: without it, its persistence test
sends work away that would still have finished at home. With d ≥ 24 h (the deadline) no work can move
and missed deadlines return to B1's level. LP for d = 4 and 12 h: pending.

### C. Forecast error (LP, Type 1 share 30 %)

Pending (full-year LP runs with σ24 = 0.05 / 0.10 / 0.20, ρ = 0.8, seed 20251121; both are
`TODO(source)` and shown as a sweep).

### How to read it

- **Coverage is set by heat, not by scheduling.** All strategies are heat-led and dump nothing, so
  coverage only changes with how much filler work exists (B: 83 % at φ = 1, 21 % at φ = 0.2). Missing
  paid work, not the algorithm, limits the heat a network delivers.
- **The network is what prevents missed deadlines.** In summer a house's heat-led budget is only its
  hot water (≈ 0.6 servers per day in the case). Type 1 work above that cannot be done at home.
  B1 misses 15–29 % of jobs; B2 moves the excess and misses none, as long as year-round sinks exist.
- **Year-round sinks are the mechanism.** Without hotels and pools (D), B2 still moves 63,000
  server-hours but misses 13.8 %: in summer every house is short at the same time, so there is
  nowhere to send the work. Hotels, spas and pools take it.
- **Timing at home is not a substitute** (decision 0029). B1-reserve keeps tank room for the next
  cohort and saves only 0.07 points of missed deadlines, because a daily shortfall returns the next
  day. Combined with moves (B2-reserve) it saves 37 % of the moved server-hours.
- **B2 helps coverage a little when filler is scarce** (B: +0.5 to +0.9 points), because moved Type 1
  work runs where heat is needed and filler would otherwise be missing.

### Validation notes

- B1 and B2 reproduce the values of step 2 exactly (83.42 %, 0 kWh, 15.69 % / 0 %, 102,754 h).
- LP runtime on 33 sites: about 0.22 s per hour in January and 0.38 s per hour in July (Debug build,
  one core), i.e. roughly 30–60 min per year. No window shortening or clustering was needed.
- A 72-hour July window (`--start 4704 --hours 72`) showed LP 0 % missed without moves; this was an
  artefact of the empty starting tank and does not hold over a year (decision 0029).

### Limits

- Synthetic site locations and sizes; Type 1 volume is a share, not a sourced demand.
- Transfer delay is a uniform assumption sweep counted from arrival (decision 0030), no distance model;
  transfer cost is not modelled (moved server-hours are reported; CHF needs a sourced data volume, T-14).
- Persistence estimates (B2, reserves) assume daily cohorts.
- The LP knows arrived work only; perfect heat foresight unless a forecast error is set.

## Tests

`dotnet test src/DigitalHeat.sln`. Network tests:

- B1 with one site reproduces `YearSimulation` hour by hour.
- UB equals B1 on the reference house.
- The adapter rejects a different signal.
- The heat-balance order (demand, then tank, then dump).
- The LP pre-charges the tank before demand.
- The LP never dumps when it can stay idle.
- AR(1) forecasts are deterministic per seed.
- B1-reserve keeps one cohort's tank room in a summer case and equals B1 in a winter case (decision 0029).

The scored outputs of `YearRun` stay bit-identical to `main` after the refactor (SHA-256 checked).
