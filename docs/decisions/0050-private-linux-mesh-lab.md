# 0050 — Private Linux peer messaging with real batch executors

- Status: proposed; user approved real CPU batch executors with separate nested Docker
  engines, private HTTP peer messaging and a shared lab token, 2026-11-21.
- Ticket: T-63; branch feat/T-60-unified-scheduler, following T-62/ADR 0049.

Use built-in ASP.NET Core/HttpClient for a thin adapter around the same MeshPeer. Serialize
all incoming messages, submissions and timer ticks through one actor loop; MeshPeer's protocol
is deterministic/single-threaded and must not be called concurrently from HTTP handlers.
Messages use explicit snake_case type tags. A private shared token authenticates trusted lab
members; it is not per-node identity or production authentication. No custom consensus.

Run three Linux node containers on an internal Docker network. Each has its own nested Docker
daemon, own Unix socket and Docker/state volumes. Privileged DinD is explicitly user-approved
for this trusted local lab; containers are not a VM-grade security boundary. Do not mount the
outer Docker socket or host home, expose a Docker TCP port, use host networking, or touch the
running gaming/Minecraft projects. Start dockerd with an explicit Unix host. Pin pulled base
digests and record image IDs. Preload a locally built research-sim image into each inner engine;
the internal mesh does not need internet to execute jobs.

Use existing DockerWorkloadRuntime ownership labels/journals and approved local profiles only;
no remote command/image/env-file selection in messages. Reuse the repository's finite seeded
Ising batch workload. Outer CPU/RAM caps bound the lab slices; these are quota slices on one
laptop, not independent physical machines or GPUs. No GPU passthrough or duplicated GPU claims.
Node capacity/heat inputs are synthetic controlled lab signals, clearly labelled, not measured
recoverable heat or scored output. Active games are not adopted by this mesh. Persist origin
intents, target attempts and rejection tombstones in named volumes. No automatic origin failover.

Operator HTTP ports bind loopback only and require the token; health checks expose no job data.
Keep secrets in ignored local files/environment, never logs, source, journals or result reports.
Real job lifecycle tests: route to the higher-heat eligible peer, observe actual CPU work, pause
and resume from a heat update, fill/reject and replan, restart an origin without duplicate starts.
Record actual container IDs, states, resource limits, engine identities and restart counts; no
seamless migration, fractional-load, two-physical-device or production-security claim.

## Review

Pending fresh limited-context review.
