# T-84 — Renderer copy cleanup

Owner: Nikita/Codex. Status: in review; PR #74. Branch: fix/T-84-render-copy.

User confirmed retaining node names and progress while shortening each card's
heat description to Heat needed: xx kWh. Removed the preparation/shared-scene
flow row, bottom footer text and healthy Reachable labels (including summary).
Kept initial job provider, activity states, stale/unreachable warnings, node map,
progress and all underlying heat data/runtime behavior. Removed unused CSS and
footer DOM reference. No worker restart or calculation/scheduler changes.

Live HTML, JavaScript syntax (node --check), element references and diff whitespace
verified. Source commits97d0a66/2724a2c. Refresh localhost:18950/ui to load it.
Heat quantities remain synthetic lab inputs, documented in ADR0069; shortening
the labels does not turn these values into measurements.

Follow-up dbfd5fb: remove repeated preparation/frame/video aggregate line while
keeping the role-specific bottom progress count. Remove Progress explanatory
small text, completed-frame range/cached-scene note, preparation timing note and
Total render elapsed row. Removed dead DOM updates; backend timings preserved.
Live HTML/JS syntax/element references verified; no runtime or model changes.
Original PR74 merged; follow-up remains in review.
