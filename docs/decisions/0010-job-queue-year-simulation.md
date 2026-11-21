# 0010: Finite job queue in the yearly heat simulation

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex (team confirmation pending)

## Context

The case requires missed deadlines in percent of jobs, while the accepted C# core currently
assumes unlimited immediately available work. T-01 will supply measured job types and arrivals;
its schema is not yet settled. The reference-house comparison must remain reproducible.

## Decision

Keep the existing unlimited-job run unchanged. Add a source-labelled finite-job scenario at the
`YearSimulation` boundary: integer arrivals per local day and job type, work units per job,
throughput in work units per server-hour, an interruptible flag and deadline in hours. An
earliest-deadline-first queue is an `IExecutor` wrapper: it receives the unchanged heat-led
target from `SchedulerController.Apply`, limits achieved load to available work and debits work
from achieved server-hours. Actual load drives the heat balance. Jobs arrive at local day start;
the deadline is an exclusive indexed-hour boundary, and unfinished jobs expire once before that
hour runs. Report arrivals, completed, missed, backlog and processed work by job type.
Missed-deadline percentage uses only jobs whose deadline falls within the simulated year,
including jobs completed before that boundary; later deadlines remain visible as backlog or
completed counts, outside the percentage denominator. A scenario policy selects heat-led waiting or
running available work at full capacity and dumping surplus heat.

## Alternatives considered

- Treat unlimited jobs as zero missed deadlines: this would invent a performance result.
- Embed job JSON fields in the scheduler interface: T-01 is open and the live adapter should
  continue to use the accepted `IScheduler`/`IExecutor` boundary.

## Consequences

The finite scenario cannot yield scored deadline figures until T-01 provides sourced arrivals,
throughput and deadlines. `run-and-dump` is a scenario variant, not a chosen team policy. Moving
jobs to another site remains T-10/T-12. One unit of work may span hours; the queue assumes
interruptible workloads and no checkpoint overhead; finite runs reject a noninterruptible type.
The unlimited explorer cross-check remains
the regression gate. Decision number 0009 is reserved for the in-flight generic job-control PR.

## Review

Fresh-context verdict: **go with changes**. The reviewer requested a precise shared control path,
deadline boundary/denominator and behavior for noninterruptible jobs. We kept the accepted
`IScheduler` target unchanged and explicitly use the accepted `IExecutor` ability to report less
achieved load; this avoids changing the live scheduler interface. We specified exclusive hourly
deadline boundaries, count jobs completed before a post-year deadline outside the denominator,
and reject noninterruptible types. Run-and-dump remains an optional scenario variant as T-05
requires, while heat-led waiting is the reference behavior.
