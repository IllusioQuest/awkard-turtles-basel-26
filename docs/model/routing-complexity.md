# Routing: complexity, formulation and effort (first assessment)

> Status: first assessment by Claude, 2026-11-21, discussed with the team lead. Input for
> [T-11](../tickets/T-11-routing-optimisation-model.md) and [T-12](../tickets/T-12-routing-algorithms.md).
> Not a decision yet; the formal model in T-11 decides.

## Is the routing problem hard?

**In general, yes.** With many sites, hours and jobs, heat storage that carries state over time,
deadlines, jobs that cannot be split, servers that are either fully on or off, and uncertain
forecasts, the problem is a variant of scheduling on parallel machines with inventory and
deadlines. That is **NP-hard**: for large networks, an exact solution is not practical.

**But it can be formulated so that it stays tractable.** The services in the current plan
(transcription, document AI; see `docs/research/ideas.md`) split into many small work
units (audio segments, pages). If jobs may be split and servers can run at part load, the core
becomes a **flow in a time-expanded network**, which is a **linear programme (LP)** and solvable
in polynomial time.

## Time-expanded network (sketch)

- **Nodes:** (site, hour).
- **Edges:**
  - *compute here:* work processed at a site in an hour; cost = electricity − value of the heat
    actually used (heat pump, oil or gas replaced, per site); penalty if the heat is dumped
  - *wait:* work carried to the next hour at the same site, allowed only until its deadline
  - *move:* work sent to a neighbouring site, with latency and only where data rules allow it
  - *store heat:* heat carried to the next hour in the tank, up to its capacity
- **Constraints:** heat demand per site (incl. forecast), tank limits, server capacity,
  deadlines, data eligibility.
- **Objective:** minimise cost (or maximise system surplus) including penalties for dumped heat and
  missed deadlines.

The flows of this network are also the **arrows on the map** in the pitch (work moving between
houses, hotels, pools).

## Size and solver

Rolling horizon of 48 h, 50 sites, 3 job types: about 7,000 variables per solve. An LP solver such
as Google OR-Tools (GLOP, available for .NET) solves this in milliseconds to seconds; a full year is
365 re-solves. Solving is not the bottleneck.

## Where the time goes

Modelling and validation, not solving: the right objective, plausible results, explaining them.
Rough estimate:

| Step | Ticket | Effort |
|---|---|---|
| Formal model + small worked example | T-11 | 2–3 h |
| Baselines: always on, local greedy, network greedy | T-12 | 2–3 h |
| Rolling-horizon LP (OR-Tools in C#) | T-12 | 4–6 h |
| Evaluation and comparison | T-12 / T-13 | ≈ 2 h |

**About 10–14 h in total, so after the draft submission.** For the draft (2026-11-21 15:00), the
existing local greedy scheduler is enough.

## For the paper

- Main contribution: the time-expanded network / LP formulation of heat-led computing across
  sites, compared with simple baselines on the case's metrics (coverage, dump, utilisation,
  missed deadlines, surplus).
- Extensions that bring back the hard parts: integer on/off servers and minimum run times
  (MILP), non-splittable jobs, data-transfer delays, forecast errors (robust / stochastic
  variants), heuristics for very large networks.
