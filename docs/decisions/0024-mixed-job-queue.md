# 0024: Mixed finite and filler jobs in one year run

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex; T-31 approach approved by Jona

## Context

The C# year run supports either finite jobs or unlimited work, but not both. T-31 must give
finite Type 1 jobs priority within the heat-led target and sell only the remaining capacity to
an unlimited Type 2 filler. Decision 0020 makes public-document LLM the pitch Type 1 and research
simulation the Type 2; sourced LLM arrivals are missing. The existing Basel dictation counts
may be a technical queue fixture only, never a home-site or pitch demand claim.

## Decision

Add `mixed` to `JobScenario`: finite job types plus exactly one filler descriptor, each with a
sourced CHF/server-hour price. The filler also has a sourced sold-capacity share (1.0 for the
case-permitted all-work-sold comparison; 0.35 as a demand sensitivity). Keep `finite` and
`unlimited` JSON valid and behavior unchanged. Reuse `JobQueueExecutor`: the shared heat
scheduler decides the target; the executor caps it to sold, available work, serves finite jobs
by earliest deadline first, then assigns the rest to filler. Unsold filler hours never run or
earn income. Filler never forces dumping and has no deadline.

Track server-hours and computing income per type and in total. The mixed total income is the
sum of each type's hours times its scenario price. Preserve versioned result bundle v1 fields;
write an additive `jobs.json` companion for mixed job metrics. The case break-even formula and
hourly heat calculation stay unchanged. A technical Basel scenario uses finite transcription
counts only to exercise the queue; it is labelled non-pitch and processes no health records.
The same `IExecutor.ApplyTargetLoad` path is used after the availability cap.

## Alternatives considered

- Two separate year runs: cannot prove Type 1 priority and Type 2 fill within each hour.
- Modify the year scheduler: priority belongs in the executor's allocation of offered work;
  the shared heat scheduler already decides the safe load.

## Consequences

The mixed scenario is swappable without changing the physics. A sourced public-document LLM
arrival scenario remains a gap for the business pitch. Mixed results are technical fixtures
until then. The unlimited comparison must retain
58.0% coverage, zero dump, 22,661 server-hours and CHF 0.226 case break-even with zero Type 1
arrivals, while Type 2 income may use a different sourced price.

## Review

Fresh-context verdict: **go with changes**. The reviewer flagged missing sourced LLM arrivals,
unsold filler work, and the executor boundary. The decision now limits the Basel dictation run
to a technical fixture, adds a sourced sold-capacity share and caps execution before the same
inner `IExecutor`. Tests will cover empty finite queues, constrained filler and the zero-Type-1
unlimited comparison. The suggested simpler alternative (retain the unlimited scored baseline)
is adopted.
