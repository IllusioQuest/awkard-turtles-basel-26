# T-11: Routing as an optimisation problem: formal model

- **Tier:** 2 Pitch  ·  **Area:** Model / paper
- **Depends on:** T-09, T-10
- **Suggested owner:** team + Claude (needs discussion)  ·  **Owner:** Team lead / Claude (subagent)  ·  **Status:** in progress

## Goal

Write down the 'highly complex optimisation problem' from the whiteboard precisely, so baselines and solvers implement the same thing.

## Tasks

- [ ] Decision variables: which job (part) runs on which server/site in which hour, at which load.
- [ ] Objective: minimise electricity cost − value of heat that replaces heating cost (oil / gas / heat pump, per site) + penalties for dumped heat and missed deadlines; or maximise system surplus.
- [ ] Constraints: heat demand per site incl. forecast, tank limits, server capacity / utilisation, delay and distance vs. deadline, data eligibility, splittability of jobs (work units).
- [ ] Choose solution approaches: greedy baselines, rolling-horizon optimisation (LP/MILP, e.g. OR-Tools / Google.OrTools for .NET), maybe a heuristic for large networks.
- [ ] Write `docs/model/routing-optimisation.md` (math + plain words + small worked example) and a decision record; run the fresh-context review.

## Done when

- Model document agreed by the team; ADR reviewed; implementers of T-12 can start without guessing.

## Read first

- `docs/model/routing-complexity.md` (complexity, LP formulation, effort estimate)
- whiteboard photo (2026-11-21)
- `docs/vision.md (building block 4)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Core of the paper. Agree the approach with the team before writing code.
