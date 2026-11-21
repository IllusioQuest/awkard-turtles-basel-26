# T-48: Minute-level live batch deadlines

Owner: Nikita / Codex. Status: implemented/verified; preparing PR. Branch: feat/T-48-live-job-deadlines. ADR 0037.
User rejected the one-hour simulation variant and approved minute-level live enforcement:
five minutes for bounded small transcription/summary; ten minutes for finite <=320-task research.
Count queue/startup, refuse expired waiting work, mark late, stop only if lifecycle allows.
Protect active games, preserve ownership/journal safety, and keep hourly simulation unchanged.
Use measured local runtimes as context, MLPerf/Alibaba/provider data with explicit provenance;
do not infer deadlines from duration, recovered heat from electrical power or prices across hardware.
Done: compatible contract, profile application, coordinator/owner-loop enforcement, tests and scoped
Docker proof, documented limitations, code PR and documentation pushed directly to main.

Milestone: clean build, 42 orchestration tests and scoped 15-second actual Docker expiry/cleanup
proof pass. Legacy untimed hash fixture unchanged. No game stopped. See
[live deadline guide](../jobs/live-deadlines.md) and [source boundaries](../research/workload-evidence.md).
Targets remain experimental; owner-loop and UTC clock/polling limits are explicit. Code PR pending.
