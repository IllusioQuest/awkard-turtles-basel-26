# T-05: Jobs, queue and deadlines in the year simulation (must-have 3)

- **Tier:** 1 Draft  ·  **Area:** Core
- **Depends on:** T-01
- **Suggested owner:** Codex  ·  **Owner:** Jona / Codex  ·  **Status:** done (PR #6, decision 0010); sourced finite scenario: T-26

## Goal

Make the year balance job-aware so it can report missed deadlines in % of jobs.

## Tasks

- [x] Add a job queue to the year simulation: arrivals per day per job type (T-01), work done per server-hour, deadlines. The input boundary is ready; sourced T-01 data is pending.
- [x] Define what happens to a job whose heat is not needed: wait (paused) or run and dump heat, selected by scenario; move remains T-10/T-12.
- [x] Report missed deadlines (% of due jobs), work done and backlog; keep the existing figures unchanged when jobs are "unlimited".
- [x] Tests: a job with a deadline shorter than the heat-free gap is counted as missed; energy balance still closes.
- [x] Keep the explorer cross-check (58.0 %, 22,661 server-hours, 0.2261 CHF/h) for the unlimited-jobs case.

## Done when

- `YearRun` summary contains missed deadlines and job counts; tests pass.

## Read first

- `src/DigitalHeat.Core/YearSimulation.cs`
- `docs/case/challenge.md (must-have 3)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Integration status

The queue code and `YearRun` output are ready in PR #6 on `feat/T-05-job-queue`; see
`docs/model/job-queue.md` and decision 0010. The reference run remains explicitly unlimited,
so `MissedDeadlinePercent` is `null`. A scored finite result needs T-01/T-02 to provide sourced
arrival counts, work-unit throughput and deadlines. No such values were invented in this ticket.
