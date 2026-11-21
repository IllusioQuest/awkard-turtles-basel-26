# T-30: Type 2 job: interruptible batch compute (Monte Carlo), measured on a laptop

- **Tier:** 2 Pitch (a measured extra job type also strengthens must-have 1 for the draft)  ·  **Area:** Jobs
- **Depends on:** T-03 (generic executor, decision 0009)
- **Suggested owner:** anyone with a laptop  ·  **Owner:**  ·  **Status:** open

## Goal

A runnable Type 2 job (decision 0018): a container that processes many short, independent tasks
(e.g. a Monte Carlo option-pricing or π estimate in Python or C++), paused and resumed by the shared
scheduler, with measured runtime and energy per task, scaled to the 1 kW server like T-02.

## Tasks

- [ ] Container with a task queue directory: one task = seconds to minutes of work, idempotent, one result file per task; a dropped task is re-queued.
- [ ] Job descriptor per `config/jobs/job.schema.json` (interruptible yes, no hard deadline or a long one, `placementPolicy`, sources).
- [ ] Measure runtime and energy per task with the T-02 method (RAPL / battery) and state the scaling to 1 kW.
- [ ] Short LiveRun test (January and July day) through `DigitalHeat.LiveRun`.

## Done when

- Measured energy per task, descriptor, pause/resume through the same scheduler.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
