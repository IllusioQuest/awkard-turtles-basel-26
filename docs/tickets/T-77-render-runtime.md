# T-77 — Measure and improve renderer runtime

Owner: Nikita/Codex. Status: done; PR #69 merged, timeout follow-up included on main. Branch: perf/T-77-render-runtime.
Depends on GPU shades/budgets PR #68. User approved staged profiling, warm reuse
and shared-GPU concurrency investigation. Keep five roles, approved budgets,
heat-first admission, quality settings and prior results. Ask before changing
default GPU concurrency. Minecraft is excluded.

First measure native scene verification/load/setup/render and workflow overhead,
then remove demonstrated unnecessary work. Shader compilation remains included
in the Blender render call unless a reliable separate measurement is available.
Record reproducible evidence; do not infer speedups from unmatched single runs.

Implemented in593fe8b/0abf3e1;117 tests pass. Two actual48-frame MP4 runs
complete, exact scene reuse verified. Smaller concurrency samples and severe
three-worker outliers documented; user explicitly keeps3 simultaneous renders.
See docs/jobs/render-runtime.md and performance evidence. Prior results retained.
