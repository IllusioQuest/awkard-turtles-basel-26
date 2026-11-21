# T-08: Year explorer: fixes from the review

- **Tier:** 2 Pitch  ·  **Area:** Prototype
- **Depends on:** –
- **Suggested owner:** Claude (team lead's session)  ·  **Owner:** Codex  ·  **Status:** in progress

## Goal

Make the browser explorer honest enough to show: label example inputs, include hardware in the verdict, run offline.

## Tasks

- [x] Label the surplus KPI "at example price, without hardware" (or hide it until sourced prices are wired in).
- [x] Add a full-cost break-even (incl. hardware) and use it in the verdict sentence.
- [x] Inline the data and use system fonts so it runs from a file without internet; add the METHOD link.
- [x] Describe `verify.py` as "same rule, reimplemented" and add invariant checks.
- [ ] Later: read the numbers from the C# `YearRun` output instead of its own model (one model only).

## Done when

- Review findings 1, 2, 3, 6 resolved; explorer still matches the C# core.

## Read first

- `prototypes/year-explorer/METHOD.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Waiting for the team lead's go.
