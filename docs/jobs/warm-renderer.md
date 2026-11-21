# Warm native GPU rendering (T-71)

PR #62, code `180de77`, reviewed ADR [0063](../decisions/0063-warm-native-gpu-worker.md).
User chose speed for the existing renderer first, extra logical workers later.
Builds on PR #59's renderer map; merge that dependency first.

## Try the optimized version

Open **http://localhost:18920/ui** and click **Render** for a new 48-frame run.
The app retains a short stopped preview; the images are real, not simulated.
The original paused run with 14 frames remains at http://localhost:18910/ui and
in its original state directory. These are two isolated lab instances sharing
one laptop/GPU. Do not run both simultaneously for a timing comparison.
Minecraft server/client were not changed.

The node map still shows the CPU-preparation and GPU-frame peers. This ticket
adds no presentation workers and does not claim three physical GPUs. Cache reuse
is displayed with completed-frame timing; GPU utilization remains unavailable.
The two logical roles share one physical laptop and its heat/capacity assumptions.

## Why a game felt faster

A game keeps graphics state loaded across frames. The old adapter started Blender
and opened the scene again for every output image. The native diagnostic rendered
two frames in one process at unchanged 640x360/32 samples: 8.2570s then 1.0148s.
This suggests substantial first-render setup cost; it does not isolate shader
compilation from every other initialization cost. The two camera frames differ,
so it is not a controlled same-frame benchmark. The renderer also writes final
images; a render-call duration is not a game frame-rate measurement.

## What changed

Native GPU options `keep_gpu_warm` (default false) and
`warm_idle_timeout_seconds` (default 120, a lab choice) enable reuse of one owned
Blender process. The tested lab enables it explicitly. CPU preparation and
capability probes remain separate short-lived processes. All render paths use
the same trusted `native_worker.execute` function; the warm wrapper reloads
scene data only when the scene/recipe identity changes. Both scripts are part
of the immutable recipe. Existing recipe/run outputs are never rewritten.

Every frame is still a separate RuntimeAttempt through the same heat-first
MeshPeer. No batching, new scheduler, ranking change, extra GPU slot or relaxed
heat commitment was introduced. Full-hour commitments remain conservative.
Pausing finishes the active frame and prevents the next dispatch. Idle Blender
may retain its scene/GPU allocation until its timeout; this is not another active
frame or a measurement of recovered heat. Cold first frames remain possible
after idle retirement.

One shared local byte-range lock serializes command assignment, idle retirement
and stop. Pool and attempt launch intent are durable before spawn/activation.
Each result has an atomic completion marker with that frame's activation.
Completion is checked before process-death failure. Stop validates the **active
frame**, not just the reused PID, so a delayed stop for frame A cannot kill B.
Unresolved ownership is retained, not overwritten. Finished assignment is cleared
before releasing its frame slot. Native PID/start/executable checks remain.

A file-completion notification wakes the existing owner loop because a warm
frame no longer exits Blender. The existing timer is retained as fallback.
No new listener or dependency is added. Native execution still has no Docker
isolation or OS-enforced quotas. Warm byte-range locking supports Windows/Linux;
macOS warm mode is explicitly rejected. Linux GPU execution remains unverified.

## Measured checks and how to read the numbers

All results are own runs on one Intel Iris Xe, Blender 5.3.0/OpenGL,
640x360/32 samples, one admitted frame slot. See
[machine-readable evidence](../performance/warm-renderer-2026-11-21.json).

- Real scheduled two-frame verification: 9.5112s first / 1.0024s reused-scene
  render; frame-boundary pause/resume passed.
- A four-frame app run: render calls 0.9812, 1.1313, 0.9971, 0.9938s;
  active render workflow 11.1763s. CPU preparation is separate.
- Later already-warm five-frame portion: 0.9098, 1.0153, 1.0066, 0.9724,
  1.3844s. Sum = 5.2884s; mean = 5.2884 / 5 = **1.0577s/frame**,
  displayed 1.1s. Active workflow = **7.5053s**, including scheduling/result
  collection; preparation workflow is separate. All five used the same Blender
  PID. Four reused the exact loaded scene; the first reused the process/context.
- Owner-host restart preserved the earlier four-frame run. Stop finished the
  active frame and released owned attempts. A bounded new-run/stop check passed.
- One early diagnostic assertion saw a failed state while a stop was in progress;
  final cancellation settled with released attempts. A bounded follow-up rerun
  had no failure before stopping. This is retained as a limitation, not hidden.
- A separate trusted idle-loop probe used a 0.25s timeout, performed no rendering,
  set retirement under the same gate and exited successfully. This tests the
  Python idle loop, not a claim of C# ownership or a remote GPU benchmark.

Render-call time is measured with `time.monotonic` around
`bpy.ops.render.render`. Mean is the sum divided by completed frame count.
Active workflow excludes confirmed pauses and includes coordination. Its
non-render portion is not a scheduler-only benchmark. These short interrupted
runs are **not a full-animation result, guaranteed speedup, ETA or SLA**.

106 orchestration tests pass, including committed completion after worker death,
wrong activation rejection, delayed-stop fencing and stop without active
ownership. The native Linux bundle builds and contains no live token/config/state.
Layout/timing labels were visually checked; no presentation-only tests were added.

## Reproduce

From the PR branch, with portable Blender available:

```powershell
./rendering/Start-NativeRenderLab.ps1 -Port 18920 -NodePrefix 'warm-' -Frames 48 -KeepGpuWarm -PlannedGpuPeers @()
```

Choose free ports if this lab is already running; choose a unique node prefix
because coordinator ownership intentionally prevents duplicate owners. The
launcher prints the new local state directory and records exact process identities.
GPU startup must pass its real hardware probe. Open the printed `/ui`, Render,
Pause and Resume; timing and cache reuse are taken from real frame metadata.
The lab source contains synthetic heat/site inputs, not measurements.

For the two-frame scheduler/pause check, set the existing lab's `MESH_TOKEN`
privately in your shell and run its published app:
`dotnet <app>/DigitalHeat.WorkloadRun.dll render-verify <lab>/warm-cpu/config.json`.
Do not print/commit the token. Check `warm-cpu/native-verification.json` and the
per-attempt GPU ownership/results. Do not overlap runs.

Package deployment files with `./rendering/Publish-LinuxWorker.ps1 -Native`.
The bundle includes both trusted worker scripts; Linux GPU deployment remains
subject to the prerequisites in [GPU server network](gpu-server-network.md).
