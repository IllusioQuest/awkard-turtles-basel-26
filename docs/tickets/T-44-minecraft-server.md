# T-44: Integrate Minecraft as a protected persistent workload

Owner: Nikita / Codex. Status: in progress (design; user choices pending).
Planned branch: feat/T-44-minecraft-server. Depends on T-43 / PR #33 (not merged at claim time).

## User request and retained decisions

Integrate a Minecraft server into the new universal scheduler. Use C#/Docker orchestration,
Linux workload containers, node-local persistence and cold backup/restore. Protect active games;
heat-based admission affects new workloads and batch work pauses first. One world server stays
on one node; distributing its internal simulation is not included. Browser rendering/client
processes remain separate GPU workloads, without modifying the player's game client.

## Questions asked before implementation

1. Which Minecraft edition and exact version will the user connect with; do they own Java Edition?
2. Official unmodified Vanilla Java server (recommended) or Paper Java server?
3. Has the user reviewed and accepted https://www.minecraft.net/en-us/eula for this demo, or should
   preparation stop before startup? No server EULA acceptance is assumed; no credentials requested.

## Proposed implementation after answers

- Select a compatible pinned server release and required Java runtime from official metadata (or
  selected provider). Record artifact URLs, version, publisher hash, local integrity check and
  reproduction steps; keep downloaded server binaries/world data out of git.
- Build a Linux, non-root, CPU-only server image. EULA consent is an explicit ignored local
  setting/launch input, never defaulted to accepted in committed files.
- Use existing workload requirements/ContainerProfile/WorkloadCoordinator, not a separate placement
  path. Allocate a fresh trusted resource budget after inspecting available Docker resources;
  clearly source any heat assumption, since Minecraft heat/power has not been measured.
- Persistent data volume and storage/node pin; no migration of an active world. Keep authenticated
  player access and loopback lab exposure. No game client installation/authentication changes.
- Add service readiness observation, graceful save/shutdown verification and restart persistence
  checks before claiming data-safe backup. Review whether the existing bounded stop timeout fits;
  propose any shared profile/runtime extension before changing its interface.
- Prepare scripts for build/start/status/stop/backup/restore through the universal CLI, with a
  deterministic offline/config test and isolated live smoke only after EULA authorization.
- Preserve current Xonotic, desktop containers, user worlds and other agents' worktrees. A running
  server/status handshake is not proof of successful authenticated/browser gameplay.

## Sources consulted (2026-11-21)

- https://www.minecraft.net/en-us/download/server (official Java/Bedrock server downloads)
- https://help.minecraft.net/hc/en-us/articles/360058525452 (official Java server setup)
- https://www.minecraft.net/en-us/eula (user must review; no inferred acceptance)
- docs/universal-workloads.md and ADR 0033 (existing scheduler/runtime constraints)

## Acceptance and current state

Before marking ready: selected edition/version and EULA state recorded; repeatable Linux image
build; node/resource/heat/persistence profile; health/version check; protected active-session
behavior; observed save/restart/cold backup proof; relevant existing and new tests; honest source
and limitations documentation on main; code committed/pushed through a PR.

Current turn: read runtime integration points and official sources, asked important choices,
claimed ticket and documented plan. No server downloaded, EULA accepted, world created, new image
pulled, client installed or running game stopped. Code work awaits edition/software decisions;
startup additionally awaits explicit EULA authorization.

## User answers and preparation milestone

User confirmed Vanilla Java, delegated an easy pinned version, and explicitly accepted the EULA for starting this demo. Official stable metadata resolves 26.3 requiring Java 25. User rejected case-server heat assumptions and requires a measurement before heat-based startup. They agreed to prepare battery measurement; AC is still connected at observation, with invalid discharge sentinel. No heat admission is authorized with a guessed value. Build/preparation and a clearly labelled calibration run are distinct from scheduled heat placement; version/client matching and authenticated gameplay remain visible requirements. ADR 0034 records the measurement/startup boundary.
