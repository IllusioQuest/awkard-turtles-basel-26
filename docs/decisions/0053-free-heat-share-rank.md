# 0053: Rank eligible sites by free share of their hourly heat budget

- **Status:** proposed (user approved rule and scope, 2026-11-21)
- **Date:** 2026-11-21
- **Decided by:** Ennio (team to accept)

## Context

ADR 0051 ranks eligible sites by remaining useful heat in kWh, descending. In a mixed fleet one large
site wins every placement: in the T-67 Dreispitz run (20 reference houses plus an indoor pool with
627 MWh/a flat demand) the pool's hourly budget is about 107 kWh (71.6 kWh demand + 35 kWh tank) against
at most 45 kWh for a house. So the pool took 75–82 % of all work in every month and the houses
only received overflow. Compute, not heat, was the limit (about 13 busy servers on average, no heat
dumped), so the concentration bought only 4 % more used heat. Absolute ranking also turns into
"first house by ID" whenever sites are identical.

## Decision

Keep heat-first, but rank eligible sites by the **share of their hourly heat budget that is still
free**: `free_share = (budget_kwh - committed_kwh) / budget_kwh`, with
`budget_kwh = HeatCapacity.RemainingKwh(signal)` at the start of the hour. Ties go to more free heat in kWh,
then to the stable site and node ID. This is the Core type `HeatShareRank`. A site with no budget
(≤ ε) is never a candidate. To avoid splitting work into low-load chunks on tiny budgets, the
adapter first takes the best-ranked site that can still run a server at full load (spare load ≥ 1),
and only falls back to partial budgets (≥ the job's MinLoad) if none is left. Heat stays both the order and the hard
cap: a site never gets more than its useful heat, and a site without budget never gets work. With
enough placements, sites fill to an equal share of their heat need, so work is spread in proportion
to heat need (water-filling) instead of going all to the largest site.

This **amends ADR 0051's rank rule** and keeps its "one Core policy": `HeatShareRank` replaces
`HeatPlacementRank`, which is removed. Every caller uses it: the Fleet adapter, Network
(`HeatFirstNetworkScheduler`, `FederatedOffers`) and Orchestration (`WorkloadPlacement`, and through it
the peer mesh and gaming placement). Orchestration computes the site budget from the inventory's heat
signal, without committed heat. The full-load-first rule is Fleet-only: Network allocates continuous
server-hours, and Orchestration only admits a workload if the site's free heat covers the whole
workload's heat. Originally planned as a later migration by the module owner (Nikita); the user chose
to migrate at once to avoid two ranks (2026-11-21).

## Alternatives considered

- **Lowest server utilisation among sites with heat headroom:** spreads compute evenly per server,
  but heat no longer decides the order; further from the original idea.
- **Fleet-only change:** fastest, but the fleet would then rank differently from the live mesh.
- **Fleet now, other callers later:** the first plan; dropped to avoid carrying two ranks.

## Consequences

- Single-site runs (the scored reference house and fleet `reference.json`) are unchanged: there is
  only one candidate.
- Multi-site fleet results change on purpose (policy change, not a bug fix); earlier fleet outputs
  are not rewritten.
- Total used heat can differ through tank timing, chunk granularity, non-interruptible work that
  keeps running after a budget drops, and deadline effects. Check it per run: in T-67 it was equal.
- Live and network placement change too. With two idle sites the larger one still gets the first
  job, but the next one goes to the other site. Tests that asserted the old rule were updated
  to the new rule: placement, indexed-admission oracle, node feedback, peer mesh, gaming heat monitor.
- At the start of an hour, idle sites have share 1.0. Busy sites start lower, because running
  chunks re-claim heat for the new hour.

## Review

Fresh-context review (decision-reviewer): **go with changes**.
- Tiny budgets at share 1.0 could fragment work into low-load chunks. **Applied:** full-load sites
  first, partial budgets as fallback; test `TinyBudgetDoesNotWinOverAFullLoadSite`.
- Two consequences were wrong (share at hour start; sources of used-heat differences). **Applied.**
- State the amendment to 0051, the migration owner and the fleet/live divergence. **Applied.**
- Single-site equality. **Checked:** fleet `reference.json` output files are byte-identical to `main`
  (only `run.json` timing and commit differ). Core YearRun does not use the rank.
- Suggested changing the rank in place instead of a second type. **Applied later in effect:** after the user chose to
  migrate everything at once, `HeatPlacementRank` was removed and all callers use `HeatShareRank`.

Result check (T-67, 2025, both ranks on the same inputs): with the pool, heat used 93,286 kWh, dumped 0,
missed deadlines 95 and income CHF 141,031 are identical. The pool's monthly share of work drops
from 75–82 % to 10–13 %. Houses get 4,818–5,338 kWh of electricity each instead of 1,085–1,629.
Mean chunk load while busy stays 1.0.
