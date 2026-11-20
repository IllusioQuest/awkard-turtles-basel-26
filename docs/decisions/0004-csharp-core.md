# 0004: Shared C# core for heat-led scheduling

- **Status:** proposed
- **Date:** 2026-11-20
- **Decided by:** Codex; team confirmation pending

## Context

The case requires the same scheduler code to compute the annual balance and control
a real job. Nikita owns the job protocol and node runtime; the draft needs a reliable
year balance before the deadline.

## Decision

Use .NET 8 with `DigitalHeat.Core` independent of UI and source files, `YearRun` for
CSV/ElCom input and output, and xUnit tests. The core loads all reference/scenario
numbers from one source-labelled JSON file. `IScheduler.Decide(HeatSignal)` yields
a 0..1 target load per server; `IExecutor.ApplyTargetLoad` reports achieved load.
Both year simulation and the live adapter call `SchedulerController.Apply`, which
invokes the same `GreedyHeatScheduler` and applies its decision through `IExecutor`.
The year runner uses `SimulatedExecutor`; Nikita implements the real adapter and job
protocol separately. The signal contains current heat demand, tank state/capacity,
server count and recoverable heat per full-load server-hour. No job semantics enter
this interface yet.

## Alternatives considered

- Duplicating logic in the demo/runtime: would violate the case's shared-code rule.
- Coupling the scheduler to process/job types: blocks Nikita's independent work and
  makes the annual calculation depend on unfinished job semantics.

## Consequences

One decision path controls target load in both modes. Actual load can differ from
target and is recorded separately. The draft assumes immediately available work;
job arrivals/deadlines and measured real-job power remain TODO(source). The
interface may need a later ADR when job constraints or multiserver dispatch arrive.

## Review

Fresh-context review verdict: **go with changes**. Added a shared controller path
so both runtimes invoke exactly the same decision and actuation logic. Kept job
availability, interruptibility and deadlines outside this interface as requested:
Nikita owns their protocol and runtime, and the year output explicitly says missed
deadlines are not modelled. The four-server count and income price remain labelled
illustrative scenarios; the pitched server count needs team agreement.

