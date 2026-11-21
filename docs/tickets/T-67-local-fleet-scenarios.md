# T-67: Local fleet scenarios (houses around a point, plus extra sites)

- **Tier:** 3 Variant (not a scored figure)
- **Owner:** Ennio / Claude
- **Status:** in progress
- **Depends on:** T-58 (fleet simulation), T-60/T-65 (`mesh-heat-first` fleet scheduler)

## Goal

A generic way to build a fleet scenario for one location: N houses on rings around a centre point
plus named extra sites (e.g. an indoor pool), from one spec file in `config/fleet/local/`. First
example: FHNW Campus Dreispitz (Münchenstein BL), 20 case reference houses with and without one
Allschwil-sized indoor pool (627 MWh/a, flat; 20 servers, synthetic).

## Commands

```bash
python3 scripts/make_local_fleet.py config/fleet/local/dreispitz.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/dreispitz-20.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/dreispitz-20-pool.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/dreispitz-20-pool
```

## Changes outside the scenario files

- `HouseFiles.Validate` accepts `design_heating_kw = 0` (a site with only flat heat demand, e.g. a pool).

## Load balancing (decision 0053)

The fleet's heat-first scheduler now ranks sites by free share of their hourly heat budget (Core
`HeatShareRank`) instead of absolute free kWh. Results with the pool (2025, same inputs): the
pool's share of work drops from 75–82 % to 10–13 % per month. Heat used (93,286 kWh), dumped heat (0),
missed deadlines (95) and income (CHF 141,031) are identical. Network/Orchestration migrate later (Nikita).

## Open

- Centre coordinates are approximate (`TODO(source)`), display only.
- No per-site demand profile: the pool is flat all year (team choice, T-67 conversation).
