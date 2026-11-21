# T-57 — Scheduler cleanup and event-owner runtime

- Owner: Nikita / Codex
- Status: in progress
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
