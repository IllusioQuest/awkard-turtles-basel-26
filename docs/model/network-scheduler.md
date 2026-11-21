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

## Tests

`dotnet test src/DigitalHeat.sln`. Network tests:

- B1 with one site reproduces `YearSimulation` hour by hour.
- UB equals B1 on the reference house.
- The adapter rejects a different signal.
- The heat-balance order (demand, then tank, then dump).
- The LP pre-charges the tank before demand.
- The LP never dumps when it can stay idle.
- AR(1) forecasts are deterministic per seed.

The scored outputs of `YearRun` stay bit-identical to `main` after the refactor (SHA-256 checked).
