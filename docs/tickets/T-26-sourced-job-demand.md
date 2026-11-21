# T-26: Sourced job demand scenario and computing price

- **Tier:** 1 Draft  ·  **Area:** Data / model inputs
- **Depends on:** T-02 (measured throughput), T-05 (finite job queue)
- **Suggested owner:** Claude  ·  **Owner:** Team lead / Claude  ·  **Status:** in progress (two research subagents)

## Goal

Replace "unlimited jobs" and the example computing price (0.30 CHF per server-hour) with sourced
values, so that "missed deadlines in % of jobs" and the computing income are real figures (must-haves 1 and 3).

## Why now

T-02 measured about 436 audio hours per 1 kW server-hour. Demand, not capacity, limits the business;
"unlimited jobs" hides that.

## Tasks

- [ ] Research a sourced transcription demand scenario: job size (audio minutes per job), deadline
      (turnaround), arrivals per day for a realistic customer base; low/high range.
      → `config/job-scenarios/transcription-basel.json`, `docs/research/job-demand.md` (branch `data/job-demand-scenario`).
- [ ] Research and recommend how to price computing income (per server-hour vs. per audio hour with demand cap),
      with value and sensitivity range → `docs/research/computing-price.md` (branch `data/computing-price`).
- [ ] Team lead decides; then hand the values to T-06 (Jona) for the scored figures and update `config/reference-house.json` via T-06.

## Done when

- Missed deadlines and computing income in the reference-house results come from sourced inputs; the default scenario choice is agreed with the team.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
