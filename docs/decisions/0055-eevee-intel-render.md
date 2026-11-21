# 0055 — Verified Eevee rendering for Intel WSL graphics

Status: proposed; user approved an isolated Eevee test on 2026-11-21. T-66.

The laptop has Intel Iris Xe (Windows driver 31.0.101.4146). The current Debian
Blender 3.4.1 image reports CUDA/HIP compiled backends and no compute devices;
Blender documents Intel Arc/Xe HPG for Cycles oneAPI, not this Iris Xe.

Test Eevee using the existing Mesa D3D12 /dev/dxg path inside an isolated Linux
container, with read-only WSL driver libraries. Use Xvfb only as a headless X
server; verify the GL renderer is hardware D3D12 Intel, rejecting llvmpipe,
softpipe and software renderers, then require a completed Eevee render.
Do not relabel Eevee as Cycles. Keep Cycles CPU for the approved 20% CPU frames;
label the hybrid workload and forbid comparisons with different per-backend mixes.
Preserve the common heat-first MeshPeer policy and physical GPU identity. One
laptop GPU is one scheduling resource, not three independent GPUs. Start with
one GPU-enabled peer and CPU peers; multi-node splitting includes CPU frames.
Use explicit outer device/library mounts and the same inner bindings for probe
and execution. Do not change running games or the active Cycles comparison.
Only enable a new renderer preset after the actual hardware render succeeds;
archive/finish the old comparison before deployment. No measured heat claim.

Sources (accessed 2026-11-21):
- https://docs.blender.org/manual/en/3.6/render/cycles/gpu_rendering.html
- https://docs.docker.com/desktop/features/gpu/
- Local hardware inventory and Blender backend probe, reproduced in T-66 notes.

## Review
Fresh limited-context review: go with changes. Require identical per-frame backend
assignments and renderer/image/scene settings across legs; retain GL identity from
Blender's actual Eevee context alongside its completed image; when only CPU peers
have heat, GPU frames remain queued under ordinary MeshPeer admission. Accepted.
Eevee remains a capability experiment until these checks pass.
