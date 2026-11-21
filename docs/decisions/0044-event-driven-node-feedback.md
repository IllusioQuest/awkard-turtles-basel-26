# 0044 — Event-driven node feedback for the workload owner

- Status: proposed; user approved shared contract and local JSON adapter, 2026-11-21.
- Ticket: T-56, branch feat/T-56-node-feedback.

## Decision

Add a transport-independent C# NodeFeedback contract and an opt-in WorkloadRun
`watch-feedback` owner mode. A node publishes an atomic replacement JSON file in a
private local inbox. FileSystemWatcher wakes one serialized owner; a bounded one-item
signal coalesces bursts. A configured fallback timer handles missed notifications,
expiry, runtime reconciliation and existing deadline enforcement. No network probing,
Docker work or file reads inside WorkloadPlacement.TryReserve; no new scheduler.

Reports contain node identity, monotonic sequence, UTC observation/expiry, exact heat
interval start, accepting-work flag, source and optional HeatSignal. Bindings authorize
one configured node to report each site's heat; other nodes cannot change it. Reports
may update demand and tank level, not hardware, tank capacity or recoverability. Persist
accepted reports/high-water sequences before dispatch; duplicate reports do not refresh
timestamps. Unknown nodes, malformed/future/stale/out-of-order reports cannot enable work.
No fresh node report means no new placement there; no fresh authoritative heat means no
new placement anywhere at that site. Physical resources/GPU occupancy retain the separate
inventory freshness requirement: feedback does not manufacture resource telemetry.

Project fresh reports onto trusted inventory without changing its hourly boundaries or
external commitments. Advance only the effective inventory revision. Existing reservations
remain in the coordinator ledger and are subtracted by the same heat/resource logic. Never
add a fresh hourly energy budget for each event. Config can refresh inventory monotonically
but cannot change accepted jobs, deadlines, runtime endpoints or feedback policy in flight.

Full means block new admissions; it does not prove termination, release claims or evict
games. Reconsider waiting work on updates, ordered interactive then due-time batch then ID.
Interactive jobs retain their configured latency eligibility before heat ranking; batch
without latency requirements is heat ranked. Reuse batch-first preemption, reconciliation,
deadline enforcement and durable attempts. Do not automatically migrate active sessions.
Initially do not change running jobs merely from a full report. Paused batch may resume
only through existing admission checks after waiting work is considered. No unknown-game
autostart: existing measured-heat requirements remain.

Keep yearly simulation hourly and reference parameters unchanged. T-54 LiveGrid already
reacts between ticks; do not modify its coordinator or queue ownership. This owner controls
only its existing configured fleet/manifest. No HTTP endpoint or monitoring write access.
Local publishers are trusted; filesystem access is not network authentication. Document
remote transport and node-side sensors as unimplemented.

## Validation

Fake-clock/runtime tests: full -> blocked, fresh heat -> waiting job eligible in same hour,
duplicate/old/future/expired reports, restart replay, reporter scope, shared GPU/resources,
active game protection, preserved deadline rules, coalesced wake/cancellation, atomic files.
Use an isolated local process experiment for filesystem wake, not active Docker games.
Run existing scheduler/core tests and unchanged admission timing diagnostic. Policies
(timer, freshness cap, file size, finite queue) are sourced operator lab limits, not SLAs.

## Review

Pending fresh limited-context review.
