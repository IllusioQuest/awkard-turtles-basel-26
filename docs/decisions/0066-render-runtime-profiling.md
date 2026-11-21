# 0066 — Renderer runtime profiling and bounded submission pacing

Status: proposed. T-77. User approved profiling, warm reuse and concurrency
experiments; default shared-GPU concurrency remains three pending a decision.

Add optional timing fields to existing generic native artifact metadata:
scene verification/load/setup, render-call duration, executor dispatch-to-script
latency and worker duration. Shader compilation stays inside render-call time.
Cache only released successful immutable artifact metadata; rebuild after restart.
Submit at most one new frame per reported GPU peer each workflow pass instead
of one frame per second globally. Same durable IDs, uncertainty retry, heat-first
MeshPeer ranking/admission and executor ownership. No new scheduler.

Controlled benchmark uses existing pinned-node frame API on an idle lab, keeping
heat admission active. Pinning is measurement-only, not a production strategy.
Save partial/failure evidence. No default concurrency or image-quality change.

Observed Windows AccessDenied now also occurs in native ownership WriteJson,
not just mesh/coordinator journals. Reuse the existing bounded JournalReplace
helper via internal friend assembly access; no new public API or longer retry.
Preserve flushed temp, ownership and persistent-failure handling. Before stopping
an uncertain lab, verify warm assignments idle and exact recorded process identity;
retain all outputs/journals. Never clear uncertainty to restart the same jobs.

## Review
Go with changes: checkpoint benchmark manifests/IDs/results/failures; reject outstanding prior work; record actual warm-scene reuse rather than assume it; capture render duration immediately on return. Applied all three. Dispatch timing uses worker-local wall clock, includes activation/settings loading and is not scheduler queue latency. Shader work and PNG writing remain inside the Blender call.
