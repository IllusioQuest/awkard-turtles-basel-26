# Unified scheduler verification — 2026-11-21

Code: PR #52, feat/T-60-unified-scheduler; policy implementation b1b7803, final checks/fixes
5c1fa3e. Architecture/reproduction: ../model/unified-heat-first-mesh.md. This is a prototype,
not production deployment or a measured heat-recovery result.

## Correctness

47 Core, 44 Network, 74 orchestration, 99 Fleet and 57 gaming tests passed. Full solution
build passed. Reference unlimited 8760-hour outputs remain equal; finite and mixed single-site
load/deadline cross-checks pass. Retired B2 selector explicitly fails. Tests of retired policy
behaviour were replaced by new-policy invariants, not used to claim old outputs were unchanged.

Real Linux evidence: linux-mesh-batch-2026-11-21.json and linux-mesh-create-recovery-2026-11-21.json.
Three distinct inner engine IDs; home-origin batch selected hotel under controlled heat signals.
Actual Docker pause verified, origin restarted, target attempt retained, finite process finished
successfully before its 600-second budget, two result files persisted in its own volume. One CPU
sample was 26.02% of a core with a 0.25 CPU quota; instantaneous sampling can exceed nominal quota.
Other running container IDs were unchanged. Forced target interruption after create recovered
the exact original container ID and attempt without duplicate creation. The initial restart test
exposed stale containerd runtime PID files; /run tmpfs fixed the lab restart path.

These are quota slices on one laptop. Synthetic 0.4 kWh/job-hour is not measured power or water
heat. Authentication is a shared trusted-lab token. No automatic origin failover, remote security,
arbitrary heterogeneous model equivalence, or Internet-scale mesh claim.

## Runtime measurements

Raw reports contain method, inputs, seed/context, source commit and harness hash. They measure
different boundaries; do not compare microseconds of selection with milliseconds of durable IO.

- unified-fleet-2026-11-21.json: five measured first-January-day runs after one warmup. At 1000
  generated houses and proportionally scaled synthetic demand, callback median 0.0044 ms,
  nearest-rank p95 0.0177 ms, full 24-hour run median 2812.7063 ms. At 100 houses: callback
  median 0.0023 ms / p95 0.0067 ms, full run median 610.8537 ms. Includes existing physical
  engine/input loading in full-run time. Callback clock overhead included. Tick-only unchanged
  notifications do not scan jobs/houses; available sites are indexed, heat changes update affected
  sites. New-job events can still scan queued jobs when eligibility prevents assignment.
- unified-mesh-2026-11-21.json: 21 measured fresh-journal rounds after two warmups, 3 peers,
  3 fake jobs, 18 direct messages; median 126.5608 ms / p95 152.8588 ms. Includes fsync and
  constructor/reconciliation work; excludes Docker/HTTP. Full mesh traffic grows quadratically.
- unified-placement-2026-11-21.json: first loaded-laptop run, 1024 unheld nodes: independent
  domains median 2.2524 ms / p95 5.6499 ms; shared domain 2.5376 / 5.1612 ms.
- unified-placement-followup-2026-11-21.json: same code, no lab batch running during placement
  probe; existing games/services remain. Independent domains 6.3543 / 9.4963 ms, shared domain
  2.5568 / 3.4960 ms, 1024 unheld nodes. With 100 held claims: independent 7.0779 / 10.3549 ms,
  shared 2.8297 / 4.5869 ms. These are variable non-dedicated-laptop timings, not a proven
  regression attributable to the policy and not evidence of meeting a stable SLA.

The earlier fixed 5.0456 ms local guard is NOT consistently met by the 1024-node placement
runs. Keep this explicitly open. No repeated benchmark cherry-picking or raised threshold.
The small lab is functional; larger-scale placement needs an isolated paired baseline/candidate
run and profiling before claiming completion of the speed goal. Pure placement remains a linear
scan with eligibility/allocation work; Docker control uses the existing CLI runtime and can
cost much more than the pure decision. Do not optimize by weakening heat/ownership checks.

Reproduce from the feature checkout:

```powershell
.\scripts\Measure-SchedulerPerformance.ps1 -Probe Placement -OutputPath results/performance/placement.json
.\scripts\Measure-SchedulerPerformance.ps1 -Probe PeerMesh -OutputPath results/performance/mesh.json
.\scripts\Measure-SchedulerPerformance.ps1 -Probe Fleet -OutputPath results/performance/fleet.json
```

The benchmark commits identify the policy code; FleetProbe was added as a measurement-only
follow-up and its SHA is recorded in the report. No causal speedup claim is made.
