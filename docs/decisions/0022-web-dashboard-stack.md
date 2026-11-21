# 0022: One Vite/React/TypeScript app for the demo dashboards

- **Status:** proposed; user approved 2026-11-21, team confirmation pending
- **Date:** 2026-11-21
- **Decided by:** Ennio (user approval); team confirmation pending via T-00

## Context

T-17 (admin), T-18 (server owner) and T-19 (public price calculator) are separate tickets but
share one offline demo on a laptop. T-00 still lists the UI stack as open. The app must not
become a second heat or economic model, and numbers must stay traceable to their sources.

## Decision

Build one Vite + React + TypeScript single-page app in `web/`, with three hash routes (`#/` public
calculator, `#/admin` DigitalHeat, `#/owner` server owner) so the static build works with any local
file server. Navigation includes a clearly labelled demo role switch, not authentication: no
credentials, personal data or secrets are handled. Use `react-router-dom` only for shareable routes
and browser back/forward; no chart, UI or state-management library for the scaffold.

Read the committed outputs of the shared model (`data/derived/reference-house-v1/`,
`data/derived/reference-house/*.json`, `data/derived/computing-price.json`) through a typed local
adapter. The browser displays and selects precomputed scenarios; it does not recompute heat,
scheduling or economics. Parameters come from `config/reference-house.json`; scored case figures
are labelled as such, other server counts as variants, and the market-tracking price as not scored.
Where T-10/T-14 outputs are not yet available, show a visible `TODO(source)` instead of inventing
values. No network calls, tile maps or external services. The built `web/dist/` is the offline
fallback: serve it as static files without Node or npm installed, and rebuild it when inputs change.

## Alternatives considered

- Three separate Vite projects: duplicated dependencies and setup, no shared adapter, harder to
  switch audience during a five-minute pitch.
- Blazor/ASP.NET or Lovable: reconnects the demo to a server or external service and adds a new
  stack for three screens; needs a team choice before it can be justified (T-00).
- A browser-side calculation of the configurator: would duplicate the C# model and break the
  "same numbers everywhere" rule.

## Consequences

Easy to demo all three audiences from one `npm run dev` / local static build, without internet,
and one place to show sources. This scaffold is only the shell of T-17/T-18/T-19: the admin map,
flows and forecast remain unbuilt, and the full configurator stays blocked on T-10/T-14 and on
tier-1 must-haves. The three tickets stay separate in the board. Real access control is out of
scope and must never be implied to a jury.

## Review

Fresh-context review (2026-11-21), verdict **go with changes**. Applied all three points: committed
static build as offline fallback and no tile map; explicit note that the map/flows/forecast and
full configurator are not part of this scaffold and tier-1 must-haves come first; parameters read
from `config/reference-house.json` with scored figures, variants and market-tracking prices
labelled separately. The simpler one-screen alternative was rejected as the scaffold because the
user asked for all three audience areas, but its single-screen owner view is the pitch priority.
