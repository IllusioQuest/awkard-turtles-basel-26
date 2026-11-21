# T-57 — Scheduler cleanup and event-owner runtime

- Owner: Nikita / Codex
- Status: implementation complete; PR #46 in review
- Branch: fix/T-57-scheduler-cleanup, based on latest main after PR #45.

Remove redundant inventory-validator placement instances/indexes, repeated immutable queue
sorting and avoidable attempt-history copies. Break the event owner's tick into clear private
steps and simplify inbox replay/persistence handling. Preserve public contracts, snake_case
JSON, sources, validation order, heat priority, hourly commitments and active-game protection.
No new dependency, model, module boundary or scheduling policy; no ADR needed for this refactor.
Inspect unnecessary code using references rather than deleting old experiments or other agents'
files indiscriminately. Measure identical event-owner workloads before/after and retain raw data;
run existing behavioral tests and the pure admission benchmark. No live game/container changes.

Done: focused cleanup PR, tests/build pass, reproducible performance method and limitations,
and handoff documentation committed directly to main.

Results: cleanup and comments in existing modules, 61 orchestration + 46 Core tests, clean WorkloadRun build. Queued 128-node owner median 194.7 -> 94.5 ms; reverse-order comparison 99.3 -> 51.7 ms; managed allocation 87.3 -> 14.7 MB. Idle timings varied; all raw runs and caveats retained in docs/jobs/scheduler-cleanup.md. No new robustness feature, policy or live runtime change.
