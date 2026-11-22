# T-82: Real house from the organisers as a Fleet variant

- **Owner:** Ennio / Claude · **Status:** done
- **Depends on:** T-58 (Fleet simulation).

## Goal

Use the organisers' measured house data (gas, electricity, floor plans) in addition to the case
reference house, which stays the basis for scored figures (case rule, AGENTS.md §5.1).

## Done

- Data moved to `data/external/housedata/` (git-ignored); `data/raw/housedata/SOURCE.md` describes it.
- `scripts/calibrate_real_house.py`: gas × 0.86 (EU 813/2013) fitted to the case formula →
  `config/fleet/houses/real-house.json` (10.13 kW, 38.8 kWh/day, R² 0.996).
- `config/fleet/scenarios/real-house.json`; results in `docs/model/fleet-simulation.md` §16.

## Open

- 2025 gas readings for October–December are missing; other years need their weather (only 2025 is in the repo).
- The real boiler efficiency and the split of summer use into hot water and losses are unknown.
- The electricity data and floor plans are not used yet.
