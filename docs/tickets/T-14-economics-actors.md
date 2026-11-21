# T-14: Economics per actor, ownership models, heating-cost alternatives

- **Tier:** 2 Pitch  ·  **Area:** Core / economics
- **Depends on:** T-15
- **Suggested owner:** Codex or Jona  ·  **Owner:** Team lead / Claude (subagent)  ·  **Status:** in review (PR #27, decision 0028, `docs/model/economics.md`; aligned with decisions 0020 and 0027)

## Goal

Answer 'does it pay off, and for whom' with system surplus first, then the split.

## Tasks

- [ ] System surplus per year: computing income + value of heat used − electricity − hardware depreciation − operations (WP2 data).
- [ ] Value of heat per site: replaced heat pump (case: price ÷ 2.5), oil or gas (WP2 energy prices) — parameter per site.
- [ ] Split between host and DigitalHeat for the operator model and the host-owned model (heat fee, commission as parameters).
- [ ] Win-win zone: grid over electricity and computing price where both actors are positive (data for a chart).
- [ ] Keep the case break-even next to the full-cost break-even, clearly labelled.

## Done when

- Economics module + tests; win-win data exported (T-13 format).

## Read first

- `docs/vision.md (Who must win)`
- `data/raw/market/costs.csv`
- `data/raw/market/energy-prices.csv`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
