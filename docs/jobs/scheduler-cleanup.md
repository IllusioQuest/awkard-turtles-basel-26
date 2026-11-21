# Scheduler cleanup and runtime handoff

2026-11-21; T-57; [PR #46](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/46),
branch `fix/T-57-scheduler-cleanup`, based on main after PR #45. Implementation complete,
awaiting review. This is a refactor of existing behavior, not a new robustness layer.

## What was unnecessary and what changed

| Previous work | Cleanup |
|---|---|
| Two throwaway WorkloadPlacement instances built indexes just to validate each owner update | Call the existing inventory validator directly; build an index only when installing inventory |
| Config load and runtime preflight each had the same nested node/context scan | One internal indexed ValidateRuntimeDomains helper, preserving the same physical-resource rule |
| Every site scanned the full hourly attempt history | Group commitments/held heat once per site; preserve each site's summation order |
| Every notification sorted the immutable submission manifest | Sort once at owner construction |
| Each queued job enforced deadlines and then deep-copied the entire attempt history again | Reuse the defensive snapshot already returned by EnforceDeadlines |
| Owner Tick mixed input processing, batch control, cleanup and admission | Small private methods make the same execution order explicit |
| Inbox replay handling and persistence rollback were nested inside file reading | Named AcceptReport helper; identical retry, durable watermark and rollback semantics |
| Temporary array was allocated solely to iterate fresh reports | Iterate directly; make the inbox's unused internal atomic-write exposure private |

Public contracts, snake_case JSON, hourly accounting, placement priorities and runtime ownership
are unchanged. No new dependency, process, retry system or game configuration was added.
No game/container was restarted. Old experiments, untracked files, credentials and worlds were
not deleted as speculative cleanup. The extra benchmark is diagnostics, outside the runtime path.

## Comments for co-builders

The comments explain the reason behind the code, so future edits can preserve these properties:

- **Heat is the ranking priority.** Latency is a configured eligibility limit, not a lower-RTT
  ranking score. Eligible nodes with more useful heat must win. See WorkloadPlacement.TryReserve
  and NodeFeedbackTests.GamingUsesHighestHeatWithinLatencyLimitWhileUnconstrainedBatchFollowsHeat.
- **Keep I/O outside TryReserve.** Validation can run without building a placement engine/index;
  actual inventory installation still needs its index. Do not reintroduce throwaway indexes to
  call an existing validator. Copy caller-owned arrays before storing them.
- **Keep the physical-domain guard shared.** One Docker engine cannot masquerade as several
  independent machines. ValidateRuntimeDomains is used by config load and runtime preflight;
  CoordinatorTests.ConfigAndPreflightAgreeOnPhysicalDomainsPerDockerEngine checks both.
- **Keep hourly heat commitments.** Pausing/releasing a job does not reset that hour's reserved
  heat. Active reservations already count: add only retained heat not represented by active
  reservations. Grouping is an optimization, not a new formula. Preserve per-site summation order
  because floating-point boundaries can affect admission. See ADR 0044 and NodeFeedbackTests.
- **Deadline checks between admissions are intentional.** Reuse their returned snapshot rather
  than removing checks as apparent duplication. Returning internal mutable records/arrays would
  defeat the coordinator's defensive-copy boundary.
- **Wake hints are bounded; node state is not dropped.** Each tick rereads complete latest reports.
  Identical retries do not renew expiry or write another watermark. An unpersisted report cannot
  enable admission. The inbox comments explain the existing rollback requirement.

These are existing behavior/performance constraints, not new approval rules. Improve them
intentionally with the relevant behavioral checks and a comparable measurement.

## Checks

```powershell
dotnet test src/DigitalHeat.Orchestration.Tests
dotnet test src/DigitalHeat.Core.Tests
dotnet build src/DigitalHeat.WorkloadRun
```

61 orchestration tests and 46 Core tests pass; build has zero warnings/errors. Three added
parameterized cases protect the existing domain/context rule after sharing its implementation:
same-domain/same-engine permitted, separate domains/same-engine rejected before create, and
separate domains/separate engines permitted. Existing heat, deadline, replay and ownership tests
were reused. No extra tests mirror private method extraction or formatting.

## Runtime measurement

Use the existing script's new optional probe; default Placement mode is preserved:

```powershell
.\scripts\Measure-SchedulerPerformance.ps1 -Probe NodeFeedback -SourceRoot <checkout> -OutputPath <owner-report.json>
.\scripts\Measure-SchedulerPerformance.ps1 -OutputPath <placement-report.json>
```

Owner baseline source: `c26c2f6c243869c8714cec5b7599ea785bd42828` (diagnostic present,
production code unchanged from main). Cleanup: `3be21a22b17f19717f8c1eb7dcb027c68be85c97`.
Identical owner harness SHA256 in all runs:
`2d27a4d95f4cf48eb98e8e071ff83324978cd05e6c58364bd26d4b902491ef58`.
Release .NET 8.0.31, tiering disabled, 5 warmup ticks/31 samples; median and nearest-rank p95.
Managed allocations use GC.GetAllocatedBytesForCurrentThread.

The owner fixture reads actual temporary JSON files, uses a frozen clock and a fake runtime,
and validates that blocked jobs never start. Queued work intentionally lacks a capability;
held interactive jobs are created during excluded setup/warmup. Initial publication/fsync,
constructors and initial starts are excluded; steady duplicate-report reads are measured.
All hardware/heat values are synthetic and labelled in source. Existing Docker games were left
running on this non-dedicated Intel i5-1240P laptop. No Docker/network/stream timings are included.

First pair (baseline then cleanup), milliseconds per complete owner tick:

| Nodes / held / waiting | Median before | Median after | p95 before | p95 after |
|---|---:|---:|---:|---:|
| 4 / 0 / 0 | 0.6731 | 0.7082 | 2.0729 | 1.7002 |
| 4 / 4 / 32 | 22.1038 | 7.6550 | 41.9492 | 23.4321 |
| 32 / 0 / 0 | 8.9070 | 6.5755 | 18.5218 | 12.7725 |
| 32 / 16 / 32 | 82.2537 | 28.7482 | 177.3091 | 37.1592 |
| 128 / 0 / 0 | 13.6753 | 18.2370 | 22.0762 | 46.7495 |
| 128 / 16 / 32 | 194.7157 | 94.4912 | 231.5739 | 117.5004 |

The idle 128-node case measured slower. One reversed-order pair (cleanup then baseline) was
run to investigate that concern; retain both, with no further timing selection:

| Nodes / held / waiting | Median before | Median after | p95 before | p95 after |
|---|---:|---:|---:|---:|
| 4 / 0 / 0 | 1.2515 | 0.8917 | 3.1610 | 3.2645 |
| 4 / 4 / 32 | 19.8179 | 5.2881 | 35.3163 | 8.4584 |
| 32 / 0 / 0 | 8.3779 | 3.1134 | 18.8440 | 4.6143 |
| 32 / 16 / 32 | 64.0764 | 61.6383 | 143.6367 | 88.2584 |
| 128 / 0 / 0 | 28.1131 | 12.6081 | 55.9247 | 20.1151 |
| 128 / 16 / 32 | 99.3334 | 51.6660 | 213.6824 | 109.0415 |

For the largest queued fixture, first-pair median reduction is
`100 * (1 - 94.4912 / 194.7157) = 51.5%`; reversed pair is about 48.0%.
Allocation is stable across pairs: `87,309,360 -> 14,727,808 bytes/tick`, an 83.1% reduction.
Idle allocations also fell (`1,684,976 -> 1,296,000 bytes` at 128 nodes), but wall timings vary.
This supports less redundant work and lower allocations; it does not establish a universal
speedup, isolated CPU causality, sustained throughput or a response-time SLA.

The separate unchanged pure-placement fixture at 1,024 shared-domain nodes/no held claims
measured 1.7480 ms median / 4.8490 ms p95 (20 warmups/101 samples). Its pre-existing lab
threshold is `max(2 * 2.5228, 2.5228 + 1) = 5.0456 ms`; this run is below it. Placement
ranking/eligibility code is unchanged; only comments and validator visibility changed there.
Do not compare this directly with a full owner tick or call the heat model faster.

Raw evidence under docs/performance:

- node-feedback-cleanup-before-2026-11-21.json
- node-feedback-cleanup-after-2026-11-21.json
- node-feedback-cleanup-after-crossover-2026-11-21.json
- node-feedback-cleanup-before-crossover-2026-11-21.json
- scheduler-cleanup-placement-2026-11-21.json
