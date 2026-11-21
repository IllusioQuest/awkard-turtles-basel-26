# T-43: Universal resource-aware workload placement

Owner: Nikita / Codex. Status: implementation complete; final integration checks/PR in progress.
Code branch: `feat/T-43-universal-workloads`. Design: ADR 0033.

## Approved scope

User selected the recommended C# + Docker contexts, existing Xonotic/batch first, and local
storage with backup/restore. Whole workloads are distributed; an unchanged game's internal
simulation is not sharded. Current Xonotic and author worktrees stay protected. Markdown
documentation is committed/pushed directly to main; code goes through a PR.

## Delivered

- `DigitalHeat.Orchestration`: one atomic heat/CPU/RAM/slot/whole-GPU admission engine. Optional
  GPU VRAM, capabilities, locality, per-peer latency and node-local storage constraints fail closed.
- `SessionPlacement` delegates to this engine through a compatibility adapter. Public gaming
  contracts stay unchanged; legacy slots do not imply new CPU/RAM/GPU isolation guarantees.
- Single-host/operator per-fleet OS lock, flushed local journal, immutable attempt/spec identity,
  labelled runtime discovery and positive observations before resource release. Uncertain missing
  starts block admission and never blindly relaunch. Confirmed created containers can continue or
  cancel, with freshness/resource checks before recovery start.
- Docker contexts, enforced CPU/RAM/shared-memory limits, cached images only, non-root workloads,
  explicit device/volume bindings, loopback-only ports and independent readiness probes.
- Owner-authorized batch preemption only when pausing can help. Paused RAM/GPU/slots remain held.
  Batch heat control uses shared `GreedyHeatScheduler`, `SchedulerController.Apply`, `IExecutor`
  and `DockerJobExecutor`, as the yearly/live path does. Games never enter the pause path.
- Cold backup of the released attempt's own claimed volume; restore into a new fleet-namespaced
  volume, preserving private application files/ownership. No replicated storage or live snapshot.
- CLI for sourced offline planning and explicit start/status/heat/pause/resume/stop/backup/restore.
  Historical examples use existing Swiss scenario data; current game GPU is externally allocated.

## Verification and review

29 orchestration and 36 gaming tests pass. Shared Core (46) and Network (20) regressions pass;
Release solution builds cleanly. Isolated Docker smoke verifies actual batch pause/resume,
idempotent starts, CPU re-admission while RAM remains held, protected interactive capacity,
positive termination/release and private-file cold backup/restore. Exact random test fleets and
temporary volumes only; no user images, volumes, game or desktop were removed/restarted.

Independent implementation review findings applied: preflight before preemption, no pointless
pause for a retained-memory/GPU deficit, recovery/cancellation of created attempts, and backup
authorization tied to the attempt's claimed storage. New regression tests cover those cases.

## Explicit limits and handoff

This is an operator-driven prototype, not continuous telemetry/control or HA. Operators supply
fresh, trusted physical/external-allocation inventory and invoke heat updates. The file lock is
for one host/OS operator, not cross-host fencing. Current game remains external; the managed
Xonotic template is not a newly gameplay-tested stream and requires private env configuration
and a genuinely free eligible GPU. NVIDIA/remote engines are not tested on the Intel laptop.

Minecraft, automatic relocation, checkpointing, replication, GPU sharing and controller HA are
outside this approved first milestone. No new package, scored house formula or economic model.

Setup, commands, calculation path, architecture graph and code map:
[universal workloads](../universal-workloads.md).
