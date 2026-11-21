# 0033: Universal workload admission above shared heat placement

Status: proposed; user approved general direction, concrete choices pending. T-43.

## Proposal

Keep C# and existing Docker contexts unless the user selects a different stack. Describe trusted
workload requirements in snake_case: CPU cores, RAM MiB, GPU requirements/capability, sourced
recoverable heat in kWh per hour, placement policy, latency/dependency constraints, persistence
and explicit lifecycle capabilities (pause/checkpoint/restart/replicate). Values are declarations
until measured; no default fabricated game wattage or latency guarantee.

Extract/generalize the existing atomic gaming admission path behind a reusable boundary while
retaining a compatibility adapter for existing SessionPlacement callers. Do not create a second
heat-ranking algorithm or duplicate Core physics. Reuse HeatCapacity and JobPlacement locality;
retain largest absolute useful-heat headroom and deterministic ties for new eligible placements.
Keep IScheduler/IExecutor for existing heat-load control and reuse JobRuntime for isolated batch
pause/throttle operations. No change to scored reference-house formulas or economic calculations.
The team Network simulation remains its own shared network/year model, not a competing model.

Model requirements separately from observed availability and reserved ownership. Count CPU/RAM/GPU
against the actual physical resource domain; several Docker containers on the laptop do not
create multiple physical GPUs. Proposed first GPU admission is conservative/exclusive, subject
to user agreement; fractional GPU availability is not inferred from low observed utilization.
Unknown/stale inventory or an unmet locality/network/storage constraint blocks new admission.

Atomically reserve both heat and compute before dispatch. Keep the active workload pinned.
Maintain pending/starting/observed-running/releasing/failed ownership explicitly, reconcile against
labelled runtime identity, and never call detached Docker exec acknowledgment readiness. Account
for already-running external workloads, including the user's current game, before new admissions.
A retry must not launch a duplicate or release an uncertain live process. Durable ownership/fencing
and coordinator HA are separate explicit choices; do not silently claim an in-memory lock is HA.

Pause interruptible batch before disturbing interactive workloads. Pausing/freezing can reclaim
CPU time but does not reclaim its RAM or GPU allocation. Only credit RAM/GPU after an approved
checkpoint/stop action and positive resource-release observation. Batch and a game must not share
a pause-controlled container. Do not bypass protected active games to meet a heat target.
An active session may outlive a site's useful-heat window; existing policy does not authorize
seamless migration or treating that runtime heat as still useful.

Runtime remains behind a replaceable Docker-context adapter; application containers get no Docker
socket or host privileges. Start with the example selected by the user. Keep node-local volume
pinning unless a reviewed storage/replication/fencing design is selected. Backup restoration is
not seamless failover; a missing node-local world cannot be resumed safely on another node.

## Boundary and validation

This distributes workloads, sessions and explicitly independent tasks. It cannot turn an unchanged
Minecraft/Xonotic process into cross-machine distributed computation. Minecraft can run one world
server plus separate streamed clients; one shared world across workers is a separate fork-specific
experiment. Browser-only client access remains the intent. No distributed-world fork is selected.

Validate admission through the same pure decision code with fake and real runtime adapters;
cover resource exhaustion, physical GPU sharing, locality, latency/storage affinity, stale inventory,
concurrent admission, retries and failed/uncertain launch reconciliation. Verify a small isolated
Docker workload before any change to the active player session. No installation or server EULA
acceptance before the selected game's requirements are established with the user.

## Review

Fresh limited-context verdict: go with changes. Applied the accounting, restart and enforcement rules below. Concrete APIs and persistence guarantees remain proposed until the user answers the important design choices; acceptance is not inferred from elapsed time.

### Review corrections

- Inventory semantics: physical allocatable capacity minus externally owned allocations minus this
  coordinator's owned claims, each exactly once. Observed running workloads are matched to claims
  by stable deployment/workload/attempt identity; a runtime observation is not an extra second
  reservation. Snapshot revisions cannot erase an uncertain live allocation. Instantaneous low
  CPU utilization is not free reserved capacity.
- Single-coordinator prototype: require one active coordinator for the configured fleet and fail
  closed on restart until all labelled existing/uncertain attempts are reconciled. There is no
  multi-controller/HA claim without durable ownership and fencing. Conservatively retain unknown
  claims; do not blindly replay queued starts. The mechanism enforcing a single active owner is
  an implementation decision that must be specified before enabling automated launches.
- CPU cores initially mean a declared CPU-equivalent quota budget, not physically pinned/dedicated
  cores. Enforce supported CPU quotas and RAM limits in workload-owned isolated containers, with
  transparent limit/OOM behavior; otherwise clearly refuse the corresponding isolation claim.
  GPU claims name physical device/resource-domain identity and compatible capabilities. Sharing
  modes remain unsupported unless explicitly approved and actually enforceable on that hardware.
- Automatic checkpoint/stop/preemption needs explicit workload-owner authorization and a supported
  adapter. Observation of release does not authorize stopping external/user processes. Keep the
  current game external/protected until deliberate adoption, not accidental scheduler ownership.

Smaller first delivery recommended by reviewer: fixed trusted inventory, one coordinator,
conservative GPUs, local storage, reserve/start/observe/stop/release for isolated workloads.
Checkpointing, replication, HA and automatic resource reclamation can be deferred; user choices
still determine the example and storage scope. The first milestone must document these limits.
