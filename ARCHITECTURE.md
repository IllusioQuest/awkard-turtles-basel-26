# Architecture

What we built, what it uses and where it lives. Written for us, the team, as of 2026-11-22
(`main`). Commands to run everything: [`QUICKSTART.md`](QUICKSTART.md). Every external source and
where it is used: [`SOURCES.md`](SOURCES.md).

## In one picture

```
                 config/ (scenarios, houses, job types, hardware)     data/raw/ (weather, prices, traces)
                                         │                                      │
                                         ▼                                      ▼
  Fleet simulation (C#)   FleetRun ─► DigitalHeat.Fleet ─► uses DigitalHeat.Core (heat model + heat-first policy)
                                         │
                                         ▼
                          data/derived/fleet/<run>/*.csv  ─►  FleetApi (C#, :5080)  ─►  web dashboard (React, :5173)
                                                                     ▲                     /admin, /admin/<run>/houses/<id>
                                                                     │
  Live pitch demo         phone /speak ─► FleetApi inbox ─► VoiceDemo (MeshPeer scheduler) ─► Docker job (Whisper + LLM)
                                                                     │                         frozen/thawed by the scheduler
                                                                     └─► live snapshot ─► /live
```

**What counts today:** the Fleet simulation for all house and network results, and the voice demo
for the live part of the pitch. The year model (`YearRun`) is retired; the network/LP scheduler is
research for the paper; mesh lab, renderer and gaming are experiments (see "Other parts").

## Stack

| Tool | Version | Used for |
|---|---|---|
| C# / .NET | 8 (`net8.0`) | all simulation, scheduling, API and demo code (`src/`, `DigitalHeat.sln`) |
| xUnit | 2.9 | C# tests (`src/*.Tests`) |
| Google OR-Tools (GLOP) | 9.15 | LP scheduler for the network research (`DigitalHeat.Network.Lp`) |
| ASP.NET Core minimal APIs | .NET 8 | `FleetApi` (dashboard API), `WorkloadRun` (mesh/render node hosts) |
| TypeScript, React, Vite | TS 6, React 19, Vite 8 | web dashboard (`web/`) |
| Recharts, MapLibre GL | 3, 6 | charts and the network map |
| Python | 3.10+ (standard library only, no packages to install) | data fetching, derived data, analysis and measurement scripts (`scripts/`) |
| Docker | Engine/Desktop with Linux containers | real jobs, voice demo, mesh lab, renderer, Minecraft |
| whisper.cpp | v1.9.5 | transcription job (`jobs/transcription`), models `ggml-base`, `ggml-large-v3` |
| llama.cpp | b12600 | LLM job (`jobs/llm`), model Apertus 8B Instruct Q4_K_M |
| Blender (Debian package) | bookworm | landscape renderer (`rendering/`) |
| LaTeX (Tectonic or latexmk) | – | paper (`docs/paper/latex`) |

## C# projects (`src/`)

| Project | What it does | Status |
|---|---|---|
| `DigitalHeat.Core` | The shared model: case heat demand (`HeatDemand`), lossless tank balance (`HeatBalance.Step`), heat-first load (`HeatFirstScheduler`, `HeatCapacity`), queue order (`JobPriority`), site ranking (`HeatShareRank`), parameters (`ReferenceParameters`), hardware profiles. No I/O, no UI. | **core, used by everything** |
| `DigitalHeat.Fleet` | Event-driven simulation of many houses, servers and jobs over a year: job generation from sourced volumes, pricing, the heat-first scheduler adapter (`HeatFirstFleetScheduler`), accounting. Spec: `docs/model/fleet-simulation.md`. | **current results model** |
| `DigitalHeat.FleetRun` | CLI: `generate-houses`, `run <scenario>`, `post <run>` (hardware costs). | current |
| `DigitalHeat.FleetApi` | Read-only HTTP API on 127.0.0.1:5080 over `data/derived/fleet/` for the dashboard; serves the live snapshot and takes voice-demo uploads (`POST /api/voice-demo/requests`). | current |
| `DigitalHeat.JobRuntime` | Controls a real container: `DockerJobExecutor` (freeze/unfreeze, CPU quota), job descriptors (`config/jobs/`). | current |
| `DigitalHeat.Orchestration` | Real-container scheduling: `WorkloadCoordinator`, `MeshPeer` (peer-to-peer heat-first mesh with durable journals), placement, Docker runtime, deadlines. Decisions 0049–0051, 0056, 0068. | current (live demo, labs) |
| `DigitalHeat.VoiceDemo` | Pitch demo: one in-process `MeshPeer` on the local Docker, a modelled summer reference house (EU hot-water profile), a spoken/typed request as a real Whisper + LLM container, measured CPU time and energy, compact console log, `/live` snapshot. Runbook: `docs/demo/voice-request.md`. | **current pitch demo** |
| `DigitalHeat.WorkloadRun` | Node hosts for the mesh lab (`mesh-node`) and the render lab (`render-node`), operator commands. | experiment (Nikita) |
| `DigitalHeat.Network`, `.Network.Lp`, `.NetworkRun` | Multi-site network scheduler with greedy and LP strategies, experiments for the paper. | research (Jona) |
| `DigitalHeat.LiveRun` | Older live demo: replays one day with the Core scheduler against a running container. | legacy |
| `DigitalHeat.LiveFleetRun` | Older live household demo on the Fleet engine (T-72). | legacy |
| `DigitalHeat.YearRun` | Year balance of the case house with unlimited jobs. | **retired** (results no longer used) |
| `*.Tests` | xUnit tests per project; case values (206 heating days, 26,850.5 kWh, …) are checked in `Core.Tests` and `Fleet.Tests`. | – |

Dependencies point one way: `Core` ← `JobRuntime` ← `Orchestration` ← `VoiceDemo`/`WorkloadRun`;
`Core` ← `Fleet` ← `FleetRun`/`FleetApi`; `Core` ← `Network` ← `Network.Lp`/`NetworkRun`.

## Web dashboard (`web/`)

Vite dev server on 127.0.0.1:5173; `/api` is proxied to FleetApi (:5080). Without the API the admin
pages fall back to mock data (yellow banner).

| Route | Page | Data |
|---|---|---|
| `/admin` | list of Fleet runs | `GET /api/runs` |
| `/admin/<run>` | network map (site icons by type), load and heat charts, income vs electricity, time slider, side panel per house | houses, network-hourly, snapshots, weather |
| `/admin/<run>/houses/<id>` | one household over a chosen period: heat, load, money, deadlines | house series, job events |
| `/live` | live summer house of the voice demo (or the older T-72 demo) | `data/external/live-household/current.json` via `/api/live-household` |
| `/speak` | phone page: record or type a request | `POST /api/voice-demo/requests` |

Code: `web/src/admin/` (network pages, map), `web/src/household/` (house pages, live page), `web/src/voice/`,
`web/src/data/` (API client, types, mock source).

## Real jobs (`jobs/`, Docker images)

| Image | Contents | Used by |
|---|---|---|
| `digitalheat/research-sim` | Python 2D Ising Monte Carlo (Type 2 research job) | LiveRun, mesh lab, T-72 demo |
| `digitalheat/transcription-whisper` | whisper.cpp + ffmpeg, queue runner | measurements (T-02), voice image base |
| `digitalheat/llm-summary` | llama.cpp, queue runner | measurements (T-33), voice image base |
| `digitalheat/voice-assistant` | both of the above: transcribe, then answer | VoiceDemo |

Models are never inside images; they are mounted from `data/external/` (git-ignored). Measured
runtime and energy per job: `docs/jobs/transcription.md`, `docs/jobs/llm-summary.md`,
`docs/jobs/research-sim.md`, raw files in `data/raw/measurements/`.

## Configuration (`config/`)

Every number is `{value, unit, source}`; scenarios carry a `_note` saying what they change.

| Path | Content |
|---|---|
| `reference-house.json` | the case reference house constants (10 kW formula, 35 kWh tank, 12 kWh/day, 1 kW server, 80 %, CHF 0.3325/kWh, CHF 0.66/server-hour) |
| `fleet/scenarios/` | one file per Fleet run: `reference`, `real-house`, `basel-region-100`, `ch-100`, `dreispitz-5-pool`, `alps-border-50`, one-day job examples |
| `fleet/houses/` | house files (case house, organisers' real house, generated fleets) |
| `fleet/job-types/` | transcription, LLM, research simulation: volume, sizes, deadlines, price, placement |
| `fleet/local/`, `fleet/regions/`, `fleet/clients/` | inputs for the house generators and job origins |
| `hardware/` | hardware profiles (abstract 1 kW server, CPU server, L4 GPU server, …) |
| `jobs/` | job descriptors for real containers |
| `network/` | network research scenarios |
| `voice-demo.json`, `hot-water-profile-l.json` | voice demo settings, EU tapping profile L |

## Data (`data/`)

- `raw/`: source data, unchanged, each with a `SOURCE.md` (URL, access date, licence). Committed.
- `derived/`: produced by scripts from `raw/` (`data/derived/README.md` lists the command per file).
  Fleet run outputs (`derived/fleet/<run>/`) are git-ignored and must be re-run locally.
- `external/`: large or personal files, git-ignored: models, audio, demand trace downloads, the
  organisers' real house data, live demo state.

## Scripts (`scripts/`)

| Group | Scripts |
|---|---|
| Fetch and check data | `fetch_data.py` (`--check` validates against the case), `fetch_swiss_grid.py`, `fetch_elcom_municipalities.py`, `fetch_demand_traces.py`, `fetch_prices.py` |
| Build inputs | `build_buildings_heat.py`, `make_local_fleet.py`, `make_climate_fleet.py`, `make_fleet_clients.py`, `derive_residential_population.py`, `calibrate_real_house.py`, `build_network_dataset.py` |
| Analysis | `price_comparison.py`, `profitability_inputs.py`, `hardware_comparison.py`, `refurbished_comparison.py`, `network_experiments.py`, `paper_figure_data.py` |
| Measurement | `measure_job.py` (runtime and energy of a container), PowerShell measurement helpers for Windows |

## How a Fleet run works

1. `FleetRun run config/fleet/scenarios/<name>.json` loads the houses, the job types and the hardware profile.
2. Heat demand per house and hour comes from Core's case formula on that house's weather.
3. Jobs are drawn in advance (seeded): Poisson arrivals with sourced volumes and weekly shapes, sizes
   and deadlines from traces (`jobs-input.csv`).
4. The engine runs event by event (ms resolution). On each event the heat-first scheduler starts,
   lowers or pauses chunks so that no work runs without useful heat; the tank is settled exactly.
5. Prices: Type 1 fixed market prices; Type 2 quoted by load at arrival (customers may reject).
6. Output per house, job and hour in `data/derived/fleet/<name>/`; `FleetRun post` adds hardware costs.

## How the live voice demo works

1. Phone `/speak` (through ngrok) or the runner's terminal → a file in the inbox.
2. `VoiceDemo` submits it to its `MeshPeer`, which admits it only if the house's useful heat covers
   the job's declared heat.
3. The house is a replayed 15 July (10 s per hour) with the EU hot-water profile; at 13:00 there is
   no hot-water draw, the tank is full, and the scheduler freezes the container (`docker pause`).
4. The 14:30 draw brings useful heat back; the scheduler thaws it; the answer streams to the console.
5. CPU time and energy come from the container's cgroup and the CPU's RAPL counter (Linux only).

## Other parts (experiments, not in the scored figures)

| Part | Where | What |
|---|---|---|
| Linux mesh lab | `mesh/`, `WorkloadRun mesh-node` | three Docker-in-Docker peers running the mesh protocol (Windows/PowerShell) |
| Landscape renderer | `rendering/`, `WorkloadRun render-node` | Blender frames as heat-scheduled GPU/CPU jobs, browser UI on :18900/:18950 |
| Gaming | `gaming/` (own solution) | Minecraft server and heat monitor; other games retired |
| Network research and paper | `src/DigitalHeat.Network*`, `docs/paper/` | greedy vs LP scheduling over a synthetic Swiss network |
| Prototypes | `prototypes/` | early year explorer (JavaScript) and map draft |

## Where to find answers

| Topic | Read |
|---|---|
| The case and scoring | `docs/case/challenge.md`, `docs/case/key-figures.md` |
| Model, assumptions, current results | `docs/model/fleet-simulation.md` (§16 results and assumptions) |
| Scheduler rules | `docs/model/unified-heat-first-mesh.md`, decisions 0049–0051, 0056, 0068 |
| Business model | `docs/business-model.md`, decision 0020 |
| Measured jobs | `docs/jobs/` |
| Prices, demand, hardware research | `docs/research/` (index in its README) |
| Why something was decided | `docs/decisions/` (index in its README) |
| Who did what | `docs/tickets/README.md`, `docs/status.md` |
