# 0012: Component-isolated hosting lab

- **Status:** superseded by 0026 (webhosting retired, docs removed in the repo cleanup). Was: proposed; user approved component isolation scope
- **Date:** 2026-11-21
- **Decided by:** Nikita (implementation approval; team confirmation pending)

## Context
The sample currently serves HTML and API in one process; its two routed replicas share all code.
User wants separated webapp parts across more nodes while retaining failure recovery.

## Decision
Add an opt-in topology with four separate nested Docker engines: frontend A/B and API C/D.
Build separate minimal ASP.NET frontend/API projects and images with synthetic data only.
Two existing-style active/standby gateways keep localhost:18080, routing /api/** to the API
pool and other paths to frontend. Only API serves /api/server; only frontend serves the page.
After failed internal-bridge ingress, user approved ordinary inner Docker bridges with per-node firewall rules: inbound HTTP and established replies allowed, new app egress blocked. No application
Docker sockets, host networking, writable root filesystem or shared volumes; drop capabilities.
Each frontend only delivers static content. Browser calls API through the same gateway origin.
Retain existing outer-socket monitoring for this trusted lab's paused gateways only.
Stop only existing lab gateways when opting in; the old lab can be restarted separately.
Gateways and monitors are trusted; both are redundantly deployed in the new profile.

## Limits and alternatives
Docker isolation does not hide memory/data from a privileged node/outer-engine operator.
The gateway sees request metadata and API plaintext; local HTTP is synthetic-only.
All nodes share Docker Desktop/laptop and privileged dind; this is not hostile multi-tenancy.
No accounts, real data, storage, blockchain, ZKP, TEE or custom encryption in this increment.
Do not invent storage replication before a workload/storage design is approved.
T-24 eligibility must be reused before future heat-based placement; routing is still static.

## Review
Verdict: go with changes. Verify restricted inner-network reachability before scaling; use distinct backend ports and scoped monitor selectors; test frontend, API and paused gateway separately. Recovery covers individual component failures only; requests may fail during recovery.


