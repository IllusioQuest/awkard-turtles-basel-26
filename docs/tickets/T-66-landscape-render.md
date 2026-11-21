# T-66 — Landscape renderer and node timing comparison

Owner: Nikita / Codex. Status: done. Branch: feat/T-66-landscape-render.
Depends on PR #52. ADR 0052. User approved renderer, timing comparison, pause/resume and device stats.

Build a small browser app backed by the existing heat-first mesh. Render the same seeded
48-frame landscape at 640x360/32 samples on one eligible node, then multiple. Probe actual
Cycles GPU availability, prefer verified GPU and explicitly report CPU fallback. No game changes.
Show real progress, frame preview, timing and resource telemetry with unavailable values labelled.
Keep docs on main. Verify pause/resume, frame completeness and comparison math; record hardware
limits and benchmark settings. No fabricated GPU acceleration or physical-node speedup claim.

Implementation: PR #54, feat/T-66-landscape-render. Ready for review; 76 orchestration tests, clean WorkloadRun build, real two-frame-per-leg/pause verification and visual preview pass. Full 48-frame benchmark and real GPU execution not measured. See docs/jobs/landscape-renderer.md and docs/performance/landscape-render-verification-2026-11-21.json. Do not mark done before merge.

Reported timeout repaired and node split reverified: 84 tests pass; status stays responsive during rendering and paused-peer warning; user-approved 80/20 frame allocation and failed-comparison cancellation implemented. Actual follow-up uses hotel then hotel/spa. See docs/performance/render-followup-node-split-2026-11-21.json. PR #54 updated; GPU hardware test/full 48-frame run remain separate.

GPU follow-up in progress: correct probe bindings/backend diagnostics and error exit codes; isolated user-approved Eevee capability experiment under ADR 0055. Existing active comparison and games preserved.

Eevee capability tests did not pass (OpenGL 4.1 limit; Vulkan missing features). Evidence in docs/performance/render-gpu-compatibility-2026-11-21.json; diagnostic patch 931912c pushed, not deployed over active comparison. Awaiting hardware/compatibility path choice.

Current-upstream check completed: Blender 5.3.0 also fails (missing dual-source blending); Mesa 26.3.0 source explicitly disables that feature. No successful GPU render. Compatible worker path is the remaining design choice; evidence updated.

Linux worker packaging prepared under reviewed ADR 0057 after user selected this path and confirmed no GPU host. Same scheduler/executor, current pinned Blender 5.3, shared render-probe CLI, loopback/local-engine defaults and remote UI disabled. Deployment/hardware execution remains unverified. See docs/jobs/linux-render-worker.md.

Linux worker checks complete: image build, one real CPU frame, Linux archive/apphost smoke, negative GPU probes/cleanup, 88 tests plus 4 focused regressions. See docs/performance/linux-render-worker-2026-11-21.json. GPU execution/cross-host deployment awaits hardware; ticket remains in progress.

Native follow-up in progress under ADR 0058: Windows driver successfully rendered with Intel Iris Xe (Blender 5.3/OpenGL). User approved native adapter + frame-boundary pause + CPU preparation/GPU Eevee stages, then explicitly retired single-node/comparison execution. One shared multi-node workflow, common scheduler and configuration-only hardware details. Isolated real pipeline test is running; no new GPU scaling claim.

Native integration verified (00c6772, PR #54): real CPU preparation on cpu + GPU frames on gpu using Intel Iris Xe/OpenGL; frame-boundary hold/resume, queued cancellation, preserved four-frame preview and restart checks pass. Single-node/comparison source/UI retired at user request. 98 orchestration + 51 Core tests pass, shared Linux CPU builder passes. Current instructions/evidence: docs/jobs/native-renderer.md and docs/performance/native-renderer-2026-11-21.json. Code still in PR; do not mark merged/done yet.


Presentation cleanup: user keeps Minecraft and retires other game labs. PR #54 / `5d6499d` removes their executable setup, retains shared mesh/runtime and Minecraft, and shares the frontend stylesheet with the renderer (header held badge removed). Thirteen scoped containers removed without deleting volumes; only Minecraft server + Node A client remain running. 57 gaming + 98 orchestration tests and power math pass; paused native run and two frames preserved. See `docs/jobs/presentation-cleanup.md` and `docs/performance/presentation-cleanup-2026-11-21.json`. T-66 remains in progress until code PR merges.


PR #54 merged into main at `72e602f`. Multi-node-only native GPU scope and retirement of game labs are complete. Node-map/timing explanation follow-up is tracked separately as T-69 / PR #59. Earlier progress notes above are historical.
