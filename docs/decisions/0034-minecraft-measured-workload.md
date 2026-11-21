# 0034: Official Vanilla Minecraft workload and measured-power startup gate

Status: proposed; user approved Vanilla Java direction, delegated an easy server version, explicitly
accepted the Minecraft EULA for this demo, and requires measurement before heat-based startup.
T-44; dependency T-43 / PR #33.

Use the official stable Java release resolved from Mojang's manifest, pinned at integration:
26.3, Java major 25, server SHA-1 33680f5f2ac32864d6d7cf5e56a705fdb3e05f4c.
Source metadata: https://piston-meta.mojang.com/mc/game/version_manifest_v2.json and
https://piston-meta.mojang.com/v1/packages/ead839cbccf0ddf013c3f935f2840080b5cb0d26/26.3.json.
Record publisher SHA-1, locally computed SHA-256, sizes and URLs. No server binary/world in git.
Use pinned official Eclipse Temurin JDK/JRE 25 Linux images; JDK only compiles a small standard-Java
status-protocol helper. No third-party Minecraft server/fork, plugin or authentication bypass.

Server executes as a non-root user, CPU-only with local persistent /data, loopback Minecraft port,
online-mode=true. Initial lab quotas/settings live once in sourced config, not scored parameters.
The world stays pinned to one node. A protocol status response plus startup readiness check verifies
the service; it does not prove authenticated gameplay or browser rendering. A separate streamed
client remains a different GPU workload; the existing Xonotic process is protected.

Use the existing generic workload runtime, locality/storage guards and cold backup path. Keep
EULA startup consent explicit in a local env/launch flag; never default a committed eula.txt to true.
Capture save-completion evidence before claiming safe stop/backup. Verify same-world restart
and cold restore rather than assuming Docker process exit implies a clean world save. Any needed
shared shutdown interface change requires explanation/review; prefer no new generic interface.

Do not fabricate a heat estimate: the user rejected the case's abstract 0.8 kWh per server-hour.
PowerOnline currently true and BatteryStatus.DischargeRate is the invalid sentinel -2147483648;
Docker/WSL expose no usable power sensor. User agreed to prepare an unplugged battery measurement.
Provide a fail-closed Windows battery baseline/load/post-baseline procedure with raw samples,
finite positive discharge checks throughout, source boundaries and workload fingerprint. It measures
whole-laptop incremental electrical power, not CPU-package power or recovered water heat. Existing
background work must remain steady. Report idle/no-player server conditions honestly; do not use
that as a measured active-player peak. No CPU-utilization-to-watts conversion.

A separate clearly labelled calibration container may run the EULA-approved server to obtain the
measurement; it is not heat-based admission and must not masquerade as such. It uses only this
Minecraft lab's volume/labels, never an existing game/world. Heat-driven startup remains blocked
without valid measurement evidence and an explicit recovery-model/source; no re-timestamping a
synthetic inventory or implicit case heat assumption. If on AC/invalid sensor or net power is not
positive, preserve raw evidence and explain why no valid heat figure was obtained.

## Review

Fresh limited-context boundary review: go with changes. Applied predeclared measurement windows, capacity units and regression/noise policy; calibration-only opt-in; separate useful-heat recovery approval; application save proof plus successful non-OOM exit; same-world restart and cold restore tests. Code review additionally led to restoring the original idle-pause setting, recomputing export acceptance from raw samples, and cleaning up a failed measurement safely. The battery trial was rejected; heat-based startup stays blocked. See ../minecraft-server.md for evidence and calculation path.

Browser-client follow-up reuses the existing Selkies Node A and desktop application runtime with the official Linux launcher, installed into its private persistent home. No new shared runtime interface or game modification. Microsoft sign-in and client download remain user actions; account caches must never enter git or world backups. Launcher window and TCP reachability observed, authenticated gameplay pending. User requested local testing before push to main.
