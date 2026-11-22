# Renderer runtime work (T-77)

Code: PR #69 (`perf/T-77-render-runtime`), implementation593fe8b.
ADRs0066 and0067 were reviewed with changes, all applied. User approved exact
prepared-scene reuse, keeping all five roles, heat-first placement and GPU budgets
1.5/1/.5. Default concurrency remains three and quality remains640x360/32samples.
Minecraft and other agents' core models were not changed.

## Changes and their purpose

- Feed at most one new frame per GPU peer per workflow pass. Previously one new
  frame per second globally limited how quickly three workers could receive jobs.
  Same durable IDs and uncertain-reply reconciliation; MeshPeer still admits and
  places every frame using the common heat-first policy.
- Cache only metadata of released successful native attempts. Pending/failed
  attempts remain uncached; restart reconstructs from durable records. Avoid
  rewriting unchanged frame lists to the session journal.
- CPU1 may verify/reuse a registered immutable scene if script/settings recipe,
  actual Blender build/version, size and SHA-256 agree. Python verifies and writes
  the exact bytes; changes rebuild the scene. Malformed optional registrations
  are ignored with diagnostics; ownership journals are never ignored.
- Keep normal frame-boundary pause/restart behavior. No increase to warm idle
  lifetime (120seconds), physical GPU capacity or native thread setting.
- Fix a reproduced native ownership WriteJson replacement AccessDenied by reusing
  the existing bounded JournalReplace helper, with internal assembly access only.
  Persistent failures remain visible and retain ownership. This extends the prior
  mesh/coordinator fix to the native writer; it does not remove reconciliation.

## How to measure

Run the normal presentation launcher and two successive renders from the app:

```powershell
.\rendering\Start-PresentationRenderLab.ps1 -Port 18950 -Frames 48 -NodePrefix demo-
```

Native artifact metadata now records scene_verify_seconds, scene_load_seconds,
frame_setup_seconds, render_seconds, worker_seconds, dispatch_to_execute_seconds,
completed_utc and prepared_scene_reused. Render seconds are captured immediately
after Blender returns: include shader compilation and PNG writing, not GPU-only
time. Dispatch timing uses wall clocks within the same worker host, includes
activation/settings loading, and is not scheduler queue latency. Worker time
excludes process startup. Parallel worker sums can exceed workflow wall time.
Do not add parallel sums to workflow time or call them utilization percentages.

For a controlled comparison on a completed idle lab:

```powershell
$lab = Get-Content .\rendering\TestResults\latest-presentation-lab.txt
python .\rendering\Measure-RenderConcurrency.py --lab $lab --frames 6 --order 1,2,3,3,2,1
```

This uses normal heat-admitted frame jobs with benchmark-only pinned-node subsets,
not a replacement dispatcher or production pinning policy. It retains manifest,
IDs, partial samples and failures. First verify no outstanding jobs. Warm reuse
is recorded, not assumed. On failure it holds peers before cancelling only its
own IDs. Resume through normal app controls for the next presentation run.
Do not run against an active user render, run builds during timing, or claim a
ranking from incomplete samples. Existing partial diagnostics encountered severe
warm-process stalls and native ownership failures; no reliable1/2/3 ranking yet.
No default concurrency change is justified by those failed experiments.

## Evidence and limits

Initial profiled48-frame run completed in80.7853 active workflow seconds.
Earlier unprofiled run took133.7017seconds with the same scene settings, but these
were not randomized matched trials; no guaranteed percentage speedup is claimed.
The final code cold run completed in93.3555seconds,45warm scene-reuse frames.
Frame-time median3.0867seconds; aggregate render calls229.5716 worker-seconds,
scene verification0.6060 and scene loading0.0831worker-seconds. Rendering dominates
these measured worker costs; CPU preparation workflow took2.0330seconds.
See sanitized evidence in docs/performance/render-runtime-2026-11-21.json.

Second consecutive final-code run completed all 48 frames and MP4 in131.3246
active seconds with the identical scene hash. CPU preparation reused=true:
0.00972worker-seconds versus0.99469 on the first build. All48 GPU frames reused
the loaded scene, with zero scene-load seconds. Render-call aggregate341.9092
worker-seconds; median2.6831seconds. Thus reduced preparation/loading does not
establish a total-runtime improvement: long GPU-call outliers remain.

117 orchestration tests pass, including pending/failed-result exclusion, result
reconstruction after restart and corrupt optional cache behavior. Actual native
write failure evidence is retained in the old lab, not erased. Full MP4 results
remain downloadable from the presentation app. No new UI panels were added.

## Final concurrency check and user decision

Six-frame samples in order1,2,2,1 completed: one-worker11.5588/8.3956seconds,
two-worker7.0674/8.5350seconds; medians9.9772 and7.8012seconds. A separate
three-worker warmup timed out at60seconds, so no completed3-way sample exists.
All diagnostic jobs were cancelled through held peers and artifacts retained.
This is a small sequential test, not a claim of general GPU scaling or an SLA.
After these findings the user explicitly chose **keep three simultaneous GPU
renders**. Do not quietly replace that decision with two slots. Normal heat-first
placement, five roles, approved budgets and image quality remain unchanged.
The completed48-frame video remains in the app; peer holds were released only
after confirming no outstanding attempts/errors from the diagnostic.
