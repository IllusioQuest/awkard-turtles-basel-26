# T-30: Type 2 job: interruptible batch compute (Monte Carlo) on a GPU node, measured

- **Tier:** 2 Pitch (a measured extra job type also strengthens must-have 1 for the draft)  ·  **Area:** Jobs
- **Depends on:** T-03 (generic executor, decision 0009)
- **Suggested owner:** anyone with a laptop  ·  **Owner:** Jona / Claude  ·  **Status:** done (measured and LiveRun-tested 2026-11-21) (branch `feat/T-30-research-simulation-job`)

## Goal

A runnable Type 2 job (decisions 0018, 0019, 0020): a container that processes many short, independent tasks.
Decision 0020 makes Type 2 **research simulations**, so the kernel should be a Monte Carlo *research*
simulation (e.g. a physics model), not option pricing. It should have a GPU path (CUDA/CuPy) and a CPU fallback for the host CPU), paused and resumed by the shared
scheduler, with measured runtime and energy per task, scaled to the 1 kW server like T-02.

## Tasks

- [ ] Container with a task queue directory: one task = seconds to minutes of work, idempotent, one result file per task; a dropped task is re-queued.
- [ ] Job descriptor per `config/jobs/job.schema.json` (interruptible yes, no hard deadline or a long one, `placementPolicy`, sources).
- [ ] Measure runtime and energy per task with the T-02 method (RAPL / battery) on the CPU path; GPU path on a
      real GPU (`nvidia-smi` power log) when one is available. State the scaling to 1 kW.
- [ ] Checkpoint at task boundaries for the GPU path (freeze is not a CUDA checkpoint, T-38 review).
- [ ] Short LiveRun test (January and July day) through `DigitalHeat.LiveRun`.

## Done when

- Measured energy per task, descriptor, pause/resume through the same scheduler.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
