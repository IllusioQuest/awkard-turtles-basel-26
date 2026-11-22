# 0071 — Active-job snapshots and snapshot-free deadline enforcement

Status: proposed. T-85. User approved reducing scheduler bookkeeping while
preserving decisions for identical input/state and leaving UI unchanged.

Coordinator.ActiveAttempts() filters out only released records under its lock
BEFORE the existing detached JSON copy. Preserve order and all remaining phases.
Use it in MeshPeer.Tick terminal/created scans and ApplyLocalHeat running/paused
scans. Never use it for retained heat, durable history or ownership reconciliation.

Extract deadline mutation logic into one private locked core. Keep the existing
EnforceDeadlines() returning full history with exactly its existing behavior;
add a void EnforceDeadlinePolicies() calling that same core without constructing
a discarded history snapshot. MeshPeer.Tick uses the void method at the same
points and same order. No batching, caching, new timing, early deadline skips,
reconciliation skips or changed persistence. Existing runtime/deadline checks
and failed stop uncertainty remain intact.

Verify returned active snapshots equal full history filtered by phase and cannot
mutate owned records. Compare snapshot and void deadline paths for running,
paused, protected, failed-stop and completed jobs. Measure allocation difference
on released histories. Run existing mesh/deadline tests and an actual renderer.

## Review
Pending fresh-context review.
