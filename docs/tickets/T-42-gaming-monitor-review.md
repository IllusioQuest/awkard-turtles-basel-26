# T-42: Review and clarify the gaming heat monitor

Owner: Nikita / Codex (review of Nikita / Claude's console implementation).
Status: in progress. Branch: `fix/T-42-monitor-review`.

The author's local monitor used T-41, but main already assigns T-41 to the client calculator.
This work preserves that implementation and tracks its integration and review as T-42.

Scope: review existing console logic/data, clarify the terminal, expose existing sourced hourly
data and observed Docker resources, fix one-shot live fetch, validate paused-node admission and
preserve the shared Core heat/placement model. No automatic launch, physics or economic changes.

Checks: gaming/monitor tests, Core/Network regressions, compact replay January/July, details,
read-only Docker snapshot and one-shot live source/fallback observation. New tests use offline
HTTP/Docker fixtures. See `docs/gaming-monitor-review.md` for review findings and reproducible commands.
