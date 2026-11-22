# 0072 — Confirmed local-node shutdown and frame retry

Status: proposed. T-86. User approved hard stop including controller, shared
completed-frame storage and retry only after confirmed termination. Three logical
GPU workers share one laptop GPU; shutdown removes a worker, not physical GPU.

Use a local-lab power-control adapter: trust only this launcher's processes.json,
matching target config, controller PID/start identity and executable; verify owned
Blender PID/start/executable identities too. Exclude origin/CPU nodes. Kill the
verified controller tree and confirm captured controller/renderer handles exited.
Persist shutdown intent BEFORE kill and proof AFTER confirmation. Uncertain kill
holds retries; on origin restart, reconcile the same intent before taking over.
Never infer fencing merely from timeout/unreachable state. Restore via fresh lab
launch; no re-enable button in this milestone. All files remain intact.

Before publishing successful native frame results, place hash-identified PNGs in
the existing shared assets directory; include image SHA in result metadata. Reuse
these bytes for preview and assembly after a worker's controller stops. Validate
recipe/build/scene/frame and image hash. This lab already shares scene assets;
remote deployments need equivalent durable shared artifact storage.

Keep durable logical-frame to current workload-ID mapping plus retry generation.
After fencing, retain verified completed frames from the fenced node; cancel old
unfinished IDs at the origin and create replacement IDs for those logical frames.
Never reopen old attempt identities or retry while an old execution might live.
Persist replacement IDs before submitting; crash recovery repeats the same IDs.
Delayed old replies/results cannot replace the selected generation.

Explicitly withdraw the fenced peer from origin offer selection (durable peer
exclusion); leave unconfirmed peers fail-closed. Skip only confirmed-off peers in
render polling/control/registration, show powered-off state in existing graph/card.
Other configured-peer failures keep existing stop-and-wait behavior. On shutdown
serialize with existing controls and dispatch, temporarily hold dispatch while
keeping other executing frames intact. Count their already accepted work before
splitting unstarted frames by remaining weights1.5/1.0. Re-register needs including
completed/in-flight contributions so no admitted job loses its heat allowance.

UI: one button to power off GPU3 (the existing GPU3 identity). No layout/copy changes
otherwise. Preserve node in visualization as powered off and show reassigned work.
Test fencing identity/failure, duplicate retry prevention, completed-artifact reuse,
restart intent recovery, heat conservation, and kill during real GPU3 rendering;
complete48frame video on remaining nodes and verify controller actually stopped.

## Review
Pending fresh limited-context review.
