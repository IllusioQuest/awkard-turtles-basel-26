# T-13: Result datasets and data contract for UIs and paper

- **Tier:** 2 Pitch  ·  **Area:** Data
- **Depends on:** T-05
- **Suggested owner:** Codex  ·  **Owner:** Nikita / Codex  ·  **Status:** in progress

## Goal

One export format that all dashboards and the paper read, so nothing recomputes the model.

## Tasks

- [ ] Define the export: per run a folder with `summary.json`, `hourly.csv` per site, `flows.csv` (job moves between sites), `parameters.json` (all inputs with sources), `run.json` (command, git commit, seed).
- [ ] Write the schema in `docs/model/data-contract.md`.
- [ ] Make `YearRun` (and later the router) write it; commit small showcase runs to `data/derived/` per its rules.

## Done when

- UIs (T-17..T-19) and the explorer can read runs without any model code.

## Read first

- `data/derived/README.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

