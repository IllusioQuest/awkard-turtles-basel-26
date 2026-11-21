# T-18: Server-owner dashboard (host, private)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI
- **Depends on:** T-13
- **Suggested owner:** anyone  ·  **Owner:** Ennio / Claude  ·  **Status:** in progress (v1 with mock data in `web/`, route `/household`)

## Goal

What a homeowner sees: current load, temperature and heat rate, heat delivered, savings.

## Tasks

- [ ] Current load of their servers, tank temperature/level, heat rate (kW), heat delivered today/this year, money saved.
- [ ] Plain language; a homeowner understands it in one minute (jury criterion).
- [ ] Reads exported runs (T-13); for the demo, a 'live' mode fed by LiveRun (T-04) is a plus.

## Done when

- One screen, understandable without explanation.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Progress (2026-11-21, Ennio / Claude)

v1 in `web/` at `/household` (default: first Basel home) and `/household/:siteId`, **mock data**
from the same site model as the admin dashboard (`web/src/admin/mock.ts`). Date range picker
(react-day-picker) with presets, from 2 years back to 12 months ahead, default last 24 h.
Buckets adapt to the range (hour, 3 h, day, week; `web/src/household/series.ts`); forecast is
shaded, load has a forecast range. Not yet: live mode from LiveRun (T-04), exported runs (T-13).
