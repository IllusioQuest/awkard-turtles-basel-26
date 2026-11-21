# T-67: Local fleet scenarios (houses around a point, plus extra sites)

- **Tier:** 3 Variant (not a scored figure)
- **Owner:** Ennio / Claude
- **Status:** in progress
- **Depends on:** T-58 (fleet simulation), T-60/T-65 (`mesh-heat-first` fleet scheduler)

## Goal

A generic way to build a fleet scenario for one location: N houses on rings around a centre point
plus named extra sites (e.g. an indoor pool), from one spec file in `config/fleet/local/`. First
example: FHNW Campus Dreispitz (Münchenstein BL), 5 case reference houses plus one Allschwil-sized
indoor pool (627 MWh/a, flat; 20 servers, synthetic). No baseline without the pool: the pool is the point.

## Commands

```bash
python3 scripts/make_local_fleet.py config/fleet/local/dreispitz.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/dreispitz-5-pool.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/dreispitz-5-pool
```

## Second scenario: Interlaken and Brig-Goms clusters across Switzerland

`scripts/make_climate_fleet.py config/fleet/regions/alps-border-50.json` builds 50 case reference
houses (10 kW, 4 servers each, only the weather differs). There are 8 on grid cells within 16 km of
Interlaken (`il`) and 5 on cells along the Rhône valley from Brig to Goms (`br`). The other 37 (`mid`)
are spread over Switzerland, by single-family count, at least 22 km apart. `--list` prints the
candidate cells with winter/summer means.
Weather is per 10 km cell at the cell's model elevation. In the Alps this can be far above the
village (Brig-Glis cell 1,786 m, Bellwald 2,548 m), so the `br` houses get mountain weather.
Job volume x4 is a visualization variant, not a sourced volume (as `ch-100` uses x10).

```bash
python3 scripts/make_climate_fleet.py config/fleet/regions/alps-border-50.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/alps-border-50.json
```

## Constant customer demand

Both T-67 scenarios set `pricing.elasticity` to 0 and `pricing.p_ref_noise_sd` to 0 (user choice, to
remove noise from the results). Acceptance = min(1, (price ÷ p_ref)^0) = 1, so every Type 2 job that
arrives is accepted. The load-based price still sets Type 2 income. Arrivals stay Poisson with the
fixed seed. The Dreispitz result table above was computed before this change (elasticity -0.8).

## Changes outside the scenario files

- `HouseFiles.Validate` accepts `design_heating_kw = 0` (a site with only flat heat demand, e.g. a pool).

## Result: work follows heat demand over the year (2025, seed 20261121)

Job volume is the sourced Basel volume (scale 1) for the whole fleet, so it is the same as for 20
houses. Monthly sums from `houses-timeseries.csv`; server use = electric kWh ÷ (servers × 730 h):

| Month | 5 houses: demand / heat used, kWh | house servers | Pool: heat used, kWh | pool servers | Houses' share of work |
|---|---|---|---|---|---|
| Jan | 26,771 / 5,459 | 47 % | 2,753 | 24 % | 66 % |
| Apr | 11,089 / 4,581 | 41 % | 3,453 | 30 % | 58 % |
| Jul | 1,860 / 1,860 | 16 % | 5,478 | 47 % | 25 % |
| Oct | 12,610 / 4,637 | 39 % | 3,306 | 28 % | 58 % |

In summer a house only needs hot water (12 kWh/day = 5 × 12 × 31 = 1,860 kWh in July). Its heat
is capped there and the rest of the work moves to the pool. No heat is dumped all year; 90 of
about 780,000 jobs miss their deadline. Before (20 houses), the same work spread over 20 houses was
about 10 kWh of heat per house-day, below the 12 kWh/day summer floor. So the cap never bound and
the load looked the same all year. The equations were correct; the fleet was short of work.

## Load balancing (decision 0056)

The fleet's heat-first scheduler now ranks sites by free share of their hourly heat budget (Core
`HeatShareRank`) instead of absolute free kWh. Measured on the former 20-house scenario (2025): the
pool's share of work dropped from 75–82 % to 10–13 % per month. Heat used (93,286 kWh), dumped heat (0),
missed deadlines (95) and income (CHF 141,031) were identical. Network/Orchestration migrate later (Nikita).

## Open

- Centre coordinates are approximate (`TODO(source)`), display only.
- No per-site demand profile: the pool is flat all year (team choice, T-67 conversation).
