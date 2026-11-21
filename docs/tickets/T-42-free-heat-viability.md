# T-42: Free-heat viability from paid demand and realised revenue

- **Tier:** 2 Pitch · **Area:** Business model / evidence
- **Depends on:** T-14, T-31, T-35, T-37
- **Owner:** Jona / Codex · **Status:** in review

## Goal

Decide whether DigitalHeat can offer free heat to the reference-house host while paying for
hardware, operations and server electricity. Find the paid Type 1/Type 2 workload and realised
price that would make the operator model viable; distinguish this threshold from observed sales.

## Tasks

- [x] Have independent subagents audit demand, achievable revenue and full costs.
- [x] Use the T-14 actor split and year results; do not implement another economics model.
- [x] Publish a short, reproducible evidence and threshold note with source dates, one worked
      example, a decision for T-18, and the missing customer/measurement evidence.
- [x] Coordinate findings with T-14 and T-31 in the PR or shared status.

## Done when

A reader can tell what DigitalHeat would need to sell for free heat to work, what is actually
documented, and whether the homeowner page may claim a realised CHF saving. No scored case figure,
interface or business-model ownership decision changes in this ticket.

## Rules

Use `docs/business-model.md` and decision 0020 for the operator case; T-14 owns the economics code.
All numbers need a calculation path and source. Prices and possible demand are not evidence of
paid orders. Follow AGENTS.md §§5, 9a and 10.

First version: [`docs/research/free-heat-viability.md`](../research/free-heat-viability.md).
User-requested follow-up: [candidate profitable private-home path](../research/profitable-home-path.md),
covering case interpretation, a physically sized base-load node and a paid-capacity contract boundary.
Keep this ticket in review until the team accepts the business conclusion; T-14 remains in PR #27.
