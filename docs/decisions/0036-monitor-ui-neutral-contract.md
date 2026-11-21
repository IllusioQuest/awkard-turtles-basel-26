# 0036: UI-independent monitor inputs and snapshots

Status: proposed; user approved shared inputs and snake_case snapshots, then requested replacing the console with the existing web frontend (2026-11-21).
Ticket: T-46. A local read-only ASP.NET Core API replaces the console; no new scheduler/model.

## Boundary

Keep MonitorConfig, MonitorSetup, HeatMonitorEngine and LiveSources in `gaming/monitor/`.
The user explicitly rejected moving the code. Convert that existing project to a library by
removing only its console entry point and Spectre.Console dependency. A thin local API references
it and transfers JSON to the existing frontend; no new heat or placement implementation.

Add schema-versioned `MonitorInputs` with replay/live mode, selected scenario hour, initial
reservation count, observe-Docker and capability-bypass flags, and display details option.
Reject unsupported versions, unknown fields, invalid hour/mode combinations and invalid counts.
Keep trusted filesystem/config paths outside the browser-facing inputs. Request schema does not
contain commands, executable paths, credentials, Docker socket access or arbitrary API endpoints.
Replay requires a selected committed hour; live requires no selected replay hour.

Add a scoped component directly to /admin (user choice) and a loopback API with configuration
metadata and POST /api/monitor/snapshot. Use Vite's same-origin proxy, reject cross-origin browser
requests, and expose no Docker write operations. Request input cannot override paths/endpoints.
Each snapshot is a stateless hourly preview with initially empty tanks, explicitly labelled; no
cross-user global simulation state or persisted console navigation history. Live mode preserves
existing weather/tariff fetch/fallback logic. No automatic live external-data polling. Docker probe
calls are serialized/cached with observation time, and requests are bounded. Frontend errors are
visible and stale requests cancelled. Binding must remain loopback; public auth is separate work.



Snapshots have schema version, actual mode/hour, selected application, observation time, effective
inputs and explicit scope/warnings. Site/node fields retain units in snake_case names; a field
guide supplies unit, meaning, source and model/observation/assumption classification. Null means
unavailable, never measured zero. Docker CPU and RAM stay raw reported strings with Docker units;
no CPU-to-watts inference. Include exact scenario/config source references and per-site demand/
tariff sources, not arbitrary account/cache data. No markup or terminal rows in the contract.

The snapshot builder maps existing engine views; it does not reimplement heat balance, tariffs,
placement or energy formulas. Shared glossary metadata feeds the web value guide and JSON.
JSON round-trip/validation and snapshot semantic tests cover this public boundary;
existing engine tests protect behavior. Web presentation is checked visually.

## Review

Fresh limited-context verdict: go with changes. Applied explicit loopback Host/Origin validation,
strict JSON, unknown/error observation semantics, cache lifetime, bounded Docker/request deadlines
and immediate busy rejection rather than unbounded waiting. User chose /admin and keeping code
in place. T-45 interfaces are untouched. Public deployment still needs authentication/authorization;
this trusted local prototype must not be exposed as a public multi-tenant service.

