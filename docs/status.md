# Status: work map

Last updated: 2026-11-20 23:45 Europe/Zurich. Every agent updates its row when it starts, when it
changes an interface, and when it finishes (see `AGENTS.md` §9a).

## Who works on what

| Person / agent | Branch | What | Interfaces touched | Status |
|---|---|---|---|---|
| team | `main` | Case context; data (Open-Meteo Basel 2025, ElCom 2026 IWB H4, WP1 AI service and compute prices, WP2 costs and energy prices, WP3 sites and profiles incl. When2Heat extract); vision draft (`docs/vision.md`); research plan; AGENTS rules | `data/raw/` and its `SOURCE` files | merged |
| Jona (IllusioQuest) / Codex | `feat/core-csharp` | .NET 8 `DigitalHeat.Core` (heat demand, tank, `IScheduler.Decide(HeatSignal)`, `IExecutor.ApplyTargetLoad`, `SchedulerController.Apply`, `GreedyHeatScheduler`, year simulation, economics), `DigitalHeat.YearRun`, xUnit tests, `config/reference-house.json`, decision 0005 (accepted) | defines `IScheduler`, `IExecutor`, `HeatSignal`, `config/reference-house.json` | merged to main (PR #2); only a `SimulatedExecutor` exists, no real job executor yet |
| Nikita | `feat/T-09-site-heat-state` | Linux gateway crash experiment, three nested Docker engines, sample, placement/contracts; ADRs 0003 and 0006 proposed | no shared heat interface changed; future integration must use existing IExecutor | merged networking milestone (0e7115a); T-09 claimed, interface reviewed; implementation awaits scope approval; plan photo still missing; whole-container pause recovery tested with either manager absent; controller/etcd/node orchestration and heat integration pending; see webhosting-runtime.md |
| team lead / Claude | `exp/year-explorer-transparency` | Browser year explorer prototype for the reference house, `METHOD.md`, `verify.py` | own JavaScript model (must match the C# core) | prototype; review findings pending: example computing price drives the surplus headline, verdict ignores hardware, offline demo, heatmap legend |
| flerovium11 | `main` | `docs/todos.md`, `docs/questions.md` | none | ongoing; no names assigned to todos yet |

## Shared interfaces

- `IScheduler.Decide(HeatSignal)` and `IExecutor.ApplyTargetLoad`, wired by
  `SchedulerController.Apply` (decision 0005, on main). Simulated and real executors
  must both sit behind `IExecutor`.
- `config/reference-house.json`: parameters of the reference house (`feat/core-csharp`).
- `data/raw/` with its `SOURCE` files (on `main`).
- Decision numbers taken: 0003 (webhosting), 0004 (Windows ingress, withdrawn), 0005 (C# core), 0006 (Linux ingress experiment), 0007 (whole-container pause proposal).

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

All planned work is now in the ticket board: [`docs/tickets/README.md`](tickets/README.md)
(24 tickets, created 2026-11-21 from the whiteboard session). Start with **T-00** (team decisions),
then the tier-1 tickets T-01 to T-07 for the draft at 15:00.
