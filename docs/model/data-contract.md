# Result bundle contract v1 (T-13)

Status: merged to main in PR #10 (e554f196), 2026-11-21.
Decision: [0011](../decisions/0011-result-export-contract.md).
This is an opt-in export of existing C# results, not a second model.

## Produce and consume
From repository root:
    dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/unlimited.json --output results/replay
Output is relative to the current directory; the scenario is relative to the repository root.
Choose an empty or nonexistent output directory. Omitting --output preserves legacy output.
Read run.json, require schema_version=1, then summary.json.sites and each hourly_path.
Dashboards display exported values; do not recalculate heat or economics.
These files contain simulation results, not hosting-node telemetry.

## Files
All JSON keys are snake_case, including nested parameter and job fields. JSON numbers are
finite numeric values, missing/undefined results use null; text is never a substitute for a number.
CSV is UTF-8 without BOM, comma-delimited, LF-terminated, dot decimals, round-trip precision;
text with quotes, commas or newlines is quoted with doubled quotes.

- summary.json: schema_version and sites array. Each site has site_id, hourly_path, summary.
  Current stable ID is reference-house; this does not imply multi-site simulation exists.
- parameters.json: schema_version, site_id, reference_parameters and job_scenario. Every reference
  parameter is {value, unit, source}, preserving the loaded central config. Job scenario preserves
  mode, excess_heat_policy, source and job_types, including their sourced parameter objects.
  These are public export snapshots, not directly loadable by the existing camelCase input loaders.
- run.json: schema_version, timezone, time_convention, step_hours, git_commit, git_dirty,
  code_provenance, seed, randomness, command (argument array), command_working_directory,
  inputs (path and sha256), flows_status. Paths and replay arguments are repository-relative.
  Git metadata is null if unavailable; dirty runs cannot be reconstructed from the commit alone.
  A clean commit plus matching input hashes identifies reproducible source/input provenance.
  Seed=null and randomness=none mean deterministic execution, not a missing random seed.
- sites/reference-house/hourly.csv: exact hourly columns listed below.
- flows.csv: local_time,job_id,from_site_id,to_site_id,work_units,work_unit. Header only.
  flows_status=not_modelled means unknown/unimplemented routing, NOT zero migrations.
  No flow rows may be inferred. A future routing implementation needs a reviewed contract extension.

## Summary fields and units
Counts: server_count, heating_days, hours.
Energy (kWh): space_heat_demand_kwh, hot_water_demand_kwh, total_heat_demand_kwh,
server_heat_used_kwh, backup_heat_kwh, dumped_heat_kwh.
server_hours is equivalent full-load server-hours. utilisation and coverage are fractions (0..1).
Money (CHF): electricity_cost_chf, heat_value_chf, computing_income_chf.
break_even_chf_per_server_hour is CHF/full-load server-hour, null when no server-hours.
missed_deadlines, job_demand_assumption, profitability_note are explanation strings.
jobs=null means unlimited supply, without a finite queue. Otherwise jobs has arrived,
due_within_horizon, completed, missed, backlog (job counts), missed_deadline_percent
(0..100 or null if no due jobs), and by_type. Each by_type item has id, work_unit,
arrived, due_within_horizon, completed, missed, backlog, work_units_processed,
backlog_work_units. Work quantities use that item's work_unit.

## Hourly columns
local_time is a nominal Europe/Zurich hourly label without offset. The source has 24 values per
day, including DST transition days; do not reinterpret it as an actual elapsed UTC timeline.
heat_demand_kwh, space_heat_kwh, hot_water_kwh, produced_heat_kwh, direct_used_kwh,
tank_charge_kwh, tank_discharge_kwh, tank_stored_kwh, dumped_heat_kwh, backup_heat_kwh,
server_electric_kwh are energy for the step (tank_stored_kwh is end-of-step content).
target_load_per_server and actual_load_per_server are fractions.
Column order is exactly the header in the file and must be read by name.

## Calculation path and interpretation
YearRun reads config/reference-house.json, the selected job scenario, Open-Meteo weather and
the ElCom snapshot; calls HeatDemand.Build and YearSimulation.Run using the shared scheduler.
No formula changes occur in the exporter. For example, hourly useful heat is direct_used_kwh
plus tank_discharge_kwh; total demand equals useful heat plus backup_heat_kwh.
Summary coverage is server_heat_used_kwh / total_heat_demand_kwh. A schematic check: if the
export says demand=10 and useful=6, coverage is 0.6; these are explanatory, not measured figures.
Break-even excludes hardware/integration/operations; retain profitability_note in presentations.

## Economics bundle (additive, T-14)
A separate opt-in export, `YearRun . --economics config/economics/reference-house.json --output DIR`,
writes run.json (same provenance fields plus kind=economics), parameters.json, levers.csv,
required-premium.csv and win-win.csv in the conventions above. It does not change summary.json or
the hourly files. Columns and formulas: docs/model/economics.md (decision 0025).

## Compatibility and privacy
Renaming/removing fields or changing units/meaning requires a schema_version bump.
Consumers may ignore additive fields; adding new core metrics does not silently add summary fields.
Configuration structure extensions require contract review.
Configuration source strings are public export content: never include personal data or secrets.
Only repository inputs, hashes, aggregate metrics and configuration are exported; no process
environment, user payloads or credentials. Hosts must still review free-text inputs before publication.

## Verified showcase
Generated at clean code revision 8651786d863a10c99e73421c63a42b90b93f4258:
    dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/unlimited.json --output data/derived/reference-house-v1
All 29 core/runtime/export tests passed. Legacy and bundle hourly files matched byte-for-byte
in the generation checkout; coverage matched exactly. Git checkout may normalize line endings.
The showcase is 8,760 hourly rows; hashes identify inputs at generation (line endings affect hashes).
Committed on feat/T-13-result-export, merged in PR #10.

