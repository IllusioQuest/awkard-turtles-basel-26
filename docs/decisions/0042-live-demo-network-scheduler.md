# 0042: Live four-node demo driven by the network scheduler (amends 0040)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona (asked to take over PR #41: "use our real scheduling algorithm, run real tasks,
  show where they are"), drafted by Claude

## Context

PR #41 (decision 0040) showed four containers on one laptop, but (1) the nodes pulled from one shared
queue, so nothing ever moved between nodes: a paused node's task stayed frozen and peers simply took
the next task; (2) the view had no movement between panels; (3) the sliders lagged and jumped, because
every slider event ran several Docker CLI calls (including `docker stats`) while holding a lock that the
state polling also needed, and polling repainted the panels with stale server values.

## Decision

1. **Ownership:** every node has its own queue directory (`queue/<node>/`). A worker claims only from
   its own queue by atomic rename into `running/<node>/` and publishes results to `done/`. A task file is
   always in exactly one place, so the view shows where each task is.
2. **The real scheduler decides:** a coordinator in `DigitalHeat.LiveGrid` runs fixed steps (one step =
   one scheduler hour, slowed to a few seconds, configurable). Each step it builds a `NetworkState` from
   the four sliders (heat demand), a persistence forecast (current demand repeated) and the open jobs as
   Type 1 cohorts (remaining tasks × server-hours per task, deadline in steps), and calls the existing
   `INetworkScheduler` (B2 `NetworkGreedyJobsScheduler` by default, the rolling-horizon LP selectable).
   No new scheduling logic.
3. **Actuation unchanged:** each node's load goes through `SchedulerController.Apply` with the
   committed network decision → `DockerJobExecutor` (pause / CPU quota). CPU readings come from one
   batched `docker stats` loop, not from the request path.
4. **Moves are real:** the decision's allocations (cohort → site → server-hours) are turned into moves of
   *waiting* task files to the allocated node's queue (atomic rename; a task already claimed by a worker
   is not moved). Running tasks stay with their node (frozen while paused). Each move, spawn, completion
   and missed deadline is an event in the API, and the view animates it.
5. **New work is explicit:** a job (N tasks, deadline D steps) appears only when someone presses the
   node's "+" button or when auto-spawn is switched on; both show a clear arrival animation at the
   origin node. Jobs past their deadline with unfinished tasks are counted as missed and their waiting
   tasks are removed.
6. **Sliders:** the room colour changes locally while dragging; targets are sent at most every 250 ms
   and once on release; the server stores the target and returns at once (no Docker call in the
   request); polling does not overwrite a slider that is being dragged or was changed in the last
   1.5 s.
7. Small additive API: `Type1Cohort.Observed(...)` so a live coordinator can build the scheduler's
   work view from observed task files; a pure, tested `LiveTaskPlanner` maps allocations to file moves.

## Alternatives considered

- Keep the shared pull queue and animate "virtual" moves: shows something the system does not do.
- Run `NetworkSimulation.Run` live: it loops over a whole year; one step at a time with observed files
  keeps the files as the single source of truth.
- Restart frozen tasks elsewhere on lease expiry (0040): recomputes work; replaced by moving waiting work.

## Consequences

The demo shows the same scheduler decisions as the year simulation, applied to real running work.
Limits: one laptop; slider temperature is an illustrative heat-demand input; one step is a slowed
"hour"; server-hours per task is a demo calibration (task runtime ÷ step length), not a measurement;
running tasks do not migrate (no checkpoint transfer). Decision 0040's shared queue is superseded for
the live demo.

## Review

To be added after the fresh-context review.
