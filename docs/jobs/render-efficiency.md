# Renderer efficiency and diagnosis (T-83)

User approved a detached read-only heat summary. ADR0070 reviewed with changes,
all applied. Core exposes HeatCommitments() and CommittedHeatKwh(hour) under its
existing gate, preserving order, retained intervals and exact reservation values.
Finished means released/terminal, including failures, matching the old formula.
Arrays are copied; callers cannot mutate ownership. No caching or reconciliation
skips. MeshPeer.Publish uses the scalar; RenderHeatNeed uses compact snapshots.
Full Attempts() and all runtime Discover/Observe/ownership checks are unchanged.

132 orchestration tests pass. On a synthetic500-attempt history,50queries:
old deep-copy query230632768allocated bytes /1437.4218ms;
compact projection2607200bytes /11.1325ms;
scalar0bytes /2.3625ms. This is98.87%fewer allocated bytes for the compact query,
not a promised98.87%faster render. Timing includes test-runner activity; allocations
are per thread. Reproduce with dotnet test and HeatSummaryTests; read output from
TRX or the detailed console logger.100-attempt measurements are also retained.

The existing T-79 benchmark was run on actual Iris Xe hardware, four frames spread
across48frames and two intended alternating rounds, same quality/threads. Warm
round1 render calls:8.539,1.273,1.225,0.985seconds. Fresh round1 median12.548s.
Fresh round2 exited3221225477 (C0000005), EXCEPTION_ACCESS_VIOLATION in Intel
igxelpicd64.dll after saving frame17. Preserve partial reports and failure evidence;
this does not establish the cause of the earlier second-run failure. Do not switch
to fresh mode on this incomplete benchmark. Existing warm mode remains enabled.

Warm-worker stdout/stderr and native exit evidence are now stored under
native-warm-worker/logs/<process-activation>/, so later generations cannot overwrite
the crash evidence. Diagnostics do not authorize automatic retries or mark a frame
successful without its validated completion marker. Exit-code recording waits for
actual process exit; ownership behavior is unchanged. Minecraft is untouched.

Evidence: docs/performance/render-efficiency-2026-11-22.json.

## Real pipeline verification

PR #73, implementation8a4e4ad (subsequent comments60fbc70). Two DISTINCT run IDs:
c55bd23b2516:74.6994s; ff0b2f1144f0:78.2986s. Both completed48frames with
GPU counts24/16/8, CPU assembly48frames and downloaded valid MP4s. Test harness
waited for a new ID because POST starts are queued; a stale prior complete snapshot
must not be counted as a second run. The earlier baseline153.7926s is unmatched:
driver caches/system load were not controlled; no guaranteed percentage speedup.

Per-generation diagnostics also captured a later idle-process exit C0000005,
while the validated frames/videos remained complete. This verifies evidence is
retained, not that Intel graphics crashes are fixed. Keep three concurrent GPU
workers as explicitly requested. The running app remains at localhost:18950/ui.
