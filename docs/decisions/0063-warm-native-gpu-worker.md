# 0063 — Reuse one owned native Blender GPU process across frame jobs

Status: proposed. T-71, Nikita/Codex. User chose to speed up the existing renderer
before adding logical workers. The two-frame probe measured 8.257s first render,
1.015s second in the same process at unchanged 640x360/32 samples. Diagnostic,
not a full-animation benchmark or SLA. Existing run/results must be preserved.

Add opt-in native GPU warm mode, default false. Keep per-frame RuntimeAttempts,
MeshPeer placement, heat commitment and frame-boundary pause unchanged. One GPU
peer/physical host and one active frame slot remain enforced. CPU preparation
and capability probes remain ordinary short-lived processes. No new scheduler.

A separate immutable warm_worker.py imports the shared native worker's trusted
frame execution function. It reloads scene only when hash/recipe changes, handles
one frame at a time, and writes each result then an atomic per-attempt completion
marker containing activation identity. The warm script joins the recipe hash.
Each job's durable native ownership points to the exact shared process PID/start
identity; completion is per job, not process exit. Observe validates the marker's
activation before returning terminal. Removing a finished attempt leaves the
warm process alive. Explicit stop of an active owned frame may kill this exact
process; the existing single-slot constraint prevents harming another frame.

The pool has a separate durable NativeOwnership record outside attempt discovery,
with recipe/fleet identity and launch intent before spawn. Activation is granted
only after PID/start identity is recorded. Restart reuses only the verified owned
process, never launches a second process on ambiguous launch intent. A configurable
120-second idle timeout (lab choice, not SLA) releases idle GPU resources; next
job restarts cold. Trusted local file mailbox, no listener or new dependency.
Pool command paths and job specs are private lab state, no external scene scripts.

Changing script recipe requires a new isolated lab/run. Current paused run and
results remain intact. Verify real consecutive GPU frames, process reuse, hold
between frames, stop and owner recovery before changing the presentation URL.
Prepare/frame entry points share code so cold and warm renders cannot drift.
No recovered heat, extra GPU, parallelism or full-animation speedup claim.

## Review
Fresh limited-context verdict: go with changes. Require a shared cross-process
file-lock gate for dispatch, idle retirement and stop; fence stop by active
activation as well as PID identity; accept activation-matching completion before
process-death handling and durably clear active assignment before slot release.
The implementation and real verification must cover these conditions.


Implementation verification: PR #62 / 180de77; 106 tests, real scheduled warm frames and pause/resume, owner restart, frame-boundary stop/rerun, isolated idle-loop exit and Linux bundle build. File completion wakes the existing timer-backed loop. Scope remains existing CPU/GPU roles only; extra workers deferred. Windows measured, Linux GPU unverified, macOS warm locking unsupported. Full method and the transient diagnostic stop observation are in `docs/jobs/warm-renderer.md`.
