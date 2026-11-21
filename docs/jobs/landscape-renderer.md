# Landscape rendering lab (T-66)

Code branch: feat/T-66-landscape-render. ADR 0052; built on the unified scheduler.

## Open and run

From the feature checkout on Windows with Docker Desktop using Linux containers:

```powershell
.\rendering\Start-Renderer.ps1
```

Open http://localhost:18900/ui. No game client, browser plugin or local Blender installation is
needed. Press **Run comparison**: 48 frames on one heat-selected eligible node, then the same
48 frames on all eligible nodes. The preview plays completed PNG frames at 12 fps (four seconds
when all 48 are complete). This is a browser frame player, not an encoded MP4 export.

Pause/Resume acts on this dedicated renderer mesh, never the existing game/Minecraft owners.
Pause gates origin submissions and persistently holds every renderer peer. It is acknowledged
only when every owned live CPU frame confirms pause. Heat returning cannot undo a manual hold.
Unreachable peers are uncertain, not successfully paused. Resume uses normal heat/resource
checks. A node restart defaults held; recovery suppresses the headline speedup because downtime
cannot be treated as a clean benchmark. Failed/incomplete runs cannot start an overlapping new
comparison automatically; inspect the failure/owned attempts first. Keep named volumes.

## What the numbers mean

- Active wall time = total elapsed wall time minus confirmed operator-pause intervals. It is NOT
  the sum of CPU seconds. Queueing, container startup, scene generation and result collection count.
- Paused time and total elapsed remain separate. An unreachable pause is not silently subtracted.
- Speedup = single-node active completion seconds / multi-node active completion seconds. Display
  only after both legs finish with all frames, matching renderer backend and no manual pauses or
  process recovery. Single-first ordering can introduce cache/thermal bias: no statistical speedup
  claim from one run. Different node counts deliberately change aggregate available resources.
- Frame counts come from successful exits plus real PNG/JSON artifacts, not a timer animation.
- CPU percent and RAM are Docker samples; 100% CPU means one logical core. GPU utilization and VRAM
  are sampled through nvidia-smi only when that tool/device is actually available. GPU numbers are
  whole-device totals, not attribution to this render. Otherwise show Unavailable, never zero.

All three local engines share ONE Windows laptop and Docker Desktop VM. Each advertises a bounded
1 CPU/1536 MiB/one-frame quota and has an outer 1.25 CPU/2304 MiB limit (control-plane overhead
included). Blender has one render thread. These are configurable lab slices, not three physical
servers. Existing gaming loads remain interference. Image preload/device probe are excluded from
both measured legs; each frame builds the same scene from scratch, no pre-rendered image cache.

## Renderer and GPU truthfulness

The portable image installs Debian Bookworm Blender 3.4.1 from the official Debian repository,
with its base digest pinned in rendering/Dockerfile. Runtime jobs pin the image config digest;
all peers must match. The scene is rendering/landscape.py inside that image: deterministic seed
66, 640x360, 32 samples, 48 camera positions, terrain/lake/trees/rocks, CPU or explicit GPU backend.
Denoising is disabled in both legs because the Debian build lacks OpenImageDenoiser; the first
calibration exposed this and was fixed before comparison. No external scene assets are fetched.

The actual laptop is Intel Iris Xe. Blender's probe reported no Cycles GPUs. The existing gaming
OpenGL/D3D12 path does not establish compute rendering support. Local execution is CPU fallback.
Blender GPU source (accessed 2026-11-21):
https://projects.blender.org/blender/blender-manual/src/branch/main/manual/render/cycles/gpu_rendering.rst
Intel oneAPI documentation specifies Arc-class support; do not promise Iris Xe acceleration.

The adapter supports operator-configured GPU bindings and a GPU-capable renderer image:

```powershell
.\rendering\Start-Renderer.ps1 -RendererImage YOUR_LOCAL_COMPATIBLE_IMAGE -GpuConfiguration PATH_TO_LOCAL_JSON
```

The custom image must implement the same trusted entrypoint/probe/scene contract as Dockerfile
and landscape.py. GPU-enabled inner Docker engines, device exposure into their outer nodes and
vendor drivers/toolkit must already be configured on compatible Linux hardware; this launcher does
NOT install NVIDIA Container Toolkit or manufacture GPU access. The default Debian image is the
verified CPU path, not a tested NVIDIA/AMD deployment. Do not edit real-device mappings blindly.
The local GPU JSON maps home/hotel/spa to `gpu` (existing GpuBinding: device_id, nvidia_device_id,
linux_devices, read_only_binds) and `gpu_backend` (OPTIX/CUDA/HIP/ONEAPI). Device IDs must identify
physical devices consistently across engines. A tiny real GPU render must pass before advertising
that backend. Duplicate GPU IDs across independent peer domains reject the comparison. A GPU
backend is preferred when available; CPU is used consistently when none is verified. In a GPU run,
CPU-only peers are ineligible for the GPU frame request. No mid-leg silent GPU-to-CPU fallback.
GPU kernel quiescence after Docker pause is not verified; only the CPU process-freeze guarantee
has been tested locally. GPU enablement needs a real hardware validation, not an advertised SLA.

## Scheduler, storage and security

No replacement scheduler: per-frame WorkloadRequest/ContainerProfile -> MeshPeer -> shared
WorkloadPlacement -> selected target recheck -> DockerWorkloadRuntime. Origin intents, target
attempts and tombstones retain the existing durable ownership rules. Independent frames can be
placed separately; no attempt to split one Blender process across nodes. Pausing does not migrate
active frames or reopen spent hourly heat. Each completed frame is copied from its unique result
volume before releasing its owned container. Per-frame render.log and result.json remain local.

The 100/300/200 kWh home/hotel/spa demand inputs are deliberately synthetic test headroom so 96
conservative hourly frame claims do not exhaust the demonstration. They are NOT realistic house
measurements. Each frame claims the case's abstract 0.8 kWh/server-hour; actual Blender power and
recoverable water heat are unmeasured. The 7200-second per-frame expiry is an explicit operator
lab limit, not a measured service deadline. Heat ranks only fresh eligible offers, never promises
instant global knowledge. No changes to scored reference parameters or economics.

The home peer publishes loopback 18900 and serves the app itself. It also has a separate operator
bridge because Docker Desktop did not publish host ports from the internal-only mesh network.
Other peer messaging stays on the private internal network with the lab token. Workload containers
use --network none, unprivileged UID, dropped capabilities and resource limits. Browser POSTs
require a same-origin custom header. This is trusted local lab infrastructure: outer DinD containers
remain privileged, no production isolation claim. No outer Docker socket/home mount or Docker TCP
listener. GPU-ready deployment is a separate operator task as described above.

The dedicated renderer uses tested overlay2 to avoid copying the full Blender filesystem per
frame. Its docker-overlay/render-overlay state paths preserve earlier unused vfs calibration data;
no old volume or game data was deleted. /run is tmpfs for correct daemon restart behavior.
rendering/TestResults is ignored: tokens, image archives, generated node JSON, binaries and raw
results stay local. Do not commit them. Source image/scene changes require a new comparison;
never relabel old artifacts as a new run.

## Verified local result (2026-11-21)

76 orchestration tests pass, including durable hold persistence and heat-update protection.
WorkloadRun builds without warnings. Browser layout, actual PNG preview and playback were
visually checked at localhost:18900/ui. A bounded `render-verify` integration run exercised the
same comparison driver against real peers with TWO frames per leg; it did not change the app's
48-frame default. Single-node active wall time 60.4182 s, total elapsed 64.4510 s including
4.0328 s confirmed operator pause. Multi-node active/elapsed 32.4812 s. Both single frames ran
on hotel (highest eligible synthetic heat); multi frames ran on hotel and spa. All four outputs
are real 640x360/32-sample Blender 3.4.1 PNGs with metadata. The earlier calibration frame used
26.1864 seconds of Blender-only rendering; that excludes startup and collection.

No headline speedup is reported for this paused verification, and these are NOT measured times
for 48 frames. Do not scale them into a promised completion time. Hardware is the single shared
Intel i5-1240P laptop; GPU probe returned no Cycles devices. GPU execution and GPU pause are
unverified here. Evidence: ../performance/landscape-render-verification-2026-11-21.json.
Code PR #54. The app is left idle, with a labelled real calibration preview, ready for Run comparison.

## Follow-up: responsiveness and mixed devices

The user reported a real 15-second timeout after the initial smoke check. Browser polls had
waited synchronously for all peers; status handlers also competed with synchronous Docker
control under the peer lock. Failure responses changed shape, breaking JavaScript refresh.
The fix publishes immutable peer snapshots, refreshes peer data off the browser request path,
keeps JSON schema stable on failure, coalesces browser polling and bounds mailbox work. Slow
peer reads wait without abandoning ownership. Frame-submission acknowledgements are queued;
a lost acknowledgement reconciles the same durable ID. Eight minimum .NET control workers
avoid synchronous Docker calls starving ASP.NET continuations under a one-core quota. This is
a lab runtime setting, not more render CPU or a numerical model parameter.

User approved 80% GPU / 20% CPU frame jobs (ADR 0053). `-GpuFrameShare 0.8` is configurable.
At 48 frames this rounds to 38 GPU frames and 10 CPU frames, evenly spread. Each frame is still
independently heat-scheduled among eligible nodes. CPU jobs request no GPU; GPU jobs require a
verified backend. The single-node leg pins both types to one GPU-capable node; multiple-node
execution can place frames on different eligible workers. Without a verified GPU, all 48 are
explicitly CPU fallback. This is frame-count allocation, not guaranteed GPU/CPU utilization.
GPU utilization can also include CPU scene preparation and other workloads. Both legs preserve
the same allocation plan across recovery; image/frame-count changes are rejected. A queued
frame can expire before it obtains an executor and is still reported, rather than waiting forever.
Node cards show current frame number, actual backend and completed frame counts in the run.

User-approved failed-run recovery now has a scoped cancellation: hold all render peers, fence
all possible frame IDs of the failed comparison (including not-yet-observed submissions), cancel
only its stoppable batch attempts, and wait for every peer to confirm release. Preserve completed
PNG images, logs, journal history and heat commitments. Delayed submissions/proposals/replies do
not recreate cancelled work. Cancellation is terminal for Resume, including after restart; a new
comparison has a new ID and archives the old comparison record. Partially acknowledged cancellation
remains held and cannot enable a new run. Games/Minecraft are excluded. ADR 0054.

The earlier failed run contained a nonzero frame exit after a renderer-node restart; its failed
result must not be relabelled successful. The user explicitly approved cancelling its unfinished
jobs and preparing a clean run. This is separate from the successful four-frame verification.

Follow-up validation: 84 orchestration tests pass; WorkloadRun builds cleanly. Ten direct-loopback
status requests during actual Blender rendering had maximum observed response 102.2412 ms.
A separate paused-peer test returned all 12 localhost status responses with stable schema and a
visible connection warning, maximum 2122.0586 ms including host connection/address-resolution
overhead. These are local samples, not an SLA or directly comparable probes. Raw evidence:
../performance/render-status-responsiveness-2026-11-21.json. The exact user-failed comparison
9ba15c1288a1 was cancelled after explicit approval; its completed hotel PNG and logs remain.
A clean Run comparison is permitted only after cancellation is confirmed on all render peers.
