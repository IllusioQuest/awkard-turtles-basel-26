# 0049 — Shared heat-first policy and origin-owned peer mesh prototype

- Status: proposed; user approved deterministic mesh first, then Linux Docker integration;
  durable origin ownership and no reassignment during origin failure, 2026-11-21.
- Ticket: T-62; implementation on the user-selected feat/T-60-unified-scheduler branch.
- Builds on Jona's ADR 0035 and unmerged T-60/ADR 0048 selected-site boundary.

## First milestone

Use one Core heat-ranking primitive (maximum useful headroom, ordinal site/node ties) for
Orchestration placement and federated offers. Keep eligibility/resource/locality/required
latency filtering separate from ranking. Add a heat-first INetworkScheduler strategy as an
explicit alternative to origin-preferring B2; keep scored B2/LP baselines labelled and unchanged.
Existing local heat/load controllers remain executors, not independent cross-site routers.
Fleet and finite/mixed YearRun still require explicit adapters: do not claim all paths unified
from sharing a primitive alone. T-61 remains open until its real-job/equivalence criteria pass.

Build a deterministic message-transport simulation using node-local WorkloadCoordinator
instances and fake executors, not a second heat-physics simulator. Every peer can exchange
versioned, expiring capacity/heat snapshots directly with every other peer. One peer owns a
physical resource domain; multiple containers sharing an engine/GPU are not independent peers.
Each origin owns a finite immutable submission queue and durable target intent. Its planner
uses the same physical placement/heat policy over received snapshots. The selected peer uses
TryStartAtSite and its current ledger: accept or reject, never secretly reroute. Fix selected-site
snapshot binding to work with the existing retained-hourly-heat projection, with regression tests.

Persist origin target intent before sending. A lost acknowledgement or origin crash retains
that target; retry/query it rather than choose another peer. Only an explicit rejection with
no prior attempt permits replanning. Duplicate proposals reconcile the existing target attempt.
Origin recovery reloads its journal. Peers never reassign another origin's job. No consensus,
election or transparent origin failover; no claim of partition-tolerant reassignment. Existing
coordinator journals retain uncertain starts, GPU/RAM/slots and full-hour heat commitments.

Only configured local profile IDs and immutable workload requirements cross the prototype
transport, not credentials, environment files or user input data. Fake delivery supports delayed,
dropped and duplicate messages and partitions. A simulation driver delivers events; it owns no
global job queue or placement decision. Active interactive sessions stay assigned. Full/heat
updates trigger new decisions; stale offers cannot authorize starts. Runtime calls stay outside
the pure hot path. No scored/reference figures, running game containers or existing Fleet default
are changed. Linux transport/authentication is a later explicit adapter decision.

## Validation

Heat ranking and configured latency, shared physical resources, stale/full peer rejection,
simultaneous origins, duplicate/lost replies, partition, origin restart without reassignment,
retained hourly heat and active-game protection. Keep existing selected-site and reference-year
tests. Measure pure placement separately from complete protocol rounds and message counts;
full-mesh offer traffic is O(n²), not free scalability. Synthetic fixture values have explicit source.

## Review

Pending one fresh limited-context review.
