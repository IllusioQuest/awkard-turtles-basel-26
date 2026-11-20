# Awkward Turtles · Basel GreenTech 2026

**Case: Useful heat from computing (DigitalHeat).**
Can computing power heat a home while doing useful work? We build a demonstrator that schedules
interruptible computing jobs on distributed servers so that their heat arrives where and when homes
need it, over a full year, with transparent and reproducible numbers.

> Status: C# .NET 8 core proposed for the reference-house draft; see
> [decision 0004](docs/decisions/0004-csharp-core.md). The team has not accepted
> the broader architecture yet.

## Start here

- [Case description (verbatim)](docs/case/challenge.md)
- [Key figures and must-have checklist](docs/case/key-figures.md)
- [External references and data sources](docs/case/references.md)
- [Deep-dive notes with the case owner](docs/deep-dive/notes-structured.md) ([raw](docs/deep-dive/notes-verbatim.md))
- [Data and how to fetch it](data/README.md) · [Assumptions](docs/assumptions.md) · [Decisions](docs/decisions/README.md)
- [Instructions for AI agents](AGENTS.md)

## Quick checks

```bash
python3 scripts/fetch_data.py --check   # validate the data against the case's expected values
scripts/deadline-reminder.sh            # time left until our deadlines
```

## C# reference-house core (proposed)

Requires the .NET 8 SDK. From the repository root:

```bash
dotnet restore src/DigitalHeat.sln
dotnet build src/DigitalHeat.sln --no-restore
dotnet test src/DigitalHeat.sln --no-build
dotnet run --project src/DigitalHeat.YearRun -- .
```

The last command reads the committed Basel 2025 hourly weather CSV, checks the
configured electricity price against the ElCom 2026 Basel H4 snapshot, and writes
`results/reference-house/hourly.csv` and `summary.json`. Pass another repository
root as the final argument when running from elsewhere. Results are generated and
git-ignored; rerunning the command reproduces them. The parameter file is
[`config/reference-house.json`](config/reference-house.json): each fixed/scenario
value has a unit and source. Four servers, zero minimum load and the example
computing price are scenario settings from the year explorer, not case mandates or
market-backed income. Change the scenario in JSON, keeping the reference-house
case values fixed for scored comparisons.

`DigitalHeat.Core` exposes `IScheduler.Decide(HeatSignal)`, `IExecutor` and
`SchedulerController.Apply`. `DigitalHeat.YearRun` supplies the simulated executor;
the real C# job adapter should call `SchedulerController.Apply` with the same
scheduler and its own `IExecutor`. The output marks missed
deadlines as `not modelled yet` and assumes immediately available computing work.
Electricity, used-heat value and case break-even exclude hardware and operating
costs, so the result is not a full-cost profitability claim.

The calculation uses the local day's mean temperature to decide whether the case
heating formula applies, then adds 12 kWh/day of hot water evenly across 24 hours.
Each hour, the scheduler requests enough server heat to meet demand and fill remaining
tank space, limited by server count and minimum load. The balance records heat sent
directly to the house, charged to/discharged from the lossless tank, backed up and
dumped. Electricity cost is server kWh × the ElCom price; used-heat value is heat
actually used × that price ÷ 2.5; case break-even is their difference divided by
server-hours sold. For one full-load 1 kW server-hour whose 0.8 kWh recoverable heat
is all used, electricity costs CHF 0.3325, heat is worth CHF 0.1064, and the case
break-even is CHF 0.2261 per server-hour. These inputs come from the case and the
ElCom snapshot, with exact source labels in the JSON file.

