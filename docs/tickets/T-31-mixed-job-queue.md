# T-31: Mixed queue: finite Type 1 jobs plus unlimited Type 2 filler in one year run

- **Tier:** 2 Pitch (1 Draft if the team wants the two-type balance in the draft)  ·  **Area:** Core / year simulation
- **Depends on:** T-05 (decision 0010), T-26 (Basel scenario), decision 0018
- **Suggested owner:** Jona / Codex (owner of the queue)  ·  **Owner:** Jona / Codex  ·  **Status:** in progress

Decision 0020 supersedes medical transcription as the pitch's Type 1 business job: Type 1 is
public-document batch LLM, while Type 2 is research simulation. A sourced finite LLM arrival
count is still missing. The user approved the existing Basel transcription demand **only as a
technical queue fixture** on 2026-11-21; it must not become a home-site or pitch scenario.

## Goal

One scenario with finite Type 1 job types (sourced arrivals and deadlines, e.g.
`config/job-scenarios/transcription-basel.json`) served first, and Type 2 filling the rest of the
heat-led target as "unlimited jobs" at the pinned price. Results are reported per type.

## Tasks

- [x] Scenario format: `mixed` accepts finite job types and one filler with sourced prices and sold-capacity share (decision 0024).
- [x] Priority: Type 1 before Type 2 within the heat target; Type 2 cannot use the dump policy.
- [x] Results per type: server-hours, income, missed deadlines (Type 1 only), plus the case key figures for the whole house.
- [x] Cross-check: with zero Type 1 arrivals and fully sold filler, the physical result equals the unlimited run (58.0 % coverage, 0 kWh dumped, 22,661 server-hours, break-even 0.226).
- [x] Tests for priority, constrained filler, export and the reference cross-check.

## Done when

- One documented command produces the two-type yearly balance; the unlimited comparison is unchanged.

Implementation and a worked calculation are in `docs/model/mixed-job-queue.md`. The technical
fixture is runnable now; it is **not** a demand forecast or the decision-0020 pitch scenario.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
