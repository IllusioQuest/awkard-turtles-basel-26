# T-44: Integrate Minecraft as a protected persistent workload

Owner: Nikita / Codex. Status: implemented locally; user gameplay test pending.
Branch: feat/T-44-minecraft-server. Dependency: T-43 / PR #33.

## Agreed scope

Official Vanilla Java, version chosen from official metadata (26.3, Java 25). The user explicitly
accepted the Minecraft EULA and owns Java Edition. One persistent CPU world server; separate
Linux Selkies client on Node A, accessed in the browser with user-controlled Microsoft sign-in.
No Windows client install, game-code changes, offline authentication bypass or world sharding.
Existing Xonotic on Node C remains untouched. Heat placement requires measurement, not case heat.

## Implemented and checked

Pinned Linux server image, protocol readiness helper, explicit calibration/EULA flags, protected
stop/save proof, same-world restart and cold backup/restore. Measurement collector stores raw
capacity samples; export recomputes evidence and fails closed. Local client launcher preparation
verifies the official archive pin and uses the existing C# desktop runtime for future dispatch.
The launcher process/window and client-to-server TCP connection were observed. No authenticated
player connection or Minecraft rendering has yet been verified.

36 gaming and 29 orchestration tests pass, along with Minecraft syntax, power math/tamper,
EULA/heap/rejected-measurement guards and live persistence tests. World/backup/restore volumes
retained. The laptop battery trial was inconclusive; no accepted electrical or useful-heat value.
Scheduled heat-based startup remains blocked, calibration server available on localhost:25566.

## Handoff

See [Minecraft guide](../minecraft-server.md) for the diagram, exact browser/client connection,
commands, measurement calculation and limitations. User requested testing before pushing to main;
code and new docs remain local until the user confirms. Do not mark merged/done or claim browser
gameplay/physical multi-node redundancy from service readiness alone.

## Remaining

- User signs in inside Node A, installs Java 26.3 and joins host.docker.internal:25566.
- Record observed gameplay result, then publish code and docs after user confirmation.
- Valid electrical/useful-heat evidence and any separate recovery-model approval before scheduler admission.
