# Status: work map

Last updated: 2026-11-21 02:35 Europe/Zurich. Every agent updates its row when it starts, when it
changes an interface, and when it finishes (see `AGENTS.md` §9a).

## Who works on what

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| team | `main` | Case context; data (Open-Meteo Basel 2025, ElCom 2026 IWB H4, WP1 AI service and compute prices, WP2 costs and energy prices, WP3 sites and profiles incl. When2Heat extract); vision draft (`docs/vision.md`); research plan; AGENTS rules | `data/raw/` and its `SOURCE` files | merged |
| Jona (IllusioQuest) / Codex | `feat/core-csharp` | .NET 8 `DigitalHeat.Core` (heat demand, tank, `IScheduler.Decide(HeatSignal)`, `IExecutor.ApplyTargetLoad`, `SchedulerController.Apply`, `GreedyHeatScheduler`, year simulation, economics), `DigitalHeat.YearRun`, xUnit tests, `config/reference-house.json`, decision 0005 (accepted) | defines `IScheduler`, `IExecutor`, `HeatSignal`, `config/reference-house.json` | merged to main (PR #2); real Docker executor added by PR #3 |
| Jona (IllusioQuest) / Codex | `feat/T-05-job-queue` | T-05 job arrivals, queue, deadlines and year results; no runtime or example-job work | extends `YearSimulation` with optional finite-job inputs while keeping unlimited comparison intact | merged to main (PR #6); T-01 sourced job catalog data pending |
| Jona (IllusioQuest) / Codex | `docs/T-06-reference-results` | T-06 reproducible reference-house result variants and cost caveats | reads YearRun outputs and WP2 costs; no core or runtime interface changes | first version ready in PR #8; 4-server draft presentation baseline selected after user delegation; sourced finite job inputs pending |
| Nikita | `feat/T-27-hosting-isolation` | Four Linux hosting nodes: separate frontend/API pools, per-node firewall isolation, redundant gateways/monitors | No shared scheduler change; T-24 retains locality policy; snake_case export T-13 merged | T-09 and T-13 done; T-23 catalog integration pending; T-27 verified in PR #12 awaiting merge. 17 hosting tests, four node-failure and paused-gateway recovery scenarios pass; see webhosting-isolation-runtime.md and webhosting-privacy.md |
| Jona (IllusioQuest) / Codex | `feat/job-runtime` | Generic Docker control of a running job through the shared scheduler; day replay and decision 0009 (was 0006). Does not implement example jobs. | Adds `IContainerControl` and `DockerJobExecutor : IExecutor`; consumes core interfaces without changing them. | merged to main (PR #3); 9 .NET tests pass and a Busybox Docker smoke replay paused, resumed and throttled as requested |
| Jona (IllusioQuest) / Codex | `feat/T-24-job-data-locality` | T-24 customer/contract-sourced job location policy and pre-execution eligibility guard; coordinate with T-01 catalog owner | `config/jobs/job.schema.json`, `JobDescriptorLoader`, Core `JobPlacement.IsEligible`, `DigitalHeat.LiveRun` preflight | draft PR #9; 31 .NET tests pass, mismatched-site LiveRun smoke denied before Docker; T-01/T-02 descriptors need the new field, T-10/T-12 must reuse guard with trusted site metadata |
| Ennio / Claude | `feat/T-02-transcription-job` | T-02: whisper.cpp (base) transcription container, laptop runtime and energy measurement (RAPL + battery), scaling to 1 kW | Supplies `config/jobs/transcription-whisper.json` (schema from decision 0009) and a running container for `DigitalHeat.LiveRun` | in progress |
| team lead / Claude | `exp/year-explorer-transparency` | Browser year explorer prototype for the reference house, `METHOD.md`, `verify.py` | own JavaScript model (must match the C# core) | prototype; review findings pending: example computing price drives the surplus headline, verdict ignores hardware, offline demo, heatmap legend |
| team lead / Claude | `feat/T-01-job-catalog` | T-01: schema change for always-on services (webhosting) once Nikita has decided the representation; transcription descriptor comes from T-02 | `config/jobs/job.schema.json`, `JobDescriptorLoader` (decision 0009) | blocked: waiting for Nikita's decision (options in T-01) |
| team lead / Claude | `docs/T-07-draft-submission` | T-07: fresh-clone check and must-have checklist (`docs/submission/draft-checklist.md`); README demo section once T-02 is merged | none | in progress |
| team lead / Claude (research subagents) | `data/job-demand-scenario`, `data/computing-price` | T-26: sourced transcription demand scenario (`config/job-scenarios/transcription-basel.json`) and computing-price recommendation; values go to T-06 (Jona) after the team lead decides | new scenario file only; default scenario unchanged | in progress |
| team lead / Claude (subagent) | `feat/T-28-price-updater` | T-28: Azure Retail Prices snapshot (Switzerland North) and derived CHF per server-hour (`data/derived/computing-price.json`) | new files only (`scripts/`, `data/raw/market/price-snapshots/`, `data/derived/`); does not change `config/reference-house.json` (T-06) | PR #11 open; derived C1 0.687 vs adopted 0.66 |
| team lead / Claude (subagent) | `data/T-15-hardware-profiles` | T-15: hardware profiles (CPU server, GPU inference server) in `config/hardware/*.json`, loader + test, `docs/research/hardware.md` | new files only; reference house keeps the case's 1 kW server | in progress |
| team lead / Claude (subagent) | `docs/T-11-routing-model` | T-11: formal routing optimisation model (`docs/model/routing-optimisation.md`) and decision record; document only, no code | none yet; proposes interface changes for T-12 in the decision record | in progress |
| flerovium11 | `main` | `docs/todos.md`, `docs/questions.md` | none | ongoing; no names assigned to todos yet |

## Open coordination

- **Ennio (T-02) ↔ team lead/Claude (T-26):** demand model for the scored figures (premium transcription + filler load vs. heavier Whisper model vs. transcription only). Details and options: `docs/tickets/T-26-sourced-job-demand.md`. Ennio, please answer there.
- **Jona (T-06):** team lead adopted CHF 0.66 per server-hour as computing price for the draft (adjustable, range 0.25–1.80, `docs/research/computing-price.md`); please update `config/reference-house.json` in T-06.
- **Nikita (T-23) ↔ T-01:** representation of always-on services in the job catalog (options in `docs/tickets/T-01-job-catalog.md`).

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

## Nikita handoff: T-13 and hosting privacy (2026-11-21)
T-13 claimed on feat/T-13-result-export: opt-in snake_case result bundles from the existing model.
No shared scheduler or scored formula changes. Export design uses decision 0011.
Hosting component isolation requested separately; threat model awaiting user choice.
Jona owns T-24 data-locality checks: hosting must reuse that rule when dynamic placement exists,
not invent a competing country/site policy. T-01 always-on catalog decision remains pending.
T-27: user approved frontend/API component isolation with documented operator limits; four-engine opt-in lab, decision 0012. T-13 exporter implementation underway; no shared input schema changes.

T-13: 29 tests pass; 8,760-row showcase generated from clean 8651786, legacy hourly/coverage comparison exact. T-27 renumbered from T-25 after concurrent team animated-house ticket collision; no files from that task changed.
T-13 ready for review in PR #10 (29 tests, exact hourly legacy comparison). T-27 images build; internal bridge ingress failed. Awaiting user choice on per-node firewall rules before network rollout. Existing hosting lab left running; privacy research and topology in docs/webhosting-privacy.md.
T-27: four independent frontend/API engines verified with firewall isolation, 17 hosting tests, browser inspection, all four node-kill recovery scenarios and paused-gateway recovery. Runtime guide contains measured results; code PR pending, no automatic controller or heat-routing claim.
T-13 complete: PR #10 verified merged as e554f196; snake_case export contract and showcase are now on main. T-27 remains a separate reviewed implementation branch.
