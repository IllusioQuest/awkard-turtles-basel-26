# T-62 — Deterministic origin-owned mesh and shared heat-first placement

- Owner: Nikita / Codex; coordinate Jona (T-60/T-61).
- Status: in progress
- Branch: feat/T-60-unified-scheduler (explicit user-selected branch).
- Decision: 0049; user approved simulation-first and durable origin ownership.

Implement the peer-protocol milestone with direct peer offers, common heat-first ranking,
local selected-site admission and origin-persisted target intents. Exercise stale/full reports,
duplicates, lost replies, partitions and origin recovery with fake executors. Preserve the
hourly heat/resource ledger and active games. Keep old algorithms as named comparisons.

This ticket does not complete T-61's scored finite/mixed + real-job equivalence or live Linux
integration. Fleet/Network/real-runtime adapters must be listed accurately rather than claiming
all schedulers are unified from a shared comparator or a synthetic transport alone.

Done: reviewed design; executable deterministic peer simulation; existing/new behavioral and
performance checks; code push/PR; docs and co-builder handoff directly on main.

Verification: implementation pushed in PR #52; tests and real Linux recovery checks pass. See docs/performance/unified-scheduler-verification-2026-11-21.md. Large-scale placement speed guard remains open; not marked done before merge. Old policy-retention text is superseded by explicit user-approved ADR 0051.
