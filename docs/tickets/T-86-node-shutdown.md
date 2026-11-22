# T-86 — Whole-node shutdown and confirmed frame retry

Owner: Nikita/Codex. Status: in progress. Branch: feat/T-86-node-shutdown.
Depends on T-85 bookkeeping PR76. User explicitly chose hard power-off of GPU3
including its controller, with unfinished frames rescheduled. Approved local-lab
process-tree fencing, shared completed-frame storage and retry only after proof.
Keep completed frames, heat-first policy and other UI unchanged except the button.
No automatic takeover after an unconfirmed remote outage. Remote power adapter
is outside this local-lab milestone. ADR0072 review before implementation.
