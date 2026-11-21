# Faster local Docker observations (T-50)

2026-11-21. Implementation: [PR #40](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/40),
dependent on monitoring PR #35. ADR 0039 reviewed with corrections. Code remains in
`gaming/monitor/`; only additive snapshot fields and help text reach the existing `/admin` page.
No games, worlds, daemon settings or scored model parameters were changed.

## Measured comparison

The T-47 diagnostic `scripts/Measure-MonitorApiLatency.ps1` in PR #36 sends four identical
replay snapshots with Docker observation enabled. Before requests 0 and 2 it waits outside the
timer for the five-second cache to expire. The timer covers HTTP request through complete body
read, excluding subsequent JSON parsing. Observation timestamps confirm fresh versus cached data.
The API was restarted before the new measurement; request 0 includes context resolution/version
negotiation, request 2 reuses its connection. The same three configured desktop containers remain
running on this active, non-dedicated Windows laptop/Docker Desktop engine. These are two fresh
and two cached observations, not a percentile distribution or guaranteed speedup.

| Request | Previous CLI path (ms) | Local Engine API (ms) |
|---|---:|---:|
| 0: first fresh observation | 3117.6825 | 531.1959 |
| 1: cached snapshot | 2.9547 | 3.0457 |
| 2: fresh observation, connection reusable | 3099.8341 | 170.6506 |
| 3: cached snapshot | 4.1544 | 2.7734 |

Read as latency, lower is better. Example: 3099.8341 / 170.6506 = approximately 18.2 times
less latency for the second fresh request in these runs. This is not scheduler decision speed;
the separate [admission benchmark](indexed-admission.md) measures that. System load, process
startup, version negotiation and different CPU sampling semantics affect this comparison.

Raw evidence: [previous CLI run](monitor-api-latency-2026-11-21.json) and
[native run](monitor-api-native-2026-11-21.json). Native source revision is recorded in its JSON;
the diagnostic's historical `mode` text still names PR35, while `source_code` identifies T-50.

```powershell
# Start API from the T-50 checkout, then invoke the PR #36 diagnostic:
dotnet run --project gaming/api -c Release
# In a second terminal, from the diagnostic checkout:
./scripts/Measure-MonitorApiLatency.ps1 -ApiCheckout <T50-checkout> -OutputPath <output.json>
```

## What changed and what the values mean

Built-in HttpClient reuses connections to the explicitly selected context's local named pipe or
Unix socket. A bounded CLI context inspection resolves the endpoint; connections are keyed by
context and endpoint, rechecked after the configured 60-second interval and evicted on failure.
Unsupported remote transports use the existing explicit-context CLI fallback; their latency is
not claimed to improve. Fixed GET version/list/process-list/stats routes, exact Compose labels
and validated IDs constrain reads. No proxies, redirects, new daemon port, shell, exec or lifecycle
API is used. Process arguments are matched transiently, never returned or logged. Both native
responses and CLI output retention are capped; cancellation, per-read timeouts and bounded
concurrency apply. A failed read does not dispose the connection while sibling reads use it.

CPU = delta(container CPU ns) / delta(engine system CPU ns) x online CPU count x 100.
The first, missing or reset counter sample returns null, never a fabricated zero. The interval
between valid samples is exposed in `cpu_sample_interval_seconds` and displayed in `/admin`.
Thus this is an average over actual refresh intervals, whereas the old CLI took its own short
sampling window. Pausing a container drops its baseline; resuming requires two valid samples.
CPU may exceed 100% when multiple cores are used. No CPU-to-watts conversion is made.

RAM reports binary units and subtracts Linux `total_inactive_file` (cgroup v1), or
`inactive_file` (v2), when less than reported usage, following Docker CLI accounting. State and
resource observations have separate per-node timestamps and an `observation_source`. Paused,
ambiguous, failed and unavailable observations cannot become healthy synthetic values. These
snapshots are display data, never live admission authorization. Refresh remains manual and the
five-second display cache, Host/Origin guards and request gate remain in place.

Primary references, accessed 2026-11-21: [Docker Engine API](https://docs.docker.com/reference/api/engine/)
and [Docker stats accounting](https://docs.docker.com/reference/cli/docker/container/stats/).

## Verification and integration

- 57 gaming/monitor tests pass: counter/reset/cache accounting, pause baseline, sibling failure,
  cancellation, endpoint restrictions, fixed GET routes, response cap, sanitized output and CLI fallback.
- API Release build: zero warnings/errors. Frontend production build and lint pass; existing
  large-bundle warning remains. Actual API smoke passes Host/Origin/input/content-type guards.
- Actual local observations identify all three desktops and detect Xonotic on C; browser check
  confirms CPU intervals and RAM timestamps. A/B's unconfigured Xonotic status remains unknown.
- Merge PR #35 before #40, then retarget #40 to main. PR #37 contains deadlines; PR #38 depends
  on #37 for indexed admission. Human review/merge remains required for these architectural changes.

This remains a trusted local prototype: access to Docker's socket/pipe itself is powerful, even
though this adapter exposes only fixed reads. It is not a public authenticated service.
