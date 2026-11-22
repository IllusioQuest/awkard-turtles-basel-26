# T-80 — Measure and reduce C# render-app overhead

Owner: Nikita / Claude. Status: done (merged to main from claude/beautiful-allen-ukjq15).
Scope: the C# render lab app (`RenderNodeHost`, `RenderSession`, `/ui`), not Blender (T-79).
No change to the heat-first policy, five roles, GPU budgets, concurrency or quality.

Done: measured where the app spends time per run in a Linux replica of the five-node lab;
peer `/status` takes an optional `job_prefix` run filter that the render owner uses, so its
polling and `/ui/status` stay O(current run). A 250 ms poll trial was measured and reverted.
Write-up: `docs/jobs/render-app-overhead.md`.

Open (need a decision, not started):
- Heat budget: each frame commits 0.8 kWh for the whole clock hour, so GPU 3 drops out after
  about 4 runs per hour and the lab stalls after about 8. Team/user decision.
- Host CPU still grows with runs per lab session (shared Orchestration journals are saved and
  deep-copied in full; reconcile re-reads every past attempt). Needs an ADR with the
  Orchestration owners. Workaround: relaunch the lab (each launch starts with empty state).
