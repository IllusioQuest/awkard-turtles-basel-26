# T-68: Household simulated job event log

- **Owner:** Codex · **Status:** in progress · **Branch:** `feat/T-68-household-job-log`
- **Depends on:** T-18 household dashboard and T-58 fleet simulation.

Show chronological, per-house job lifecycle events in the household view and provide two reproducible, explicitly simulated reference-house day scenarios: two completed winter jobs and one summer job with a heat-related pause and later resume. Use the actual fleet scheduler and preserve the separate annual reference scenario. Document artificial inputs and limits. See decision 0060.

## Inputs and reproduction

- The two runs use `config/fleet/houses/reference.json` or its summer copy, which has identical physical case parameters and Basel hourly weather. The latter starts with 32.016714845 kWh stored: the calculated end-of-hour tank state from the reference annual FleetRun at 2025-07-14 23:00, using `config/fleet/scenarios/reference.json` at commit `4e363d6`. This is a simulation state, not a measured house reading, and comes from the annual scenario's different job stream.
- The job arrivals and sizes are illustrative, not historical: `config/fleet/demo/*-jobs.csv`. Both days use the same single-server 2,500,000-task research job. Its nominal energy is 2,500,000 × 4.09e-6 = 10.225 kWh from `config/fleet/job-types/research-sim-montecarlo.json` (a CPU measurement used as a GPU placeholder). Per user direction, the job runs at full load or pauses; its minimum sustainable load has not been measured (`TODO(source)`).
- The same heat-first scheduler as the Fleet year is used. The summer job pauses and resumes as a whole on one server, with a simulated initial tank state from the separate annual run. This is not an actual process or evidence that a live execution can pause. The case hot water is constant 12 kWh/day; it is not a measured demand profile. Single-day economics are not an annual balance, especially with pre-existing stored heat.

```bash
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference-winter-jobs.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference-summer-jobs.json
dotnet run -c Release --project src/DigitalHeat.FleetApi
cd web && npm ci && npm run dev
```

In the dashboard select the run and its `reference` house. Every entry describes a recorded scheduler action or engine completion; hover or inspect the existing hourly charts for load, heat, tank and temperature over the selected day. These are simulation outputs, not sensor measurements.

## Verification

The two simulated day runs were regenerated: both winter jobs finish; the single summer job starts, pauses, resumes and finishes. Its resume follows a new hourly heat-budget calculation; the case hot-water demand does not increase during this example.
