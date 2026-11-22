# 0068: Mesh freezes batch work whose heat cannot be used or stored

- **Status:** proposed
- **Date:** 2026-11-22
- **Decided by:** Ennio (team discussion, 2026-11-22); T-78

## Context

The mesh heat-first scheduler (`MeshPeer.ApplyLocalHeat`, PR #52) paused a running batch container
only when the shared `HeatFirstScheduler` returned load 0, i.e. when the site's useful heat
(`demand + tank room`, `HeatCapacity.RemainingKwh`) was exactly zero. Its containers are
freeze-only (`DockerJobExecutor` with `supportsThrottle: false`): any positive target load runs the
job at its full declared heat. So with a full tank and a demand above zero but below the job's heat,
the job kept running and the excess heat was dumped. The research LP and the hourly models never
dump in heat-led mode, because their load is continuous (`docs/model/network-scheduler.md`).

The T-78 live demo (summer reference house with an EU tapping profile) needs this case to behave
correctly: a hot-water draw that is smaller than the job's heat must not keep the job running.

## Decision

Freeze-only batch containers round the shared heat-first load down to whole jobs. In
`MeshPeer.ApplyLocalHeat`, the budget is the site's useful heat `HeatCapacity.RemainingKwh` (demand
plus tank room, the numerator of `HeatFirstScheduler` and of admission). Protected and non-pausable
running work is counted first. Pausable running batch jobs are then kept in priority order
(earliest deadline, then ID, `JobPriority`), and every job whose heat no longer fits is frozen.
Paused jobs thaw in the same order, each counted once it thaws, when their heat fits the budget. The
existing resume admission still applies, and so do the retained hourly commitments: a frozen job's
heat stays committed for the current hour. An optional `minimumFreeze` (default 0, so existing
callers are unchanged) keeps a frozen job frozen for at least that long and prevents fast flapping.
The zero-heat pause and the operator hold are unchanged.

## Alternatives considered

- **Throttle to the fractional heat-first load** (CPU quota through the existing `DockerJobExecutor`
  path): it matches the LP and Core most closely and never dumps. Not chosen for now: the team wants a
  visible freeze/thaw, and throttling changes per-job runtime limits in the shared runtime adapter
  (spec hash, reconciliation). It remains a later option.
- **Keep pausing only at zero useful heat:** this dumps heat whenever demand is above zero but below
  a job's heat. It contradicts the heat-led "wait, do not dump" policy (decision 0010).
- **A demo-only pause in the runner:** forbidden by the case (the same scheduler must control the real job).

## Consequences

- Mesh batch jobs no longer dump heat while the tank is full. In Nikita's lab peers (no tank, demand
  from `/heat`), a heat update that is lower than the admitted jobs' heat now freezes the
  lowest-priority job instead of letting it run. Games and non-pausable work are not affected.
- The rule uses declared job heat, not measured heat. A job whose real heat is lower is frozen
  conservatively.
- Freeze and thaw use the same threshold. Hysteresis comes from the retained hourly commitment
  (within an hour a frozen job's heat stays committed) and from the optional `minimumFreeze`.
- Scored yearly figures are unchanged: `YearSimulation`, Fleet and Network keep their continuous
  loads. This rule only covers the freeze-only container adapter.
- Tests in `PeerMeshTests`: `BatchFreezesWhenItsHeatExceedsWhatCanBeUsedOrStored`,
  `BatchKeepsRunningWhileTheTankCanStoreTheSurplus` (room smaller than one job),
  `LowestPriorityBatchFreezesFirstAndOnlyWhatFitsThaws`, `FrozenBatchWaitsTheMinimumFreezeTime`. All
  other Orchestration tests are unchanged
  (one pre-existing failure on `main`, `LegacyJournalRetainsExactPreDeadlineContainerHash`).

## Review

Fresh-context review (decision-reviewer, 2026-11-22): **go with changes**. Applied:
1. The budget is now `HeatCapacity.RemainingKwh` instead of a separate "tank full" test. A tank with
   less room than one job's heat no longer dumps, and admission and freezing use one formula.
2. Thawing now runs in priority order and counts each thawed job, so two jobs cannot oversubscribe
   in one pass.
3. The rule is documented in `docs/model/unified-heat-first-mesh.md` as whole-job rounding of the
   shared heat-first load. Tests were added for priority order, a single thaw, a small tank room and
   the minimum freeze time. The operator hold was already covered.

Not applied: the reviewer saw a one-job deadband with the shared budget. With equal thresholds there
is none, so we added `minimumFreeze` (the user asked for a minimum wait after a pause). Retained
hourly commitments already provide hysteresis within the hour. No dedicated test covers offers of
frozen jobs; `Publish` already advertises all retained hourly heat.
