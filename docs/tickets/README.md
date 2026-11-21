# Tickets

> Work board for the team and all agents. Pick a ticket, write your name in **Owner** and set
> **Status** to `in progress` (here and in the ticket file), work on a branch named after the ticket
> (e.g. `feat/T-03-docker-executor`), reference the ticket in your PR, set `done` when merged.
> Before substantial work, agree the approach with your human (AGENTS.md §10). Created 2026-11-21
> from the team's whiteboard session (structure, routing optimisation, UI areas).

Tiers (see `docs/vision.md`): **1 Draft** = needed for the 15:00 draft; **2 Pitch** = pitch core;
**3 Pitch stretch**; **4 Later** = after the hackathon.

| ID | Title | Tier | Depends on | Suggested owner | Owner | Status |
|---|---|---|---|---|---|---|
| [T-00](T-00-team-decisions.md) | Team decisions that unblock the tickets | 1 Draft | – | whole team (15 min) | Nikita / Codex (coordination) | in progress |
| [T-01](T-01-job-catalog.md) | Job model and job catalog (must-have 1) | 1 Draft | T-00 | Codex or anyone | | open |
| [T-02](T-02-measure-laptop-job.md) | Measure one real job on a laptop (must-have 1) | 1 Draft | T-00 | anyone with a laptop | Ennio / Claude | in progress |
| [T-03](T-03-generic-job-executor.md) | Generic real-job executor behind IExecutor (must-have 2) | 1 Draft | T-02 (job image) | Nikita or Codex (prompt prepared in chat) | | open |
| [T-04](T-04-live-demo-run.md) | LiveRun: one day replayed with the real job (demo for must-have 2) | 1 Draft | T-03 | same person as T-03 | | open |
| [T-05](T-05-jobs-in-year-simulation.md) | Jobs, queue and deadlines in the year simulation (must-have 3) | 1 Draft | T-01 | Codex | | open |
| [T-06](T-06-reference-house-results.md) | Reference-house key figures and assumptions for the draft | 1 Draft | T-05 | anyone | | open |
| [T-07](T-07-draft-submission.md) | Draft submission package (due 2026-11-21 15:00) | 1 Draft | T-00, T-06 | one owner | | open |
| [T-08](T-08-explorer-fixes.md) | Year explorer: fixes from the review | 2 Pitch | – | Claude (team lead's session) | | open |
| [T-09](T-09-site-heat-state-forecast.md) | Per-site heat state and forecast | 2 Pitch | T-00 | Codex | Nikita / Codex | in progress (design) |
| [T-10](T-10-multi-site-network.md) | Generic sites and network model | 2 Pitch | T-09 | Codex | | open |
| [T-11](T-11-routing-optimisation-model.md) | Routing as an optimisation problem: formal model | 2 Pitch | T-09, T-10 | team + Claude (needs discussion) | | open |
| [T-12](T-12-routing-algorithms.md) | Routing baselines and optimiser | 2 Pitch | T-11, T-05 | Codex | | open |
| [T-13](T-13-results-datasets-contract.md) | Result datasets and data contract for UIs and paper | 2 Pitch | T-05 | Codex | | open |
| [T-14](T-14-economics-actors.md) | Economics per actor, ownership models, heating-cost alternatives | 2 Pitch | T-15 | Codex or Jona | | open |
| [T-15](T-15-hardware-profiles.md) | Hardware profiles: CPU vs GPU | 2 Pitch | T-00 | anyone | | open |
| [T-16](T-16-price-comparison.md) | Price comparison with known providers | 2 Pitch | T-02, T-14 | anyone | | open |
| [T-17](T-17-admin-dashboard.md) | Admin dashboard (DigitalHeat) | 3 Pitch stretch | T-13, T-00 (UI stack) | anyone (Lovable possible) | | open |
| [T-18](T-18-owner-dashboard.md) | Server-owner dashboard (host, private) | 3 Pitch stretch | T-13 | anyone | | open |
| [T-19](T-19-customer-configurator.md) | Customer configurator and cost-benefit analysis (public) | 3 Pitch stretch | T-10, T-14 | anyone | | open |
| [T-20](T-20-marketing-website.md) | Marketing website | 4 Later | T-19 | open | | open |
| [T-21](T-21-pitch.md) | Pitch storyboard, demo script and slides | 2 Pitch | T-00 | team lead + Claude | | open |
| [T-22](T-22-paper.md) | Documentation and paper skeleton | 3 Later | T-11, T-13 | open | | open |
| [T-23](T-23-webhosting-integration.md) | Connect the webhosting platform to the heat scheduler | 2 Pitch | T-03 | Nikita | | open |

## Dependency sketch

```
T-00 ─┬─ T-01 ─ T-05 ─ T-06 ─ T-07 (draft 15:00)
      ├─ T-02 ─ T-03 ─ T-04 (live demo)        T-03 ─ T-23
      ├─ T-09 ─ T-10 ─ T-11 ─ T-12 (routing)   T-05 ─ T-13 ─ T-17/T-18
      ├─ T-15 ─ T-14 ─ T-16, T-19               T-11 + T-13 ─ T-22
      └─ T-21 (pitch)
```
