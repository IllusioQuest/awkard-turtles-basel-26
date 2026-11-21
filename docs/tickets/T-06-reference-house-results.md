# T-06: Reference-house key figures and assumptions for the draft

- **Tier:** 1 Draft  ·  **Area:** Results / docs
- **Depends on:** T-05
- **Suggested owner:** anyone  ·  **Owner:** Jona / Codex  ·  **Status:** in progress (branch docs/T-06-reference-results)

## Goal

Produce the official key figures for the reference house, with every assumption visible and a clear 'does not pay off when…' statement.

## Tasks

- [ ] Run `YearRun` for the agreed scenario (servers per house decided with the team; show 2–3 server counts).
- [ ] Write `docs/results/reference-house.md`: servers per house, share of demand covered, heat dumped, utilisation, missed deadlines, electricity cost, computing income, break-even CHF/server-hour (case formula) and full-cost variant (hardware from WP2).
- [ ] List all assumptions (link `docs/assumptions.md`, add missing rows) and the conditions under which it does not pay off.
- [ ] Commit the small result files the jury needs (`data/derived/` rules).

## Done when

- One page a juror can read in 2 minutes; every number traceable to a command and a source.

## Read first

- `docs/case/key-figures.md`
- `docs/assumptions.md`
- `data/derived/README.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
