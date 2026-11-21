# T-42: Review and clarify the gaming heat monitor

Owner: Nikita / Codex (review of Nikita / Claude's console implementation).
Status: in review, [PR #32](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/32). Branch: `fix/T-42-monitor-integration`.

The author's local monitor used T-41, but main already assigns T-41 to the client calculator.
This work preserves that implementation and tracks its integration and review as T-42.

Scope: review existing console logic/data, clarify the terminal, expose existing sourced hourly
data and observed Docker resources, fix one-shot live fetch, validate paused-node admission and
preserve the shared Core heat/placement model. No automatic launch, physics or economic changes.

Checks: gaming/monitor tests, Core/Network regressions, compact replay January/July, details,
read-only Docker snapshot and one-shot live source/fallback observation. New tests use offline
HTTP/Docker fixtures. See `docs/gaming-monitor-review.md` for review findings and reproducible commands.

Final validation: 36 gaming + 46 Core + 20 Network tests pass; Release gaming solution builds with zero warnings/errors. Compact replay/detail, live source/fallback and read-only Docker checks completed. Code committed/pushed as 2a4f9b2; docs on main. Original author checkout, open Debug monitor and game preserved. PR awaits integration; no automatic-launch or measured-power claim.
