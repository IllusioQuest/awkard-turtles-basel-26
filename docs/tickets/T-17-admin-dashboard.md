# T-17: Admin dashboard (DigitalHeat)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI
- **Depends on:** T-13, T-00 (UI stack)
- **Suggested owner:** anyone (Lovable possible)  ·  **Owner:**  ·  **Status:** open

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
