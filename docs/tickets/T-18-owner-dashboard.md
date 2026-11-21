# T-18: Server-owner dashboard (host, private)

- **Tier:** 3 Pitch stretch  ·  **Area:** UI
- **Depends on:** T-13
- **Suggested owner:** anyone  ·  **Owner:** Jona / Codex  ·  **Status:** in review

## Goal

What a homeowner sees: current load, temperature and heat rate, heat delivered, savings.

## Tasks

- [ ] Current load of their servers, tank temperature/level, heat rate (kW), heat delivered today/this year, money saved.
- [ ] Plain language; a homeowner understands it in one minute (jury criterion).
- [x] Reads exported runs (T-13); for the demo, a 'live' mode fed by LiveRun (T-04) is a plus.

## Done when

- One screen, understandable without explanation.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## First version for review

Draft PR #28: https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/28.

`prototypes/owner-dashboard/` opens the committed T-13 showcase and accepts another result-bundle
folder. It shows annual heat coverage, useful heat, regular heating, modelled heat value, hourly
server load, useful heat rate and tank energy. It has no live telemetry, authentication or measured
tank temperature; customer bill savings cannot be stated without a contract. The one-minute
comprehension criterion remains for a human to assess before this ticket is marked done.
