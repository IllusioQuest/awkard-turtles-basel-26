# 0041: Isolated CS 1.6 compatibility trial on Linux

- Status: proposed; user approved isolated Wine trial and two new client nodes; team acceptance pending.
- Date: 2026-11-21.
- Ticket: T-53. Number 0040 is reserved by the parallel four-node demo branch.

## Context

The user supplied a Windows x86 CS 1.6 installer, wants the browser-streamed shooter replaced,
requires public/community multiplayer and Linux-based nodes, and wants Minecraft unchanged.
The unsigned third-party package has not been executed or authenticated as a Valve distribution.
The existing gaming runtime launches configured applications independently from pure admission.

## Proposed decision

Use Wine from the Linux distribution repository in two separate Linux
streaming nodes, not the Windows host or existing Minecraft/Xonotic node. Each own prefix/home and
game data must not share credentials, worlds, host home, Docker socket or sensitive mounts.
Before execution, inspect and record actual user, mounts, capabilities, network, limits and ports.
Copy the installer into disposable Docker-managed storage, with no host bind mounts. Distinguish
Docker-managed disk writes from access to host files. Provision no application profile until
installer and multiplayer feasibility are established; containers are not a VM-level trust boundary.
Do not grant privileged mode or extra capabilities. Use existing streaming setup and named,
centrally configured CPU/RAM/port limits; label those as lab limits, not measured requirements.
An initial installer trial should have no external network, no host file writes and bounded time;
inspect any displayed license instead of accepting it silently. The package hash identifies the
tested artifact, not its trustworthiness. Windows compatibility is unverified.

Retain Xonotic until stream/input and actual multiplayer pass. Target server/build/authentication
requirements must be identified; do not promise arbitrary public compatibility, bypass Steam,
anti-cheat or server content rules. Do not patch authentication-related files. If the supplied
package cannot connect legitimately, report that and ask about an official client instead.

Keep installation, extraction, Wine setup and process launch out of placement decisions. Reuse
existing application profiles and scheduler interfaces; no second scheduler. Pure admission
latency must be distinguished from game startup/stream latency. Preserve active interactive games,
batch-first preemption, bounds and freshness guards. Compare the existing diagnostic on the same
source/config before and during the game if the trial reaches that stage. PR #38's indexed
admission is unmerged; integrate it explicitly before attributing its speed to a new branch.
New game power/heat is unknown; no fabricated measurement or scored-parameter changes.

Proposed lab regression rule, fixed before any CS-loaded measurement: for the same reservation
fixture, investigate/fail acceptance if loaded p95 exceeds `max(2 * preflight_p95,
preflight_p95 + 1 ms)`. This tolerance is an engineering test policy, not a sourced hardware limit
or production SLA. Use the existing fixed-seed diagnostic, 20 warmups/101 samples, identical source
commit and parameter sets. Record median and p95 separately; do not tune the threshold after seeing
the result. Installation/observation/launch/stream timings must be reported independently.

## Alternatives

Official native Linux CS client avoids Wine but needs a legitimate installed base/Steam setup;
the user previously declined the proposed CS:GO Steam trial, so ask about CS 1.6 separately.
Running the installer on Windows conflicts with the Linux-node requirement and is not proposed.

## Review

Fresh limited-context verdict: go with changes. Applied: inspect actual runtime configuration,
use disposable Docker-managed storage without host binds, and defer application profiles until
feasibility is known. Require one named authorized community server with documented build/auth
requirements and an actual playable session; scope any success claim to that target. Fix benchmark
source/config/sample count and a lab regression criterion before the game-loaded run. No installer
has run as of this decision review. User subsequently approved Wine, two separate client nodes
and selecting a target server. Test a shared local match first, then a compatible authorized
community target if feasible; only client work is distributed, not a match server's simulation.

Reviewed existing runtime assumes trusted operator profiles and is not an untrusted tenant sandbox.
Use the stronger inspected trial configuration before deciding whether to add the new profile.
