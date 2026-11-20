# Status: work map

Last updated: 2026-11-20 23:45 Europe/Zurich. Every agent updates its row when it starts, when it
changes an interface, and when it finishes (see `AGENTS.md` §9a).

## Who works on what

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| team | `main` | Case context; data (Open-Meteo Basel 2025, ElCom 2026 IWB H4, WP1 AI service and compute prices, WP2 costs and energy prices, WP3 sites and profiles incl. When2Heat extract); vision draft (`docs/vision.md`); research plan; AGENTS rules | `data/raw/` and its `SOURCE` files | merged |
| Jona (IllusioQuest) / Codex | `feat/core-csharp` | .NET 8 `DigitalHeat.Core` (heat demand, tank, `IScheduler.Decide(HeatSignal)`, `IExecutor.ApplyTargetLoad`, `SchedulerController.Apply`, `GreedyHeatScheduler`, year simulation, economics), `DigitalHeat.YearRun`, xUnit tests, `config/reference-house.json`, decision 0005 (accepted) | defines `IScheduler`, `IExecutor`, `HeatSignal`, `config/reference-house.json` | merged to main (PR #2); only a `SimulatedExecutor` exists, no real job executor yet |
| Nikita | `feature-webhosting` | Resilient local webhosting platform prototype (ASP.NET Core/YARP gateways, node-local Docker engines, etcd, replica placement), decision 0003 (proposed); Linux-only runtime, Windows dev host | none with the heat scheduler yet | open, not connected to the heat scheduler |
| team lead / Claude | `exp/year-explorer-transparency` | Browser year explorer prototype for the reference house, `METHOD.md`, `verify.py` | own JavaScript model (must match the C# core) | prototype; review findings pending: example computing price drives the surplus headline, verdict ignores hardware, offline demo, heatmap legend |
| flerovium11 | `main` | `docs/todos.md`, `docs/questions.md` | none | ongoing; no names assigned to todos yet |

## Shared interfaces

- `IScheduler.Decide(HeatSignal)` and `IExecutor.ApplyTargetLoad`, wired by
  `SchedulerController.Apply` (decision 0005, on main). Simulated and real executors
  must both sit behind `IExecutor`.
- `config/reference-house.json`: parameters of the reference house (`feat/core-csharp`).
- `data/raw/` with its `SOURCE` files (on `main`).
- Decision numbers taken: 0003 (webhosting), 0004 (Windows ingress, withdrawn), 0005 (C# core).

## Known integration risks

1. **No generic real-job executor yet** (must-have 2). The webhosting node runtime uses Docker and
   could provide it, but nothing connects it to `IExecutor`.
2. **Must-have 1 has no owner:** ≥ 2 job types, one measured on a laptop, energy scaled to 1 kW.
3. **Year explorer (JavaScript) and C# core must give the same numbers.** Explorer default:
   4 servers, min load 0 %, tank 35 kWh, 0.3325 CHF/kWh → 58.0 % covered, 0 kWh dumped,
   22,661 server-hours, break-even 0.226 CHF/h.
4. **Webhosting scope vs. the draft deadline** (2026-11-21 15:00).
5. **Decision numbers:** resolved. The C# core decision was renumbered to 0005 when PR #2 was merged.

## Next steps

- Assign owners for must-have 1 (job types, laptop measurement) and must-have 2 (real-job executor
  behind `IExecutor`).
- Done: `feat/core-csharp` merged (PR #2); C# core reproduces the explorer numbers exactly (58.0 %, 22,661 server-hours, 0.2261 CHF/h).
- Add a cross-check between the year explorer and the C# core for the default case above.
- Decide how much webhosting work goes into the draft.
