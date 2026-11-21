# T-05: Jobs, queue and deadlines in the year simulation (must-have 3)

- **Tier:** 1 Draft  ·  **Area:** Core
- **Depends on:** T-01
- **Suggested owner:** Codex  ·  **Owner:** Jona / Codex  ·  **Status:** in progress (T-01 input pending)

## Goal

Make the year balance job-aware so it can report missed deadlines in % of jobs.

## Tasks

- [ ] Add a job queue to the year simulation: arrivals per day per job type (T-01), work done per server-hour, deadlines.
- [ ] Define what happens to a job whose heat is not needed: wait (paused), run and dump heat, or move (later, T-10/T-12). Make it a parameter.
- [ ] Report missed deadlines (% of jobs), work done, backlog; keep the existing figures unchanged when jobs are "unlimited".
- [ ] Tests: a job with a deadline shorter than the heat-free gap is counted as missed; energy balance still closes.
- [ ] Keep the explorer cross-check (58.0 %, 22,661 server-hours, 0.2261 CHF/h) for the unlimited-jobs case.

## Done when

- `YearRun` summary contains missed deadlines and job counts; tests pass.

## Read first

- `src/DigitalHeat.Core/YearSimulation.cs`
- `docs/case/challenge.md (must-have 3)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
