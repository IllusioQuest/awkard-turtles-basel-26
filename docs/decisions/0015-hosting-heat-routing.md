# 0015: Hosting routing adapter for shared heat decisions

- **Status:** proposed; awaiting user choice
- **Date:** 2026-11-21
- **Decided by:** pending Nikita; team confirmation pending

## Context
T-27 separates frontend/API across four engines; traffic is currently round-robin.
User requested scheduler integration. T-11 proposes a network LP, but its objective and exact
interfaces are not agreed. HTTP traffic is an always-on service, not a pausable batch queue.

## Proposed decision
Use the existing IScheduler/GreedyHeatScheduler with validated per-site HeatSignal values.
Add a hosting adapter, not a second heat model or a substitute INetworkScheduler.
Each component's configured policy/origin plus node site/country metadata is operator-controlled,
never accepted from request headers. Reuse JobPlacement.IsEligible before any dispatch.
Eligibility filtering must apply even with one destination and must not be bypassed by gateway
fallbacks; an empty allowed set returns unavailable. Only healthy component-matching nodes count.
Prefer the highest target_load_per_server; round-robin ties. At zero heat, continue serving.
Do not pause or throttle replicas, gateways or monitors. No load/energy delivery is inferred from
HTTP request counts. Two warm replicas per component remain the desired deployment.
Use read-only snake_case heat snapshots with source, revision and expiry. Missing/stale heat
falls back to round-robin among eligible healthy nodes; invalid placement metadata fails closed.
Policy and heat must come from one consistent request snapshot; validate finite quantities,
identities, timestamps and units. Synthetic lab signals are labelled, not reported as measurements.
Add a .NET 8 Core reference to the .NET 10 hosting adapter; existing YARP supplies routing.
No new package, solver, scored formula, annual model or shared core contract is introduced.

## Alternatives
Implementing T-12's optimiser now requires its unresolved objective/queue/allocation decisions.
Pausing hosting replicas first risks availability and needs drain/readiness/replacement semantics.

## Review
Pending fresh-context review. User approval requested before implementation.
