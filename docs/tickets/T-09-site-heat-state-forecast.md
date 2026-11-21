# T-09: Per-site heat state and forecast

- **Tier:** 2 Pitch  ·  **Area:** Core / model
- **Depends on:** T-00
- **Suggested owner:** Codex  ·  **Owner:** Nikita / Codex · **Status:** in progress (implemented and tested; awaiting PR review/merge)

## Goal

Give each server/site the state from the whiteboard: needed heat (now), max heat it can produce (const), predicted heat need (forecast).

## Tasks

- [x] Extend the heat signal per site: `NeededHeatKwh`, `MaxHeatProducedKwh`, `PredictedHeatKwh[horizon]` (24–72 h).
- [x] Forecast source: perfect foresight from the weather data (label it so) plus a variant with forecast error (parameter).
- [x] Keep the existing greedy scheduler working (backwards compatible); decision record for the interface change.
- [x] Tests: forecast arrays have the right length and units; perfect foresight equals the actual demand.

## Done when

- Schedulers can read current and predicted heat per site; ADR written and reviewed.

## Read first

- `src/DigitalHeat.Core/Scheduling.cs`
- `docs/vision.md (perfect foresight)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

Implementation: src/DigitalHeat.Core/SiteHeatState.cs; tests: SiteHeatStateTests.cs. Core suite: 20 passed including existing annual checks. Forecasts retain source labels/id/index, copy arrays, validate finite arithmetic, and support the 24/72-hour horizons. The greedy adapter deliberately ignores forecasts; optimisation is T-12. ADR 0008 reviewed; no shared interface was replaced. T-00 choices stay open and do not set hardware values in this generic contract.

Handoff: PR #5 (https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/5), branch feat/T-09-site-heat-state. After integrating Jona's merged Docker runtime, all 23 shared-core/runtime tests pass. Run: dotnet test src/DigitalHeat.Core.Tests/DigitalHeat.Core.Tests.csproj -c Release. Ready for review; do not mark done before merge. Follow-on T-10 may consume SiteHeatState; T-12 implements forecast-aware routing policy. No forecast policy changes the scored annual simulation in this ticket.

