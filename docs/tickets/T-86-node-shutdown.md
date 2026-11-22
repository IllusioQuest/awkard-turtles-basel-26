# T-86 — Whole-node shutdown and confirmed frame retry

Owner: Nikita/Codex. Status: in review; PR #77. Branch: feat/T-86-node-shutdown.
Depends on T-85 bookkeeping PR76. User explicitly chose hard power-off of GPU3
including its controller, with unfinished frames rescheduled. Approved local-lab
process-tree fencing, shared completed-frame storage and retry only after proof.
Keep completed frames, heat-first policy and other UI unchanged except the button.
No automatic takeover after an unconfirmed remote outage. Remote power adapter
is outside this local-lab milestone. ADR0072 review before implementation.

Implementation6119140 complete.148 tests passed. Real shutdown of GPU3 during
frame5: verified controller/renderer exit, original deadline retained, completed
frame reused,48-frame MP4 completed28/19/1. Second run with node off recovered
through origin restart and completed29/19. All five nodes restored afterward.
Fresh limited-context review applied; details and reproducible steps in
[docs/jobs/node-shutdown.md](../jobs/node-shutdown.md). Docs pushed directly to main;
feature code awaits human merge because shared mesh/lifecycle interfaces change.
