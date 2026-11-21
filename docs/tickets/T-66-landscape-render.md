# T-66 — Landscape renderer and node timing comparison

Owner: Nikita / Codex. Status: in progress. Branch: feat/T-66-landscape-render.
Depends on PR #52. ADR 0052. User approved renderer, timing comparison, pause/resume and device stats.

Build a small browser app backed by the existing heat-first mesh. Render the same seeded
48-frame landscape at 640x360/32 samples on one eligible node, then multiple. Probe actual
Cycles GPU availability, prefer verified GPU and explicitly report CPU fallback. No game changes.
Show real progress, frame preview, timing and resource telemetry with unavailable values labelled.
Keep docs on main. Verify pause/resume, frame completeness and comparison math; record hardware
limits and benchmark settings. No fabricated GPU acceleration or physical-node speedup claim.

Implementation: PR #54, feat/T-66-landscape-render. Ready for review; 76 orchestration tests, clean WorkloadRun build, real two-frame-per-leg/pause verification and visual preview pass. Full 48-frame benchmark and real GPU execution not measured. See docs/jobs/landscape-renderer.md and docs/performance/landscape-render-verification-2026-11-21.json. Do not mark done before merge.

Reported timeout repaired and node split reverified: 84 tests pass; status stays responsive during rendering and paused-peer warning; user-approved 80/20 frame allocation and failed-comparison cancellation implemented. Actual follow-up uses hotel then hotel/spa. See docs/performance/render-followup-node-split-2026-11-21.json. PR #54 updated; GPU hardware test/full 48-frame run remain separate.
