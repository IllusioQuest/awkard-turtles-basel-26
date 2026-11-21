# 0052 — Landscape frame rendering on the heat-first mesh

Status: proposed; user approved Blender Cycles CPU+GPU-preferred, a 48-frame 640x360/32-sample
landscape animation, measured one-node versus multi-node comparison, whole-job pause/resume.
Ticket T-66; feature branch feat/T-66-landscape-render depends on unified scheduler PR #52.

Use the existing C# MeshPeer, WorkloadPlacement and DockerWorkloadRuntime for per-frame jobs.
Do not create a second scheduler. Each frame is a fixed trusted renderer profile, identical scene,
seed, samples, resolution and Blender image on every peer. Origin owns frame intents. A small
C# web app on a dedicated Linux node serves Render comparison/Pause/Resume and snapshots.
Use three separate Linux engines; keep game/Minecraft owners untouched. Reuse trusted private
lab token and loopback-only browser access. No arbitrary user Python/scripts/commands accepted.

Probe Cycles devices on every peer. Prefer a successfully tested GPU backend, otherwise explicitly
label CPU fallback; no inference from OpenGL support. Backend/device class stays fixed across
both comparison legs; changing devices invalidates speedup. CPU work also supports scene prep.
GPU eligibility must reflect configured physical device identity; never clone one physical GPU
as independent GPU capacity. Laptop Iris Xe is not a supported Arc-class oneAPI device. The
local test is expected to fall back to CPU; no measured GPU result claimed without real render.

Single leg restricts eligibility to one heat-selected node; multiple leg uses all eligible peers.
Both run exactly frames 1..48 sequentially by leg, fixed per-node concurrency and resource limits.
Per-frame committed heat remains conservative until the hour ends; sufficient synthetic test
headroom is clearly labelled, not a physical measurement. Show active wall time, paused wall
 time and total elapsed, completed frames, per-frame render times, CPU/RAM telemetry and
GPU/VRAM only when supported. Speedup = single active completion / multi active completion,
with total elapsed also visible, pause and device mismatches flagged. Startup/prep/queue count.
Preload images/probe before measurement, no render cache reuse; warm-order bias disclosed.

Pause gates future submissions and freezes every owned live frame; resume only through normal
heat/resource checks. Do not reset heat or reassign uncertain frames. Completed PNGs persist,
preview is a frame sequence (no new encoder dependency). Failures are visible, never fake 100%.
Local compare/UI state durable; recovery defaults paused until reconciled. Node data/images
transfer via trusted private HTTP; never expose Docker control socket to the browser app.

## Review
Pending fresh limited-context review.
