# T-41: Web dashboard v1, client calculator

- **Tier:** 2 Pitch  ·  **Area:** UI
- **Depends on:** decision 0020 (operator model)
- **Suggested owner:** Ennio  ·  **Owner:** Ennio / Claude  ·  **Status:** dropped (2026-11-21: calculator removed on request, only admin and household dashboards are kept; shared styling and ElCom canton prices remain)

## Goal

A first page of the web dashboard (`web/`, Vite + React + TypeScript, Recharts) where a prospective
host enters canton, heat price and house data and sees yearly savings, share of heat from servers,
costs and break-even. Shared dark styling for all later dashboard pages.

## Tasks

- [x] Defaults from the reference house, each with unit and source (`web/src/params.ts`).
- [x] Operator-model calculation as a pure function (`web/src/calc.ts`), daily approximation.
- [x] Minimal dark layout, one cumulative cost chart.
- [ ] Replace the daily approximation with results from the C# core (later, not in v1).

## Notes

- Client inputs: canton, servers (slider 1–13), heat price (slider), household size (2/4/6 persons). Everything else is internal (`INTERNAL` in `web/src/params.ts`).
- Household sizes scale space heating linearly with persons from the reference house (`TODO(source)`); hot water 3 kWh/person/day from the case.
- Choosing a canton sets the heat price to the ElCom 2026 H4 cantonal median (standard product) ÷ 2.5 (`data/raw/elcom/canton-median-h4-2026.json`, fetched by `scripts/fetch_data.py`). Weather stays Basel 2025.
- Coverage is an upper estimate: space heating is spread evenly over heating days, no tank, no
  hourly profile. Connection cost CHF 500 per server, paid once by the host, is a guess (`TODO(source)`).
- No integrations in v1.
