# 0024: Network scheduler engine: interface, project and LP solver (T-12)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona (IllusioQuest) / Claude; team confirmation pending. Implements the model of
  decision 0014 (T-11); this is the "own decision record in T-12" that 0014 §7 asks for.

## Context

The team wants a scheduler that runs a network of sites with realistic forecasts. The goal is a live
map of Switzerland: heat maps per site and work flowing between sites, produced by our scheduler on
a simulated test dataset. Today's code schedules one site with the hour-by-hour greedy rule
(`GreedyHeatScheduler`, `YearSimulation`).

Two facts shape the design:

- **On the reference house the greedy is already optimal, under stated conditions:** minimum load
  0 (`config/reference-house.json`), a lossless tank without charge or discharge rate limits
  (`HeatBalance`), unlimited work. Greedy keeps the tank full and never produces more than demand
  plus free tank space. With no dump, produced heat = used heat + final tank content, and greedy
  maximises both. So no scheduler covers more demand, dumps less or runs more server-hours. The
  claim fails with a minimum load above 0, finite jobs with deadlines, or tank losses. A smarter
  scheduler pays off only there and with several sites (decision 0014 §8.1).
- **Perfect foresight is not a result.** Plans use forecasts with errors (`HeatForecast`,
  decision 0008). A perfect-foresight run is labelled and used only as an upper bound.

## Decision

1. **Two new projects.** `src/DigitalHeat.Network` (references Core only, **no third-party
   dependency**) holds the interfaces, the network simulation and the solver-free strategies (B0, B1,
   B2). `src/DigitalHeat.Network.Lp` (references Network and OR-Tools) holds the LP strategies. Core,
   YearRun and LiveRun stay free of OR-Tools. LiveRun can use the network engine with B1/B2 without
   native solver binaries.
2. **One heat model (done).** The hourly heat balance is the pure Core function
   `HeatBalance.Step`, used by `YearSimulation` and the network simulation. After the refactor the
   scored outputs are bit-identical to `main` (SHA-256 of `hourly.csv` and `summary.json`
   compared on 2026-11-21; 40 tests pass).
3. **Interface.** `INetworkScheduler.Decide(NetworkState) → NetworkDecision`.
   - `NetworkState`: the hour, one `SiteHeatState` per site (signal plus forecast, decision 0008),
     and the site parameters (location, servers, tank, prices, heat value). Pending work comes in
     step 2.
   - `NetworkDecision`: one `SchedulerDecision` per site. Work allocation per cohort and site, and
     moves between sites, come in step 2.
   - **Actuation is unchanged.** Each site's decision goes through `SchedulerController.Apply` →
     `IExecutor`, via an adapter `IScheduler` that returns the committed decision. **The adapter
     checks that the `HeatSignal` it receives equals the signal the decision was made on and throws
     otherwise.** So the same path drives simulated and real Docker executors (AGENTS.md §5.3). The
     live demo uses whatever strategy LiveRun runs: B1 today, the network engine once LiveRun can run
     it with one site (step 1b). Moving work between sites needs an executor extension; it gets its
     own record in step 2.
4. **Strategies on the same interface:** B0 always on; B1 local greedy (wraps
   `GreedyHeatScheduler`; with one site it must reproduce `YearSimulation` exactly, a regression
   test); B2 network greedy (step 2/3); **LP**, a rolling horizon (`H` = 48 h, commit `R` = 1 h,
   decision 0014 §6.2); **UB**, a full-year LP with perfect foresight, upper bound only, labelled. Regression test: on
   the reference house, **UB equals B1 within solver tolerance**, and the forecast LP can only match
   or lose to B1 there.
5. **Solver: Google OR-Tools, GLOP LP solver**, NuGet `Google.OrTools` pinned to 9.15.6755
   (Apache-2.0). Restore and a test solve worked on the team laptop on 2026-11-21 (NuGet reachable,
   unlike PyPI). LP only; MILP extensions stay as listed in 0014 §7.
6. **Objective mode is a parameter.** The default is **heat-led hard**: no dumping (constraint),
   then **lexicographic in two stages**. Stage 1 maximises used heat. Stage 2 fixes used heat at
   least at the stage-1 optimum minus a tolerance, then maximises server-hours. No weights are
   needed. The economic mode (CHF terms of 0014 §5.1) is
   available; that it would choose "always on and dump" at CHF 0.66 is stated openly (0014 §5.4).
   The team still has to confirm the pitch mode (0014 §11.1).
7. **Forecast error model:** relative error on each forecast hour, `e_k = ρ·e_{k−1} + σ_k·ξ_k`
   with ξ standard normal from a seeded generator (seed in the scenario). σ grows with the lead time
   `k`, and ρ correlates neighbouring hours, so the errors are not i.i.d. (an AR(1) process). A fresh
   draw is made for every planning hour. Values of σ and ρ: `TODO(source)`. The candidate source is
   the Open-Meteo historical-forecast archive compared with the actual 2025 weather for Basel. Until
   then we show a labelled sweep (σ at 24 h = 0 %, 5 %, 10 %, 20 %).
8. **Outputs** per site-hour plus flows between sites, designed for the map. The export contract
   extends decision 0011 (T-13) in step 3.

Steps: (1) engine, B0/B1/LP/UB on the reference house; (1b) LiveRun can run the network engine
with one site; (2) jobs with deadlines (Type 1) and filler
(Type 2, decision 0020), missed deadlines; (3) Swiss multi-site test dataset, B2, export for the
live map.

## Alternatives considered

- **Extend `IScheduler`/`YearSimulation` in place:** this mixes one-site and network logic, and the
  scored path would change with every network feature.
- **Own LP/heuristic code without a solver:** this avoids a dependency, but gives no optimality
  reference and costs more time than it saves.
- **Other solvers** (HiGHS via a wrapper, lp_solve): less mature in .NET. OR-Tools is the one named
  in `docs/model/routing-complexity.md`.
- **Perfect-foresight LP as "our scheduler":** not realistic, so it is used only as an upper bound.

## Consequences

- Expected result on the reference house: UB = B1, and the forecast LP ≤ B1. This shows
  the greedy is near-optimal there and saves us from claiming gains that do not exist.
- The value of the network scheduler appears with deadlines and with several sites (steps 2–3).
  This is where the pitch and the paper make their claims.
- New parameters need sources: forecast error, site data for the Swiss test dataset, and the
  heat value per heating type (0014).
- Scored figures stay on `YearSimulation` with B1.

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes.** All three
points applied:

1. The optimality claim now lists its conditions. "UB = B1" is a regression test, not "UB slightly
   above".
2. The adapter checks the signal it applies. LiveRun gets the network engine (step 1b), and the
   solver lives in a separate project so LiveRun stays free of OR-Tools. Moves between sites get
   their own record.
3. The two-stage lexicographic solve replaces the weights. The forecast error model is specified
   (AR(1), seeded) with a candidate source. The heat-balance refactor is done and verified.

Not adopted: "skip the LP in step 1". The rolling-horizon LP is the same code that later carries
deadlines and sites; on the reference house it gives the UB = B1 check at low extra cost.
