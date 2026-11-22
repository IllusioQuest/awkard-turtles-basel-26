# C# render-app overhead (T-80)

Where the render lab app (five `RenderNodeHost` processes, `RenderSession`, the `/ui` page)
spends time outside Blender, and what was changed. Evidence:
`docs/performance/render-app-overhead-2026-11-22.json`.

## Method

- Cloud container, 4 vCPU, Linux. The real C# app (this branch's `DigitalHeat.WorkloadRun`),
  five nodes with the roles, weights and settings of `rendering/presentation-profile.json`,
  started by a test-only Linux copy of `Start-PresentationRenderLab.ps1`.
- Blender 5.3.0 is the `bpy` module behind a test-only launcher (not committed). The frame
  render call is replaced by a fixed 1.0 s sleep, so everything left is app time. Prepare and
  video assembly are real Blender calls.
- Each run is started through `/ui/render`; `/ui/status` is polled every 2 s (like the page).
  Host CPU = user + system seconds of the five .NET processes during the run.
- For the 8-run series the synthetic heat budget was raised to 5000 kWh (test only) so that
  quotas never ran out; the heat finding below used the real 500 kWh.

Not measured on the presentation laptop. Absolute numbers will differ; the trends are the point.

## Findings

**1. Fixed overhead per run is small.** Run 1, 48 frames x 1.0 s on 3 workers (ideal 16 s):

| Phase | Seconds |
|---|---|
| Rendering start -> first frame starts (includes warm Blender startup) | 0.85 |
| First frame start -> last frame end | 17.34 |
| Last frame end -> video encoder starts | 1.71 |
| Video encode + finish | 0.43 |
| **Active total** | **20.33** |

Median gap between two frames on one worker: 0.053 s. So the app adds about 4 s per run, about
4 % of the laptop's 93 s run (T-77). Polling every 250 ms instead of 0.5-1 s cut the tail from
1.71 to 0.88 s but doubled host CPU in run 1 (22.5 -> 48.0 s) and slowed the frame span
(17.3 -> 18.3 s): no net gain, reverted.

**2. Status grew with every run.** Each node reported every job and attempt ever recorded
(about 4 KB per frame attempt), and the render owner pulled that from all five nodes every
1-2 s; the page pulled the merged result every 2 s.

| Run in one lab session | 1 | 2 | 4 | 6 | 8 |
|---|---|---|---|---|---|
| `/ui/status` KB, before | 582 | 1102 | 2145 | 3188 | 4232 |
| `/ui/status` KB, with run filter | 582 | 581 | 582 | 583 | 582 |
| `/ui/status` median ms, before | 6.1 | 10.3 | 18.7 | 29.1 | 34.0 |
| `/ui/status` median ms, with run filter | 5.1 | 5.2 | 4.2 | 4.3 | 6.5 |
| Active s, before | 20.3 | 19.3 | 19.4 | 20.6 | 20.9 |
| Active s, with run filter | 19.3 | 19.3 | 20.3 | 20.4 | 21.5 |
| Host CPU s, before | 22.5 | 12.6 | 24.1 | 36.8 | 46.5 |
| Host CPU s, with run filter | 25.8 | 13.2 | 24.0 | 36.2 | 48.6 |

Change: `GET /status?job_prefix=<origin>.<run id>-` returns that run's jobs, attempts and
results plus every origin job that is not accepted/cancelled and every attempt that is not
released, so "still active" and ownership checks still see all live work. `RenderSession` uses
it; the unfiltered default is unchanged (`Measure-RenderConcurrency.py`, launch scripts).
Unit test: `RenderStatusFilterTests`.

Result: status size and latency stay flat. Host CPU growth is **not** fixed: it comes from the
shared Orchestration core, not the status transfer. In run 9 each GPU node used about 13.5 s of
CPU and the origin 11.2 s; `MeshPeer`/`WorkloadCoordinator` save and deep-copy their full
journals on each change and reconcile re-reads every past attempt. Wall time stayed flat on 4
idle vCPUs; on the laptop that CPU competes with Blender. Fixing it needs an ADR with the
Orchestration owners. Workaround: relaunch the lab, since every launch starts with empty state.

**3. The heat budget stalls repeated runs (the biggest visible slowdown).** With the real
500 kWh profile, each frame commits 0.8 kWh for the whole clock hour (`retainHourlyHeat`), so
quotas 150/100/50 kWh admit about 187/125/62 frames per hour. Frames per GPU worker by run:

| Run | 1-3 | 4 | 5-6 | 7 | 8 |
|---|---|---|---|---|---|
| gpu-1 / gpu-2 / gpu-3 | 16/16/16 | 17/17/14 | 24/24/0 | 36/12/0 | 38/0/0, then stalled |

Run 8 stopped at 38/48 with all GPU workers idle until the next hour. On the laptop (about 93 s
per run) GPU 3 would drop out after about 4 runs in one clock hour. This is the configured
synthetic heat model, not a bug.

**Follow-up (ADR 0069, user decision):** the GPU weights now mean heat *need* per run. Each run is
split by weight (48 frames -> 24/16/8) and each GPU node's `DemandKwh` is its share of the current
run plus heat it already committed this hour; placement code is unchanged. On the replica (2 s
placeholder frames, real 500 kWh profile) every run gave 24/16/8 in about 56-58 s (about 38 s before,
when the split was 16/16/16), and runs no longer stall within the hour.

## Not changed

Blender settings, heat-first placement, roles, GPU budgets, concurrency, deadlines, the page.
