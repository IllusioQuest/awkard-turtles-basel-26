# T-47: Data for the Swiss-wide simulation

Owner: Ennio / Claude. Status: in progress. Branch: `data/T-47-swiss-grid-data`.

## Goal

Inputs for the large-scale simulation (canton or all of Switzerland) and its heatmap: weather in
many locations, forecasts the scheduler can use, building sizes and heat demand by region,
electricity price per municipality and house type, compute demand and prices for the three job
types. All time-dependent where a source allows it.

## Agreed with Ennio (2026-11-21)

- Weather 2025, hourly, 10 km grid (≈ 410 points), committed (gzipped).
- Real archived forecasts, 1–3 days ahead, same grid.
- Electricity: ElCom yearly price per municipality and category H1–H8; no time-of-day prices.
- Demand: real public traces for daily/weekly shapes and job lengths, scaled by assumed volumes.

## Deliverables

- `data/raw/swiss-grid/` (grid, weather, forecasts), `scripts/fetch_swiss_grid.py`
- `data/raw/elcom/municipalities-2026/`, `data/derived/elcom/`, `scripts/fetch_elcom_municipalities.py`
- `data/derived/buildings/`, `scripts/build_buildings_heat.py`, `data/raw/buildings/SOURCE.md`
- `data/raw/demand-traces/`, `data/derived/demand/`, `scripts/fetch_demand_traces.py`,
  `docs/research/compute-demand-traces.md`, new rows in `data/raw/market/*.csv`

No simulation code is changed; consumers (T-12 network, web map) pick up the files later.
