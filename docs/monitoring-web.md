# Admin monitoring data integration (T-46)

Implementation branch: `feat/T-46-monitor-value-labels`. User changed scope from improving the
console to displaying its data directly inside the existing `/admin` web dashboard. The old
terminal entry point and Spectre.Console dependency are retired. **Existing monitor logic stays
in `gaming/monitor/`**, as explicitly requested; it is now a library used by a thin local API.
No heat/placement calculations moved into JavaScript and no existing gameplay was stopped.

## Run and read it

From the implementation checkout, use two terminals:

```powershell
dotnet run --project gaming/api -c Release
# Separate terminal:
cd web
npm ci
npm run dev
```

Open http://127.0.0.1:5173/admin. The **Compute & useful heat** section receives data from C#.
The older map/charts below retain their separate mock data source and time controls; they are
explicitly not relabelled as real Docker telemetry. `/household` is unchanged.

Select replay/current-weather mode, an hourly scenario label, requested planned sessions and
whether to read Docker. January/July are presets. Select **Update preview** to apply inputs;
pending changes and errors are visible. Readings are on-demand snapshots, not an automatic live
feed. Observation time is shown. No game is launched, paused, moved or stopped. What-if bypasses
application compatibility only and is visibly labelled. Ready prechecks do not guarantee admission.

Every snapshot is one independent full-hour preview with **empty initial tanks**. It does not
preserve the former console's step/back history or claim cumulative results. Its engine is the
same existing `HeatMonitorEngine`, `SessionPlacement`, Core `HeatCapacity` and `HeatBalance.Step`.
Current weather preserves the existing source/fallback rules: only the reference house recomputes
its case formula; other sites retain same-calendar-hour historical demand. No meter is added.

### Meaning of values

| Value | Meaning and limits |
|---|---|
| Container / app process | Docker state and configured Xonotic process observation, not gameplay or players. Errors are unknown, not confirmed stopped. Minecraft world-server status is outside this configuration. |
| CPU / RAM | Docker-reported percentage and used/limit strings. 100% CPU is one core; several cores can exceed it. Missing readings stay null/unavailable, not zero. Never converted into watts. |
| Planned sessions / slots | In-memory reservations / configured desktop slots, not connected users or measured throughput. |
| Heat needed | Space heating plus hot water energy for the selected hour, kWh. Building/weather model, not customer meter data. |
| Planned heat | Reservation heat for one full hour, kWh. A-018 still assumes 0.8 kWh/h per full case-server session; not measured Minecraft/Xonotic/laptop heat. |
| Tank stored / capacity | Start-of-hour / maximum energy in kWh, not litres or temperature. |
| Heat headroom | `max(0, demand + tank capacity - stored energy - planned heat)`, kWh, from shared Core helper. Not CPU/RAM/GPU capacity. |
| Wasted heat / backup heating | Unusable computing heat / heat supplied by another heater, kWh. Backup does not mean a standby computer or saved world. |
| Outside temperature | Historical weather or current forecast, degrees C; never presented as an on-site sensor. |
| Tariff / displaced heat value | CHF/kWh, annual tariff and displaced heating value. H4 is a proxy for non-household sites, not hourly spot pricing. |
| Potential heat value | CHF/h from direct use plus tank charging; not revenue, realised savings or profit. |

**What every value means** expands meanings, units, classifications and exact source references.
Metric tooltips use those same C# metadata fields. Model sites are mapped to local test containers;
all currently share a single laptop/engine, not physically separate homes/hotels.

## Transferable data contract

`MonitorContract.cs` in the existing monitoring project defines strict schema-versioned snake_case
inputs and snapshots. Browser types live in `web/src/admin/monitoring/types.ts`. No command,
executable, filesystem path, credentials or API endpoint can be supplied in a snapshot request.
Trusted configuration paths remain server-side. Export inputs/snapshot buttons download ordinary
JSON. A different frontend can post the same input without parsing a console or reimplementing physics.

```json
{
  "schema_version": 1,
  "mode": "replay",
  "selected_hour_local": "2025-07-15T18:00",
  "initial_reservations": 3,
  "observe_docker": false,
  "bypass_capabilities": true,
  "show_details": true
}
```

`GET /api/monitor/config` returns defaults, supported hours and input limits.
`POST /api/monitor/snapshot` accepts that contract. `live` requires a null selected replay hour.
Unknown/missing fields, unsupported versions, fractional/out-of-range counts and absent scenario
hours are rejected. Snapshot numeric metrics carry `value`, `unit`, `kind`, `meaning`, `source`;
Docker resource strings retain Docker's units. Unavailable values are JSON null. Snapshot inputs,
actual mode/hour, generation/observation times, sources and warnings travel with the values.

Worked reproducible comparison (committed July preset, empty tanks, three what-if claims): home
demand 0.50 + tank 35.00 - planned 0.80 = **34.70 kWh** remaining useful-heat headroom. Hotel/spa:
7.2893 + 245 - 0.8 = **251.4893 kWh** (display 251.49). Inputs come from the existing
`config/network/switzerland-test.json` hourly scenario and `gaming/deploy/heat-monitor.json`
session assumption, not invented live heat. The shared helper computes the value; browser only formats it.

## Local API boundary

ASP.NET Core uses only the .NET shared framework. Service limits are defined once in
`gaming/deploy/monitor-api.json`: loopback port 5088, frontend port 5173, 5 s observation-cache
lifetime and command deadline, 45 s total snapshot deadline, 32 KiB request-body limit. These are
local service-policy choices, not measured performance or scored inputs. Vite proxies the API
on the same origin, binds loopback and requires its configured port. API rejects unexpected
Host/Origin headers and non-JSON snapshot requests. No permissive CORS or public binding.
One snapshot executes at a time; concurrent requests fail with a retryable 503 instead of queuing.
Docker command execution has fixed read-only verbs and no host shell. Existing API weather calls
are bounded by the whole request deadline. A failed refresh leaves an explicitly stale previous
snapshot, never substituted mock observations. No automatic current-weather external polling.

This is a trusted local prototype, not a public authenticated service. Node operators can inspect
containers and their data; JSON snapshots must not be published as public telemetry by default.
Deployment/reverse proxy/authentication and static production serving remain separate work.
The Minecraft measured-heat admission gate is unchanged: its battery trial remains rejected.

## Review, coordination and validation

ADR 0036 fresh limited-context verdict: **go with changes**. Applied explicit loopback Host/Origin
guards, unknown-vs-stopped observation semantics, fixed observation cache, bounded process/request
execution and no unbounded request queue. User chose `/admin` and keeping logic files in place.

Checked other work: Jona / Codex's T-45 pure federated offer/claim model and paper proposal landed
on main in f629865 during this task; live Docker dispatch remains unimplemented. Open work includes T-14 economics and T-18 household
dashboard. Only a component import/render was added to `AdminPage`; their model/map/chart logic
and household files were preserved. Shared status/branches are evidence of reported work, not a
claim about agents actively running. No external messages sent.

Checks: clean API build; 42 gaming/monitor tests and 41 Network integration tests, including input JSON round-trip, strict rejection,
snapshot agreement with engine views and unknown Docker errors; frontend type/build/lint; HTTP
smoke (replay, read-only actual Docker, Host/Origin 403, invalid/unknown inputs 400, content type
400); browser check of controls, July values, resource observations and expanded explanations.
Production web build retains the existing large-bundle warning; no new frontend dependency added.

```powershell
dotnet test gaming/tests -c Release
gaming/scripts/Test-MonitorApi.ps1
gaming/scripts/Test-MonitorApi.ps1 -ObserveDocker
cd web
npm run build
npm run lint
```

User-facing browser preview is local; code is in [PR #35](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/35), not yet merged. Documentation
is committed directly to main per the standing team workflow. Historical T-42 console commands in
older docs describe that retired version; use this guide for the replacement.
