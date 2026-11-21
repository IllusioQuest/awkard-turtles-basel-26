# 0058 — Portable native Blender runtime and prepared-scene rendering

Status: proposed. T-66. User approved a native trusted-scene adapter, pause at
frame boundaries, CPU scene/assets preparation followed by consistent GPU Eevee
frames. Native Windows Blender 5.3 already rendered using Intel Iris Xe/OpenGL;
the original Linux Docker path failed driver feature checks. No machine purchase.

Keep MeshPeer, WorkloadCoordinator, heat ranking and IWorkloadRuntime unchanged.
Add a narrow native renderer runtime behind that interface. Executable path,
script directory, graphics API, thread count, expected Blender version and
physical device identity live in worker configuration. No OS/vendor branching in
placement. Docker remains an execution adapter. Native mode runs only our trusted
scripts, disables .blend autoexec, and does not claim Docker isolation or hard RAM/
CPU quotas. Worker loopback/private token boundary stays in place.

CPU preparation is an ordinary heat-admitted batch job that writes scene.blend
and metadata. Prepare once before the two timed legs; display its time separately.
Use a configured shared local content-addressed artifact directory for this first
fixture; no new remote artifact transport. Pack dependencies, verify SHA-256 and
bound size before atomic publication. Register immutable scene/frame profiles only
after all participating peers confirm the exact asset. Frames use one engine,
scene hash, Blender version/settings manifest; do not compare mixed Cycles/Eevee
images. GPU frames require verified EEVEE capability and an exclusive physical GPU.
Single leg pins GPU frames; multiple leg allows eligible GPU peers. One laptop
cannot demonstrate multiple physical GPUs. CPU preparation can use another node.

Native frame jobs advertise AllowPause=false. Hold blocks new admission and waits
for the current owned frame to finish; UI shows pause pending until drained. No
GPU kernel suspension claim. Stop/expiry targets only exact owned native attempts.
Persist ownership before spawn, PID/start time/executable identity before an
activation gate allows rendering. Reconcile uncertainty fail-closed; never
kill/restart by PID alone or relaunch an ambiguous frame. Preserve output/logs.

Use a separate local native-node fixture while the original lab comparison and
games remain intact. Small platform bootstrap/config files are permitted; the
job contract, scene scripts and scheduler remain common. Native GPU percentages
are unavailable until a sensor is verified; CPU process time/RAM can be measured.

## Review
Fresh limited-context verdict: go with changes. Accepted the smaller first
milestone: shared local immutable artifacts, one native GPU worker and CPU prep;
no remote artifact transport yet. Bind packed scene/settings/script/version
manifest to results, gate rendering on persisted process identity/activation, and
retain uncertain ownership. Pause must drain frames before confirming/timing.
No immediate suspension or physical multi-GPU scaling claim.
