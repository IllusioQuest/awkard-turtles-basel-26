# Gaming GPU eligibility: heat and speed safeguards

2026-11-21; T-55, ADR 0043; code [PR #44](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/44),
stacked on CS PR #42 and indexed admission PR #38. This is a compatibility/validation step,
not an automatic GPU dispatch implementation or a verified FPS improvement.

## Placement behavior

```mermaid
flowchart LR
  J[New gaming request] --> E[Fresh eligible nodes: GPU, capacity, locality and latency]
  E --> H[Existing useful heat ranking]
  H --> R[Reserve one physical GPU and node]
  R --> S[Start whole session when heat and runtime evidence are valid]
```

No new heat formula or scheduler: use existing WorkloadPlacement. More heat at a CPU-only
node cannot satisfy a GPU-required request. Among eligible GPU nodes the greatest remaining
useful heat capacity still wins. Hotels/spas are candidate sites when their useful heat
capacity and other constraints qualify. One game does not combine remote GPUs. An active
session remains assigned even if another site's heat increases. Interactive sessions cannot
be paused as batch work. Batch pause retains GPU ownership while the process holds it.

The laptop has one Intel Iris Xe GPU shared by A/C/E. These containers are not three physical
GPU servers. Represent its external occupancy once, reconcile all external users before
releasing it, and block automatic admission if that occupancy is unknown. CS recoverable
heat has no measurement: no automatic real CS heat-based startup is enabled by these tests.

## Verified experiment

49 orchestration tests pass, including GPU eligibility before heat ranking, occupied-GPU
rejection without software fallback, and active-game protection as heat demand changes.
Existing differential tests continue checking the optimized admission implementation.

Command from the T-55 checkout (explicit contention approval required when other GPU
containers run):

```powershell
.\gaming\scripts\Test-Cs16IntelGraphics.ps1 -Apply -AllowSharedGpuProbe
docker --context desktop-linux exec --user ubuntu --env DISPLAY=:20 digitalheat-cs16-session-e-1 timeout 15 glxinfo -B
dotnet test src/DigitalHeat.Orchestration.Tests
```

Only unused E was recreated. Its prior running image ID was unavailable in the local image
store, so the script uses the locally built configured CS image, resolves its immutable ID,
and disables pulls. Tested image: `sha256:6d3de17b9512066488da16b1eeea08a9b1c81d6f59062452e4ecab05bf7de4bd`.
E's own home/limits/internal network remain. Device `/dev/dxg` and WSL driver-library
read-only mappings match the existing Intel experiment. No Docker socket or host home.
Merged config verified software-render forcing disabled and D3D12 selected; CPU encoding remains.

`glxinfo -B` reported `Accelerated: yes`, `D3D12 (Intel(R) Iris(R) Xe Graphics)`, Mesa
26.0.8, OpenGL compatibility 4.1. This is a 64-bit desktop GL result: no CS/Wine GPU
compatibility, hardware encoding, gameplay FPS or free-GPU capacity claim. The missing
32-bit D3D12 driver at the expected path remains an integration question. Unified-memory
numbers printed by GL are not a dedicated VRAM budget. No GPU-ready CS capability published.
E browser HTTP returned 200. D started at 11:27:40 UTC and Minecraft at 09:05:03 UTC;
both retained those timestamps and zero restart counts after the probe.

## Scheduler timing

Unchanged diagnostic command, using the PR #36 checkout as harness:

```powershell
& <performance-checkout>\scripts\Measure-SchedulerPerformance.ps1 -SourceRoot <T-55-checkout> -OutputPath <output.json>
```

Harness SHA256: `1a03b75cd3363dd9efdeace64a3a49de64db7d51ff8ae811aeac3e061ff22133`.
Release .NET 8.0.31, tiered compilation disabled, 20 warmups and 101 samples per case,
nearest-rank p95. Both reports retain source IDs and fixture details. Production Core and
Orchestration source is unchanged from the CS preflight. The first report captured commit
c1bae86 while the probe-script-only correction was being committed; steady report uses
0e1738d. No scheduler edits occurred during either measurement.

| 1,024 nodes, shared domain, no held claims | Median ms | p95 ms |
|---|---:|---:|
| Earlier CS preflight | 1.6022 | 2.5228 |
| E startup/probe run | 1.6560 | 6.2534 |
| Follow-up after E settled | 1.3960 | 4.1384 |

Read `elapsed_microseconds / 1000` as milliseconds. The predeclared lab threshold is
`max(2 * 2.5228, 2.5228 + 1) = 5.0456 ms`. The first run **exceeds** it; the steady
follow-up passes. Keep both: laptop load varies, so this does not establish a no-regression
guarantee. Startup interference is a hypothesis, not an isolated causal finding. No further
timing repetitions were used to select a favorable result. Three-node independent-domain
reservation in the first run was 0.0130 ms median / 0.0249 ms p95.

These are synthetic pure-admission timings, not end-to-end session latency or GPU-job
benchmarks (the unchanged diagnostic has no GPU fixture). Graphics probes and Docker calls
remain outside admission. Synthetic fixture heat values are not measured CS/laptop heat.
Raw reports: `docs/performance/scheduler-gpu-eligibility-2026-11-21.json` and
`docs/performance/scheduler-gpu-steady-2026-11-21.json`.
