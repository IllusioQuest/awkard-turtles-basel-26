# T-25: Animated example house (pitch visual)

- **Tier:** 2 Pitch  ·  **Area:** UI / presentation
- **Depends on:** T-04 (LiveRun), T-13 (export format)
- **Suggested owner:** anyone (Lovable possible)  ·  **Owner:**  ·  **Status:** open

## Goal

The heart of the pitch (scene "Inside the example house" in `docs/pitch/storyboard.md`): a cutaway of the
reference house that shows servers, tank, radiators and hot water hour by hour, in sync with the scheduler.

## Tasks

- [ ] Agree the look with the team lead first (sketch or screenshot, AGENTS.md §10).
- [ ] Cutaway with: server rack (load 0–100 %), tank level, heat to radiators, hot water, backup heater, dumped heat.
- [ ] Play 15 January and 15 July from exported runs (T-13) or a LiveRun log; optional live mode fed by LiveRun (T-04).
- [ ] Visible moment when the tank is full and the real job freezes, then resumes.
- [ ] Offline, no second model: only reads exported data.

## Done when

- The two days play smoothly offline and a homeowner understands the picture without explanation.

## Read first

- `docs/pitch/storyboard.md`
- `docs/tickets/T-13-results-datasets-contract.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
