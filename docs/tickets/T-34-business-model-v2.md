# T-34: Business model v2 — sovereign GPU cloud that heats homes profitably

- **Tier:** 2 Pitch  ·  **Area:** Business model
- **Depends on:** T-14 (economics), T-35 (refurbished hardware), T-11/T-12 (routing)
- **Suggested owner:** team lead + Claude  ·  **Owner:** Team lead / Claude  ·  **Status:** in progress (direction decided 2026-11-21)

## Goal

Turn the night's findings into one coherent business model and make **residential houses profitable**
for the host and for DigitalHeat.

## Why (decided by the team lead, 2026-11-21)

- Electricity is not the problem (0.33 CHF/h cost vs. 0.11 CHF/h heat value); **hardware and demand are**
  (`docs/research/hardware.md`, `job-demand.md`, `filler-demand.md`).
- Competitors failed or retreated exactly at single homes (Nerdalize bankrupt 2018, Cloud&Heat closed
  residential, Qarnot moved to year-round boilers and was restructured 2026; `docs/research/competitors.md`).
- **Meta-insight:** DigitalHeat has not worked with single-family houses so far, yet the case gives a simple
  residential house as reference and talks about homes throughout. The task is to **solve the residential
  problem**, not to avoid it. Larger buildings are an add-on, not an escape.

## Decided direction

"Swiss sovereign GPU cloud that heats homes." Transcription was only a generic example and is no longer the flagship.

| Lever | Status | Content |
|---|---|---|
| **A** Size for year-round heat | core | size houses for base load (hot water, shoulder seasons), peaks from the backup; year-round sites as network partners |
| **B** Premium = compute-heavy sovereign AI | core | private LLM / document AI etc., priced near Swiss on-demand; data rules for homes respected (`filler-demand.md`) |
| **D** Refurbished hardware | core, adopt fully | second-life servers/GPUs, incl. wear and failure costs (T-35); check what DigitalHeat already does |
| **G** Route work instead of dumping heat | core, part of the model | routing T-11/T-12 |
| **C** Heat as a service fee | variant | no precedent found |
| **E** Grid flexibility revenue | variant / upside | scheduler as controllable load |
| **F** Host- or investor-owned hardware | variant | no precedent found |
| Filler as spot capacity | under consideration | fill rate ≈ 0.35 (range 0.2–0.6) |

**Job model (Ennio, decision 0017, proposed):** Type 1 = sovereign AI services (mainly transcription + batch LLM, OCR later) = lever B; Type 2 = CPU batch compute filler (Monte Carlo, simulations) via our own broker at a heat-led price = the filler lever. No unpaid work.

## Tasks

- [ ] Economics with these levers, reference house first (T-14).
- [ ] Refurbished hardware cost and wear (T-35).
- [ ] What DigitalHeat already does (hardware, sites, model) → `docs/research/digitalheat-company.md`.
- [ ] Creative ideas for profitable homes → `docs/research/creative-ideas.md`; team picks.
- [ ] Write `docs/business-model.md`: value proposition per actor, revenue streams, cost structure, why it works where others failed, when it does not pay off.
- [ ] Feed the pitch (T-21): competitors slide, residential focus.

## Done when

- One page business model with sourced numbers shows under which conditions a residential house is profitable for both host and DigitalHeat, and the pitch tells that story.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
