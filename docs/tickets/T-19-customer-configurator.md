# T-19: Customer configurator and cost-benefit analysis (public)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI / core
- **Depends on:** T-10, T-14
- **Suggested owner:** anyone  ·  **Owner:** Ennio / Claude  ·  **Status:** in progress (scenario viewer only; full configurator blocked on T-10/T-14, decision 0022)

## Goal

A potential host enters their building and gets: servers, heat covered, savings, and when it does not pay off.

## Tasks

- [ ] Inputs: location, building type/size or yearly heat demand, hot water, tank, electricity price, ownership model.
- [ ] Outputs from the C# core (small API or precomputed grid — decide in T-00): servers per house, coverage, savings per year, payback, conditions where it stops paying off.
- [ ] No second model in the UI.

## Done when

- A host can try 3 different houses and gets consistent, sourced answers.

## Read first

- `docs/research/sites-and-profiles.md (generic site parameters)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
