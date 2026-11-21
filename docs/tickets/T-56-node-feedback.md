# T-56 — Event-driven scheduler updates from node feedback

- Owner: Nikita / Codex
- Status: implementation complete; PR #45 in review (not merged)
- Branch: feat/T-56-node-feedback, from latest main
- Decision: 0044, user approved local JSON adapter and shared C# contract.

Implement fresh ordered node full/available and authoritative site-heat reports, atomic
local publisher/inbox, bounded event wake plus fallback timer, and WorkloadRun owner mode.
Reuse shared placement, heat model, durable coordinator, deadline and batch-first rules.
Preserve interactive jobs, hourly scored model, physical resource ownership and latency
requirements. No HTTP service or changes to T-54 LiveGrid, the read-only monitor or games.

Done: reviewed decision; meaningful deterministic tests plus local event experiment;
documented reproducible commands, limitations and timing; code PR and docs pushed on main.


Validation: 58 orchestration tests, 46 Core tests, clean WorkloadRun build, actual atomic CLI publication and filesystem early wake. Pure admission at 1024 shared-domain nodes: 0.9381 ms median / 1.7863 ms p95. Docs/jobs/node-feedback.md contains method, reproduction and limits. User confirmed heat priority and anytime Updates; conservative commitments survive pause/release/restart. No running game adopted or restarted. Remote agents/sensors and real-game measured-heat startup remain outside this ticket.
