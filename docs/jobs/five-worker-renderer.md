# Five-worker renderer presentation (T-73)

Code: PR #65, `feat/T-73-five-worker-render`, implementation `d43be26`.
Depends on PR #64 journal fix. ADR 0065 received a fresh-context go-with-changes review.

Open http://localhost:18950/ui and refresh. Five real worker processes appear:
CPU 1 prepares the landscape; three GPU workers render separate frames; CPU 2
assembles the ordered completed frames into a downloadable MP4. They share one
physical Intel Iris Xe on this laptop. This demonstrates actual job distribution,
not three physical GPU machines or a scaling benchmark. Minecraft is unchanged.

Render starts a new run. Pause finishes current frames before holding dispatch.
Restart reconciles current jobs at lifecycle boundaries, preserves old outputs,
and starts a fresh run. Video preview/download appears after CPU assembly.
Older renderer services were stopped, with their results retained.

## Reproduction and configuration

From the feature checkout, run:

```powershell
.\rendering\Start-PresentationRenderLab.ps1 -Port 18950 -Frames 48
```

Use `rendering/presentation-profile.json` for roles, budgets and native settings.
The launcher prints the URL and stores its private token locally; do not commit it.
Stop an existing lab first using `Stop-RenderLab.ps1 -LabDirectory <lab-directory>`;
this verifies process identity, drains work and retains files.

The user-approved relative GPU work weights are 1.1 / 1.0 / 0.9. They affect logical
admission budgets, not physical GPU percentage caps or a promised frame ratio.
Heat-first MeshPeer admission/ranking is unchanged. One inherited synthetic
500 kWh lab quota is divided once: weight sum = 1+1+1.1+1+0.9 = 5;
GPU 1 receives 500*1.1/5 = 110 kWh, others 100/100/100/90 kWh.
These are unmeasured admission quotas, not recovered heat or extra hardware.
Four requested Blender threads per process means up to twelve in the GPU stage;
CPU preparation/assembly are separate stages. This is not an OS utilization cap.

Shared-GPU aliases require matching physical host, GPU identity, group and limit.
Default duplicate-device rejection remains for deployments without this explicit
lab setting. Strict capabilities prevent CPU roles from taking GPU frames;
assembly verifies ordered hashes, scene, recipe and Blender build before publishing.
Do not remove these checks or replace the common heat policy with a demo dispatcher.

## Verification

114 orchestration tests passed. The full 48-frame run completed in 114.1642 active
workflow seconds; three GPU jobs overlapped, each GPU worker completed sixteen
frames, and CPU 2 produced a valid 48-frame MP4 at 12 fps (4 seconds).
The equal observed counts illustrate that weights are not guaranteed frame shares.
Six-frame checks passed pause/resume and restart both after completion and during
GPU work, preserving prior results. No performance or reliability SLA is claimed.
See `docs/performance/five-worker-renderer-2026-11-21.json` for method and evidence.
Browser layout still needs user confirmation; API and actual rendering were verified.
