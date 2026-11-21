# 0003: Resilient local webhosting prototype

- **Status:** superseded by 0026 (webhosting retired, docs removed in the repo cleanup). Was: proposed
- **Date:** 2026-11-20
- **Decided by:** user approved stack/structure/DTO approach; team confirmation pending

## Context

The user requests a Flux-inspired platform with replicated app components and controller/gateway
failure recovery. The laptop uses Windows/Docker Desktop; the draft is due in about 16 hours.
This is an isolated hosting task, not a replacement for the reference-house must-haves.

## Decision

Propose the stack, projects and DTOs in the design proposal (`docs/webhosting-design-proposal.md`, removed in the repo cleanup; see git history).
Use ASP.NET Core/YARP, node-local Docker engines and existing etcd consensus with fenced execution.
First prove a stable browser endpoint with redundant gateways in the user's Windows/Docker Desktop
environment; ingress details still require approval and verification. Then implement reconciliation.
Ask the user before adopting libraries, boundaries or contracts; commit/push are already authorized.

## Alternatives considered

- Kotlin: viable, but the user selected C#.
- Shared node engine or a single controller/gateway: do not meet the requested failure model.
- Custom consensus or automatic source-code partitioning: too large for the prototype.

## Consequences

Nested engines, replicated state and resilient ingress add significant setup/test work.
One simulated node failure is the proposed bound; the laptop/outer engine remain shared dependencies.
Do not claim the Windows browser ingress or case must-haves work until independently verified.

## Review

Fresh-context review: **go with changes**. Applied: require explicit role anti-affinity and
whole-node tests; specify execution command identities, generations and uncertain outcomes;
propose an ingress time box and a fixed-app first milestone before generic deployments.
Crash/pause recovery is distinct from network-partition safety. The user approved libraries,
structure and DTO approach, and selected Windows rather than a separate Ubuntu WSL lab.
Only the Visual Studio scaffold is implemented; ingress feasibility remains an open prerequisite.
