# Webhosting design proposal for user approval

Status: library, structure and contract approach approved by the user on 2026-11-20;
solution plus pure placement/validation implemented. Live service behavior is not implemented.
Team-wide acceptance is pending.
Confirmed: C#, separate Docker engines, small app, resilient controllers/gateways, stable browser URL.

## Libraries and infrastructure

- .NET 10 / ASP.NET Core: HTTP APIs and background reconciliation.
- YARP: gateway routing; avoid writing a custom HTTP reverse proxy.
- Built-in System.Text.Json and HttpClient: JSON contracts and HTTP communication.
- Docker Engine HTTP API through the node-local Unix socket: avoid another Docker client library.
- Three-member etcd: durable desired state, transactions and leadership leases. Use its documented
  HTTP/gRPC gateway via HttpClient, with a small adapter; no custom consensus protocol.
- Keepalived: candidate Linux virtual-IP failover between gateways, including gateway health checks.
- xUnit: core and integration tests. Plain HTML/JS status page to avoid a separate frontend framework.
- Pin resolved versions in project files/images when implemented; versions besides .NET remain pending.

## Proposed structure

```text
webhosting/
  src/
    Webhosting.Core/          # Pure placement rules and domain models
    Webhosting.Contracts/     # Versioned JSON DTOs, separate from domain models
    Webhosting.Infrastructure/# Docker and etcd adapters
    Webhosting.Controller/   # Deployment API and reconciliation leader
    Webhosting.Node/         # Local execution and heartbeat reporting
    Webhosting.Gateway/      # YARP, routes and health checks
  samples/                   # Stateless frontend and API
  tests/                     # Placement, reconciliation and failure tests
  deploy/                    # Compose, node images, networking and scenario settings
  scripts/                   # Run, failure demo and verification commands
```

This structure is local to the prototype and does not choose the whole repository's stack.
Configuration holds node counts, ports, timeouts and capacity; values are labelled demo choices.

## Proposed public contracts

HTTP JSON with camelCase fields and a `/v1` API prefix. C# immutable records; IDs are strings.
Use explicit request/response DTOs rather than serializing internal domain objects.

| DTO | Fields |
|---|---|
| DeploymentSpec | deploymentId, components[] |
| ComponentSpec | componentId, image, containerPort, healthPath, replicas, cpuLimitCores, memoryLimitMiB, routePrefix |
| NodeHeartbeat | nodeId, observedAtUtc, cpuCapacityCores, memoryCapacityMiB, acceptingWork, replicas[] |
| ReplicaStatus | replicaId, deploymentId, componentId, nodeId, state, endpoint, observedGeneration |
| DeploymentStatus | deploymentId, generation, desiredReplicas, readyReplicas, replicas[], conditions[] |
| DrainNodeRequest | reason, timeoutSeconds |
| ApiError | code, message, details |

Proposed endpoints: idempotent `PUT /v1/deployments/{id}`, `GET /v1/deployments/{id}`,
`GET /v1/nodes`, `POST /v1/nodes/{id}/drain`, `POST /v1/nodes/{id}/resume`.
Reject path/body identity mismatch, invalid resources and duplicate component routes. Nested
prefixes such as `/` and `/api` are supported; the gateway must prefer the longest matching prefix.
Images must already be available for the offline demo; use immutable image references in repeatable runs.
Heartbeats use controller receipt time for liveness; sender timestamps are diagnostic only.
Container ports and advertised endpoints are validated against the configured node network.
Proposed internal `ExecutionCommand` fields: commandId, leadershipEpoch, deploymentGeneration,
nodeId, replicaId, action, spec. Track command outcomes durably and serialize changes per replica.
Validate authority when executing, not only when receiving a command. Reject stale generations,
and reconcile uncertain Docker outcomes by inspecting labelled containers before retrying.
A leadership change cannot undo an already-issued Docker operation: finish or observe its outcome
and reconcile against current desired state. Test an old controller resuming with an outstanding
command. These semantics and their achievable guarantees must be checked in implementation;
a lease or fencing integer alone does not prove split-brain safety.
Heat scheduling uses the team's shared policy boundary; do not invent physical inputs in these DTOs.

## Browser ingress: feasibility must come first

Only the `docker-desktop` WSL distribution is currently installed; Docker Desktop is now running.
Docker Desktop hides Linux container networks behind a VM; publishing one gateway's port does
not make that port fail over. A single additional proxy would remain a single gateway failure point.

The user selected Windows / Visual Studio with Docker Desktop. Do not install the previously
proposed separate Ubuntu WSL lab. Investigate an ingress mechanism compatible with this environment,
then present the concrete choice before implementing it. The laptop/outer engine remain the explicit
shared test-harness failure domain. Docker Desktop reachability of a floating IP is unproven.
A client that switches URLs is not an acceptable substitute. Document any networking changes.

If this environment cannot expose the stable address reliably, report the result and ask before
changing the design. Prove ingress first, before implementing the controller or application logic.
Propose a 30-minute investigation time box once Docker Desktop is running; if unsuccessful,
report concrete evidence and alternatives rather than silently weakening the stable-URL requirement.

## Proposed milestones and role placement

First demonstrator: static node membership, preloaded sample images and configured replica counts.
Prove complete-node failure recovery before adding generic deployment updates and planned drains.
This sequencing needs the user's agreement; the later functions remain in the requested plan.

| Role | Node A | Node B | Node C |
|---|---|---|---|
| Coordination store | Member A | Member B | Member C |
| Controller | Instance A | Instance B | Replacement capacity |
| Gateway / VIP candidate | Instance A | Instance B | Replacement capacity |
| Sample frontend and API | Initial replicas | Initial replicas | Replacement capacity |

This is a configurable demo topology, not hardware sizing. Enforce anti-affinity for each role
and app component, and check surviving capacity before starting the experiment. Test loss of
each entire node, including every role placed there. Initial scope is crash/pause failure;
network partitions require separate VIP-ownership and execution-safety analysis before claims.
Record request success/failure and elapsed recovery seconds. Agree a configurable recovery
acceptance threshold with the user before claiming the demo passes a performance target.

## Acceptance boundary

Target one simulated node failure at a time with sufficient surviving capacity and etcd quorum.
Pause the active controller, elect a replacement, then resume the old one without accepting stale
mutations. Losing quorum freezes changes, not existing healthy traffic. Gateways keep valid routes
and independently test backend health. Test whole-node failure with co-located roles.
Count interrupted requests; recovery does not imply every request survives or connections migrate.
All nodes still share a laptop: laptop/outer-engine failure is outside this demonstrator's scope.

References (checked 2026-11-20):
[Docker Desktop networking](https://docs.docker.com/desktop/features/networking/),
[WSL networking](https://learn.microsoft.com/en-us/windows/wsl/networking),
[etcd](https://etcd.io/docs/v3.6/faq/).
