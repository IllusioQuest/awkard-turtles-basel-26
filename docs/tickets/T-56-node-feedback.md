# T-56 — Event-driven scheduler updates from node feedback

- Owner: Nikita / Codex
- Status: in progress
- Branch: feat/T-56-node-feedback, from latest main
- Decision: 0044, user approved local JSON adapter and shared C# contract.

Implement fresh ordered node full/available and authoritative site-heat reports, atomic
local publisher/inbox, bounded event wake plus fallback timer, and WorkloadRun owner mode.
Reuse shared placement, heat model, durable coordinator, deadline and batch-first rules.
Preserve interactive jobs, hourly scored model, physical resource ownership and latency
requirements. No HTTP service or changes to T-54 LiveGrid, the read-only monitor or games.

Done: reviewed decision; meaningful deterministic tests plus local event experiment;
documented reproducible commands, limitations and timing; code PR and docs pushed on main.
