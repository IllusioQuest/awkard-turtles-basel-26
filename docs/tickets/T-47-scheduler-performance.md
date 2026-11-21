# T-47: Measure scheduler latency and suggest improvements

Owner: Nikita / Codex. Status: analysis complete; reproduction scripts preparing for review.
Branch: exp/T-47-scheduler-performance.

Measure the existing .NET Release scheduler/placement paths with deterministic diagnostic inputs,
report median/p95 latency and allocated bytes, inspect scaling and separate decision time from
Docker/network/UI latency. No algorithm, scored model, safety guard or running workload changes.
Use the current main revision; optional existing PR #35 API timings must be labelled separately.
Record machine/runtime, warmup, timer/sample method, fixture shape, limitations and reproduction.
Suggest optimizations ranked by measured cost and risk; do not implement them without agreement.

Results: 62 measured cases; core greedy ~0.107 microseconds, 3-node admission ~0.023 ms,
1,024-node shared-domain admission ~106 ms / 73.2 MiB allocated per operation (synthetic/no claims).
Separate PR #35 API fresh Docker observation ~3.10 s vs ~3-4 ms cached. See
[report and recommendations](../performance/scheduler-performance.md), adjacent raw JSON and
reproduction commands. No scheduler optimizations or game lifecycle changes made.
