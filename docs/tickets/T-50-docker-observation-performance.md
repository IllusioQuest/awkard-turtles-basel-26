# T-50: Faster read-only Docker observation

Owner: Nikita / Codex. Status: in review, PR #40; T-48/T-49 code is in review.
Branch: feat/T-50-docker-observations. Depends on T-46 / PR #35. ADR 0039.
User approved built-in .NET named-pipe/Unix-socket Engine API transport, no library or new daemon
port, bounded CLI fallback for unsupported remote contexts. Preserve application scope, privacy,
errors/unknown states, sampling meaning, Host/Origin guards and cache age. Only fixed read-only GETs.
Done: transport/parser/fallback tests, actual local read-only comparison and published latency docs.

Implemented and checked: 57 gaming/monitor tests, clean API build, web build/lint, actual HTTP
guards/native Docker observations and browser sampling labels. Fresh latency 531.20 ms on first
connection, 170.65 ms on reuse; cached 3.05/2.77 ms in this four-request run (not an SLA).
Evidence: `docs/performance/docker-observation-improvement.md`. Merge #35, then retarget #40.
No games/worlds changed; unsupported remote contexts keep CLI behavior. Ticket is not marked done
until code is merged. Docs shared on main immediately per user workflow.
