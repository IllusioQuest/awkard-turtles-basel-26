# 0062: Size servers per house by marginal value

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Ennio / Claude (T-70); team to accept

## Context

Servers per house were a fixed comparison value (4) or `share_of_peak` (peak demand only). Neither
uses the year's heat profile or the hardware cost, so neither says whether one more server pays.
The question "how many servers per household?" needs a rule that is traceable, uses the shared
heat model and stays honest about when nothing pays.

## Decision

Add Core `ServerSizing`: run the unchanged `YearSimulation` with `HeatFirstScheduler` (minimum load 0,
unlimited pausable work) for n = 0, 1, … until 100% coverage, and choose the n that maximises
`H(n)·price + used_heat_value − electricity − idle_electricity − n·(purchase/lifetime + ops)`. Expose it
only as the reference-house report `YearRun --sizing` (pitch scope, user decision 2026-11-21). No Fleet
or interface change; a per-house Fleet policy was built, then dropped from scope. Scored
reference-house figures are not changed.

## Alternatives considered

- Closed-form load-duration estimate without the tank: faster, but a second heat model (AGENTS §9a).
- Jointly optimising servers inside the fleet run (sold hours from real jobs): right long-term, but
  needs many full event runs per decision; too heavy now. Kept as the next step.
- Maximise utilisation or coverage: ignores the cost of a server, so either too few or too many.

## Consequences

- The reference-house answer reacts to hardware cost, lifetime and price (assumptions table in the result page).
- It overstates sold hours wherever job demand, not heat, is the limit. The result page says so.
- At the adopted CHF 0.66/h the optimum is 0 for every profile. That is a finding, not a bug.
- Cost: up to 14 year runs (< 1 s).

## Review

Fresh-context review (decision-reviewer, 2026-11-21): **accept with changes**. Arithmetic, the lack of
double counting, greedy optimality and the default-method safety were confirmed. Applied:
(1) break-even hours now include idle draw and use one electricity price, and the result table labels
the idle draw of every profile; (2) the headline bullets now say "upper bound"; (3) the Fleet-policy
points (profile/run mismatch check, calendar window) became moot when the Fleet policy left scope; the
report keeps its own 1 kW / 0.8 check.
