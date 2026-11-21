# T-69 — Explain render timing and visualize node contributions

Owner: Nikita / Codex. Status: in review. Branch: `fix/T-69-render-node-map`.
PR: #59. User requested an explanation of slow rendering and a live node visualization.

Scope: use existing status data; group logical nodes by physical host, show actual
CPU/GPU contributions and completed/executing/pending frames, expose measured
per-frame timing. Preserve heat-first admission, current paused run and Minecraft.

Done when the map agrees with reported execution, shared hardware is explicit,
frame-time math and limits are documented, and UI is visually checked. These
checks passed for the real paused five-frame run. Active/stale labels were
inspected without launching/disrupting jobs. No new runtime/API fields.
Docs: `docs/jobs/render-node-visualization.md`; evidence:
`docs/performance/render-node-map-2026-11-21.json`. Code awaiting PR merge.


User-expanded scope (757a56b): two separate GPU server profiles and all-node network map, with additive read-only connection status under reviewed ADR 0061. Two servers are planned only: no hardware/address/access supplied. 102 orchestration tests, native Linux packaging/archive checks and live paused-run UI/recovery pass. No remote execution or scaling claim. See `docs/jobs/gpu-server-network.md`. Code remains PR #59, in review; connecting physical hosts is pending external hardware/access.
