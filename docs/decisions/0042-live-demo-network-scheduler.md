# 0042: Live four-node demo driven by the network scheduler (amends 0040)

- **Status:** proposed (implemented on `feat/T-51-four-node-live-demo`, ticket T-54)
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
2. **The real scheduler decides:** a coordinator in `DigitalHeat.LiveGrid` keeps an hour clock (one hour =
   2 s, configurable) for deadlines and the heat budget, and calls the scheduler on every tick **and whenever
   something changes** (job arrives, a task starts or finishes, a slider moves; checked every 250 ms), like
   the event-driven admission in `DigitalHeat.Orchestration` (`WorkloadPlacement.TryReserve`). Each step it builds a `NetworkState` from
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
   work view from observed task files; pure, tested `LiveNetwork` (state builder) and `LiveTaskPlanner`
   (allocations → file moves) in `DigitalHeat.Network`.
8. **Calibration (labelled demo settings, `config/live-demo-four-node.json`):** one node = one site with
   `ServerCount = 3` (three worker slots per container, 0.5 core each at full load) and `HoursPerStep = 1`
   (one step = 2 s); no tank (the slider is this
   hour's demand); φ = 1: Type 2 research filler is always available (grey tokens, local, never moves),
   so a house that wants heat always runs at its heat-led load and paid Type 1 jobs go first; 1 task = 1 server-hour, sized with
   `ising.py calibrate` to take about one step at one full core.
9. **Ownership after a move:** cohorts are rebuilt every step per (job, node the files are on now), with
   that node as origin. Moved work therefore belongs to its new node, which is the one difference from
   the year simulation (where a cohort keeps its origin); a task moves on only if its new node cannot
   finish it either. Allocations are server-hours for one hour; every allocation of a job to another
   node moves ⌈server-hours ÷ server-hours per task⌉ waiting tasks there at once (at least one), so a job
   spreads to all nodes the scheduler picked in the same step (a first version accumulated fractional
   allocations, which made moves trickle one node after another). Running tasks count as one
   full task (progress is not observed) and never move.
10. **Races and deadlines:** a failed rename on either side means the other side won (worker claimed it /
    coordinator moved it). Files are listed once per step. A job is missed when the step counter reaches
    its due step (as `WorkLedger.Expire`, `DueHour <= hour`); its waiting tasks are removed, a running
    task finishes but does not count.

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

## Two devices with four nodes each (plan, not built)

- **One coordinator, eight sites.** LiveGrid on laptop A keeps running the one scheduler over a single
  `NetworkState` with eight sites; the config gets a `devices` list (name, base URL) and a `device` per node.
- **Node agent on laptop B.** The same binary in `--agent` mode owns its four containers and its work
  folder and exposes a small HTTP API: list task files, accept a task file, hand over a waiting task,
  set load (pause / CPU quota), CPU readings. Laptop A drives it through a `RemoteContainerControl :
  IContainerControl`, so the actuation path (`SchedulerController.Apply` → `DockerJobExecutor`) stays the same.
- **Cross-device move without loss or duplicates:** the source first renames the waiting file into a
  local `outbox/` (atomic against its own worker), sends it, the destination writes it to its queue
  (temp + rename) and acknowledges, then the source deletes the outbox copy. An unacknowledged outbox
  file is returned to its queue after a timeout.
- **Network:** direct Ethernet cable or a phone hotspot, static IPs, no internet needed; a 4 s step
  hides any LAN latency. If B is unreachable its nodes are shown offline and marked not eligible.
- **View:** each laptop shows its own four houses (`?nodes=1-4`, `?nodes=5-8`) or one projector shows
  all eight (the grid already switches to 4 columns); a token leaving one screen exits at the edge
  facing the other laptop and enters there (layout recorded in the config).
- **Effort and risks:** about half a day. Risks: Windows firewall rules, Docker Desktop bind-mount rename
  semantics (rehearse on the demo laptops), keeping both clocks out of the logic (only A counts steps).

## Review

Fresh-context review (decision-reviewer, 2026-11-21): **go with changes**. Applied:
(1) capacity units: one node = `ServerCount 1`, `HoursPerStep 1`, φ = 0 (decision item 8);
(2) ownership after a move: cohorts per (job, current node), whole-task rounding with carried credit,
test that nothing moves back without a cause (item 9, `LiveNetworkTests`);
(3) races and deadlines: failed rename = other side won, one listing per step, `DueHour <= hour`
semantics, running task of a missed job finishes but does not count (item 10); still to rehearse on the
demo laptop's Docker Desktop. Audience: every move shows its reason (in the receiving house and the
log); a paused node shows its frozen task.
