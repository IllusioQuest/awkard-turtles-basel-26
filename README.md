# Awkward Turtles · Basel GreenTech 2026

**Case: Useful heat from computing (DigitalHeat).**
Can computing power heat a home while doing useful work? We build a demonstrator that schedules
interruptible computing jobs on distributed servers so that their heat arrives where and when homes
need it, over a full year, with transparent and reproducible numbers.

> Status: C# .NET 8 core proposed for the reference-house draft; see
> [decision 0005](docs/decisions/0005-csharp-core.md). The team has not accepted
> the broader architecture yet.

## Start here

- [Case description (verbatim)](docs/case/challenge.md)
- [Key figures and must-have checklist](docs/case/key-figures.md)
- [External references and data sources](docs/case/references.md)
- [Deep-dive notes with the case owner](docs/deep-dive/notes-structured.md) ([raw](docs/deep-dive/notes-verbatim.md))
- [Data and how to fetch it](data/README.md) · [Assumptions](docs/assumptions.md) · [Decisions](docs/decisions/README.md)
- [Instructions for AI agents](AGENTS.md)

## Demo in four steps

Requirements: .NET 8 SDK, Docker (Docker Desktop with Linux containers on Windows/macOS). Runs offline
after the image is built. The demo job is the Type 2 research simulation (2D Ising Monte Carlo, no
personal data, no model download); details in [`docs/jobs/research-sim.md`](docs/jobs/research-sim.md).

1. **Yearly balance of the reference house** (must-have 3):

   ```bash
   dotnet run --project src/DigitalHeat.YearRun -- .
   ```

   Prints coverage, server-hours and the case break-even; writes `results/reference-house/`.
   Published draft figures: [`docs/results/reference-house.md`](docs/results/reference-house.md).

2. **Build and start the real job** in its own container:

   ```bash
   docker build -t digitalheat/research-sim jobs/research-sim
   docker run --rm -v "$PWD/data/external/research-sim/work:/work" digitalheat/research-sim prepare
   docker run -d --name research-sim -e PASSES=0 -v "$PWD/data/external/research-sim/work:/work" digitalheat/research-sim run
   ```

3. **January day: the scheduler controls the running job** (must-have 2). The same scheduler code as
   step 1 sets the job's CPU allowance hour by hour:

   ```bash
   dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-01-15 --job config/jobs/research-sim-montecarlo.json --container research-sim --cpu-cores 8 --step-seconds 2
   ```

4. **July day with a forced pause** (tank full → job frozen, then resumed; press `P` for a manual pause):

   ```bash
   dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-07-15 --job config/jobs/research-sim-montecarlo.json --container research-sim --cpu-cores 8 --step-seconds 2 --force-pause-step 12
   ```

   Clean up afterwards with `docker rm -f research-sim`.

## Live network demo (T-54)

Four Docker containers act as four houses on one laptop. The **real network scheduler** (B2 by
default, the LP or B1 one click away) decides every scheduler hour from the four heat sliders and
moves real waiting Ising tasks between the houses' queues; each container's load goes through
`SchedulerController.Apply` → `DockerJobExecutor` (pause / CPU quota). New jobs appear only when you
press **+ New job** (or keys 1–4) or switch on auto-spawn. Decision
[0042](docs/decisions/0042-live-demo-network-scheduler.md).

**Start (one line, repository root, Docker Desktop running, .NET 8 SDK):**

```
dotnet run --project src/DigitalHeat.LiveGrid -- --up
```

It builds and starts the four containers, opens <http://127.0.0.1:5087> and stops the containers on
**Ctrl+C**. Press **F11** for full screen.

The terminal prints one line per scheduler hour with each house's heat request, planned load and
observed Docker CPU load. Job arrivals, real task moves, completions and missed deadlines appear
as separate event lines; the CPU figure is a measurement, not a heat reading.

**Showing it:** set House 1 to 0 % heat and House 2 to 100 %, then press **1**. Job 1 arrives at
House 1 (blue banner); House 1 pauses and the scheduler sends its tasks one by one to the warm
houses (flying tokens; the reason appears in the receiving house and in the log). Switch to "No
network" (B1) and repeat: the job stays and misses its deadline. Keys: 1–4 new job, A auto-spawn,
S scheduler. Reset empties all queues.

**Recorded fallback:** while LiveGrid is running, record two minutes and replay the same page without
Docker. Replay serves the saved frames in a loop; controls are inert during playback.

```
python scripts/record_live_demo.py --minutes 2
dotnet run --project src/DigitalHeat.LiveGrid -- --replay data/derived/live-demo/recording.jsonl
```

The recorder polls `/api/state` every 0.4 seconds. Use `--output` to choose another recording path.

**Two laptops, eight actual containers:** decision [0047](docs/decisions/0047-two-device-live-agent.md)
implements the two-device plan in 0042. Both laptops need this branch, Docker Desktop, .NET 8 and
the same temporary `DIGITALHEAT_AGENT_TOKEN` value. They must be on one reachable LAN. Laptop A is
the only scheduler and model clock. Laptop B hosts four Docker workers and an agent; it does not
run another scheduler. First on laptop B:

```powershell
$env:DIGITALHEAT_AGENT_TOKEN = '<same temporary token on both laptops>'
dotnet run --project src/DigitalHeat.LiveGrid -- --agent --up --config config/live-demo-eight-node.json --port 5088
```

Then on laptop A, replacing the address with laptop B's WLAN/Ethernet IPv4 address:

```powershell
$env:DIGITALHEAT_AGENT_TOKEN = '<same temporary token on both laptops>'
dotnet run --project src/DigitalHeat.LiveGrid -- --up --config config/live-demo-eight-node.json --peer-url http://<laptop-B-IPv4>:5088 --listen-lan
```

Laptop A opens `http://127.0.0.1:5087/?nodes=1-4`. On laptop B open
`http://<laptop-A-IPv4>:5087/?nodes=5-8`. Both pages read the same coordinator state; each shows
four rooms. The browser's `+ job` and slider on either screen reach the same scheduler. The node
agent requires the token on every request. Allow inbound TCP 5088 on B and 5087 on A through the
Windows firewall if prompted. `Test-NetConnection <laptop-B-IPv4> -Port 5088` on A checks the
connection. Stop A and B with Ctrl+C, then verify `docker ps`; if demo containers remain, run
`docker compose -f compose.live-demo.yml down` on A and
`docker compose -f compose.live-demo-peer.yml down` on B. This route has been checked with two
processes and eight containers on one machine, including a simulated peer outage; a physical
two-laptop rehearsal is still required. Eight containers overloaded the single test laptop and one
32-task job missed its synthetic deadline by eight tasks. That is not evidence about two-laptop
throughput. Task time was calibrated in isolation on A, so measure B and concurrent throughput
before claiming equal model server-hour capacity on both CPUs.

**Measured workload and scale:** one job is a technical 32-task Ising parameter scan, with 5,000
measurement and 500 thermalisation sweeps per task: 180,224,000 spin updates are actually executed.
Each task took 3.719, 4.025 and 4.007 s in three isolated 0.5-CPU container runs on the development
laptop (median 4.007 s, 2026-11-21). One model hour is 2 wall seconds, so a task represents 2 model
server-hours and the batch represents 64. The 20-model-hour deadline (40 wall seconds) is a **synthetic
demo scenario**, not a customer requirement. A single warm house with three server slots has only
60 server-hours before that deadline; distribution across houses is useful. See
`config/live-demo-scale.json` for the conversion and sources. The UI limits visible waiting circles
to 14 per house and shows the remaining count; every circle corresponds to a real task file. CPU is
observed; the case's 1 kW server and recovered heat are not measured by this laptop run.

**Task size on a new laptop:** first stop the demo containers, then measure at the actual per-slot
0.5-CPU quota. One task should take about 4 s on one slot. Run the command three times and use the
median full-task duration; update `taskSweeps` and `config/live-demo-scale.json` together:

```
docker compose -f compose.live-demo.yml down
docker run --rm --cpus 0.5 -e TARGET_SECONDS=4 -e PROBE_SWEEPS=5000 digitalheat/research-sim:live-demo calibrate
```

Queue check without Docker (four workers, a mover that keeps moving files):
`python jobs/research-sim/test_fleet.py`. Limits: the slider is an illustrative heat demand, one
step is a slowed hour, running tasks do not migrate, laptop power and heat are not measured.

Other measured jobs (transcription, LLM summary) use the same LiveRun command with their own
descriptor in [`config/jobs/`](config/jobs/) and container; see [`docs/jobs/`](docs/jobs/).
The full LiveRun options (placement rules, observed load) are under
"Live control of a containerized job" below.

## Quick checks

```bash
python3 scripts/fetch_data.py --check   # validate the data against the case's expected values
scripts/deadline-reminder.sh            # time left until our deadlines
```

## C# reference-house core (proposed)

Requires the .NET 8 SDK. From the repository root:

```bash
dotnet restore src/DigitalHeat.sln
dotnet build src/DigitalHeat.sln --no-restore
dotnet test src/DigitalHeat.sln --no-build
dotnet run --project src/DigitalHeat.YearRun -- .
```

The last command reads the committed Basel 2025 hourly weather CSV, checks the
configured electricity price against the ElCom 2026 Basel H4 snapshot, and writes
`results/reference-house/hourly.csv` and `summary.json`. Pass another repository
root as the first argument when running from elsewhere. An optional second argument
selects a finite or mixed job scenario; the default is
[`config/job-scenarios/unlimited.json`](config/job-scenarios/unlimited.json).
Results are generated and
git-ignored; rerunning the command reproduces them. The parameter file is
[`config/reference-house.json`](config/reference-house.json): each fixed/scenario
value has a unit and source. Four servers, zero minimum load and the example
computing price are scenario settings from the year explorer, not case mandates or
market-backed income. Change the scenario in JSON, keeping the reference-house
case values fixed for scored comparisons.

`DigitalHeat.Core` exposes `IScheduler.Decide(HeatSignal)`, `IExecutor` and
`SchedulerController.Apply`. `DigitalHeat.YearRun` supplies the simulated executor;
`DigitalHeat.JobRuntime` supplies a Docker executor behind that same interface.
The default output marks missed deadlines as `not modelled yet` because unlimited
work has no finite job count. A finite scenario reports arrivals, completed jobs,
backlog, processed work and the percentage of jobs due within the simulated year
that missed their deadline. See [`docs/model/job-queue.md`](docs/model/job-queue.md).
The T-31 mixed-run example below is a technical queue fixture. Decision 0020 excludes medical
transcription as a home-site product; sourced finite arrivals for the pitch's public-document
LLM Type 1 are still missing.

```bash
dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/mixed-basel-technical.json --output results/t31-mixed-basel
```

Open `results/t31-mixed-basel/jobs.json` for per-type server-hours and income. The old
`summary.json` export stays at schema v1. The method, a 35% sold-capacity sensitivity, sources
and a worked hour are in [`docs/model/mixed-job-queue.md`](docs/model/mixed-job-queue.md).
Electricity, used-heat value and case break-even exclude hardware and operating
costs, so the result is not a full-cost profitability claim.

The calculation uses the local day's mean temperature to decide whether the case
heating formula applies, then adds 12 kWh/day of hot water evenly across 24 hours.
Each hour, the scheduler requests enough server heat to meet demand and fill remaining
tank space, limited by server count and minimum load. The balance records heat sent
directly to the house, charged to/discharged from the lossless tank, backed up and
dumped. Electricity cost is server kWh × the ElCom price; used-heat value is heat
actually used × that price ÷ 2.5; case break-even is their difference divided by
server-hours sold. For one full-load 1 kW server-hour whose 0.8 kWh recoverable heat
is all used, electricity costs CHF 0.3325, heat is worth CHF 0.1064, and the case
break-even is CHF 0.2261 per server-hour. These inputs come from the case and the
ElCom snapshot, with exact source labels in the JSON file.

## Fleet simulation (T-58)

Many houses with servers, jobs arriving over a year, a pluggable scheduler and pricing. Spec:
[`docs/model/fleet-simulation.md`](docs/model/fleet-simulation.md); outputs and columns:
[`data/derived/fleet/README.md`](data/derived/fleet/README.md).

```bash
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/ch-1000.json
dotnet run --project src/DigitalHeat.FleetRun -- generate-houses config/fleet/houses/ch-1000.spec.json -o config/fleet/houses/ch-1000.csv
dotnet run --project src/DigitalHeat.FleetRun -- post data/derived/fleet/reference --hardware config/hardware/gpu-inference-l4.json
dotnet test src/DigitalHeat.Fleet.Tests
# read-only results API for the dashboard (port 5080; endpoints in docs/model/fleet-api.md)
dotnet run -c Release --project src/DigitalHeat.FleetApi -- --runs data/derived/fleet --port 5080
```

Results go to `data/derived/fleet/<scenario>/` (git-ignored). Mock scheduler and mock pricing are
placeholders; see the known simplifications in the output README.

## Live control of a containerized job

`DigitalHeat.LiveRun` attaches to an **already running Linux container**. It does not
start or implement a workload. Ennio's transcription work owns
its containers, descriptors and measurements; the descriptor format is
[`config/jobs/job.schema.json`](config/jobs/job.schema.json). It records an ID,
image or container command, supported controls, work unit, optional deadline,
runtime and energy per unit (each number with unit and source), and an optional
host-visible progress file. Missing measurements are `null` with a visible
`TODO(source)` in the job's documentation. The job must be interruptible and support
freeze; throttle is optional. Checkpoint is descriptive until its owner provides
a protocol.

Every descriptor also needs a sourced `placementPolicy`:
`{"mode":"anywhere","source":"customer authorization reference"}`,
`{"mode":"origin-site","source":"customer contract reference"}`, or
`{"mode":"country-allowlist","allowedCountryCodes":["CH"],"source":"customer contract reference"}`.
The customer or their contract determines this rule; the job provider only records it.
The source must identify the actual authorization or contract clause. These strings are
examples, not claims about an existing customer. For `origin-site`, supply
`--data-origin-site-id <id> --execution-site-id <id>` at run time. For a country
allowlist, supply `--execution-country-code <two-uppercase-letter-code>`.
Missing or mismatched location data blocks a restricted job **before Docker control**.
These CLI values are self-reported demo inputs; production routing needs trusted site
identity and must call `JobPlacement.IsEligible` before assigning work. The current
single-site year simulation does not enforce geographic routing. See
[`docs/decisions/0013-job-data-locality.md`](docs/decisions/0013-job-data-locality.md).

From the repository root, after the owner has started a dedicated container that
the replay may exclusively control:

```bash
dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-01-15 --job config/jobs/<owner-job>.json --container <container-id> --cpu-cores <allowed-cpu-cores> --step-seconds <positive-seconds>
dotnet run --project src/DigitalHeat.LiveRun -- --date 2025-07-15 --job config/jobs/<owner-job>.json --container <container-id> --cpu-cores <allowed-cpu-cores> --step-seconds <positive-seconds> --force-pause-step 12
```

Press `P` during a terminal replay to force one full-tank/zero-demand signal. The
forced step option makes the pause repeatable without a keyboard. At the end,
LiveRun sets the declared CPU allowance and unpauses the container; it does not
restore an arbitrary prior container state. Use
Docker Desktop with Linux containers on Windows/macOS, or Docker Engine on Linux.
`DockerCliContainerControl` is the only class that calls Docker; Nikita's node
runtime may reuse it or implement `IContainerControl` with its own Docker client.
The replay uses the same `GreedyHeatScheduler` and `SchedulerController.Apply`
as YearRun, but each hour uses an independent **reference simulated tank** snapshot,
not a sensor reading. A forced pause does not modify later reference snapshots.
`docker stats` CPU use divided by the allowed CPU cores is shown as observed
CPU load; it does not establish electrical power or recovered heat. See
[`docs/decisions/0009-generic-job-control.md`](docs/decisions/0009-generic-job-control.md).


For the separate user-requested browser gaming prototype, see [gaming prototype](docs/gaming.md). The webhosting feature has been retired.
