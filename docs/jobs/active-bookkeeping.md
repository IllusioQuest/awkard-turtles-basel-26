# Active-job bookkeeping (T-85)

User approved reducing job-management overhead and required the renderer UI remain
unchanged. ADR0071 received a fresh-context go review. ActiveAttempts filters only
released attempts before detached copying; every other phase/order is preserved.
Mesh Tick terminal/created scans and batch heat-control scans use this view.
Retained heat, admission, recovery, unknown/reappeared ownership and public full
history keep their existing paths. No records or checks are deleted.

Both deadline APIs execute ONE private core while holding the existing gate.
EnforceDeadlines keeps its full returned history; EnforceDeadlinePolicies performs
identical deadline actions without constructing that unused result. Tick calls it
at the same two points. No cadence, priority, persistence or deadline-rule changes.

141 tests passed. Both entry points were exercised for running/paused expiry,
protected jobs, failed stops/recovery and completed jobs. Active snapshot tests
cover all12phases/order, nested-array isolation and retained full history. Released
heat still blocks new admission despite an empty active snapshot.

In a500released-attempt fixture,50queries allocated229695440bytes with the old
full-copy/filter versus14408bytes using the active query. Deadline result copying
allocated229704888bytes versus3600bytes for snapshot-free enforcement. These are
isolated bookkeeping allocations; no percentage GPU/frame speedup is implied.
The new path still scans records; runtime reconciliation and journal saves still
scale with history. Source: docs/performance/active-bookkeeping-2026-11-22.json.
Reproduce using HeatSummaryTests and detailed console/TRX test output.

This task does not implement the separately requested GPU3 disable button. That
requires agreed drain/reassignment semantics and a separate ownership design.

PR #76 /967cf22. Real48-frame run492343cfae36 completed in84.9243active
seconds, GPU counts24/16/8 and valid48-frame MP4. This is verification of
function, not a matched end-to-end speedup benchmark. UI unchanged.
