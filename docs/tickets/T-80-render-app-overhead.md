# T-80 — Measure and reduce C# render-app overhead

Owner: Nikita / Claude. Status: done (merged to main from claude/beautiful-allen-ukjq15).
Scope: the C# render lab app (`RenderNodeHost`, `RenderSession`, `/ui`), not Blender (T-79).
No change to the heat-first policy, five roles, GPU budgets, concurrency or quality.

Done: measured where the app spends time per run in a Linux replica of the five-node lab;
peer `/status` takes an optional `job_prefix` run filter that the render owner uses, so its
polling and `/ui/status` stay O(current run). A 250 ms poll trial was measured and reverted.
Write-up: `docs/jobs/render-app-overhead.md`.

Done (follow-up, user decision 2026-11-22, ADR 0069): GPU weights 1.5/1/0.5 are heat NEED per
run. Each run's frames are split by weight (48 -> 24/16/8) and each GPU node's existing
`DemandKwh` is its share of the current run, so the unchanged heat-first placement follows the need
and repeated runs no longer stall within the hour.

Open:
- Host CPU still grows with runs per lab session (shared Orchestration journals are saved and
  deep-copied in full; reconcile re-reads every past attempt). Needs an ADR with the
  Orchestration owners. Workaround: relaunch the lab (each launch starts with empty state).
