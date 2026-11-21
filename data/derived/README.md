# Derived data

Files here are produced by scripts from `data/raw/` and are never edited by hand.

For every derived file, document below: the producing command, its inputs, and whether it is committed.
Commit a derived file when the demo, the dashboard or the jury needs to see it without running
anything (e.g. the yearly results of the reference house). Otherwise it is git-ignored and rebuilt.

| File | Produced by | Inputs | Committed? |
|---|---|---|---|
| `reference-house/*.json` (five server-count comparisons) | `python scripts/export_reference_house.py` (.NET 8 required) | Basel 2025 hourly weather, ElCom 2026 snapshot, `config/reference-house.json`, `config/reference-house-results.json`, `config/job-scenarios/unlimited.json`, WP2 `data/raw/market/costs.csv`; script invokes the shared C# `DigitalHeat.YearRun` | yes; compact draft/jury figures |
| `computing-price.json` | `python3 scripts/fetch_data.py --prices` (new snapshot) or `python3 scripts/fetch_prices.py --snapshot <file>` (pinned, offline) | latest snapshot in `data/raw/market/price-snapshots/` (path + SHA-256 recorded in the file), `config/computing-price-method.json`, `ServerElectricKw` from `config/reference-house.json`, RunPod rows of `data/raw/market/compute-prices.csv` | yes; derived market-tracking price (C1) and A1/A2, **not** the adopted scored value (that stays in `config/reference-house.json`, T-06) |
| `profitability/summary.json` | `python3 scripts/profitability_inputs.py` | `data/raw/market/*.csv` (T-37 rows), `config/hardware/*.json`, `reference-house/explorer-default.json` | yes; tables of `docs/research/profitability-inputs.md` |
| `economics-reference-house/` (`levers.csv`, `required-premium.csv`, `win-win.csv`, `parameters.json`, `run.json`) | `dotnet run --project src/DigitalHeat.YearRun -- . --economics config/economics/reference-house.json --output <empty dir>` | Basel 2025 weather, ElCom 2026, `config/reference-house.json`, `config/economics/reference-house.json`, `config/hardware/*.json` (hashes in `run.json`) | yes; T-14 economics variants (`docs/model/economics.md`), not scored figures |
