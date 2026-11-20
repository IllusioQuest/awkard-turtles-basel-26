# 0007: Whole-container pause recovery for the local lab

- **Status:** proposed; user approval required before implementation
- **Date:** 2026-11-20
- **Decided by:** pending

## Context

A local watchdog can kill a frozen gateway worker, but Docker pause freezes the watchdog too.
The still-open port blocks standby binding. The user requests a design covering this next.

## Decision proposed

Add two independent Linux management containers, each able to observe both gateways and
fence a paused active gateway by stopping it through Docker Desktop's outer Engine API.
They poll the shared URL, remember the last active gateway, and require repeated failures.
Probe `/_platform/gateway` independently of backend health; reset counters on recovery or identity
change. Immediately before stopping, re-probe health and re-inspect the immutable ID and paused state.
Resolve container IDs from an explicit Compose project/service allowlist, recheck identity/state,
and stop only a matching paused gateway. Do not restart it automatically; restore it explicitly
after the experiment. Concurrent stops of the same immutable container ID must be idempotent.
If no active identity is known, only a matching paused gateway may be fenced after shared-endpoint
failure; otherwise report uncertainty. A healthy shared URL must not trigger fencing.

## Alternatives considered

- Local watchdog only: already approved but freezes with its container.
- Manual stop/unpause from Windows: useful recovery action, not autonomous resilience.
- Real independent Linux hosts with redundant routing: closer to deployment, outside this laptop lab.

## Consequences

No native Windows service or singleton adapter is introduced. Either management container can fail
while the other remains able to act. Both still depend on the shared outer Docker Engine/laptop.
Mounting that engine's socket grants broad host control: the allowlist is application logic,
not a security sandbox. Use only trusted local lab images; user must approve this new access.
First run a minimal paused-container termination experiment with bounded Engine request timeouts
and verified listener release. Prefer termination without a separate unpause operation.
HTTP health checks and Engine mutations are not atomic; test stale identities, concurrent managers
and a gateway recovering during detection. Do not expose an Engine TCP listener.
Verify Docker stop semantics on paused containers before claiming success. If explicit unpause
is required before termination, document that brief thaw and recheck the container identity.
Test active-container pause with either manager missing, unchanged URL, measured failed probes,
restoration of redundancy, and no action against unrelated or healthy gateway containers.
This does not implement controller/etcd resilience or solve production network partitions.

## Review

Verdict: go with changes. Applied: gateway-specific health with counter reset and final state
recheck; bounded termination experiment as an implementation gate; explicit trusted-lab socket
opt-in and tests protecting unrelated containers. The reviewer emphasized the non-atomic race
and broad socket authority. Manual recovery remains the fallback until the user approves.
