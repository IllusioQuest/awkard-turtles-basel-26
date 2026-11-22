# 0073 — Restart a fenced local render worker

Status: proposed. T-87. User approved current-run rejoin for unstarted frames.
Extends ADR0072; same local trusted-lab boundary, same single heat-first policy.
No remote timeout-based recovery, new libraries or game changes.

Keep the target controller's original state directory, coordinator/heat history,
cancellation tombstones and completed artifacts. Before launch, re-confirm its
recorded controller/native identities cannot execute. Record explicit failed exit
for its unreleased native attempts only after that proof. Coordinator then releases
those attempts normally while held; never reset the coordinator or reuse old IDs.
Completed released records remain unchanged. Superseded attempts still retain
hourly heat, but count as other work when deriving new run need (not successful
frames). Add an optional superseded-workload-ID list to RunHeatNeed; old requests
remain compatible. Original queue-inclusive deadlines remain unchanged.

Persist restart intent before spawn, then exact new PID/start/executable before
activation. A crash between spawn and PID persistence leaves restart held; never
blindly spawn again. Restart a held controller using its existing config and the
current lab executable. Update only its launcher manifest entry after durable PID.
On replay adopt only that exact process. A missing new process after recorded
spawn fails closed; operator can explicitly retry after proof, not auto-duplicate.

Hold new dispatch during redistribution, keep existing frames running. Wait for
restarted worker's matching recipe/build/scene, reconcile and cancel superseded IDs
while held. Preserve completed and already assigned current-frame work, including
proposals with uncertain replies. Divide only unassigned work among all three heat
weights. Save exact needs before registration; replay them on registration failure.
Remove durable peer withdrawal only after fresh authenticated status and cleanup;
clear any stale offer so a fresh offer is required. Never undo cancellation IDs.
Publish peer as back online and resume dispatch after registrations and re-admission.
Allow subsequent off/on cycles with accumulated generations/deadlines/tombstones.

UI: reuse the same button as Power off GPU3 / Power on GPU3 / switching state.
Other layout unchanged. Capability startup may take time; surface errors and hold
on ambiguity. Test new process identity, launch uncertainty, repeat/recovery ordering,
stale offers/cancelled IDs, heat conservation including failed attempts, and a real
48-frame off/on run where GPU3 receives later work and the final video is complete.

## Implementation (T-87, taken over by Nikita / Claude)

- `POST /ui/nodes/{node}/startup`; the power button reads "Power on GPU 3" while the node is off and
  "Powering on GPU 3…" while it restarts. Power-on also works between renders.
- Stages after "ready": "starting" (intent saved, no process proven), "started" (exact new PID/start saved in
  the session, then in `processes.json`), "rejoined" (back in the mesh; the record is kept so retry deadlines and
  fenced frames stay available for this render). A crash in "starting" fails closed (operator relaunch);
  "started" is resumed by pressing Power on again (the saved process is adopted, then readiness is awaited).
- `LocalNodePowerControl.Restart` re-runs the T-86 verified stop on the recorded identities, starts the same
  controller (`dotnet DigitalHeat.WorkloadRun.dll render-node <its config>`, same state directory, inherited
  lab token) and writes its own logs (`RENDER_NODE_OWN_LOGS`). The node reconciles and releases the attempts
  killed at power-off by itself; power-on waits (up to 3 min) for a reachable, matching recipe/build, verified
  GPU and no unreleased attempts.
- The restarted node gets the current scene and this render's cancellation IDs; every other node readmits it
  (`MeshPeer.ReadmitPeer` drops its stale offer, so only a fresh offer is planned against).
- During a render, `RenderFailoverPlan.RejoinNeeds` splits only frames that are neither finished nor running by
  heat weight; `RunHeatNeed.SupersededIds` lists run attempts released without a result (e.g. killed), whose
  committed heat then counts as other work. A second power-off in the same render keeps earlier retry deadlines.
- Deviation from the text above: the needs are recomputed and re-registered on each attempt instead of being
  saved first; registration is idempotent and a failed attempt leaves the node held in "started".
- Two T-86 fencing bugs that only show on Linux were fixed: exact start-time comparison across processes
  (tolerance 1 s on Linux, exact on Windows) and reading the module of a child that is still exiting.

Verified on a Linux replica of the five-node lab (2 s placeholder frames): off/on within one render, two cycles
in one render, off during a render and on after it, then a normal render; all 48/48 with video, GPU 3 rendering
again after power-on, 5 controllers at the end, 1 proposal rejected in total. Browser check of the button.
Evidence: `docs/performance/node-rejoin-2026-11-22.json`. Not yet run on the Windows laptop.

## Review
Pending fresh limited-context review.
