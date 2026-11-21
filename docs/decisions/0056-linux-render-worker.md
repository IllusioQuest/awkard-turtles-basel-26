# 0056 — Portable Linux render worker packaging

Status: proposed. User chose a compatible Linux GPU worker on 2026-11-21;
no GPU machine is available yet. T-66 / PR #54.

Package the existing self-contained WorkloadRun render-node for linux-x64 as a
host process using that machine's own Docker engine. No second scheduler,
remote Docker TCP socket or nested NVIDIA runtime. Preserve existing MeshPeer
heat eligibility/ranking, GPU exclusivity, origin ownership and frame admission.
Supply a checksum-pinned official Blender 5.3 CPU/Cycles image; GPU advertising
requires the existing actual GPU render check using configured GPU bindings.
NVIDIA CUDA/OptiX is the documented first deployment candidate, not a hardware
purchase choice or verified deployment. Other vendors need appropriate runtime
libraries/device permissions and actual tests; no zero-configuration claim.

Use one immutable registry image digest shared by all peers (classic versus
containerd Docker stores can assign different local image IDs). Configuration
contains unique physical host/GPU identity, peer URLs and labelled lab heat.
The bundle contains no token/private key, performs no host installation, and
does not contact any remote machine. Keep games/current render run unchanged.

The wrapper listens on loopback by default; remotely exposed workers disable
browser routes via serve_ui=false, retaining lab-token checks on peer APIs.
Only configure private authenticated tunnels or HTTPS with trusted certificates
for cross-host traffic; no public unauthenticated HTTP setup. No automatic VPN,
certificate or NVIDIA-toolkit installation. Actual connectivity waits for a host.

Extract one shared render-probe command from startup, with explicit user,
resource limits/isolation and the same GPU bindings as jobs. Poll an exact owned
probe container up to 90 seconds to allow cold compilation; stop/remove it and
its anonymous volumes on completion/failure. Exit 0 means GPU verification;
exit 2 explicitly means no verified GPU. This is deployment diagnostics, not a
new execution policy or a heat-controlled workload.

## Review
Fresh limited-context verdict: go with changes. Applied: create a unique probe
name/ownership label before the Docker call, clean up by that exact owned identity
including ambiguous creation acknowledgements, and force-remove after stop failure;
pin the host launcher to the local Unix socket instead of inherited Docker contexts;
document extraction-time executable chmod and run a Linux apphost smoke test.
No remote GPU verification is claimed without hardware.
