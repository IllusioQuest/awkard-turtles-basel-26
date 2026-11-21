# T-66 — Landscape renderer and node timing comparison

Owner: Nikita / Codex. Status: in progress. Branch: feat/T-66-landscape-render.
Depends on PR #52. ADR 0052. User approved renderer, timing comparison, pause/resume and device stats.

Build a small browser app backed by the existing heat-first mesh. Render the same seeded
48-frame landscape at 640x360/32 samples on one eligible node, then multiple. Probe actual
Cycles GPU availability, prefer verified GPU and explicitly report CPU fallback. No game changes.
Show real progress, frame preview, timing and resource telemetry with unavailable values labelled.
Keep docs on main. Verify pause/resume, frame completeness and comparison math; record hardware
limits and benchmark settings. No fabricated GPU acceleration or physical-node speedup claim.
