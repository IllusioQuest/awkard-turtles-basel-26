# Status: work map

Last updated: 2026-11-21 (repo cleanup). Per-ticket owner and status live only in the
[ticket board](tickets/README.md); this file holds what the board cannot: active branches, open
coordination between people, shared interfaces and integration risks. Keep your row current
(`AGENTS.md` §9a) and delete it when your work is merged. History is in git.

## Active work

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| Codex (house-data analysis) | `feat/T-63-house-siting` | T-63: private house archive, local evidence extraction and siting constraints | standalone Python CLI; scored reference inputs unchanged | in progress; raw archive stays local, live AI runtime unavailable |
| Nikita / Codex | feat/T-60-unified-scheduler | T-62 mesh prototype on Jona T-60 boundary; heat-first shared selection | Core ranking, peer offers/origin intents, retained-heat selected-site binding | in progress; ADR 0049; user selected deterministic mesh first, no reassignment while origin is down; T-61 equivalence remains open |
| Nikita / Codex | data/T-59-building-owner-mix | BFS 2020 building/owner mix analysis; coordinate T-58 | data/provenance and proposed generator mapping | PR #49 in review; 125 source rows + deterministic 1000-building composition; ADR 0045 reviewed; Ennio/T-58 notified on PR #48; mixed thermal integration awaits category heat/spatial inputs |
| Nikita / Codex | fix/T-57-scheduler-cleanup | Remove redundant validation/indexing and clarify event loop | no public interface/model changes | PR #46 in review; 61 orchestration + 46 Core tests; queued owner about half median runtime in both run orders, allocations down 83%; mixed idle timings documented; live games untouched |
| Nikita / Codex | feat/T-53-cs16-linux | CS on D recovered in OpenGL local test; aiming works; low-FPS investigation | existing C# profile launcher + indexed admission reused | draft PR #42; user confirms map/aiming and prior Internet access; 2-CPU quota saturated/throttled, 20-FPS software stream; CPU increase superseded by user-approved GPU direction; E accelerated GL verified in T-55; actual CS GPU/FPS pending; Minecraft unchanged |
| Nikita / Codex | feat/T-55-gaming-gpu-eligibility | GPU eligibility with unchanged heat ranking | no production scheduler/interface edits; ADR 0043 reviewed | PR #44; 49 tests; E Intel accelerated GL verified; timing 6.25 ms p95 exceeded threshold during probe, 4.14 ms steady follow-up passed; actual CS GPU/heat/occupancy remain gates |
| Nikita / Codex | docs/T-52-reborn-assessment | Inspected supplied ReBorn mod; public/community compatibility required | none | user declined isolated Steam/CS:GO setup; no replacement started; Minecraft and Xonotic unchanged; docs/jobs/reborn-compatibility-assessment.md |
| Nikita / Codex | feat/T-50-docker-observations | T-48 deadlines #37, T-49 indexed admission #38 and T-50 local observation #40 in review | ADRs 0037-0039 reviewed; no scored model change | T-49: 46+36 tests and 62-case comparison; T-50: 57 tests, web/API checks and 531/171 ms first/reused fresh observations; docs/performance evidence on main; games protected |
| Nikita / Codex | exp/T-47-scheduler-performance | Scheduler latency/scaling investigation complete | none; diagnostics only | 62 cases + raw evidence/recommendations in docs/performance; scripts in PR #36; no production or game changes |
| Nikita / Codex | feat/T-46-monitor-value-labels | Transfer monitor data into /admin; keep logic in place; retire terminal UI | snake_case inputs/snapshots + thin read-only API; ADR 0036 | implementation checked; 42 tests + web build/lint + HTTP/browser checks; PR #35 in review |
| Nikita / Codex | feat/T-44-minecraft-server | Minecraft world server plus browser client on Node A | T-43 / PR #33; existing desktop runtime, no new shared interfaces | code integrated into main by user request; gameplay unverified; rejected battery measurement blocks heat placement |
| Nikita / Codex | `fix/T-42-monitor-integration` | Review/clarify new gaming console; integrate author monitor under T-42 (T-41 already taken) | Existing Core/placement unchanged; read-only resource probe and console controls | PR #32 ready; 36 gaming/46 Core/20 Network tests and clean build; original author checkout preserved; gaming-monitor-review.md |
| Jona / Codex | `docs/T-06-final-results` | T-06 reference-house price and cost sensitivity | none | PR #21 open |
| Jona / Claude | `feat/T-14-economics` | T-14 economics per actor on the 0020 job model and L4 basis (decisions 0027, 0028 on that branch) | new economics module and config; scored case figures unchanged | PR #27 open |
| Jona | `feat/T-18-owner-dashboard` | T-18 offline homeowner result view | none | PR #28 open; overlaps `web/` /household (PR #30) |
| Jona / Codex | `docs/T-41-free-heat-viability` | T-42 free-heat audit and profitable private-home boundary | none | PR #29 in review; Dell R570 card-count mismatch handed to T-14/T-15 |
| Jona / Claude | `feat/T-12-network-validation` → `main` | T-12 step 3: LP validation, experiments, paper §5/§8 (decisions 0029, 0030) | `NetworkRun` options; map export format unchanged (0025 §7) | in progress |
| Ennio / Claude | `main` (`web/`) | T-17/T-18 web dashboard; next: feed `/admin` from the T-12 dataset (`docs/model/network-map-data.md`) | none | first version merged (PR #30) |
| team lead / Claude | `docs/T-07-draft-submission` | T-07 draft package, must-have checklist | none | in progress |
| Nikita / Codex | `featur-gaming` | Gaming lab follow-up: automatic heat-to-launch integration (`docs/gaming.md`) | none | pending |
| Jona / Codex | `codex/T-45-federated-scheduling` | T-45 federated job ownership design, offer/claim protocol model and paper proposal | new pure Network offer selector and claim gate; existing Core/T-43 interfaces unchanged | boundary review addressed; 40 Network tests pass; paper compiles; T-43 handoff only for live integration |

## Open coordination

- **T-59 / T-58 (Nikita -> Ennio):** user approved a separate BFS 2020 covered-residential composition profile. PR #49 supplies counts, conditional owner shares and synthetic composition only. Preserve SFH-only scenarios as labelled segments; source excludes ZH/VS. Do not infer private-SFH or hotel/spa shares from ownership totals. See docs/research/swiss-residential-node-mix.md for mapping, source precision and missing mixed-category heat/spatial inputs.

- **Jona's Codex (T-45 federation) ↔ Claude (T-12):** LaTeX paper ownership to avoid edit conflicts: Claude edits `sections/05-scheduling.tex`, `08-evaluation.tex` and the appendix rows for T-12 experiments; Codex edits `09-federation.tex`. Both may add to `references.bib`. Note: two files start with `09-` (`09-federation.tex`, `09-limits.tex`); whoever touches `main.tex` next should renumber (federation → its own section number) and tell the other. T-12 results live in `data/derived/network/experiments/` and `docs/model/network-scheduler.md`; reuse them, do not recompute.
- **Hardware cost (T-06, T-14):** one Dell R570 draws ≈ 0.16 kW, so a 1 kW reference server is
  ≈ 6.15 R570s (CHF 50,743); the T-06 depreciation sensitivity (A-017) is ≈ 6× too low. See
  `docs/research/hardware.md`.
- **Pitch hardware (team lead, 2026-11-21):** GPU inference server (L4 profile) is the main
  scenario for the pitch, with the three-profile comparison on one slide; scored reference-house
  figures keep the case's abstract 1 kW server.
- **Computing price:** CHF 0.66 per server-hour adopted for the draft (range 0.25–1.80,
  `docs/research/computing-price.md`); T-06 updates `config/reference-house.json`.
- **Demand model (T-26 ↔ Ennio):** options in `docs/tickets/T-26-sourced-job-demand.md`; Ennio
  answers there.
- **Business model:** decision 0020 (operator model; Type 1 = batch LLM on non-personal documents,
  Type 2 = research simulations) supersedes parts of 0018/0019; `docs/business-model.md`.

## Shared interfaces

- `IScheduler.Decide(HeatSignal)` and `IExecutor.ApplyTargetLoad`, wired by
  `SchedulerController.Apply` (decision 0005). Simulated and real executors both sit behind
  `IExecutor`.
- `IContainerControl` and `DockerJobExecutor` (decision 0009); job descriptors conform to
  `config/jobs/job.schema.json`; `JobPlacement.IsEligible` enforces data locality (T-24).
- `INetworkScheduler`, `DigitalHeat.Network(.Lp)`, shared `HeatBalance.Step` (decision 0024);
  map export contract (decision 0025 §7).
- `HeatCapacity` (shared useful-heat helper, decision 0022), used by the greedy scheduler and the
  gaming reservations.
- Snake_case result export (decision 0011).
- `config/reference-house.json`, `config/hardware/*.json`, `config/network/*.json`, and `data/raw/`
  with its `SOURCE` files.
- Decision numbers: see [`decisions/README.md`](decisions/README.md); 0027/0028 are taken on
  `feat/T-14-economics`.

## Known integration risks

1. **Year explorer (JavaScript) and C# core must give the same numbers.** Explorer default:
   4 servers, min load 0 %, tank 35 kWh, 0.3325 CHF/kWh → 58.0 % covered, 0 kWh dumped,
   22,661 server-hours, break-even 0.226 CHF/h. No automated cross-check exists yet.
2. **Two homeowner views:** `web/` /household (PR #30, merged) and PR #28 (T-18) overlap; decide
   which one the pitch uses.
3. **Gaming lab:** reservations exist, but automatic reservation-to-launch, failure reconciliation
   and session routing are not integrated (`docs/gaming.md`).
