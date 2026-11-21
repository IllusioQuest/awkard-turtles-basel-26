# T-74 — Renderer label cleanup

Owner: Nikita/Codex. Status: in review. Branch: fix/T-74-render-labels.

User requested removal of the bottom-right Worker details panel, physical-host
heading, role/work-budget explanations in node subcards, and configured-link
explanatory caption. Implemented only these presentation removals and removed
unused worker-details DOM code. Node map, progress, stats, controls, shared GPU
budgets and heat-first policy remain unchanged.

Code: 7a3551a. Live at http://localhost:18950/ui after refresh.
Verified served HTML and JavaScript syntax (`node --check`), plus diff whitespace.
Browser preview access unavailable; no visual verification claim. No runtime tests
needed for this presentation-only removal. Existing render outputs are retained.
