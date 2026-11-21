# DigitalHeat web demo

One Vite + React + TypeScript app with three demo views:

- `#/` — public price calculator (sourced scenario viewer)
- `#/admin` — DigitalHeat operator: run provenance, variants and one exported hour
- `#/owner` — homeowner view of the fixed case reference house

The view switch is a demo navigation aid, not authentication. No data is sent anywhere.

## Run

```bash
cd web
npm install
npm run dev
```

Open the printed local address. To check types or build:

```bash
npm run typecheck
npm run build
```

For the offline pitch, `dist/` is committed. It needs no Node, npm or internet:

```bash
python3 -m http.server 4173 --directory dist
```

Then open `http://localhost:4173/#/owner` (or `#/` / `#/admin`). Rebuild `dist/` whenever the
inputs below change.

## Data and sources

The app imports committed files directly; it contains no heat, scheduling or economic model.

| What | File |
|---|---|
| Case parameters and sources | `../config/reference-house.json` |
| Scored 4-server case figures, hourly export and provenance | `../data/derived/reference-house-v1/` |
| Server-count variants | `../data/derived/reference-house/*.json` |
| Computing price references | `../data/derived/computing-price.json` |

Scored case figures are labelled as such; other server counts are variants; the market-tracking
price is explicitly not the adopted scored value. Every displayed figure has its source next to it.

## Scope

This is the shell of T-17, T-18 and T-19 (decision 0022), not the full dashboards. The admin map,
flows and forecast remain open in T-17. The public calculator selects among precomputed runs;
custom-house inputs and full-cost break-even need T-10/T-14 outputs and are marked `TODO(source)`
in the UI. Tier-1 must-haves for the case remain the first priority.
