# 0065 — Five-worker shared-GPU presentation pipeline

Status: proposed. T-73, Nikita/Codex. User explicitly approved three concurrent
local GPU workers sharing Iris Xe, two CPU roles (scene preparation and final
video assembly), increased CPU budgets, relative GPU work budgets 1.1/1/.9,
and Restart render. This is one laptop/GPU, not extra physical GPUs.

Keep MeshPeer/heat-first policy and one RuntimeAttempt per frame. Native role
capabilities restrict preparation to CPU1, Eevee frames to GPU1/2/3, and final
assembly to CPU2. All use identical trusted scripts/build/recipe. Blender's
bundled FFmpeg/VSE is locally verified available; no added library. Assembly
is a normal heat-admitted CPU job after all frames, validating a shared manifest
and PNG hashes, then writing MP4. Private shared assets only, no arbitrary paths.

An explicit shared-GPU lab concurrency parameter allows three aliases of the
same GPU on the same physical host, only when all peers agree. Default remains
one and the existing duplicate-device guard remains for other deployments.
Three separate warm Blender processes execute real frame jobs. Four Blender
threads per GPU worker = 12 during GPU stage, within the measured 16-thread CPU;
CPU prepare/encode stages use four threads and do not overlap GPU rendering.
Thread settings are not OS/driver quotas or promised utilization.

Partition the existing synthetic 500kWh lab total (old 300+200) once across
five logical heat-budget slices with weights 1,1,1.1,1,.9: 100,100,110,100,90kWh.
These are unmeasured lab quotas, not extra physical heat demand. Same heat-first
eligibility/ranking and retained full-hour job commitments. Work shares are
approximate; GPU percentages are neither set nor fabricated.

Restart finishes current work at its lifecycle boundary, cancels remaining owned
jobs, preserves outputs, and starts a fresh run. Display all five real worker
roles plus their actual contributions and downloadable/previewable MP4.
New isolated validation before replacing the user's presentation URL. Preserve
old results and Minecraft. Verify full 48 frames, every role contributing,
assembly integrity, restart, pause and sustained journal-fix behavior. No three-GPU
hardware or speedup claim; Intel driver concurrency remains experimental.

## Review
Fresh limited-context verdict: go with changes. Applied explicit shared-group/host/device/concurrency agreement, independent state directories, strict role capabilities and runtime preflight, ordered hashed frame manifests with build/recipe checks and successful MP4 validation, and assembly-aware cancellation/restart serialization. Implementation/hardware verification follows; design review is not proof of runtime behavior.

## Implementation verification
PR #65 implements the reviewed checks. All 114 orchestration tests pass; real
48-frame run completed with all five roles, three concurrent GPU workers and
valid MP4 assembly. Six-frame pause/restart tests passed. One physical GPU; no
physical scaling claim. Method and limits: docs/jobs/five-worker-renderer.md.
