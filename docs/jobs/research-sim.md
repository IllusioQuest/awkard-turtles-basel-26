# Type 2 job: Monte Carlo research simulation (2D Ising model)

Ticket T-30, decision [0020](../decisions/0020-business-model-operator-two-job-types.md).
**Status: built, run and measured on a laptop (2026-11-21).** LiveRun replay: see below.

## What the job is

A phase-transition scan of the 2D Ising model with Metropolis Monte Carlo (checkerboard update,
periodic L × L lattice, J = 1, k_B = 1), the textbook statistical-physics workload (Newman &
Barkema, *Monte Carlo Methods in Statistical Physics*, 1999, ch. 3). One **work unit = one
simulation task** = one (temperature, seed) point. Default grid: 16 temperatures from 1.5 to 3.5 ×
2 seeds = 32 tasks, L = 32, 2,000 thermalisation + 10,000 measurement sweeps. All parameters are
environment variables in [`jobs/research-sim/ising.py`](../../jobs/research-sim/ising.py).
The kernel is **pure Python (standard library only)**: the first version used NumPy, but `pip`
could not reach PyPI from the hackathon network (TLS interception, certificate verification failed
on 2026-11-21). Without dependencies the image builds and runs offline once `python:3.12-slim` is
present, which also suits the offline demo rule (AGENTS.md §4). Runtime and energy below are for
this implementation; a vectorised or compiled code would do more sweeps per kWh.

**Why it represents research simulations (decision 0020):** many independent, seeded, CPU-bound
tasks with no personal data, as in research parameter sweeps. Results can be checked against the
exact critical temperature T_c = 2 / ln(1 + √2) ≈ 2.269 (Onsager 1944): the susceptibility should
peak near it. Production customers would run codes such as GROMACS; this kernel only stands in
for them in measurement and demo.

## Interruption

- **Freeze:** `docker pause` stops all worker processes; nothing is lost.
- **Throttle:** CPU quota through `DockerJobExecutor` (decision 0009).
- **Checkpoint:** each result is written atomically (`.part` file, then rename). After a kill, the
  next `run` deletes stale `.part` files and redoes only the tasks without a result.
- **Deadline:** none (`deadlineHours: null`), as the Type 2 filler of decision 0020.

Host contract (same shape as the transcription job): `/work/in/task_NNNN.json`,
`/work/out/pass_NNN/task_NNNN.json`, `/work/out/log.csv`, `/work/progress.json`.

## How to run

```bash
docker build -t digitalheat/research-sim jobs/research-sim
docker run --rm -v "$PWD/data/external/research-sim/work:/work" digitalheat/research-sim prepare
docker run -d --name research-sim -e PASSES=0 -v "$PWD/data/external/research-sim/work:/work" digitalheat/research-sim run
```

## Measurement (2026-11-21)

**Machine:** Intel Core Ultra 7 255H laptop, 31 GB RAM, Windows 11, Docker Desktop 29.7.2 (Linux VM
with 16 CPUs), `python:3.12-slim`. Container limited to `--cpus 8`, `WORKERS=8`.

**Meter:** whole-laptop power from the battery discharge rate (WMI `root\wmi BatteryStatus.DischargeRate`,
mW), sampled once per second while on battery, with
[`scripts/measure_job_windows_battery.ps1`](../../scripts/measure_job_windows_battery.ps1). It is the
Windows equivalent of the battery meter in `scripts/measure_job.py` (T-02). Linux RAPL counters are
not reachable on Windows/Docker Desktop. The firmware updates the value only every 15–30 s and takes
about 60 s to follow a step from idle to load.

**Reproduce:** build the image, unplug the laptop, then
`powershell -ExecutionPolicy Bypass -File scripts/measure_job_windows_battery.ps1 -Runs 2 -Passes 10 -IdleSeconds 120`.

| Run | Tasks | Wall time | Mean power | Energy gross | Energy net of idle (8.16 W) |
|---|---:|---:|---:|---:|---:|
| 1 | 320 | 264.0 s | 14.02 W | 1.0283 Wh | 0.4301 Wh |
| **2** | 320 | **274.4 s** | **17.17 W** | **1.3089 Wh** | 0.6870 Wh |

Raw samples, task logs and results: `data/raw/measurements/research-sim-ising/energy-2026-11-21/`.
A first attempt with 32-task runs of 28 s each was too short for the meter's update interval; its
raw data is kept in `energy-2026-11-21-short-runs/` and not used.

**Values used** (run 2, because it started after load and is steady state; run 1 includes the
meter's ramp from idle and underestimates):

- **Runtime:** 274.4 s ÷ 320 = **0.858 s per task** (throughput with 8 workers). One task on one
  worker takes 6.4 s on average (`log.csv`, both runs).
- **Energy:** 1.3089 Wh ÷ 320 = **0.00409 Wh = 4.09 × 10⁻⁶ kWh per task**, whole laptop, gross (as
  T-02 reports whisper `base`). Net of idle: 0.00215 Wh per task.
- **Scaling to the 1 kW reference server:** 1,000 Wh ÷ 0.00409 Wh ≈ **244,000 tasks per server-hour**,
  assuming energy per task stays constant (T-02 method). Run 1 would give 311,000; the spread shows
  the meter's uncertainty.

**Physics check:** in run 2 the mean susceptibility peaks at T = 2.30 (grid step 0.133), next to the
exact T_c ≈ 2.269. The kernel computes what it should.

**Limits:** battery meter granularity and lag; whole-laptop power includes display and idle load;
one machine; pure Python is slower per unit of physics than production codes.

## LiveRun: the shared scheduler controls the running job

Container `dh-research-sim` (`--cpus 8`, `WORKERS=8`, `PASSES=0`) controlled by
`DigitalHeat.LiveRun` with the same `GreedyHeatScheduler` and `SchedulerController.Apply` as the year
run (decision 0009). Command, from the repository root:

```bash
for d in 2025-01-15 2025-07-15; do
  dotnet run --project src/DigitalHeat.LiveRun -- --date $d --job config/jobs/research-sim-montecarlo.json     --container dh-research-sim --cpu-cores 8 --step-seconds 3 --force-pause-step 12
done
```

| Day | Scheduler target | Observed container CPU load | Forced pause at 12:00 | Tasks done in 24 steps × 3 s |
|---|---|---|---|---|
| 15 January 2025 (10.1 kWh demand at 00:00, empty tank) | 100 % every hour | 100 % (38–48 % in two steps where a pass ended and workers waited for the last tasks) | target 0 %, CPU 0 %, progress frozen at 57 tasks, resumed at 13:00 | 111 |
| 15 July 2025 (0.5 kWh/h hot water, full tank) | 16 % | 11–16 % | target 0 %, CPU 0 %, resumed at 13:00 | 24 (120 → 144) |

Logs: `data/raw/measurements/research-sim-ising/liverun-2026-11-21/`. Observed CPU load is
`docker stats` CPU ÷ allowed cores; it shows control, not electrical power or recovered heat.
