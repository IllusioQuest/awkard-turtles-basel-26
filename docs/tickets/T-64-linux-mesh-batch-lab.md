# T-64 — Linux peer messaging and real batch execution

- Owner: Nikita / Codex
- Status: in progress; user approved real batch executors and nested engines.
- Branch: feat/T-60-unified-scheduler
- Depends on: T-62, T-60 selected-site admission; decision 0050.

Implement a private HTTP adapter for MeshPeer, a serialized node actor, separate Linux inner
engines, and a scoped lab harness. Reuse existing finite research workload and Docker runtime.
Test real placement/pause/resume/origin restart without touching active games. Document the
synthetic heat inputs, shared-laptop quota slices, inner engine privileges and remaining live
deployment limits. Preserve heat priority and durable origin ownership.

Done: reviewed decision, verified isolated Linux lab, meaningful checks and reproducible
commands/evidence; code pushed for review and documentation on main. This does not close
T-61's finite/mixed scored equivalence or establish multi-device operation.


Verification: implementation pushed in PR #52; tests and real Linux recovery checks pass. See docs/performance/unified-scheduler-verification-2026-11-21.md. Large-scale placement speed guard remains open; not marked done before merge. Old policy-retention text is superseded by explicit user-approved ADR 0051.
