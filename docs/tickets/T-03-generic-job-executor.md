# T-03: Generic real-job executor behind IExecutor (must-have 2)

- **Tier:** 1 Draft  ·  **Area:** Runtime
- **Depends on:** T-02 (job image)
- **Suggested owner:** Nikita or Codex (prompt prepared in chat)  ·  **Owner:** Jona / Codex  ·  **Status:** done (PR #3, decision 0009)

## Goal

Control ANY real job (no cooperation needed) from the scheduler's target load: freeze, throttle, optionally checkpoint/restart.

## Tasks

- [ ] Read ADR 0005 (C# core) and Nikita's ADR 0007 (`0007-whole-container-pause-recovery.md`) (whole-container pause) and `docs/webhosting-runtime.md`; reuse the measured pause/resume behaviour, do not build a second runtime.
- [ ] Implement `DockerJobExecutor : IExecutor`: target 0 = `docker pause`, 0<x<1 = `docker update --cpus`, 1 = full; return the achieved load (from `docker stats`). Docker calls behind one small `IContainerControl` interface.
- [ ] Optional level for jobs that declare it: stop at checkpoint and restart without repeating finished work units.
- [ ] Unit-test the load→action mapping with a fake container control.
- [ ] Decision record (next free number) for the control levels; update `docs/status.md`.

## Done when

- A real container job is frozen, throttled and resumed by `SchedulerController.Apply`; tests pass; limits (e.g. pause latency) documented.

## Read first

- `src/DigitalHeat.Core/Scheduling.cs`
- `docs/decisions/0005-csharp-core.md`
- `docs/decisions/0007-whole-container-pause-recovery.md`
- `docs/webhosting-runtime.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Windows/macOS work through Docker Desktop (Linux VM).
