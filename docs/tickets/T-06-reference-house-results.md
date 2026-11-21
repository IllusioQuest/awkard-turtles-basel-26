# T-06: Reference-house key figures and assumptions for the draft

- **Tier:** 1 Draft  ·  **Area:** Results / docs
- **Depends on:** T-05
- **Suggested owner:** anyone  ·  **Owner:** Jona / Codex  ·  **Status:** in review (final price and hardware pass)

## Goal

Produce the official key figures for the reference house, with every assumption visible and a clear 'does not pay off when…' statement.

## Tasks

- [x] Run `YearRun` for 3, 4 and 5 server comparisons plus 1 and 13 as bounds; use 4 as the draft presentation baseline after user delegation, without claiming an optimized installation.
- [x] Write `docs/results/reference-house.md` with all currently computable key figures, an explicitly undefined missed-deadline rate, a hardware-only sensitivity and the full-cost formula with missing inputs.
- [x] List assumptions in `docs/assumptions.md` and the conditions under which the model does not pay off.
- [x] Generate small result JSON files through `scripts/export_reference_house.py` under `data/derived/`.
- [x] Re-run the C# model at the adopted CHF 0.66/server-hour price; normalize the user-approved Dell illustration to estimated 1 kW, and export a separate sourced finite-only deadline diagnostic.

## Done when

- One page a juror can read in 2 minutes; every number traceable to a command and a source.

## Read first

- `docs/case/key-figures.md`
- `docs/assumptions.md`
- `data/derived/README.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Integration status

The first result page and reproducible files were merged through PR #8. The user delegated the
draft presentation choice; 4 servers is the baseline because both C# and the year explorer
reproduce it. This branch updates the adopted CHF 0.66/server-hour income and corrects the
illustrative Dell cost to equivalent 1 kW power using the T-15 estimate. T-26 has a sourced
finite-only transcription scenario, but the agreed premium-plus-filler result needs T-31's mixed
queue. All 40 Core tests pass and the six JSON results reproduce byte for byte. Keep the
unlimited-work assumption explicit until the mixed result exists. The branch changes an input
and a partial-cost illustration, not the scored case formula or physical heat model.
