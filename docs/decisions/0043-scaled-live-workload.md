# 0043: Measured workload scaling for the live network demo

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona approved the scaling concept and delegated the workload choice; a synthetic demo deadline is permitted

## Context

The live network scheduler uses server-hours per task and deadlines in model hours. The current
Ising containers execute real tasks, but `serverHoursPerTask = 2`, six tasks per job and a 20-step
deadline are demo settings. The measured Ising catalog entry has no deadline. A two-second demo
step is not a physical hour, and CPU quota is not a measurement of heat.

## Decision

1. Keep the existing `INetworkScheduler` and its server-hour, deadline and placement inputs.
   A demo step represents one model hour and lasts `stepSeconds` wall-clock seconds.
2. Use a named job descriptor with measured work-unit runtime and energy. A reproducible scaling
   manifest records the source job, measured device and worker count, work-unit definition, measured
   values and sources, the relation between visible tokens and executed tasks, actual task parameters,
   `serverHoursPerTask`, `stepSeconds`, and the resulting compression factor. The current 0.858 s/unit
   is throughput from an eight-worker run, not single-slot runtime. Recalibrate the visible task on
   the pitch laptop at its actual CPU allowance before assigning a task-to-work-unit ratio.
3. Each visible token represents one **actually executed** Ising task and its actual queue location,
   progress and result. The interface caps the number of waiting tokens in a panel and shows the
   remainder as a count. The model time axis is compressed; no unexecuted workload is presented as
   completed and no heat is claimed to have been measured.
4. Convert all model quantities together. If a demo step of `s` wall seconds represents one model
   hour, one server slot delivers at most `s × allowed_cores / slots` core-seconds per step. A task
   representing `h` model server-hours must require about `h × s × allowed_cores / slots` measured
   core-seconds at that slot's allowance. A job with `n` tasks presents `n × h` server-hours to the
   scheduler; its deadline is `d` model steps or `d × s` wall seconds. The live and yearly paths
   call the same `INetworkScheduler`; a conversion check rejects inconsistent settings before the
   live run. Do not infer equal heat from equal core-seconds.
5. A deadline without a customer or source is explicitly a **synthetic scenario**. The measured
   Ising catalog job remains a deadline-free Type 2 filler; deadline-bearing work in the demo is
   a separate labelled scenario until a real contract or trace is available.
6. CPU is observed from Docker. The catalog's gross laptop energy and the case's 1 kW server with
   80% recovery have different measurement bases and are displayed separately. No production
   workload volume or delivered-heat claim is derived from a laptop sample; a physical claim
   requires a power and heat measurement on the relevant device.

## Consequences

The audience sees real execution of the documented 32-task technical scan with a compressed model
time axis. Scheduler decisions and
deadline arithmetic use the same units as the year simulation. Claims about production workload
volume or delivered heat remain conditional on the measured source and scenario assumptions.

## Review

Fresh-context review: **go with changes**. Applied its three points: distinguish eight-worker
throughput from single-slot runtime and require pitch-laptop calibration; state dimensional
invariants for work, capacity and deadlines; separate laptop energy from reference-server heat.
Adopted the suggested small manifest instead of adding a second scheduler or simulation path.

First pitch-laptop probe while the four-node demo was running: 2,200 sweeps took 1.310, 2.321 and
1.707 s in three separate containers (`docker compose -f compose.live-demo.yml run --rm
-e TARGET_SECONDS=2 node-1 calibrate`, 2026-11-21). The large spread means this is not a stable
single-slot calibration and must not be used to overwrite `taskSweeps`. Repeat without competing
demo work and measure a full task at its actual per-slot CPU allowance.

The four compose workers were subsequently stopped and removed. On this laptop, three isolated
`docker run --rm --cpus 0.5 -e TARGET_SECONDS=4 -e PROBE_SWEEPS=5000
digitalheat/research-sim:live-demo calibrate` runs took 3.719, 4.025 and 4.007 seconds for 5,000
measurement plus 500 thermalisation sweeps. The median 4.007 s is within 0.2% of two model hours at
2 s/hour. All 32 tasks of the documented technical batch run physically (180,224,000 spin updates),
while only the model time axis is compressed. The 20-hour deadline remains synthetic. The manifest
`config/live-demo-scale.json` records the calculation and the live process rejects contradictory
config values. Other laptops require recalibration.
