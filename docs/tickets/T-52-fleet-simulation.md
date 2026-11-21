# T-52: Fleet simulation (event-driven, many houses)

Owner: Ennio / Claude. Status: in progress. Branch: `feat/T-52-fleet-simulation`.

## Goal

Event-driven C# simulation of many households with GPU servers (one load value each), sourced job
demand, a pluggable scheduler (mock for now), pluggable Type 2 dynamic pricing and archived forecasts.
Logs per house, per job and per network.

## Spec and decision record

[`docs/model/fleet-simulation.md`](../model/fleet-simulation.md) (reviewed by Ennio 2026-11-21; the
spec is the decision record).

## Depends on

T-51 (grid weather, forecasts, buildings, ElCom, demand traces), Core `HeatDemand` / `HeatBalance`.

## Done when

Everything in the spec is built, its tests (§13) pass, and the three scenarios (§12) run.
