# Presentation runtime cleanup (T-66 follow-up)

Date: 2026-11-21. Owner: Nikita / Codex. Code: PR #54, `5d6499d`.
User explicitly chose to keep Minecraft and remove all other game labs.
This is a presentation cleanup, not a scheduler or heat-model change.

## Retained and retired components

The live renderer is at http://localhost:18910/ui, with native CPU and GPU
peers at 18910/18911. Intel Iris Xe renders Eevee frames; the CPU prepares
the shared scene and still does process/driver/shader/result work. A GPU render
is not a promise of zero CPU usage. Both peers share one laptop, not two GPUs.

Only these Docker containers remain running for this project:

- `digitalheat-minecraft-calibration`: existing Minecraft server and world.
- `digitalheat-gaming-session-a-1`: existing browser desktop/Minecraft launcher.

Removed exactly 13 scoped lab containers: CS 1.6 D/E and their relay,
Xonotic/Dozen C, software desktop B, the three old mesh batch lab peers,
the three legacy Docker render peers, and two stopped Minecraft restore-test
helpers. Named volumes were preserved; images and unrelated containers were
not pruned. Historical docs are retained as records, not current startup advice.
Do not restart those retired labs for the rendering presentation: they consumed
CPU even while the native renderer was idle.

Removed the Dozen/Xonotic image recipes, startup/test scripts and Vulkan cube
experiment; removed desktop B from Compose. The default runtime and read-only
monitor now describe only Minecraft A. The shared runtime/monitor, Minecraft
scripts, generic Linux mesh executor and scheduler remain reusable. Multi-node
monitor tests now use explicitly synthetic fixtures instead of a retired game
installation. The generic workload CLI example is batch-only.

Minecraft's measured-power prerequisite remains in its real startup path.
The read-only monitor's case-derived 0.8 kWh/hour preview is explicitly abstract,
not measured Minecraft power and not permission for heat-based startup.
Minecraft world data, server settings, EULA handling and accounts are preserved.

## Renderer appearance and current state

The renderer serves the existing `web/src/index.css` as `/ui/theme.css`; it uses
the same charcoal surfaces, blue accent, fonts, spacing and card radius.
No React code or existing `/admin` or `/household` code was moved. The header's
`rendering / held` badge and duplicate held labels are removed. Render, Pause,
Resume, frame preview and node details remain. Layout was checked visually.

Only the drained CPU/UI host was restarted to load the new stylesheet route.
The existing run `365486092f70` remained paused with two completed GPU frames;
it now reports recovery-paused. Use **Resume** to continue the same job identities.
GPU worker and Minecraft were not restarted. The stylesheet served over HTTP
was checked against its source and matched exactly.

## Checks and evidence

Reproduce from the PR branch:

```powershell
dotnet test gaming/tests/DigitalHeat.Gaming.Tests.csproj
dotnet test src/DigitalHeat.Orchestration.Tests
& ./gaming/scripts/Test-MinecraftPowerMath.ps1
```

57 gaming/runtime tests and 98 orchestration tests pass. Minecraft power-math
checks pass; the existing launcher executable is still present in Node A and
Compose validates to only `session-a`. Full Minecraft guard-script execution
requires local build metadata absent from this renderer checkout and was not
claimed. See [machine-readable evidence](../performance/presentation-cleanup-2026-11-21.json).

Own Docker point samples found CS D alone at 204.51% CPU before removal
(100% = one CPU core). After cleanup, the two retained containers sampled
0.49% and 1.19%. These are different instants, not a controlled benchmark,
not recovered-heat measurements, and not a CPU-utilization guarantee.
