# T-47: Measure scheduler latency and suggest improvements

Owner: Nikita / Codex. Status: in progress.
Branch: exp/T-47-scheduler-performance.

Measure the existing .NET Release scheduler/placement paths with deterministic diagnostic inputs,
report median/p95 latency and allocated bytes, inspect scaling and separate decision time from
Docker/network/UI latency. No algorithm, scored model, safety guard or running workload changes.
Use the current main revision; optional existing PR #35 API timings must be labelled separately.
Record machine/runtime, warmup, timer/sample method, fixture shape, limitations and reproduction.
Suggest optimizations ranked by measured cost and risk; do not implement them without agreement.
