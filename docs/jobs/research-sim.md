# Type 2 job: Monte Carlo research simulation (2D Ising model)

Ticket T-30, decision [0020](../decisions/0020-business-model-operator-two-job-types.md).
**Status: code written, not yet built, run or measured.** Runtime and energy are `TODO(source)`
until the measurement below is done.

## What the job is

A phase-transition scan of the 2D Ising model with Metropolis Monte Carlo (checkerboard update,
periodic L × L lattice, J = 1, k_B = 1), the textbook statistical-physics workload (Newman &
Barkema, *Monte Carlo Methods in Statistical Physics*, 1999, ch. 3). One **work unit = one
simulation task** = one (temperature, seed) point. Default grid: 16 temperatures from 1.5 to 3.5 ×
2 seeds = 32 tasks, L = 64, 1,000 thermalisation + 4,000 measurement sweeps. All parameters are
environment variables in [`jobs/research-sim/ising.py`](../../jobs/research-sim/ising.py).

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

## Measurement (to do)

Planned method on the team laptop (Lenovo 21SR, Intel Core Ultra 7 255H, 16 cores, Windows 11,
Docker Desktop): 3 runs of the 32-task grid with `WORKERS` = 16, runtime per task from `log.csv`
(wall time of the full grid ÷ tasks for the energy basis).

- **Energy:** no software meter is available here. Windows exposes no RAPL/"Energy Meter"
  counters on this machine, Docker's Linux VM has no `/sys/class/powercap`, and the battery meter
  only works while discharging (the laptop was on mains, checked 2026-11-21). The case allows
  runtime × device power. Candidate figure: **processor base power 28 W** (minimum assured 20 W,
  maximum turbo 115 W), [Intel ARK, Core Ultra 7 255H](https://www.intel.com/content/www/us/en/products/sku/241751/intel-core-ultra-7-processor-255h-24m-cache-up-to-5-10-ghz/specifications.html),
  read from a search snippet on 2026-11-21 (check on the page). This is a CPU-package estimate at
  full load, not a measurement. A battery-powered run with `scripts/measure_job.py` on a Linux
  laptop would replace it.
- **Scaling to the 1 kW reference server:** tasks per server-hour = 1,000 Wh ÷ energy per task,
  as for T-02 (assumes energy scales linearly).
- **LiveRun:** January and July replay with `--force-pause-step`, pending the .NET 8 SDK.
