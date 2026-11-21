# Render speed and node visualization (T-69)

Owner: Nikita / Codex. Date: 2026-11-21. PR #59, `6027676`.
This follow-up uses the existing status contract only. No runtime, placement,
heat accounting, workload lifecycle or Minecraft behavior changes.

## Why the current run is slow

The two logical peers share one physical laptop and one Intel Iris Xe GPU.
CPU prepares the scene; GPU renders independent frames. Adding more processes
on this same GPU does not add physical GPU capacity. Native render execution
starts a new Blender process per frame (`NativeRenderRuntime.Start`), reloads
the packed scene and creates a graphics context. Work inside the render call
can include shader/setup work as well as drawing; no pure GPU timing is claimed.

Run `365486092f70` has five completed frames: 15.1146, 16.9146, 9.4958,
12.1161 and 10.1188 seconds, measured around `bpy.ops.render.render` using
`time.monotonic` in `rendering/native_worker.py`. Their sum is 63.7600 seconds;
mean = 63.7600 / 5 = **12.7520 seconds/frame**. The app rounds to 12.8 seconds.
The same snapshot reports 71.3458 seconds active render workflow and 5.0611
seconds CPU preparation, shown separately. Active workflow minus render-call
sum is about 7.5858 seconds; that includes startup, queueing, orchestration,
result collection and other work and is **not** a scheduler benchmark.

Paused time was 1052.3134 seconds at one snapshot and continues increasing while
paused. Total elapsed includes pause; active time excludes it. The sample is
five frames, not a full-animation result, throughput promise or controlled
hardware comparison. No recovered heat or GPU-utilization measurement exists.

For more speed, a later measured experiment could reuse one Blender process per
GPU worker to avoid repeating scene/context initialization. That changes the
process/lifecycle boundary and requires a separate design/review; it is not
implemented here and no speedup is promised. More physical GPU workers could
parallelize admitted frame jobs. Neither option changes heat-first placement.

## How to read the visualization

Open http://localhost:18910/ui and refresh if it was already open.

- Physical-host boxes contain the logical nodes reported by that host.
- Node cards show CPU/GPU capability, actual executing jobs and completed
  scene/frame contributions for this run. Origin/queued jobs do not prove
  execution placement and are not drawn as executing work.
- The arrows describe workflow stages, not a packet/network trace.
- Blue frame tiles are complete; orange tiles are executing; empty tiles are
  pending (including work not yet submitted). Hover for frame/node details.
- The mean/range uses completed frames' `render_seconds`; it excludes pauses,
  process startup and queue time. It is not an ETA.
- Stale peer status is labelled and active animation is suppressed. Tile colors
  are work state, not GPU utilization or measured heat.

Visualization refreshes through the existing two-second status poll, with no
additional API requests. Host/node/result/frame processing is linear in snapshot
size and frame count. Rendering uses the shared dashboard theme and supports
reduced motion. No new dependencies, contract fields or settings are introduced.

## Verification and reproduction

The live paused run was visually checked: two logical peers grouped under one
physical host/GPU; CPU has one preparation, GPU has five frames; five tiles are
complete and 43 pending; mean/range and active/pause times agree with metadata.
No job was resumed or restarted for this check. The active/stale rendering
branches were inspected, not exercised by launching or disrupting work. No
presentation-only tests were added, per AGENTS.md. PR #54's runtime checks remain
recorded in the native renderer/cleanup documentation.

Reproduce the timing calculation: GET `/ui/status`, collect
`frames[].metadata.render_seconds`, sum and divide by the number of measured
frames. Inspect `peers[].physical_host_id`, `physical_gpu_id`, `attempts` and
`results` for node contributions. The source data and calculation are preserved
in [the evidence file](../performance/render-node-map-2026-11-21.json).
