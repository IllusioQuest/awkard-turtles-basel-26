# T-35: Refurbished hardware: cost and wear

- **Tier:** 2 Pitch  ·  **Area:** Data / model
- **Depends on:** T-15
- **Suggested owner:** Claude  ·  **Owner:** Team lead / Claude (subagent)  ·  **Status:** in review (PR #26)

## Goal

The team decided to use refurbished / second-life servers and GPUs (lever D). Quantify what that
does to the cost per server-hour: lower purchase price, but shorter remaining life, more failures,
and lower work per kWh than new generations.

## Tasks

- [ ] Research with sources: purchase prices of refurbished/used datacentre servers and GPUs
      (CH/EU), consumer GPUs used, refurbisher warranty terms.
- [ ] Research wear: failure rates by age (fleet studies), effect of thermal cycling and of running
      hot (water heating at 50–70 °C), remaining lifetime, repair cost, efficiency old vs new.
- [ ] Who already does it (Qarnot refurbished OCP servers, others).
- [ ] Per 1 kW server (method of `docs/research/hardware.md`): CHF per sold server-hour incl.
      depreciation over the remaining life, expected failure/replacement cost, efficiency penalty;
      compare with the new profiles.
- [ ] Profile(s) `config/hardware/*-refurbished.json` in the existing format; loader tests pass.
- [ ] `docs/research/refurbished-hardware.md`: method, tables, wear model with formula, worked
      example, recommendation, open `TODO(source)`.

## Done when

- Refurbished profile(s) load in `HardwareProfile`; `dotnet test` passes.
- The research note gives a sourced CHF per server-hour for refurbished vs new, including wear.

## Read first

- `docs/research/hardware.md`, `config/hardware/*.json`
- `docs/research/competitors.md` (lesson 6), `docs/research/filler-demand.md`

## Rules

- Follow AGENTS.md: every number with a source (§5), build on the T-15 profiles and loader (§9a),
  scored reference-house figures keep the case's abstract 1 kW server, update `docs/status.md`.
