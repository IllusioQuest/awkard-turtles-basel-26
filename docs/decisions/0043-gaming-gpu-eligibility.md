# GPU eligibility with unchanged heat-aware admission

- Status: proposed; user approved direction on 2026-11-21; review pending.
- Ticket: T-55. ADR 0042 and T-54 are reserved by the four-node demo branch.

## Decision

Reuse WorkloadPlacement's existing GPU capability, shared physical resource domain,
external allocation, latency, locality, freshness and heat constraints. Among eligible
nodes retain the greatest remaining useful heat capacity ranking and deterministic ties.
No second scheduler, remote GPU aggregation or new heat formula. Protect active games;
moving a running session requires explicit reconnect approval. Batch pause does not free
GPU memory/ownership while its process retains them.

Keep graphics probing and Docker operations outside admission. Probe unused CS node E
only, with the existing Intel WSL D3D12 device and read-only driver-library mappings.
Preserve its home volume, resource limits, isolation and loopback browser endpoint.
Do not restart D or Minecraft. Verify no game process before recreating E. Hardware GL
success is not proof of 32-bit Wine compatibility, hardware encoding or playable FPS.
Do not publish GPU-ready capability from device existence or a software-renderer result.

The laptop's containers share one Intel Iris Xe GPU, not a GPU per container. Existing
A/C activity must be represented as external allocations before automatic GPU admission.
No silent CPU fallback for a GPU-required request. Unknown physical identity, occupancy,
renderer compatibility or fresh telemetry blocks automatic startup. CS recoverable heat
is unmeasured: synthetic tests do not authorize real heat-based startup. Keep that gate.

## Validation

Test GPU filtering before heat ranking, GPU contention without CPU fallback, and active
session protection against changed heat. Existing differential admission tests retain
latency/locality/freshness coverage. Run the unchanged fixed scheduler diagnostic for
timing; report environment/load, source revision, median and p95 separately from stream FPS.
No probe, network request or subprocess may run inside TryReserve. Do not alter scored
reference-house parameters. Production scheduler remains unchanged in this first step.

## Review

Pending one fresh limited-context review.
