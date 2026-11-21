# T-49: Improve admission lookup and copy costs

Owner: Nikita / Codex. Status: queued after T-48. Planned branch: feat/T-49-indexed-admission.
ADR 0038; user approved T-47 improvements. Index validated topology and aggregate per-decision
usage; replace selected JSON copies with safe typed deep copies. Preserve all resource, locality,
freshness, lifecycle, deadline and ownership invariants. Differential/regression tests plus identical
T-47 before/after timings and allocation evidence. No journal durability shortcuts.
