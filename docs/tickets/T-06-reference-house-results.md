# T-06: Reference-house key figures and assumptions for the draft

- **Tier:** 1 Draft  ·  **Area:** Results / docs
- **Depends on:** T-05
- **Suggested owner:** anyone  ·  **Owner:** Jona / Codex  ·  **Status:** in progress (scenario and job inputs pending)

## Goal

Produce the official key figures for the reference house, with every assumption visible and a clear 'does not pay off when…' statement.

## Tasks

- [x] Run `YearRun` for three sourced comparison counts; the team's main server count is still pending.
- [x] Write `docs/results/reference-house.md` with all currently computable key figures, an explicitly undefined missed-deadline rate, a hardware-only sensitivity and the full-cost formula with missing inputs.
- [x] List assumptions in `docs/assumptions.md` and the conditions under which the model does not pay off.
- [x] Generate small result JSON files through `scripts/export_reference_house.py` under `data/derived/`.

## Done when

- One page a juror can read in 2 minutes; every number traceable to a command and a source.

## Read first

- `docs/case/key-figures.md`
- `docs/assumptions.md`
- `data/derived/README.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Integration status

The result page and reproducible files are ready on `docs/T-06-reference-results`. The official
single-scenario headline and a finite-job missed-deadline percentage await the team server-count
decision and sourced T-01/T-02 inputs. The WP2 hardware anchor is an illustrative sensitivity,
not a full-cost installation quote. Keep this ticket in progress until those gaps are resolved.
