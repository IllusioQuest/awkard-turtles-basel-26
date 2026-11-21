# Status: work map

Last updated: 2026-11-21 (repo cleanup). Per-ticket owner and status live only in the
[ticket board](tickets/README.md); this file holds what the board cannot: active branches, open
coordination between people, shared interfaces and integration risks. Keep your row current
(`AGENTS.md` §9a) and delete it when your work is merged. History is in git.

## Active work

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| Nikita / Codex | `fix/T-42-monitor-review` | Review/clarify new gaming console; integrate author monitor under T-42 (T-41 already taken) | Existing Core/placement unchanged; read-only resource probe and console controls | in progress; gaming-monitor-review.md |
| Jona / Codex | `docs/T-06-final-results` | T-06 reference-house price and cost sensitivity | none | PR #21 open |
| Jona / Claude | `feat/T-14-economics` | T-14 economics per actor on the 0020 job model and L4 basis (decisions 0027, 0028 on that branch) | new economics module and config; scored case figures unchanged | PR #27 open |
| Jona / Codex | `feat/T-31-mixed-queue` | T-31 mixed Type 1 + filler year scenario | year simulation inputs | PR #25 open |
| Jona | `feat/T-18-owner-dashboard` | T-18 offline homeowner result view | none | PR #28 open; overlaps `web/` /household (PR #30) |
| Jona | `docs/T-41-free-heat-viability` | Free-heat audit and profitable home-compute boundary | none | PR #29 open |
| Jona / Claude | `feat/T-12-network-validation` → `main` | T-12 step 3: LP validation, experiments, paper §5/§8 (decisions 0029, 0030) | `NetworkRun` options; map export format unchanged (0025 §7) | in progress |
| Ennio / Claude | `main` (`web/`) | T-17/T-18 web dashboard; next: feed `/admin` from the T-12 dataset (`docs/model/network-map-data.md`) | none | first version merged (PR #30) |
| team lead / Claude | `docs/T-07-draft-submission` | T-07 draft package, must-have checklist | none | in progress |
| Nikita / Codex | `featur-gaming` | Gaming lab follow-up: automatic heat-to-launch integration (`docs/gaming.md`) | none | pending |

## Open coordination

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
