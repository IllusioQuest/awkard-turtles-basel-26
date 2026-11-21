# T-65 — Retire competing scheduler policies

Owner: Nikita / Codex. Status: in progress. Branch: feat/T-60-unified-scheduler.

User explicitly requested one heat-first policy and removal of old scheduler implementations
and selectable modes, after correctness and speed checks. ADR 0051; builds on T-60/T-62/T-64.

Core owns heat target, stable site rank and deadline/job-ID ordering. Network, live containers
and Fleet retain execution-specific eligibility, work ledgers, lifecycle and physical models.
Remove origin-first B2, local-only B1, reserve/always-on/rolling-LP policy modes and Fleet urgency
heat override. Preserve original datasets, historical research outputs and analytical bounds.
Do not claim equivalence for intentionally changed multi-site decisions. Preserve exact
unlimited reference-house hourly outputs; verify finite/mixed conservation and deadlines.
Measure placement, full protocol and event-driven Fleet costs separately. No game restarts.

Done: adapters wired; obsolete modes fail explicitly; relevant suites pass; benchmark evidence
and changed-result notes shared on main; code PR available for human merge.

Verification: implementation pushed in PR #52; tests and real Linux recovery checks pass. See docs/performance/unified-scheduler-verification-2026-11-21.md. Large-scale placement speed guard remains open; not marked done before merge. Old policy-retention text is superseded by explicit user-approved ADR 0051.
