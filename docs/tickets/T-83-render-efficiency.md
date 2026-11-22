# T-83 — Renderer efficiency and repeat-run diagnosis

Owner: Nikita/Codex. Status: in progress. Branch: perf/T-83-render-efficiency.
User requests faster rendering. Preserve heat-first placement, per-run24/16/8
need, three concurrent GPU workers, quality and Minecraft. Coordinate with T-79
by running its existing warm/fresh benchmark on the actual laptop, not inventing
a second tool. No change to execution mode before measurement/user decision.
Investigate repeat process failure and retain logs per warm-process generation.
Proposed read-only heat-summary interface awaits user approval and ADR review;
no ownership reconciliation skips or automatic retries of failed jobs.
