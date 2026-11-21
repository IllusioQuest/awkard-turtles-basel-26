# Tickets

> Work board for the team and all agents. Pick a ticket, write your name in **Owner** and set
> **Status** to `in progress` (here and in the ticket file), work on a branch named after the ticket
> (e.g. `feat/T-03-docker-executor`), reference the ticket in your PR, set `done` when merged.
> Before substantial work, agree the approach with your human (AGENTS.md §10). T-24 (job data locality, Jona) lives on its branch until merged. Created 2026-11-21
> from the team's whiteboard session (structure, routing optimisation, UI areas).

Tiers (see `docs/vision.md`): **1 Draft** = needed for the 15:00 draft; **2 Pitch** = pitch core;
**3 Pitch stretch**; **4 Later** = after the hackathon.

| ID | Title | Tier | Depends on | Suggested owner | Owner | Status |
|---|---|---|---|---|---|---|
| [T-00](T-00-team-decisions.md) | Team decisions that unblock the tickets | 1 Draft | – | whole team (15 min) | Nikita / Codex (coordination) | in progress |
| [T-01](T-01-job-catalog.md) | Job model and job catalog (must-have 1) | 1 Draft | T-00 | Codex or anyone | Team lead / Claude | blocked: waiting for Nikita's decision on always-on representation (then Claude implements the schema) |
| [T-02](T-02-measure-laptop-job.md) | Measure one real job on a laptop (must-have 1) | 1 Draft | T-00 | anyone with a laptop | Ennio / Claude | done (PR #15) |
| [T-03](T-03-generic-job-executor.md) | Generic real-job executor behind IExecutor (must-have 2) | 1 Draft | T-02 (job image) | Nikita or Codex (prompt prepared in chat) | Jona / Codex | done (PR #3, decision 0009) |
| [T-04](T-04-live-demo-run.md) | LiveRun: one day replayed with the real job (demo for must-have 2) | 1 Draft | T-03 | same person as T-03 | Jona / Codex | done (PR #3: DigitalHeat.LiveRun day replay) |
| [T-05](T-05-jobs-in-year-simulation.md) | Jobs, queue and deadlines in the year simulation (must-have 3) | 1 Draft | T-01 | Codex | Jona / Codex | done (PR #6, decision 0010); sourced finite scenario: T-26 |
| [T-06](T-06-reference-house-results.md) | Reference-house key figures and assumptions for the draft | 1 Draft | T-05 | anyone | Jona / Codex | in review (price and hardware pass; T-31 mixed queue pending) |
| [T-07](T-07-draft-submission.md) | Draft submission package (due 2026-11-21 15:00) | 1 Draft | T-00, T-06 | one owner | Team lead / Claude | in progress (fresh-clone check + must-have checklist; contents of the submission depend on T-00) |
| [T-08](T-08-explorer-fixes.md) | Year explorer: fixes from the review | 2 Pitch | – | Claude (team lead's session) | | open |
| [T-09](T-09-site-heat-state-forecast.md) | Per-site heat state and forecast | 2 Pitch | T-00 | Codex | Nikita / Codex | done (PR #5 merged) |
| [T-10](T-10-multi-site-network.md) | Generic sites and network model | 2 Pitch | T-09 | Codex | | open |
| [T-11](T-11-routing-optimisation-model.md) | Routing as an optimisation problem: formal model | 2 Pitch | T-09, T-10 | team + Claude (needs discussion) | Team lead / Claude (subagent) | done as draft (PR #13 merged, decision 0014 proposed; team review pending) |
| [T-12](T-12-routing-algorithms.md) | Routing baselines and optimiser | 2 Pitch | T-11, T-05 | Codex | Jona / Claude | in progress (Claude, takeover 2026-11-21): steps 1–2 on main; step 3 validation + experiments A/B/D done, LP experiments running; decision 0029 |
| [T-13](T-13-results-datasets-contract.md) | Result datasets and data contract for UIs and paper | 2 Pitch | T-05 | Codex | Nikita / Codex | done (PR #10 merged) |
| [T-14](T-14-economics-actors.md) | Economics per actor, ownership models, heating-cost alternatives | 2 Pitch | T-15 | Codex or Jona | Team lead / Claude (subagent) | in review (PR #27, decision 0026) — reconcile with decision 0020 before merge |
| [T-15](T-15-hardware-profiles.md) | Hardware profiles: CPU vs GPU | 2 Pitch | T-00 | anyone | Team lead / Claude (subagent) | done (PR #14 merged) |
| [T-16](T-16-price-comparison.md) | Price comparison with known providers | 2 Pitch | T-02, T-14 | anyone | Jona / Claude | in progress (Claude; approach proposed, waiting for approval) |
| [T-17](T-17-admin-dashboard.md) | Admin dashboard (DigitalHeat) | 3 Pitch stretch | T-13, T-00 (UI stack) | anyone (Lovable possible) | Ennio / Claude | in progress (`web/`, /admin, mock data first) |
| [T-18](T-18-owner-dashboard.md) | Server-owner dashboard (host, private) | 3 Pitch stretch | T-13 | anyone | Ennio / Claude | in progress (`web/`, /household, mock data first) |
| [T-21](T-21-pitch.md) | Pitch storyboard, demo script and slides | 2 Pitch | T-00 | team lead + Claude | Team lead / Claude | in progress (storyboard direction agreed: docs/pitch/storyboard.md) |
| [T-22](T-22-paper.md) | Documentation and paper skeleton | 3 Later | T-11, T-13 | open | Jona / Claude | in progress (skeleton on main) |
| [T-23](T-23-webhosting-integration.md) | Connect the webhosting platform to the heat scheduler | 2 Pitch | T-03 | Nikita | Nikita / Codex | retired by user request (T-40) |
| [T-24](T-24-job-data-locality.md) | Enforce job data locality before execution | 1 Draft | T-01; T-10/T-12 for routing | Codex | Jona / Codex | in progress |
| [T-25](T-25-animated-example-house.md) | Animated example house (pitch visual) | 2 Pitch | T-04, T-13 | anyone (Lovable possible) | Jona / Codex (previous attempt) | paused — unfinished; user stopped visual work, PR #17 closed |
| [T-26](T-26-sourced-job-demand.md) | Sourced job demand scenario and computing price | 1 Draft | T-02, T-05 | Claude | Team lead / Claude | research done (merged); price 0.66 decided; demand model: waiting for Ennio |
| [T-28](T-28-price-updater.md) | Self-updating computing price (snapshotted) | 2 Pitch | T-26, T-06 | Claude | Team lead / Claude (subagent) | done (PR #11 merged; 0.66 pinned, 0.687 live cross-check) |
| [T-30](T-30-type2-batch-compute-job.md) | Type 2 job: research simulation (Monte Carlo research kernel), measured | 2 Pitch | T-03; decisions 0018, 0020 | anyone with a laptop | Jona / Claude | done (measured, LiveRun-tested) |
| [T-31](T-31-mixed-job-queue.md) | Mixed queue: finite Type 1 + Type 2 filler in one year run | 2 Pitch | T-05, T-26; decisions 0018, 0020 | Jona / Codex | Jona / Codex | in review (draft PR #25; technical scenario only) |
| [T-32](T-32-heat-led-dynamic-price.md) | Heat-led dynamic price for Type 2 compute | 3 Pitch stretch | T-31, T-26, T-28 | anyone | | open |
| [T-33](T-33-local-llm-job.md) | Local LLM batch service (Apertus 8B, llama.cpp): container and laptop measurement (Type 1) | 2 Pitch | T-02 | Ennio | Ennio / Claude | done (merged 2026-11-21; Type 1 under decision 0020) |
| [T-37](T-37-profitability-research.md) | Profitability research: power, purchase cost, capacity, demand, trends (Basel-Stadt → CH, 2030) | 2 Pitch | T-15, T-26; decision 0018 | Ennio | Ennio / Claude | done (PR #20) |
| [T-35](T-35-refurbished-hardware.md) | Refurbished hardware: cost and wear | 2 Pitch | T-15 | Claude | Team lead / Claude (subagent) | done (PR #26 merged) |
| [T-42](T-42-free-heat-viability.md) | Free-heat viability from paid demand and realised revenue | 2 Pitch | T-14, T-31, T-35, T-37 | Codex | Jona / Codex | in review |

| [T-41](T-41-client-calculator-web.md) | Web dashboard v1: client heating cost and break-even calculator (shared dark styling) | 2 Pitch | decision 0020 | Ennio | Ennio / Claude | dropped (calculator removed on request; styling kept for T-17/T-18) |

## Dependency sketch

```
T-00 ─┬─ T-01 ─ T-05 ─ T-06 ─ T-07 (draft 15:00)
      ├─ T-02 ─ T-03 ─ T-04 (live demo)        T-03 ─ T-23
      ├─ T-09 ─ T-10 ─ T-11 ─ T-12 (routing)   T-05 ─ T-13 ─ T-17/T-18
      ├─ T-15 ─ T-14 ─ T-16                     T-11 + T-13 ─ T-22
      └─ T-21 (pitch)
```


## Hosting follow-up
| ID | Title | Owner | Status |
|---|---|---|---|
| [T-27](T-27-hosting-component-isolation.md) | Component isolation across hosting nodes | Nikita / Codex | retired by user request (T-40) |


| [T-36](T-36-hosting-heat-routing.md) | Shared scheduler drives hosting request routing | Nikita / Codex | retired by user request (T-40) |
| [T-34](T-34-business-model-v2.md) | Business model v2: sovereign GPU cloud that heats homes profitably | 2 Pitch | T-14, T-35, T-11/T-12 | team lead + Claude | Team lead / Claude | in progress (provisionally fixed: decision 0020, docs/business-model.md) |


| [T-38](T-38-hybrid-service-review.md) | Review hybrid GPU/CPU service proposal | Nikita / Codex | done: analysis only; implementation awaits user choice |

| [T-39](T-39-intel-browser-gaming.md) | Intel browser gaming compatibility experiment | Nikita / Codex | done: verified Xonotic prototype (PR #22); CS2 deferred |

| [T-40](T-40-gaming-profiles.md) | Reusable gaming profiles and retire webhosting | Nikita / Codex | done (PR #22) |

| [T-42](T-42-gaming-monitor-review.md) | Review and clarify gaming heat monitor | Nikita / Codex | in review (PR #32) |

| [T-43](T-43-universal-workloads.md) | Universal resource-aware workload placement | Nikita / Codex | done (code integrated into main with T-44 on user request) |

| [T-44](T-44-minecraft-server.md) | Minecraft persistent server workload integration | Nikita / Codex | code integrated; gameplay test pending; measured heat gate blocked |
| [T-45](T-45-federated-scheduling.md) | Federated scheduling and safe job ownership | Jona / Codex | in progress (design, offer/claim model and paper; live runtime not implemented) |
| [T-46](T-46-monitor-value-labels.md) | Transfer monitoring data into existing admin web app | Nikita / Codex | implementation checked; PR #35 in review; ADR 0036 |
| [T-47](T-47-scheduler-performance.md) | Measure scheduler latency and scaling | Nikita / Codex | analysis complete; 62 cases + raw evidence; diagnostics only |
| [T-48](T-48-live-job-deadlines.md) | Minute-level live batch deadlines | Nikita / Codex | PR #37 in review; 42 tests + Docker proof |
| [T-49](T-49-admission-performance.md) | Indexed admission and safe copy optimization | Nikita / Codex | PR #38 in review; depends on #37; identical-probe evidence published |
| [T-50](T-50-docker-observation-performance.md) | Faster local Docker observations | Nikita / Codex | PR #40 in review; 57 tests + real observations; depends on #35; ADR 0039 |
| [T-51](T-51-swiss-grid-data.md) | Data for the Swiss-wide simulation: weather grid + forecasts, buildings and heat demand, ElCom per municipality, compute demand and prices | Ennio / Claude | in review |
| [T-52](T-52-reborn-compatibility.md) | Assess ReBorn replacement and multiplayer compatibility | Nikita / Codex | assessment complete; user declined proposed setup; no runtime changes |
