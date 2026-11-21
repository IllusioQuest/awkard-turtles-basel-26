# 0030: Transfer delay for moved Type 1 work (amends 0025 §3)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona (asked for T-12 task 2e), drafted by Claude

## Context

Decision 0025 §3 records moves of Type 1 work between sites as simulated flows "with no transfer delay
or cost; both TODO(source)". The experiments (T-12, `docs/model/network-scheduler.md`) show that B2
prevents missed deadlines by moving work to year-round sinks. Without any transfer friction this is
optimistic, especially for distant sites. No sourced values exist for the delay (data upload,
container start) or for the cost (data volume per server-hour × network price).

## Decision

1. **Transfer delay `d` (whole hours, default 0), "data staged at arrival":** work of a cohort may run
   at a site other than its origin only from hour `arrival + d` on; its due hour does not change. This
   assumes the cohort's data starts copying to eligible sites when it arrives, so it is a **lower bound
   on transfer friction**; a delay measured from the move decision would need moves committed ahead of
   time (not modelled). `d ≥ deadline` means no moves at all. Work at the origin is unaffected.
   - **B2 / B2-reserve:** a cohort may move only when the current hour ≥ arrival + d. Its excess that
     cannot move yet is treated as gone from the origin's queue in the EDF test (added to the
     "already sent away" amount), so later cohorts are not pushed out twice.
   - **LP:** move variables `x[c, s ≠ origin, k]` exist only for window hours `k ≥ arrival + d − hour`
     (and before the due hour), i.e. `deadline − d` usable hours at the destination.
   - B1 and B1-reserve do not move and are unaffected.
2. **No transfer cost parameter.** Runs report moved server-hours and **moved server-hours per avoided
   missed job** (versus B1 on the same run). Converting moves to CHF needs a sourced data volume per
   server-hour (`TODO(source)`) and belongs to the economics module (T-14). Moves can still occur for
   reasons other than deadlines (e.g. the LP's used-heat and server-hours stages with φ < 1), so
   moved server-hours are always reported.
3. **Values are a sweep, labelled as assumptions:** d ∈ {0, 1, 4, 12} h.
4. One delay for all sites (no distance model); a per-link delay can be added later on the same variables.
5. No interface change: constructor parameters of `NetworkGreedyJobsScheduler` and
   `RollingHorizonWorkLpScheduler`, `--transfer-delay` in `NetworkRun`, and an additive
   `transferDelayHours` field in the export's `run.json` (decision 0025 §7 stays compatible).

## Alternatives considered

- Distance-based delay (km → hours): needs a second unsourced constant; deferred.
- A cost parameter `c` (CHF per moved server-hour): a second unsourced default; dropped in favour of
  reporting moved server-hours (review).

## Consequences

Experiment E shows how quickly the network benefit shrinks with friction. A 24 h deadline with
`d = 12` leaves only 12 h at the destination. Results stay comparable to step 3 at `d = 0`.

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes**. Applied: (1) the delay
counts from arrival and is named "data staged at arrival", a lower bound on friction, with d ≥ deadline
meaning no moves; (2) B2 no longer double-counts blocked excess; (3) the claim that cost cannot change
decisions is removed and the cost parameter dropped (its simpler alternative); moved server-hours and
moved server-hours per avoided missed job are reported; (4) `transferDelayHours` is written to `run.json`.
