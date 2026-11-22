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

## Review
Pending fresh limited-context review.
