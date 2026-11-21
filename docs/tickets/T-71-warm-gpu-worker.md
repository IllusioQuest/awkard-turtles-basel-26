# T-71 — Keep the existing GPU renderer warm between frame jobs

Owner: Nikita / Codex. Status: in review. Branch: `feat/T-71-warm-gpu-worker`.
Depends on native renderer PR #54 and network UI PR #59. User chose speed first,
extra logical workers later. ADR 0063 proposed; fresh review go with changes; applied.

Own two-frame diagnostic: one Blender process rendered frame 1 in 8.2570s and
frame 2 in 1.0148s, same 640x360/32-sample prepared scene and Iris Xe/OpenGL.
This suggests initialization reuse is useful; it is not a full-run speedup claim.
Evidence: `docs/performance/warm-frame-probe-2026-11-21.json`.

Implement opt-in warm native GPU execution without changing per-frame jobs,
heat-first admission or pause between frames. Preserve the current paused run
and outputs; test on an isolated lab first. Verify real frames, pool ownership,
activation completion, stop/recovery and CPU/cold-path compatibility. No extra
physical GPU, simulated speedup or extra logical workers in this ticket.


Implemented in PR #62 / `180de77`, built on PR #59. 106 orchestration tests pass; real scheduled warm frames/pause, owner recovery, stop/rerun, idle loop and native Linux packaging checked. Tested app at localhost:18920/ui, old 14-frame paused run preserved at 18910. Five-frame warm portion: 7.5053s active, mean render 1.0577s; no full-animation/SLA claim. See `docs/jobs/warm-renderer.md` and evidence JSON. No extra workers; keep the heat-first path and activation/active-frame fencing. Remains in review until merge.
