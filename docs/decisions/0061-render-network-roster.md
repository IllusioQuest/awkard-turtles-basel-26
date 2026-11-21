# 0061 — Truthful renderer network roster and uniform native GPU workers

Status: proposed. T-69 follow-up, Nikita / Codex. User requests two additional physical GPU
servers, the same software on both, and a map of all connected and used nodes.
No remote addresses or machines are available; deployment must not be claimed.

Extend the existing local renderer status snapshot with configured peer IDs,
per-peer status-probe reachability/check time, and optional planned GPU peer IDs.
Planned nodes are display-only: never inserted into runtime Peers, counted as
capacity, or probed. Their dashed map entries explicitly say not connected.
Probe existing configured peers concurrently, once per existing refresh, with
existing three-second limits. Preserve the prior all-or-nothing session peer
snapshot and dispatch hold on any probe failure; record fresh reachability
separately. Verify returned peer ID matches the configured ID. No placement,
heat ranking, ownership, executor or asset-transfer change.

SVG map includes all configured/planned IDs, grouped details by physical host,
actual per-run work highlights, and a legend: edges represent only the UI origin
to its configured peers, not unobserved links between other servers. Reachability
is from the UI owner only. Historical work/device snapshots carry a timestamp
and a stale label when any required probe fails.
Unavailable nodes are labelled, never shown as verified GPU execution.

Prepare two equivalent native Blender 5.3/Eevee/OpenGL worker templates with
unique IDs/physical hosts and configurable OS executable/paths. Default to
Linux x64 deployment packaging using the existing native adapter; driver/context
and successful GPU startup probe remain prerequisites. No hardware purchase,
remote connection or driver installation. Examples use .invalid endpoints,
zero unmeasured heat budget, no secrets, and stay outside live runtime config.
All four peers (local CPU/GPU plus two remote GPU peers) need real private
addresses and a privately mounted shared assets directory before remote frames
can execute: current native adapter validates local shared scene files and does
not implement asset transfer. Mixed Cycles legacy Docker workers are not
compatible with the current prepared Eevee pipeline. Keep that limitation clear.
Native bundle copies only allowlisted files, not local token/config/state.

## Review
Fresh limited-context verdict: go with changes. Applied all three points:
retain/display the last complete snapshot timestamp on partial failure; draw
only owner-to-peer configured links, not assumed complete-mesh edges; require
matching Blender build hashes as well as version/recipe and document the private
shared-assets prerequisite. Mixed-platform deployment remains unverified.
No geographic coordinates or synthetic performance/capacity claims. Existing
native security/host ownership limits apply.
