# Indexed admission results (T-49)

Implementation: [PR #38](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/38), stacked
on live-deadline [PR #37](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/37).
Merge #37 first, then retarget #38 to main. Code is not yet merged; documentation/evidence is on main.

## What changed

The existing placement class remains the single admission path. It owns a deep snapshot, validates
it, builds node/domain/site and external-allocation indexes, then publishes both under the same lock.
Owned usage is grouped once per decision, excluding the current request before summing for resume.
No rounded-total subtraction. Paused batch jobs retain RAM/GPU/slot ownership; disappeared nodes'
claims still count in their stored domain. Candidate selection preserves heat/site/node ordering.

Known inventory/request/allocation-array types use explicit defensive copies of mutable arrays and
policy allowlists. Immutable scalar records may be shared. Generic coordinator/journal copying and
JSON identity formats remain. No lock, freshness, validation, locality/storage/latency, deadline,
exclusive GPU, decimal overflow or journal durability safeguard was removed.

## Measured before / after

Same C# probe SHA-256 `1a03b75cd3363dd9efdeace64a3a49de64db7d51ff8ae811aeac3e061ff22133`,
62 cases, .NET 8.0.31 Release, 20 warmups and 101 samples, optimized steady-state. Fixture and
percentile/allocation method are unchanged from [T-47](scheduler-performance.md). The PowerShell
wrapper only gained a source-checkout option. Before: `92fd2aa`; optimized production code:
`d7bcff3` (see full hash in raw report). The following test-only commit adds heat-boundary coverage
without changing the measured production implementation. The after version also retains T-48's
additive deadline checks. All fleet inputs are synthetic, not real deployment capacity or heat.

| Fixture | Before median | After median | After p95 |
|---|---:|---:|---:|
| 3 nodes, independent domains, no held claims | 0.0227 ms | 0.0070 ms | 0.0075 ms |
| 512 nodes, shared domain, no held claims | 26.0504 ms | 0.4042 ms | 0.6944 ms |
| 1,024 nodes, independent domains, no held claims | 51.9556 ms | 0.8879 ms | 1.5906 ms |
| 1,024 nodes, shared domain, no held claims | 106.4718 ms | 0.8912 ms | 1.4995 ms |
| 1,024 nodes, shared domain, 100 held claims | 120.0605 ms | 1.2857 ms | 2.4665 ms |

For the 1,024 shared-domain no-held-claims case, allocation fell from **76,756,048 B** to
**1,140,672 B per reservation**: about 73.20 to 1.09 MiB. These are allocated managed bytes,
not retained heap size or evidence of a prior memory leak. Removing repeated large domain-node
membership sets explains the allocation shape; indexes themselves retain some snapshot memory.

Observed median ratio: `106.4718 / 0.8912 = about 119.5`. Allocation ratio:
`76,756,048 / 1,140,672 = about 67.3`. Inventory refresh at 1,024 independent domains fell from
67.5246 to 1.8045 ms median, so the decision gain was not merely shifted to a slower update.
Old probe operation names containing `json_copy` refer to the same `WorkloadJson.Copy` facade;
after optimization those selected overloads perform typed copying, not text serialization.

Both runs used an active, non-dedicated i5-1240P laptop with games/Docker/web work left running.
Thermal/OS/GC conditions varied. Ratios describe this observed comparison, not guaranteed speedups,
parallel throughput or service levels. All 62 raw after cases, allocation counts and p95/p99 are in
[the optimized report](scheduler-indexed-2026-11-21.json); [the baseline](scheduler-latency-2026-11-21.json)
is preserved unchanged. Unoptimized control paths also varied, so do not interpret every small
case difference as algorithmic. No claim about game-frame rate, cold startup, LP solve time or WAN.

## Correctness and reproduction

Fresh ADR 0038 review required preserving floating arithmetic order, copy-before-validation and
effective-claim accounting. Implemented those safeguards. **46 orchestration tests pass**, including
1,600 seeded operations against a test-only pre-optimization oracle; exact and adjacent CPU/heat
boundaries; nested input/returned-array mutation; concurrency; missing-node/paused ownership;
deadlines, stop recovery and legacy hash compatibility. **36 gaming tests pass**. A real isolated
smoke verified shared-scheduler batch pause/resume, protected interactive admission, idempotent
starts, confirmed release and cold backup/restore. Only its random test fleet/volumes were touched.

```powershell
dotnet test src/DigitalHeat.Orchestration.Tests -c Release
dotnet test gaming/tests -c Release
scripts/Test-WorkloadRuntime.ps1
# Diagnostic scripts are in PR #36; C# fixture source is unchanged:
C:\path\to\diagnostics\scripts\Measure-SchedulerPerformance.ps1 -SourceRoot C:\path\to\indexed-checkout
```

The oracle is compiled only into the test assembly; it is not an alternative production scheduler.
The next independent task, T-50, addresses fresh Docker observation latency rather than these
already sub-millisecond synthetic decisions.
