# Warm-process render penalty (T-79)

Diagnostic for the native landscape renderer. Nothing in the production path changes.

## Question

The warm GPU workers (T-71, T-77) keep one Blender process alive and render every assigned
frame in it. Does a warm process render its later frames slower than its first one? If so,
starting a fresh process per frame (or recycling every K frames) could be faster overall,
even though each fresh process pays startup, scene load and shader compilation again.

## How to measure

On the presentation laptop, with the render lab stopped (`rendering/Stop-RenderLab.ps1`):

```powershell
python .\rendering\bench_frame.py --blender (Get-ChildItem .\rendering\TestResults\native-blender\*\blender.exe | Select-Object -First 1).FullName
```

- Prepares the scene once with `native_worker.py` (CPU prepare stage).
- **warm**: one Blender process calls `native_worker.execute(..., keep_scene=True)` for each
  frame, the same call the warm worker makes.
- **fresh**: one Blender process per frame, `keep_scene=False`.
- Same Blender flags as `NativeRenderRuntime.cs`; threads and graphics API come from
  `presentation-profile.json`; quality from `scene_settings.json`.
- Default 6 frames spread over the 48-frame animation, 2 rounds, mode order alternating per
  round (warm/fresh, then fresh/warm).
- Output: `rendering/TestResults/bench-frame-<utc>/bench-results.json` (per-render rows plus
  summary) and a printed summary. Copy the JSON to `docs/performance/` to keep it.

How to read the summary:

| Field | Meaning |
|---|---|
| `warm_first_render_median_s` | Blender render call, first frame in a warm process |
| `warm_later_render_median_s` | Render call for frames 2..N in the same process. Much larger than the first = penalty |
| `fresh_render_median_s` / `fresh_process_median_s` | Render call / whole process (startup, load, shaders, render) per fresh frame |
| `warm_wall_seconds_per_frame` / `fresh_wall_seconds_per_frame` | Total process wall time divided by frames: the number that decides |
| `identical_images_across_modes` | Hash of PNG pixel data (not the metadata chunks Blender stamps with date and time) matches per frame |

Limits: sequential single worker, not the three concurrent GPU workers of the presentation;
few rounds on one machine. Enough to decide whether recycling is worth an ADR, not an SLA.
Render seconds include shader compilation and PNG writing (as in T-77).

## Result so far: software OpenGL only (not the laptop)

Cloud container, 4 vCPU, no GPU, Mesa llvmpipe under Xvfb, `bpy` 5.3.0 behind a test-only CLI
shim, `--allow-software`, 4 frames (1, 17, 32, 48) x 2 rounds.
Evidence: `docs/performance/bench-frame-llvmpipe-2026-11-22.json`.

| | Seconds |
|---|---|
| Warm, first render in process (median) | 6.25 |
| Warm, later renders (median) | 19.54 |
| Fresh, render call (median) | 6.25 |
| Fresh, whole process per frame (median) | 6.99 |
| Warm wall per frame | 16.39 |
| Fresh wall per frame | 6.96 |

Pixels identical across modes for all four frames. Worked example: a warm process with 4
frames costs about 6.25 + 3 x 19.5 + startup/prepared-scene load ≈ 65.6 s (16.4 s per frame);
four fresh processes cost 4 x 6.99 ≈ 27.9 s.

Earlier one-off checks in the same container (not in the evidence file) showed the extra time
is per TAA sample (32 samples: 6.2 → 19.5 s; 4 samples: 3.0 → 3.9 s), disappears with
`eevee.use_shadows = False` (3.3 s), and is unaffected by shadow pool size, shadow resolution
scale, reopening the .blend between frames, merging the 176 objects into 7, or disabling the
compositor. On llvmpipe the extra CPU time runs in Blender's threads inside the GL driver, so a
real GPU may pay it as GPU work, or not at all. T-77 measured a warm median of 2.7–3.1 s on the
Iris Xe under three-way sharing, which neither confirms nor rules out the effect.

**Status:** unverified on the presentation hardware. Do not change the warm worker on this
evidence alone; run the laptop benchmark first.
