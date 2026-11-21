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
| Nikita | `feat/T-36-hosting-scheduler` | Six Linux nodes, heat-aware routing and automatic stateless replica relocation; common batch/always-on catalog | Reuses shared scheduler and locality policy; adds optional serviceMode and LiveRun guard | 45 hosting + 51 core/catalog tests pass; Docker heat/drain/failover and six-node isolation verified; all eight final Docker checks passed; PR #19 awaiting review/merge. Depends on PR #12; docs/webhosting-scheduler.md |
| Jona (IllusioQuest) / Codex | `feat/job-runtime` | Generic Docker control of a running job through the shared scheduler; day replay and decision 0009 (was 0006). Does not implement example jobs. | Adds `IContainerControl` and `DockerJobExecutor : IExecutor`; consumes core interfaces without changing them. | merged to main (PR #3); 9 .NET tests pass and a Busybox Docker smoke replay paused, resumed and throttled as requested |
| Jona (IllusioQuest) / Codex | `feat/T-24-job-data-locality` | T-24 customer/contract-sourced job location policy and pre-execution eligibility guard; coordinate with T-01 catalog owner | `config/jobs/job.schema.json`, `JobDescriptorLoader`, Core `JobPlacement.IsEligible`, `DigitalHeat.LiveRun` preflight | draft PR #9; 31 .NET tests pass, mismatched-site LiveRun smoke denied before Docker; T-01/T-02 descriptors need the new field, T-10/T-12 must reuse guard with trusted site metadata |
| Jona (IllusioQuest) / Codex | `feat/T-25-animated-house` | T-25 house visual, stopped after user review of SVG, text-first and 3D attempts | presentation files only; no core or scheduler interface change | unfinished and paused by user; draft PR #17 closed without merge; local experiment retained for reference |
| Ennio / Claude | `feat/T-02-transcription-job` | T-02: whisper.cpp transcription container (`jobs/transcription/`), `scripts/measure_job.py`, measured base and large-v3 on a laptop, scaling to 1 kW, LiveRun January/July replay with the real job (`docs/jobs/transcription.md`) | Adds `config/jobs/transcription-whisper-base.json` and `-large-v3.json` (schema of decision 0009); no shared interface changed | merged (PR #15); base 4.62 s and 0.0382 Wh per audio min (whole laptop), large-v3 70.3 s and 0.477 Wh (CPU package) |
| Ennio / Claude | `feat/T-33-local-llm-job` | T-33: local LLM batch service (Apertus 8B, llama.cpp) container, laptop measurement, per-token energy and income per server-hour (`docs/jobs/llm-summary.md`) | Adds `config/jobs/llm-summary-apertus-8b.json`, `--memory` option in `scripts/measure_job.py` | PR open; 8.9 s and 0.0614 Wh per audio min summarised, 0.254 kWh per 1M tokens (CPU package) |
| Ennio / Claude | `docs/T-37-profitability-research` | T-37 profitability research + synthesis; decision 0019 (proposed): Type 2 rides on Type 1 hardware, GPU nodes only, no CPU-only servers | new CSVs in `data/raw/market/`, Azure region snapshot; feeds T-14/T-34 parameters; changes T-30 to a GPU kernel with CPU fallback | merged (PR #20); only batched Type 1 LLM on consumer GPUs clears full cost in year 1 (8-42 % fill); over 6 years at falling commodity prices nothing pays without a premium or fixed contracts; GeForce licence excludes "datacenter deployment" (open risk) |
| team lead / Claude | `exp/year-explorer-transparency` | Browser year explorer prototype for the reference house, `METHOD.md`, `verify.py` | own JavaScript model (must match the C# core) | prototype; review findings pending: example computing price drives the surplus headline, verdict ignores hardware, offline demo, heatmap legend |
| team lead / Claude | `feat/T-01-job-catalog` | T-01: schema change for always-on services (webhosting) once Nikita has decided the representation; transcription descriptor comes from T-02 | `config/jobs/job.schema.json`, `JobDescriptorLoader` (decision 0009) | approved: narrow always-on support implemented by Nikita in T-36; broader catalog work remains with Claude |
| team lead / Claude | `docs/T-07-draft-submission` | T-07: fresh-clone check and must-have checklist (`docs/submission/draft-checklist.md`); README demo section once T-02 is merged | none | in progress |
| team lead / Claude (research subagents) | `data/job-demand-scenario`, `data/computing-price` | T-26: sourced transcription demand scenario (`config/job-scenarios/transcription-basel.json`) and computing-price recommendation; values go to T-06 (Jona) after the team lead decides | new scenario file only; default scenario unchanged | in progress |
| team lead / Claude (subagent) | `feat/T-28-price-updater` | T-28: Azure Retail Prices snapshot (Switzerland North) and derived CHF per server-hour (`data/derived/computing-price.json`) | new files only (`scripts/`, `data/raw/market/price-snapshots/`, `data/derived/`); does not change `config/reference-house.json` (T-06) | PR #11 open; derived C1 0.687 vs adopted 0.66 |
| team lead / Claude (subagent) | `data/T-15-hardware-profiles` | T-15: hardware profiles (CPU server, GPU inference server) in `config/hardware/*.json`, loader + test, `docs/research/hardware.md` | new `HardwareProfile` loader in Core (new file, no existing interface changed); reference house keeps the case's 1 kW server | PR open; 40 .NET tests pass. Finding for T-06: one R570 is ≈ 0.16 kW, not 1 kW |
| team lead / Claude (subagent) | `docs/T-11-routing-model` | T-11: formal routing optimisation model (`docs/model/routing-optimisation.md`) and decision record; document only, no code | none yet; proposes interface changes for T-12 in the decision record | in progress |
| team lead / Claude (subagent) | `data/T-15-hardware-profiles` | T-15: hardware profiles (CPU server, GPU inference server) in `config/hardware/*.json`, loader + test, `docs/research/hardware.md` | new files only; reference house keeps the case's 1 kW server | in progress |
| team lead / Claude (subagent) | `docs/T-11-routing-model` | T-11: formal routing optimisation model (`docs/model/routing-optimisation.md`) and decision record; document only, no code | none; decision 0014 proposes `INetworkScheduler`/`NetworkState`/`NetworkDecision` and an allocation-aware queue for T-12 (own ADR there) | draft ready for team review; 3 team questions in model §11 |
| team lead / Claude (subagent) | `data/T-35-refurbished-hardware` | T-35: refurbished/second-life hardware (lever D): prices, wear model, `config/hardware/*-refurbished.json`, `docs/research/refurbished-hardware.md` | new profile files only; reuses T-15 `HardwareProfile` loader and schema | in progress |
| team lead / Claude (subagent) | `feat/T-14-economics` | T-14: economics per actor in the C# core (system surplus with premium + filler income, heat value per alternative, hardware profile, operator and host-owned split, case vs full-cost break-even, lever scenarios A/B/D/G, win-win grid export) | extends Core with a new economics module and config file; scored case figures unchanged; reads `config/hardware/*.json` (T-35 profiles plug in) | in progress |
| Ennio / Claude | `docs/two-job-types-strategy` | Decision 0018 (two job types: Type 1 transcription + batch LLM, Type 2 CPU batch compute filler with heat-led price), tickets T-30 to T-32 | none (docs only) | merged (PR #16) |
| flerovium11 | `main` | `docs/todos.md`, `docs/questions.md` | none | ongoing; no names assigned to todos yet |

## Open coordination

- **Jona (T-06), hardware cost:** T-15 found that one Dell R570 draws ≈ 0.16 kW, not 1 kW; a 1 kW reference server is ≈ 6.15 R570s (CHF 50,743). The hardware depreciation sensitivity in T-06 (A-017: one R570 per server) is therefore ≈ 6× too low. See `docs/research/hardware.md`.
- **Team lead decision (2026-11-21):** GPU inference server (L4 profile, `config/hardware/`) is the main hardware scenario for the pitch, with the three-profile comparison on one slide; scored reference-house figures keep the case's abstract 1 kW server.
- **Ennio (decision 0018):** two job types as the job model of T-34: Type 1 = transcription + batch LLM (GPU in the pitch, CPU measured for scored figures), Type 2 = CPU batch compute filler, own broker, heat-led price, no unpaid work. Mixed queue: T-31.
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
- Decision numbers taken: 0003 (webhosting), 0004 (Windows ingress, withdrawn), 0005 (C# core), 0006 (Linux ingress experiment), 0007 (whole-container pause proposal), 0008 (site heat state), 0009 (generic job control, renumbered from 0006), 0014 (routing optimisation model, T-11), 0018 (two job types).

## Known integration risks

1. **Generic real-job executor implemented** (must-have 2). Fake-control tests and a neutral
   Busybox container demonstrate command mapping; an owner-supplied real job is still needed for
   the scored end-to-end demo.
2. **Must-have 1 example jobs:** Nikita owns webhosting; Ennio's transcription job is measured (PR for T-02). At least one
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

## Hosting scheduler handoff (2026-11-21 03:15 Europe/Zurich)
Nikita / Codex claims T-29 for the user's requested scheduler connection. Proposed scope is a
hosting adapter using existing IScheduler and JobPlacement, keeping both component replicas
running; decision 0015 is pending user agreement/review. No T-10/T-12 network optimiser or shared
schema files are claimed. T-01 owner: user has been asked to choose one batch/always-on catalog
versus separate catalogs; no approval is implied until that reply is recorded.
T-29 decision 0015 reviewed (go with changes): mandatory pre-selection locality filtering, whole-set fallback for incomplete telemetry, controlled-demo traffic only. User routing/catalog choices pending; runtime unchanged. T-27 PR #12 is still a code dependency outside main at this check.
T-29: Nikita approved six-node automatic stateless replica move/pause and common batch/always-on catalog. T-01 decision is now recorded in its ticket; schema/loader implementation remains Claude-owned. Lifecycle decision 0016 under review; gateway controller reuses shared scheduler and locality, scored model unchanged.
T-29 implementation on feat/T-29-hosting-scheduler (stacked on unmerged T-27): shared scheduler + locality + synchronized admission/drain lifecycle; 43 tests pass. Six-node Linux build/integration running. T-01 catalog approval recorded; no competing schema edits.

Hosting scheduler ticket renumbered to T-36 because Ennio has a separate remote branch
feat/T-29-local-llm-job. No LLM files changed. Runtime code branch is now feat/T-36-hosting-scheduler.
Eight real Docker checks passed including actual pause states, heat swap, request drain,
stale/partial heat, locality denial and active-gateway kill after observing pause phase.
T-23/T-01 handoff: no unmerged implementation exists on feat/T-01-job-catalog (triple-dot diff
against main empty). Nikita now implements the narrow approved serviceMode loader/schema guard
and frontend/API catalog descriptors to finish the requested integration; Claude retains broader
catalog/research ownership. No new economic or measurement values are invented.
T-36/T-23: six-node real Docker lifecycle and failover checks pass; 45 hosting + 51 core/catalog tests pass. Narrow common catalog extension implemented, T-01 notified; always-on is blocked from LiveRun batch freeze. Final startup recovery check and PR publication next.

## Nikita: hybrid service review (T-38)
Reviewed the user-supplied GPU/CPU engineering manual against shared scheduler and vendor docs. Findings and proposed scope: research/hybrid-gpu-cpu-review.md. No code or interface changes; implementation awaits user choice. T-36 remains in PR #19. T-37 was already occupied by profitability research, hence T-38.

Nikita / Codex: T-39 on featur-gaming, Intel browser gaming discovery. Script pushed; host graphics paths exist but ordinary containers lack access. No Vulkan/encoding/CS2 support proven. Awaiting streaming-stack choice; see tickets/T-39-intel-browser-gaming.md. Shared scheduler interfaces unchanged.

## Business model provisionally fixed (2026-11-21, Jona / Claude)
Branch `docs/T-34-business-model-fixed`: decision 0020 (operator model; Type 1 = batch LLM on non-personal
documents, Type 2 = research simulations; transcription and webhosting out of the business model) and
`docs/business-model.md`. Reviewed (go with changes, applied). Docs only; no code, interface or scored
figure changed. Affects: T-30 (now a Monte Carlo research kernel), T-31 (Type 1 = LLM), T-14 (operator
model as main case), T-21 storyboard money scene. Ennio: 0018/0019 status lines updated, please check.
T-39 milestone: two healthy Selkies nodes, Intel OpenGL accelerated, CPU H.264 capture/encode proven; Vulkan CPU-only and VAAPI failed. Browser smoke and zero-heat admission choice pending. See gaming-lab.md. No session coordinator or CS2 gameplay claim.

T-39 update: browser desktop/input now confirmed by user screenshot/test. Summer session placement should use hotel/spa heat sites regardless of player location, preserving active matches. Live wallpaper stretch applied; GPU Vulkan/CS2 and coordinator still pending. No available useful-heat site fallback needs user choice.
## Paper skeleton (2026-11-21, Jona / Claude)
T-22: `docs/paper/outline.md` with title, draft abstract, candidate contributions, sections, figure list
(which run produces each figure) and team questions. Docs only.

T-39 interface coordination: user approved largest remaining useful heat capacity. Proposed ADR 0022 factors the existing GreedyHeatScheduler numerator into a shared helper (valid results unchanged), adds a gaming-only in-memory reservation model/demo; does not implement T-11/T-12 network optimisation or alter IScheduler/IExecutor. Runtime launch/preemption remains separate.

T-39: PR #22 now includes atomic per-site heat-capacity gaming reservations and deterministic home/hotel/spa demo. Shared Greedy numerator factored into HeatCapacity (IScheduler/IExecutor unchanged); 14 gaming and 40 Core tests pass. Runtime session launch/preemption still pending. See ADR 0022 and gaming-lab.md; no scored inputs changed.

T-39: user approved an isolated Mesa Dozen Vulkan build/probe. ADR 0023 proposed; existing A/B desktops untouched during build. Separate source-pinned image only; no Windows driver changes or game download. Core interfaces unchanged.
## T-30 research simulation job (2026-11-21, Jona / Claude)
Branch `feat/T-30-research-simulation-job`: 2D Ising Monte Carlo container (`jobs/research-sim/`),
descriptor `config/jobs/research-sim-montecarlo.json`, `docs/jobs/research-sim.md`. Done: pure Python
(PyPI unreachable from the hackathon network), measured on battery (0.858 s and 4.09e-6 kWh per task,
whole laptop), January/July LiveRun with forced pause through the shared scheduler. New Windows battery
meter `scripts/measure_job_windows_battery.ps1`. 40 .NET tests pass. No shared interface changed.

T-39 Dozen milestone: source build succeeded, hardware Vulkan identifies Intel Iris Xe (API 1.2.335), vkcube succeeds headlessly and in separate Selkies desktop C (18883), A/B untouched. Experimental/non-conformant; VAAPI still unavailable, no CS2 claim. User cube-stream confirmation and storage choice pending; current C: ~20 GB versus Valve's 85 GB requirement.
