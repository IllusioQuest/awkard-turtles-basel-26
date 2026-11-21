# T-17: Admin dashboard (DigitalHeat)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI
- **Depends on:** T-13, T-00 (UI stack)
- **Suggested owner:** anyone (Lovable possible)  ·  **Owner:** Ennio / Claude  ·  **Status:** in progress (v1 with mock data in `web/`, route `/admin`)

## Goal

Internal tool: map with sites, job flows, heat map, weather, analysis over time and forecast.

## Tasks

- [ ] Map of sites (Basel), flows between sites (from `flows.csv`), heat map over time, weather overlay.
- [ ] Time slider over the simulated year; charts for coverage, dump, utilisation, surplus; forecast view.
- [ ] Reads only exported runs (T-13); works offline for the demo.

## Done when

- Opens a run and shows map + flows + time series without internet.

## Read first

- whiteboard photo (2026-11-21)

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Progress (2026-11-21, Ennio / Claude)

v1 in `web/` at route `/admin`, **all data mocked** (`web/src/admin/mock.ts`, deterministic):
97 sites (Basel cluster + Swiss cities), heat-led load from the case heating formula, mock weather,
jobs and missed deadlines. Map: MapLibre with CARTO dark tiles (needs internet; markers and
overlays are drawn by us), zoom-dependent clustering, load / free-capacity heatmaps, weather
overlay, site side panel. Time machine: −30 days to +7 days forecast; monthly economics chart
12 months back and 12 ahead. Flows between sites and reading exported runs (T-13) are still open.
