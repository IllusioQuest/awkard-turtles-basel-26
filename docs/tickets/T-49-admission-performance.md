# T-49: Improve admission lookup and copy costs

Owner: Nikita / Codex. Status: in review, [PR #38](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/38), based on PR #37. Branch: feat/T-49-indexed-admission.
ADR 0038; user approved T-47 improvements. Index validated topology and aggregate per-decision
usage; replace selected JSON copies with safe typed deep copies. Preserve all resource, locality,
freshness, lifecycle, deadline and ownership invariants. Differential/regression tests plus identical
T-47 before/after timings and allocation evidence. No journal durability shortcuts.

Verified: 46 orchestration +36 gaming tests; 1,600 seeded oracle comparisons; adjacent CPU/heat
boundaries; real isolated pause/resume/admission/release/backup smoke. Identical 62-case probe:
1,024 shared-domain median ~106.47 ->0.8912 ms; allocation ~73.20 ->1.09 MiB per decision.
See [results and raw evidence](../performance/indexed-admission.md). Observed active-laptop
comparison, not an SLA. No active game touched. Merge #37 before this stacked PR.
