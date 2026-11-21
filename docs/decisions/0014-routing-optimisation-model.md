# 0014: Routing optimisation model (rolling-horizon LP on a time-expanded network)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** team lead / Claude (subagent draft); team review pending

## Context

T-11 asks for a precise statement of the routing problem (which work runs on which site in which
hour, at which load) so that baselines and the optimiser in T-12 implement the same thing.
Constraints: the reference house's scored figures must not change (AGENTS.md §5.1); the same
scheduler path must drive the simulation and the real job (§5.3); data locality from T-24 /
decision 0013 must be a hard rule; the existing interfaces (`IScheduler`, `IExecutor`,
`SiteHeatState` from 0008, the job queue from 0010) should be reused. The adopted filler price
(CHF 0.66 per server-hour, `docs/research/computing-price.md`) is above the electricity cost of a
server-hour (CHF 0.3325).

## Decision

Adopt the model in [`docs/model/routing-optimisation.md`](../model/routing-optimisation.md):

1. **Objective:** maximise system surplus per planning window = premium and filler income + value
   of heat used (heating cost replaced per site: heat pump `e ÷ COP`, gas or oil `price ÷
   efficiency`) − electricity − dump penalty − missed-deadline penalty (+ optional terminal tank
   value). Hardware and operations stay outside the hourly objective (T-14).
2. **Formulation:** continuous LP on a time-expanded network in path form: variables per (cohort,
   eligible site, hour), filler and total load per site-hour, heat used, dumped and stored per
   site-hour. Eligibility (0013), latency limits, transfer delay and deadlines are enforced by the
   variable domain; tank, capacity, demand and work balance are linear constraints; deadlines are
   soft (penalised) so every solve is feasible.
3. **Modes:** economic (`π^dump = ε`, tie-break only), heat-led soft (dump penalty above the best
   margin incl. deadline penalties, `(max_k (p_k + π_k) − e·P) ÷ a`), heat-led hard (no dumping, even
   to meet a deadline), case-ranking (lexicographic). The mode is a parameter; the team picks the
   one used in the pitch.
4. **Procedure:** rolling horizon (`H = 48 h`, commit `R = 1 h` by default), realised outcomes from
   the existing heat-balance code, every result labelled as perfect foresight or forecast error;
   a full-year perfect-foresight LP is an upper bound only.
5. **Baselines:** always on (B0), local greedy (B1, the existing scheduler, which is the one-site,
   one-hour special case of the LP), network greedy (B2).
6. **MILP** only for listed extensions (on/off servers with minimum part load, idle power, start-up,
   indivisible or non-preemptive jobs, all-or-nothing payment, fixed transfer costs).
7. **Interfaces:** reuse `SiteHeatState`, `JobPlacement.IsEligible` and the per-site
   `SchedulerController → IExecutor` path; add a network-level scheduler
   (`INetworkScheduler.Decide(NetworkState) → NetworkDecision`) and an allocation-aware queue. The
   exact interface and the solver library get their own decision record in T-12.

## Alternatives considered

- **MILP from the start** (on/off servers, whole jobs): closer to reality, but NP-hard, slower and
  harder to explain; the planned services split into small work units, so the LP captures the main
  effect. MILP stays a documented extension.
- **Heuristics only** (network greedy): simpler and may suffice for small networks, but gives no
  optimality reference and no clean statement for the paper; kept as baseline B2.
- **Arc-based network flow** (explicit wait/move variables): equivalent while links have no
  capacity; the path form is smaller and easier to read. Switch to arcs when link bandwidth is
  modelled.
- **Minimise cost only, ignore income classes:** cannot distinguish premium from filler, which is
  the business case under consideration.

## Consequences

- At the adopted filler price the **economic optimum is always-on with dumping**; heat-led routing
  only matters with a dump penalty / no-dump rule or with filler priced below 0.3325 CHF per
  server-hour. This must be stated openly (transparency) and needs a team choice.
- LP results depend on what the solver knows; the worked example shows the whole gain of the LP
  over network greedy can come from perfect foresight of arrivals. Labels are mandatory.
- New parameters (heating type, fuel price, boiler efficiency, filler availability, class prices,
  penalties, link delays) need sources; gas/oil boiler efficiency and oil calorific value are
  `TODO(source)`.
- Implementers must not create a second heat model: the hourly heat balance is factored into one
  shared function.
- Scored reference-house figures are unaffected (they remain B1).

## Review

Fresh-context review (2026-11-21, separate non-interactive Claude call with read-only tools and
only the packet: this record, the model document, `Scheduling.cs`, `YearSimulation.cs`,
`SiteHeatState.cs`, `computing-price.md`). Verdict: **go with changes**. The reviewer re-checked
the worked example, the thresholds 0.2261 / 0.409 / 0.31 and the match with the greedy formula and
heat-balance order in the code. Applied all points:

1. The soft heat-led threshold now includes deadline penalties, `(max_k (p_k + π_k) − e·P) ÷ a`,
   and the hard mode is stated as "never dump, even to meet a deadline" (0010 wait policy).
2. Definition gaps closed: `W_c` is the work still left at `τ`; capacity written with
   `Δt = HoursPerStep`; worked example states `μ = 0`; economic mode uses a tiny `ε` dump penalty
   to break the dump-vs-store tie.
3. §6.4 weakened: the realised balance reproduces the LP's total heat use, not its hourly `h`/`z`;
   regression tests compare per-hour loads and totals.

Simpler alternative from the review (adopted as a fallback, not a change): if time runs short,
B2 plus a perfect-foresight LP is enough for the pitch; the rolling horizon can follow.
