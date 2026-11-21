# Scheduler speed investigation (T-47, 2026-11-21)

The current small lab is **not limited by the heat-decision arithmetic**. Fresh Docker observation
dominates the visible monitoring delay. The universal placement implementation does have a
substantial scaling/allocation cost at hundreds to thousands of synthetic nodes. These are local
diagnostic measurements, not hardware capacity, game-start latency or a service-level guarantee.
No production algorithm, journal safety rule or running container was changed.

## Measured results

Source: main revision `92fd2aa9e5640898651346f40d5310fab8064b21` (production source unchanged from
the preceding main; that commit only claims the ticket). Intel Core i5-1240P, 16 logical processors,
.NET 8.0.31, Windows, Release, explicit `DOTNET_TieredCompilation=0` for optimized steady-state.
Existing Docker desktops/games and the web/API remained running. CPU frequency, thermal state and
OS scheduling were not controlled. Treat the figures as this observed run, not universal constants.

| Pure in-memory operation | Fixture | Median | p95 |
|---|---|---:|---:|
| Core `GreedyHeatScheduler.Decide` | One signal; batched calls | 0.107 microseconds | 0.172 microseconds |
| `WorkloadPlacement.TryReserve` | 3 nodes, independent domains, no held claims | 0.0227 ms | 0.1607 ms |
| Same | 32 nodes | 0.1707 ms | 0.3250 ms |
| Same | 128 nodes | 0.8588 ms | 1.6392 ms |
| Same | 512 nodes | 14.6134 ms | 22.2259 ms |
| Same | 1,024 nodes | 51.9556 ms | 62.3954 ms |
| Same | 1,024 nodes, one shared resource domain | 106.4718 ms | 124.2301 ms |
| Same | 1,024 nodes, shared domain, 100 held claims | 120.0605 ms | 143.5879 ms |
| Federated `BestEligible` offer selection | 1,024 unique site offers; batched | 1.0622 ms | 1.2374 ms |

Each scaling fixture has one logical node per heat site. The shared-domain case deliberately
models many logical nodes sharing a resource budget; it is **not** a claim that 1,024 real servers
share one host. Capacities scale additively so every tested reservation remains admissible. Numbers
are synthetic diagnostics, not the scored reference house or measured Minecraft heat.

At 1,024 nodes, a reservation allocates **2,028,624 B (1.935 MiB)** with independent domains, versus
**76,756,048 B (73.200 MiB)** with a shared domain. With 100 held claims the shared case reaches
79,784,632 B (76.089 MiB). These are total managed bytes allocated per operation, not simultaneously
live memory or a memory leak. Allocated bytes / 1,048,576 converts B to MiB.

Inventory refresh is separate work: at 1,024 independent domains, `Update` measured 67.525 ms
median, including validation and defensive copy. Inventory JSON copy by itself measured 33.724 ms.
A small request JSON defensive copy alone measured 12.330 microseconds and 3,960 B. These are
separate experiments, not an exact additive profile or proof that removing a component saves its
whole separately measured time. Core greedy allocated 128 B/call, a low-priority optimization here.

### The visible web-monitor delay is different

The existing PR #35 API at `fe1737b8ff79a67459b5890e425a52b24dcd966b` was measured separately.
Two explicitly expired-cache requests with actual read-only Docker observation took **3,117.68 ms**
and **3,099.83 ms**. Their subsequent cache-hit requests took **2.95 ms** and **4.15 ms**.
Observation timestamps confirm which requests refreshed the cache. Four requests are not a
latency percentile distribution. This times HTTP through receipt of the complete response, not
only the placement engine. Cache-expiry waits and JSON parsing were outside the timer.

The API uses multiple Docker CLI processes (`ps`, `inspect`, process probes, then `stats`), and
the work is serialized for a bounded snapshot. That I/O and process startup is separate from a
22.7-microsecond three-node reservation. It is incorrect to call a three-second UI response a
three-second scheduler decision. Conversely, this investigation did not measure actual game/world
startup, scheduler actuation, disk journal flushes, failover, WAN latency or rolling-horizon LP solving.

## Recommended improvements, in order

### 1. For a faster monitor: improve the observation path

Keep the existing bounded observation cache and its age visible. Prefer one long-lived Docker API
connection per engine, batched inspect/list results, a bounded stats collector and event-triggered
refresh, instead of spawning several CLI processes per node for every cache miss. Keep snapshot
calculation independent from telemetry collection so an observation refresh does not block unrelated
model previews. Return explicitly stale/unknown data while refreshing rather than inventing state.

This targets the measured seconds-long delay. It needs a reviewed Docker-client choice and failure
handling. **Do not simply extend admission freshness limits**: display caching must not authorize
a start from stale resources or hide stopped/paused containers. No speedup factor is claimed yet.

### 2. For larger fleets: index the validated inventory once

In `WorkloadPlacement.Candidates`, each candidate currently repeats `Domains.Single`, scans all
nodes to build `domainNodeIds.ToHashSet()`, filters claims/external allocations, and scans node lists
per site. With one node per site this includes repeated quadratic work. In the shared-domain case
the same large domain membership set is rebuilt once per candidate, explaining the allocation shape.
This is a code-supported hotspot hypothesis corroborated by timings/allocation counts, not a CPU
profiler's exact attribution of every millisecond.

Build `nodeById`, `domainById`, `nodesBySite`, `nodesByDomain` and external-allocation groupings
when publishing a validated inventory. Share immutable membership sets within that revision.
Maintain or compute once-per-decision held CPU/RAM/slot/heat/GPU aggregates by node/domain/site.
This should remove repeated full-fleet scans from the decision path; measure the resulting time and
memory before promising a complexity or speedup target. Rebuilding indexes on every inventory
refresh still has cost; include it in the comparison.

Safety constraints remain: atomic inventory/index publication, shared-domain resource accounting,
paused-batch RAM/GPU/slot retention, claims for disappeared nodes, locality/storage/latency guards,
exclusive GPU ownership and deterministic ties. Keep an atomic commit/recheck; do not race two
requests by merely removing the lock. This is the strongest next **scheduler** optimization to prototype.

### 3. Replace JSON copying only with equally safe ownership boundaries

`WorkloadJson.Copy` serializes and deserializes requests, inventories and allocation snapshots.
`WorkloadCoordinator.Update` also copies an inventory after placement has copied it. Immutable
internal structures or explicit typed deep copies could avoid text serialization and repeated
allocations, especially on refresh. Keeping JSON at persistence/transport boundaries is useful.

Never replace this with shallow record copies over mutable arrays. Preserve validation, immutable
snapshot ownership, journal identity/retry checks and protection against caller mutation. Existing
tests for these guarantees must still pass; add mutation/equivalence tests for a new representation.

### 4. Lower priority: allocation and candidate-selection cleanup

After indexing, profile repeated LINQ materialization and small temporary arrays. A deterministic
one-pass best-candidate loop may help, but .NET can already optimize ordered `FirstOrDefault`:
do not assume the current expression necessarily performs a complete sort or promise an O(n log n)
saving without measuring it. Preserve the existing heat/site/node tie order and federated conflicting-
offer checks. Federated offer selection is currently much cheaper than universal admission here.
Removing the small array allocation in `HeatCapacity.RemainingKwh` is lower value than fixing
shared-domain membership rebuilding; the arithmetic is already fast in the small lab.

### 5. Measure contention and durable dispatch before redesigning them

`WorkloadPlacement` and `WorkloadCoordinator` serialize operations. At larger fleets the measured
CPU cost also holds the placement lock longer. Benchmark concurrent callers and actual durable
dispatch separately. Consider validated versioned snapshots and an atomic admission recheck only
after proving equivalence under races. Do not shard locks by logical node when nodes share heat,
CPU or an exclusive GPU domain. The coordinator flushes its journal before uncertain Docker
transitions for crash safety: removing flushes is not an acceptable shortcut. A smaller write-ahead
format is a possible later design, not a measured recommendation to implement now.

## Method and reproduction

Reproduction scripts are in [PR #36](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/36), pending merge; report/raw evidence are already on main. Diagnostic source: `scripts/performance/SchedulerProbe.cs`, invoked by
`scripts/Measure-SchedulerPerformance.ps1`. The script compiles a temporary Release harness against
the production projects without adding a product console UI or new dependency. It writes ignored
`results/performance/scheduler-latency.json`. JSON source/metadata and all 62 cases are preserved in
[the measured report](scheduler-latency-2026-11-21.json). The harness SHA-256 records the exact
diagnostic source used. The optimized environment flag is scoped to that child process.

```powershell
scripts/Measure-SchedulerPerformance.ps1
# Optional: requires the already-running PR #35 API and its own checkout/config:
scripts/Measure-MonitorApiLatency.ps1 -ApiCheckout C:\path\to\monitor-feature-checkout
```

For each case: 20 warmup samples, then 101 timed samples; `Stopwatch` frequency 10,000,000 Hz.
Reservation samples time one `TryReserve`; cleanup/release occurs outside the timer, as do fixture
construction and initial Update/Restore. Update samples include their validation/copy operation.
Core calls use batches of 10,000; request-copy and offer cases use batches of 10. Percentiles of
these batched cases describe **batch-mean per-operation time**, not isolated-call tail latency.
Nearest-rank p95 is the 96th ordered sample of 101; managed bytes use
`GC.GetAllocatedBytesForCurrentThread`. Formula:
`microseconds_per_operation = (end_ticks - start_ticks) / frequency_hz * 1,000,000 / batch_size`.
GC remains enabled and may affect measured tails. Timer/loop overhead is not subtracted.

For example the 1,024 shared-domain median 106,471.8 microseconds / 1,000 = 106.4718 ms. This
is elapsed latency, not operations-per-second throughput under contention. Existing workloads
and power/thermal management can change the values; repeat on an otherwise idle machine before
publishing a hard performance claim. This first pass does not cover GPUs, nontrivial network
latency limits, external allocations, concurrency, rejected requests, LP optimization, or cold JIT.

The separate [API evidence](monitor-api-latency-2026-11-21.json) records cache-refresh flags and
observation ages. Diagnostics only inspect the already-running lab; they never start/stop games.
The benchmark's success/release checks and report percentile/order checks passed; no production
behavior changed, so no new scheduler regression run was required merely for measuring it.
