# T-50: Faster read-only Docker observation

Owner: Nikita / Codex. Status: queued after live deadlines/admission work.
Planned branch: feat/T-50-docker-observations. Depends on T-46 / PR #35. ADR 0039.
User approved built-in .NET named-pipe/Unix-socket Engine API transport, no library or new daemon
port, bounded CLI fallback for unsupported remote contexts. Preserve application scope, privacy,
errors/unknown states, sampling meaning, Host/Origin guards and cache age. Only fixed read-only GETs.
Done: transport/parser/fallback tests, actual local read-only comparison and published latency docs.
