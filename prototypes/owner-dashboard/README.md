# T-18: Homeowner view

This is a one-screen, offline review tool for a **simulated** reference house, not live telemetry or
an authenticated private account. It reads the T-13 result bundle; it contains no scheduler or
second heat model. The default example is the committed `data/derived/reference-house-v1` run.

From the repository root, start a local static server:

```sh
python -m http.server 8000
```

Open `http://localhost:8000/prototypes/owner-dashboard/` and switch between 15 January and
15 July. Choose a bar or move the hour slider. **Open another run** accepts a folder produced by:

```sh
dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/unlimited.json --output results/replay
```

The folder must contain `summary.json`, `parameters.json`, `run.json`, and the hourly CSV named
by `summary.json.sites[0].hourly_path`. All files stay in the browser; no server upload occurs.
The view supports result-bundle schema v1 and shows its first site. The static server only serves
local files; it needs no internet. Opening `index.html` directly from `file://` does not load the
default example because browsers restrict local fetches; the folder picker still works.

## How to read the numbers

- Annual coverage, delivered heat, backup heat and heat value come directly from
  `summary.json`. The bar uses the exported coverage fraction.
- For a selected hour, **heat from computing** is `direct_used_kwh + tank_discharge_kwh` from the
  hourly CSV. **Regular heating** is `backup_heat_kwh`. **Server load** is the exported
  `actual_load_per_server` fraction. Storage is `tank_stored_kwh` at the *end* of the hour.
  Useful heat rate is hourly useful heat divided by `run.json.step_hours`.
- The day figure sums the same exported useful-heat columns over that day's 24 nominal local
  hours. The chart stacks those values with `backup_heat_kwh`. No new economic result is computed.
- Example: in the committed run at 2025-01-15 18:00, computing directly supplies 3.20 kWh and
  the regular heater supplies 5.35 kWh of the 8.55 kWh demand. The CSV is the source; the case
  formula and inputs are in `docs/case/challenge.md` and `config/reference-house.json`.

The CHF value is the **modelled avoided heat-pump electricity cost**, calculated by the shared C#
core as used heat × (ElCom 0.3325 CHF/kWh ÷ case COP 2.5). It is not a documented saving on a
homeowner's bill: the commercial contract and who pays for the server electricity are unresolved.
The tank has only a modelled usable energy level, not a measured temperature. The default run
assumes unlimited jobs, and hardware and installation costs are outside its case break-even.
