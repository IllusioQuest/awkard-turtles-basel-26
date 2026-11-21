# 0016: Six-node hosting replica lifecycle

- **Status:** proposed; user approved six nodes, automatic move/pause, common job catalog
- **Date:** 2026-11-21
- **Decided by:** Nikita; team confirmation pending
- **Extends:** 0015 (routing adapter, superseded scope; implementation now includes lifecycle)

## Decision and boundary
Use three separate Linux node engines per component (frontend/API), at least two desired serving
replicas. Move means resume/start a stateless replacement, probe readiness, switch routing,
drain old in-flight HTTP requests, then pause the old replica. No process-memory/data migration.
Rank eligible sites using the existing IScheduler/GreedyHeatScheduler; reuse JobPlacement.
Preserve component isolation and the shared reference-year model. Inputs are labelled synthetic
heat snapshots until a measured signal producer exists; no HTTP-to-kWh inference.

The active Linux gateway worker owns serial lifecycle reconciliation only after successful
exclusive port binding. The standby never mutates nodes until it binds. Gateway watchdogs and
the existing two trusted paused-container monitors retain failover responsibility.
On activation, restore all eligible candidates before considering scale-down; no scale-down on
missing/stale/invalid heat data, failed control operation, unknown readiness or insufficient replicas.
Remove the candidate from new routing before drain. On drain timeout, keep it unpaused.
Immediately before pausing recheck replacement health and eligibility; do not pause a sole survivor.
A new active gateway restores candidates before committing new pauses. Reconciliation repairs
a failed node by choosing a prepared spare; replacing destroyed engines is outside this increment.

Control uses the outer Docker Unix API from trusted gateway containers, scoped in code to the
configured lab project/component labels and the known inner component container. This grants
broad engine authority despite application-side scoping; application containers get no socket.
Nested control reapplies the existing per-node firewall before starting a stopped inner app.
No unauthenticated Docker TCP API or remote mutation endpoint is exposed.

Common catalog: batch and always-on; always-on service is non-interruptible even though replica
lifecycle is managed. T-01 owns schema/loader changes; do not implement a competing loader.

## Limits
Exclusive port ownership is local lab leadership, not a distributed production consensus system.
Docker operations may outlive a failed worker; restore-first recovery and serial changes limit
risk but do not establish cross-host fencing. Test interrupted transitions; make no linearizability,
zero-downtime or production HA claim. Shared laptop/Desktop and privileged dind remain trusted.
Warm spare recovery cannot overcome all nodes failing or forbidden placement rules.
Controlled synthetic demo traffic only; heat target is not serving-capacity telemetry.

## Review
Verdict: go with changes. Implemented a requirement for two OTHER ready eligible replicas
immediately before pause; synchronized admission and in-flight accounting through forwarding
completion; no pause on drain timeout. Config content fingerprint is rechecked after drain.
Continuous restore-first reconciliation verifies mutations and restores missing serving capacity.
Interrupted-transition integration test required; late Docker operations remain a documented
local-lab limitation rather than a production fencing guarantee.


