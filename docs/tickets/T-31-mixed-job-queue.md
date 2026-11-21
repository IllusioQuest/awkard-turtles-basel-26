# T-31: Mixed queue: finite Type 1 jobs plus unlimited Type 2 filler in one year run

- **Tier:** 2 Pitch (1 Draft if the team wants the two-type balance in the draft)  ·  **Area:** Core / year simulation
- **Depends on:** T-05 (decision 0010), T-26 (Basel scenario), decision 0018
- **Suggested owner:** Jona / Codex (owner of the queue)  ·  **Owner:**  ·  **Status:** open

## Goal

One scenario with finite Type 1 job types (sourced arrivals and deadlines, e.g.
`config/job-scenarios/transcription-basel.json`) served first, and Type 2 filling the rest of the
heat-led target as "unlimited jobs" at the pinned price. Results are reported per type.

## Tasks

- [ ] Scenario format: allow finite job types and one unlimited filler type together (extension of decision 0010; decision record if the interface changes).
- [ ] Priority: Type 1 before Type 2 within the heat target; Type 2 never forces dumped heat.
- [ ] Results per type: server-hours, income, missed deadlines (Type 1 only), plus the case key figures for the whole house.
- [ ] Cross-check: with zero Type 1 arrivals the result equals the unlimited run (58.0 % coverage, 0 kWh dumped, 22,661 server-hours, break-even 0.226).
- [ ] Tests for the priority rule and the cross-check.

## Done when

- One documented command produces the two-type yearly balance; the unlimited comparison is unchanged.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
