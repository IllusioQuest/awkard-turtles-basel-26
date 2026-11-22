# T-83 — Renderer efficiency and repeat-run diagnosis

Owner: Nikita/Codex. Status: in review; PR #73. Branch: perf/T-83-render-efficiency.

Implemented user-approved read-only compact heat summary and scalar committed
heat query (ADR0070 reviewed with changes applied). Same phase/order/value rules,
detached interval arrays and unchanged ownership reconciliation. Per-generation
warm stdout/stderr/exit evidence prevents later processes overwriting diagnosis.
No changes to heat-first placement,24/16/8 need,three concurrent GPU workers,
quality or Minecraft.132tests pass,16native tests rechecked after log change.

50queries over500attempts allocated230.63MB before versus2.61MB compact and0B
scalar. Two distinct real48-frame pipelines passed24/16/8 and MP4 in74.6994s
and78.2986s. These are unmatched end-to-end observations, not a guaranteed speedup.
Existing T-79 benchmark ran on Iris Xe: warm later median1.225s; fresh first-round
median12.548s; round2fresh crashedC0000005 in igxelpicd64.dll. Preserve partial
reports; do not claim balanced benchmark completion or solved driver reliability.

Write-up: docs/jobs/render-efficiency.md. Evidence: docs/performance/render-efficiency-2026-11-22.json.
