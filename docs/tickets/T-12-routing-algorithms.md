# T-12: Routing baselines and optimiser

- **Tier:** 2 Pitch  ·  **Area:** Core / algorithms
- **Depends on:** T-11, T-05
- **Suggested owner:** Codex  ·  **Owner:** Jona / Claude  ·  **Status:** in progress (step 1 done: engine, B0/B1, rolling-horizon LP, UB; decision 0024, `docs/model/network-scheduler.md`)

## Goal

Implement and compare scheduling/routing strategies on the same interface.

## Tasks

- [ ] Baseline A: always on (dump surplus). Baseline B: local greedy heat-following (exists). Baseline C: greedy network routing (send work to the eligible site with the highest heat need within its deadline/latency).
- [ ] Apply T-24 `JobPlacement.IsEligible` before considering a site in every routing strategy;
      no heat or price objective may override a denied job location.
- [ ] Optimiser: rolling-horizon solver from T-11.
- [ ] All behind one interface (extend `IScheduler` or add `IRouter`, per T-11 ADR); same code path for simulation and live executor.
- [ ] Benchmark harness: same scenario, same jobs, output table (coverage, dump, utilisation, missed deadlines, surplus, runtime of the algorithm).
- [ ] Tests for each strategy on small hand-checkable cases.

## Done when

- Comparison table for the reference house and the neighbourhood scenario; optimiser beats baselines or we can explain why not.

## Read first

- `docs/model/routing-complexity.md` (complexity, LP formulation, effort estimate)
- `docs/model/routing-optimisation.md (T-11)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
