# 0039: Reusable local Docker observation connections

Status: proposed; user approved built-in .NET local Engine API access with CLI fallback. T-50.
Depends on T-46 / PR #35. Keep existing monitor logic in place and its web API read-only.

Use built-in HttpClient/SocketsHttpHandler over the selected Docker context's local Windows named
pipe or Unix socket. Resolve context endpoint once with a bounded context-inspect CLI call; never
enable an insecure TCP listener or change daemon settings. Unsupported remote SSH/TLS contexts
keep the bounded CLI observation path. Context must remain explicit; cache connections per engine.

Restrict adapter operations to fixed GET version/list/inspect/process-list/stats endpoints.
Negotiate/check supported Engine API version, apply cancellation/deadlines, scope containers by
existing labels, and preserve ambiguous/paused/unavailable/unknown states. Process arguments are
transiently matched against the configured executable and must never enter logs/snapshots.
Stats stay observations with timestamps; unavailable CPU deltas remain null, not zero. Follow
Docker's CPU/memory accounting, and label any changed sampling window instead of inventing data.

Retain the current five-second display-cache freshness and local Host/Origin restrictions. Do not
use monitoring cache as live admission authorization. Batch/parallelize observations with bounded
concurrency, retain fresh/unknown error reporting, and compare timestamps plus fresh/cached latency
against T-47. No Docker create/start/stop/exec endpoints in the new adapter and no new dependency.

## Review

Fresh limited-context verdict: go with changes. Disable proxies/redirects; bind caches to context
and resolved endpoint; restrict fixed GET routes/validated IDs; refresh failed connections; require
consecutive counters for CPU or return null; label sampling/memory accounting; propagate cancellation
and sanitize diagnostics. Match process listings transiently, including CLI top fallback, without
retaining arguments/raw errors. No public daemon port or container exec mutation.
