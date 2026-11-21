# 0048: Network scheduling decision and real admission boundary

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex; user approved direction

## Context

`Network` B2 allocates heat-led work among sites and delegates physical load through Core's `SchedulerController`; `Orchestration` independently ranks real nodes on admission. This can send a job somewhere other than the network decision. The case requires the same scheduling code for yearly accounting and a real paused job.

## Decision

Use `INetworkScheduler` as the authoritative cross-site allocation and load decision for compatible batch work; B2 is the operational baseline and LP a comparison. `Orchestration` receives the selected **site** plus the planning heat signal, inventory revision and UTC hour; it may choose an eligible node within that site after its existing locality, deadline, heat, resource and ownership checks, or reject the start. It must not substitute another site. Core's `SchedulerController` remains the actuation boundary for simulated and real executors. Existing autonomous admissions remain available for interactive/legacy callers until they have a compatible network work model; they are explicitly labelled, never represented as B2 results. A node may locally lower or pause load for safety; accounting uses achieved rather than targeted load.

## Alternatives considered

- Make `Orchestration` the network scheduler: it has no hourly multi-site cohort/forecast model and would duplicate B2/LP.
- Remove autonomous admission immediately: current interactive/gaming paths would stop working before a network job representation exists.

## Consequences

Selected-site start is fail-closed; a rejected choice returns to the next network planning cycle. The unlimited reference YearRun now calls B2 each hour after exact full-year regression against the previous path; a finite Type 1 fixture is also cross-checked. Finite/mixed YearRun, persistent interactive workloads, and the unmerged two-laptop demo need explicit mapping work; they are not claimed to use one implementation end to end yet. Fractional live targets need measured achieved-load feedback, since batch pause/resume may enact only zero/full load.

## Review

Fresh-context review: **go with changes**. It identified that B2 chooses sites rather than nodes, so admission is restricted to the chosen site and bound to the same inventory revision, hour and heat signal. It also found that the scored YearRun and live batch are not yet one end-to-end `INetworkScheduler` path and that positive fractional targets may actuate as full load. We retain both as explicit migration gates instead of claiming equivalence; the one-site regression is added first. No review loop.
