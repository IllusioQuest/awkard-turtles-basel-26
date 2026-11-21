# Awkward Turtles · Basel GreenTech 2026

**Case: Useful heat from computing (DigitalHeat).**
Can computing power heat a home while doing useful work? We build a demonstrator that schedules
interruptible computing jobs on distributed servers so that their heat arrives where and when homes
need it, over a full year, with transparent and reproducible numbers.

## Start here

- [Case description (verbatim)](docs/case/challenge.md)
- [Key figures and must-have checklist](docs/case/key-figures.md)
- [External references and data sources](docs/case/references.md)
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
The default unlimited YearRun now calls Network B2 through a one-site adapter each hour;
`config/network/reference-only.json` is recorded as an input. Finite and mixed YearRun still
use the Core queue path. The exact regression checks and remaining integration work are in
[`docs/model/unified-scheduler-boundary.md`](docs/model/unified-scheduler-boundary.md).
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
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/ch-100.json
dotnet run --project src/DigitalHeat.FleetRun -- generate-houses config/fleet/houses/ch-100.spec.json -o config/fleet/houses/ch-100.csv
dotnet run --project src/DigitalHeat.FleetRun -- post data/derived/fleet/reference
dotnet test src/DigitalHeat.Fleet.Tests
# read-only results API for the dashboard (port 5080; endpoints in docs/model/fleet-api.md)
dotnet run -c Release --project src/DigitalHeat.FleetApi -- --runs data/derived/fleet --port 5080
```

Results go to `data/derived/fleet/<scenario>/` (git-ignored). Pricing is modelled; see the known
simplifications in the output README.

### Live household request

With Docker and the local `digitalheat/research-sim:live-demo` image available, start the Fleet API
and web dashboard as above, open `http://127.0.0.1:5173/live`, then run one of:

```bash
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- summer
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- winter
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- winter --busy
```

Fleet's heat-first scheduler controls a dedicated real Monte Carlo container. The dashboard separates
modelled heat, tank and accelerated time from observed process state and computed task results. The
request ends when the replay asks it to finish its current task; simulated and real task counts are
not equated. Busy mode adds an illustrative simulated queue beside the single real request.
Configuration, sources, limitations and details: [`T-69`](docs/tickets/T-69-live-household-demo.md).

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
