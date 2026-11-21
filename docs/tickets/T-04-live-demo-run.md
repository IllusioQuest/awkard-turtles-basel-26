# T-04: LiveRun: one day replayed with the real job (demo for must-have 2)

- **Tier:** 1 Draft  ·  **Area:** Runtime / demo
- **Depends on:** T-03
- **Suggested owner:** same person as T-03  ·  **Owner:** Jona / Codex  ·  **Status:** done (PR #3: DigitalHeat.LiveRun day replay)

## Goal

A console program that replays a January and a July day and lets the scheduler pause/resume the real job live.

## Tasks

- [ ] `DigitalHeat.LiveRun`: read the year data, pick a date (2025-01-15, 2025-07-15), 1 simulated hour = N seconds (configurable).
- [ ] Each step: build the `HeatSignal`, call `SchedulerController.Apply` with the `DockerJobExecutor`, log time, demand, tank, target load, achieved load, job progress.
- [ ] Manual override (flag or key) that forces "tank full" so the pause is visible on stage.
- [ ] Write a demo script (`docs/demo/live-run.md`): commands, what the audience sees, fallback (recorded run).

## Done when

- Running the command shows a real job slowing/pausing/resuming in sync with the heat signal; demo script exists.

## Read first

- `docs/case/key-figures.md (pitch)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Must run offline on a laptop (AGENTS.md §4.3).
