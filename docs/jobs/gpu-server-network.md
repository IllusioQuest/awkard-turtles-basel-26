# Two GPU server profiles and network map

T-69 / PR #59 / `757a56b`. Decision [0061](../decisions/0061-render-network-roster.md),
reviewed go with changes. User selected two **physical GPU servers**, with matching
software profiles. No hardware, addresses or remote access were supplied.
**Two server configurations are prepared; neither server is connected or verified.**
No GPU model was invented, machine purchased, driver installed or remote host contacted.

## Current app and map

Open http://localhost:18910/ui and refresh. The current runtime still has only
`cpu` and `gpu` on one laptop/Intel Iris Xe. `gpu-server-1` and `gpu-server-2`
are display-only planned entries, dashed and labelled not connected. They are
not polled or included in scheduler capacity. The paused run and its 12 completed
frames survived the CPU/UI host update; GPU worker and Minecraft were not restarted.

- Blue circles: an authenticated `/status` check from the UI owner succeeded.
- Orange circles: fresh snapshot reports an executing job from this run.
- Dashed circles/lines: planned server/connection, not deployment or capacity.
- Solid links: this UI origin's configured peer relationships. They do not prove
  links between other servers; the map does not invent a verified full mesh.
- Host cards group reported physical hosts and show actual CPU/frame contributions.
  Reported IDs are operator-provided, not hardware attestation.
- On partial probe failure, reachability is fresh per peer but work/device data
  remains the last complete snapshot. Its timestamp/stale label is visible and
  execution highlights are suppressed. Dispatch keeps its existing all-peers-ready
  requirement; no replacement work is silently launched.

The additive `network` snapshot contains `origin_peer_id`, `configured_peer_ids`,
`planned_gpu_peers`, `connections` (`peer_id`, `state`, `checked_utc`),
`peer_snapshot_utc` and `work_data_stale`. It reuses existing concurrent status
polls and their three-second timeouts; it introduces no discovery, public API,
extra polling requests, new scheduler or heat formula.

## Identical software, separate physical identity

Both prepared servers use native **Blender 5.3.0 / Eevee / OpenGL**, the same
trusted scripts and settings, and the existing shared runtime/mesh. The packaging
preset is Linux x64; native executable, paths and graphics API remain configurable.
This is a deployment candidate, not a verified Linux GPU render or a hardware
purchase recommendation. The native adapter has no Docker isolation/OS quotas.
GPU drivers and a working hardware OpenGL context are host prerequisites.

Files on the PR branch:

- `rendering/network/gpu-server-1.example.json`
- `rendering/network/gpu-server-2.example.json`
- `rendering/network/peer-addresses.example.json`
- `rendering/network/start-native-worker.sh`

The two configs intentionally differ in peer, physical-host and GPU identity.
All physical GPU IDs must be globally unique. Both have the same native software
options. Each template lists the four eventual peers (local CPU/GPU plus both
servers), uses `.invalid` address placeholders, and disables remote UI.
A zero heat-demand budget blocks admission until sourced site inputs or explicitly
labelled synthetic lab inputs are supplied. The inherited abstract 0.8 kWh/job-hour
reservation is NOT measured GPU or recovered heat. Existing Minecraft measurement
guards and heat-first ranking are unchanged.

Matching only the version number is insufficient: all peers must have the same
Blender **build hash**, exact script recipe bytes and prepared scene hash.
The existing session/registration checks reject mismatches. Cross-OS workers
remain unverified; use the same distributed recipe files, not independent
checkouts with different line endings.

## Prepare the deployment bundle

```powershell
./rendering/Publish-LinuxWorker.ps1 -Native
```

Output: `rendering/TestResults/linux-native-render-worker.tar.gz`. It contains
self-contained .NET app, shared theme/UI, allowlisted scene scripts, both example
configs and native launcher. No local token, actual node config, results or
journal state is copied. Blender and host drivers are installed separately from
their official sources by the operator. The existing Docker/Cycles package is
unchanged; it is not a drop-in worker for this prepared Eevee workflow.

On **each real GPU host**, extract the archive and make `app/DigitalHeat.WorkloadRun`
and `start-native-worker.sh` executable. Copy that host's example to a private
working JSON config; replace every placeholder executable/path/address and set
the real heat inputs. Use one private shared asset store, mounted into **all**
participants at their configured `assets_directory`. This is required: the current
adapter validates files at local paths and does not transfer scenes over HTTP.
Shared storage is a dependency, not a fault-tolerant asset service.

Set the same lab token privately on each machine. Set `ASPNETCORE_URLS` to the
worker's reachable **private** interface (the launcher defaults to loopback).
Provide a working host graphics context. Keep the HTTP token/API on a trusted
private network or protected tunnel; no TLS, firewall changes or public exposure
are automated. Then run `./start-native-worker.sh ./gpu-server-1.json` (or server 2).
Startup must complete its real hardware render probe; software rendering is rejected.

Only after these prerequisites are verified, update the real `peers` dictionary
on **all four participants**, using actual private addresses, and remove the
activated IDs from display-only `planned_gpu_peers`. Configure while the render
is paused/drained and restart the affected hosts without losing journals. Verify
all status identities/builds/recipes, confirm expected private peer connectivity,
then resume. The templates are not automatically merged into live runtime config.
The native adapter currently supports one GPU peer per physical host.

## Verification and remaining work

102 orchestration tests pass, including planned-capacity isolation, partial
failure/recovery, wrong peer identity rejection, globally distinct template GPU
IDs and zero unmeasured heat budgets. Linux native packaging and archive checks
pass. UI was visually checked against the real paused 12-frame run. Review changes:
stale snapshot timestamp, owner-only link semantics, matching build hashes.

Two remote deployments and physical GPU scaling **cannot be verified until two
GPU hosts are available**. No speedup or recovered heat claim is made. Evidence:
[render-network-roster-2026-11-21.json](../performance/render-network-roster-2026-11-21.json).
