# T-18: Server-owner dashboard (host, private)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI
- **Depends on:** T-13
- **Suggested owner:** anyone  ·  **Owner:** Ennio / Claude  ·  **Status:** in progress (scaffold with sourced figures; decision 0022)

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
