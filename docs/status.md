# Status: work map

Last updated: 2026-11-21 01:08 Europe/Zurich. Every agent updates its row when it starts, when it
changes an interface, and when it finishes (see `AGENTS.md` §9a).

## Who works on what

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| team | `main` | Case context; data (Open-Meteo Basel 2025, ElCom 2026 IWB H4, WP1 AI service and compute prices, WP2 costs and energy prices, WP3 sites and profiles incl. When2Heat extract); vision draft (`docs/vision.md`); research plan; AGENTS rules | `data/raw/` and its `SOURCE` files | merged |
| Jona (IllusioQuest) / Codex | `feat/core-csharp` | .NET 8 `DigitalHeat.Core` (heat demand, tank, `IScheduler.Decide(HeatSignal)`, `IExecutor.ApplyTargetLoad`, `SchedulerController.Apply`, `GreedyHeatScheduler`, year simulation, economics), `DigitalHeat.YearRun`, xUnit tests, `config/reference-house.json`, decision 0005 (accepted) | defines `IScheduler`, `IExecutor`, `HeatSignal`, `config/reference-house.json` | merged to main (PR #2); real Docker executor added by PR #3 |
| Jona (IllusioQuest) / Codex | `feat/T-05-job-queue` | T-05 job arrivals, queue, deadlines and year results; no runtime or example-job work | extends `YearSimulation` with optional finite-job inputs while keeping unlimited comparison intact | PR #6 open; 13 .NET tests and data check pass; T-01 sourced job catalog data pending |
| Nikita | `feat/T-09-site-heat-state` | Linux gateway crash experiment, three nested Docker engines, sample, placement/contracts; ADRs 0003 and 0006 proposed | no shared heat interface changed; future integration must use existing IExecutor | merged networking milestone (0e7115a); T-09 implemented, 23 integrated core/runtime tests pass; PR #5 review/merge pending; plan photo still missing; whole-container pause recovery tested with either manager absent; controller/etcd/node orchestration and heat integration pending; see webhosting-runtime.md |
| Jona (IllusioQuest) / Codex | `feat/job-runtime` | Generic Docker control of a running job through the shared scheduler; day replay and decision 0009 (was 0006). Does not implement example jobs. | Adds `IContainerControl` and `DockerJobExecutor : IExecutor`; consumes core interfaces without changing them. | merged to main (PR #3); 9 .NET tests pass and a Busybox Docker smoke replay paused, resumed and throttled as requested |
| Ennio / Claude | `feat/T-02-transcription-job` | T-02: whisper.cpp (base) transcription container, laptop runtime and energy measurement (RAPL + battery), scaling to 1 kW | Supplies `config/jobs/transcription-whisper.json` (schema from decision 0009) and a running container for `DigitalHeat.LiveRun` | in progress |
| team lead / Claude | `exp/year-explorer-transparency` | Browser year explorer prototype for the reference house, `METHOD.md`, `verify.py` | own JavaScript model (must match the C# core) | prototype; review findings pending: example computing price drives the surplus headline, verdict ignores hardware, offline demo, heatmap legend |
| team lead / Claude | `feat/T-01-job-catalog` | T-01: second job type (document OCR) and catalog completeness (arrivals per day with source, prices); builds on decision 0009 schema and Ennio's T-02 transcription descriptor | `config/jobs/*.json` (no schema change planned) | in progress; plan proposed |
| flerovium11 | `main` | `docs/todos.md`, `docs/questions.md` | none | ongoing; no names assigned to todos yet |

## Shared interfaces

- `IScheduler.Decide(HeatSignal)` and `IExecutor.ApplyTargetLoad`, wired by
  `SchedulerController.Apply` (decision 0005, on main). Simulated and real executors
  must both sit behind `IExecutor`.
- `config/reference-house.json`: parameters of the reference house (`feat/core-csharp`).
- `IContainerControl` (on main, decision 0009): Docker operations in one adapter class; Nikita can
  implement this interface with his node-local Docker engine or reuse the CLI adapter. Nikita's
  webhosting and Ennio's transcription jobs should each provide a sourced descriptor conforming
  to `config/jobs/job.schema.json`, plus a running Linux container ID for `DigitalHeat.LiveRun`.
  Job-specific progress is an optional host-visible file. Both owners should record reproducible
  laptop runtime and power/energy measurements (or a visible `TODO(source)`), then test pause,
  resume and throttle using the shared scheduler. No job protocol is imposed by this branch.
- `data/raw/` with its `SOURCE` files (on `main`).
- Decision numbers taken: 0003 (webhosting), 0004 (Windows ingress, withdrawn), 0005 (C# core), 0006 (Linux ingress experiment), 0007 (whole-container pause proposal), 0008 (site heat state), 0009 (generic job control, renumbered from 0006).

## Known integration risks

1. **Generic real-job executor implemented** (must-have 2). Fake-control tests and a neutral
   Busybox container demonstrate command mapping; an owner-supplied real job is still needed for
   the scored end-to-end demo.
2. **Must-have 1 example jobs:** Nikita owns webhosting, Ennio owns transcription. At least one
   measured laptop run and energy scaling to 1 kW remain to be verified by their owners.
3. **Year explorer (JavaScript) and C# core must give the same numbers.** Explorer default:
   4 servers, min load 0 %, tank 35 kWh, 0.3325 CHF/kWh → 58.0 % covered, 0 kWh dumped,
   22,661 server-hours, break-even 0.226 CHF/h.
4. **Webhosting scope vs. the draft deadline** (2026-11-21 15:00).
5. **Decision numbers:** resolved. The C# core decision was renumbered to 0005 when PR #2 was merged.

## Next steps

All planned work is now in the ticket board: [`docs/tickets/README.md`](tickets/README.md)
(24 tickets, created 2026-11-21 from the whiteboard session). Start with **T-00** (team decisions),
then the tier-1 tickets T-01 to T-07 for the draft at 15:00.

- Nikita and Ennio: publish job descriptors, measured work-unit data and the runnable container
  IDs/commands; test each with the shared heat signal (`feat/job-runtime` is now on main).
