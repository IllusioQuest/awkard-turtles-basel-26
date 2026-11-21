# T-77 — Measure and improve renderer runtime

Owner: Nikita/Codex. Status: in progress. Branch: perf/T-77-render-runtime.
Depends on GPU shades/budgets PR #68. User approved staged profiling, warm reuse
and shared-GPU concurrency investigation. Keep five roles, approved budgets,
heat-first admission, quality settings and prior results. Ask before changing
default GPU concurrency. Minecraft is excluded.

First measure native scene verification/load/setup/render and workflow overhead,
then remove demonstrated unnecessary work. Shader compilation remains included
in the Blender render call unless a reliable separate measurement is available.
Record reproducible evidence; do not infer speedups from unmatched single runs.
