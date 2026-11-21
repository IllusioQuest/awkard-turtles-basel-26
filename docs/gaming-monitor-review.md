# Gaming console review and data clarity (T-42)

## Review

The new monitor is a useful read-only heat-placement prototype. It reuses `SessionPlacement`,
`HeatCapacity`, `HeatBalance.Step`, `HeatDemand` and `NetworkScenarioLoader`; no second heat model.
Independent limited-context review identified these issues:

1. `--live --once` started the fetch but rendered replay before applying it. Fixed by awaiting and
   applying the initial result before printing, including redirected-input mode.
2. Dense tables wrapped into over 100 lines at 80 columns. Replaced the default with compact
   node/site tables, stable S1/S2/S3 legends and an optional S/--details source/calculation view.
3. “Generation” and “useful heat CHF/h” looked like observations or savings. They are now reserved
   model heat and potential heat value; storage inclusion and the lack of measurements are explicit.
4. Docker paused containers could appear available. The read-only probe now inspects running and
   paused state before admitting new claims; fixtures cover the guard.
5. Session heat and dataset-offset unit strings were unchecked. Expected units are now validated.

Unchanged limitation: final held reservations apply for the entire model hour, even after an
observed node failure. Live reservation changes are not integrated over seconds. Totals are an
hourly what-if, not a time-integrated meter or billing ledger. Existing matches are never stopped.

## Data: observations, model and assumptions

- Actual Docker CPU percentage and memory use are sampled read-only with one `docker stats`
  snapshot per engine. They are shown separately, never converted into watts. Missing stats are
  unavailable, not zero. CPU can exceed 100% for multiple cores; RAM is Docker's reported figure.
- Replay demand uses the existing 8,760-hour 2025 network scenario: Open-Meteo weather plus
  the reference case formula or sourced profile-derived, synthetic building demand. Hotel/spa
  names are scenario sites, not customers or measured properties. The S/--details view shows
  annual demand and peak from those same hourly inputs, current space/water split and sources.
- Current-hour mode fetches forecast temperature. Only the reference house recomputes its case
  demand from it. Other buildings deliberately retain their 2025 same-calendar-hour demand.
  Their current temperature is context, not evidence their demand was recalculated.
- Prices are annual ElCom H4 household tariffs, not hourly electricity-market prices. For
  non-household sites H4 is an explicitly labelled proxy. Fallback uses the committed snapshot;
  the screen warns when that occurs. No invented business tariff is substituted.
- Session heat remains A-018: 1 case kW × 80% = 0.8 kWh per hourly session. It is not measured
  Xonotic or laptop power. Actual thermal measurements remain `TODO(source)` before hardware claims.
- Each node currently represents one desktop slot; only Node C declares Xonotic installed.
  `--what-if` bypasses this compatibility guard for a visibly labelled model comparison, not a
  real deployment. All three containers still share one laptop/engine, not three heat sites.

## Reproduce from the gaming checkout

Use Release while the old Debug console remains open (Windows otherwise locks its executable).

```powershell
dotnet run --project gaming/monitor -c Release
dotnet run --project gaming/monitor -c Release -- --once --reserve 1
dotnet run --project gaming/monitor -c Release -- --no-docker --once --date 2025-07-15T18:00 --reserve 3 --what-if --details
dotnet run --project gaming/monitor -c Release -- --live --once --details
dotnet test gaming/tests -c Release
```

`--date` selects a label present in the committed scenario, `--reserve` requests up to the given
number of in-memory claims, and `--no-docker` assumes containers running. None launches games.
S toggles source/history panels; the original R/X/N/F/B/J/U/L/D/C/Q controls remain.
Avoid --details in a short interactive terminal; use --once --details to scroll the full report.

Worked July comparison (empty tanks, 2025-07-15 18:00 dataset UTC+1): home demand 0.50 + 35.00
free tank = 35.50 kWh; hotel/spa 7.29 + 245.00 = 252.29 kWh. The largest eligible reserve wins.
One 0.80 kWh claim at the spa leaves about 251.49 kWh headroom. Display rounds to two decimals;
calculations use the original CSV precision. Annual/peak details are sum/max of those same hourly
series, not an extrapolation from the current hour. Dataset building sizes/tanks remain scenarios.

## Validation

36 gaming tests pass, including the author's engine tests and new offline observations/source
fixtures. A live read-only snapshot saw the existing game and CPU/RAM observations; no game or
container was restarted. One-shot live mode applied an actual Open-Meteo response; ElCom returned
no current H4 observation and the committed 0.3325 CHF/kWh snapshot was visibly used as fallback.
Compact January/July and full detail text were visually inspected at 80 columns. Further final
Core/Network/build results are recorded in the ticket at integration.
