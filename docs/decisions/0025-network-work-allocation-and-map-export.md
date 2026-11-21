# 0025: Network work allocation, moves between sites, and the map export (T-12 steps 2–3)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona (IllusioQuest) / Claude; team confirmation pending. Extends decision 0024. Its
  review asked for a separate record before work moves between sites.

## Context

Step 1 (decision 0024) showed that on one house the greedy rule is already optimal. The value of a
network scheduler shows only with deadlines and several sites:

- In summer a house has little heat-led capacity, about 0.6 servers for hot water (case).
- Type 1 jobs with deadlines (decision 0020) then wait and miss their deadlines.
- Sites with year-round heat demand (hotel/spa, indoor pool; `docs/research/sites-and-profiles.md` §B)
  could run that work instead.

The team's target is a live map of Switzerland with heat maps per site and work flowing between
sites. The existing finite queue (decision 0010, `JobQueueExecutor`) is single-site and has no
filler, so it cannot express this. It stays unchanged for the scored YearRun path.

## Decision

1. **Work model in `DigitalHeat.Network`** (no change to Core's queue):
   - **Type 1 cohorts:** the jobs of one type that arrive at one origin site on one day (at local
     00:00, as in decision 0010). Each cohort has a due hour (exclusive), remaining server-hours,
     a job count and a placement policy. The placement is checked with `JobPlacement.IsEligible`
     (decision 0013) against each site's id and country **before** any strategy may use that site.
   - **Type 2 filler:** research simulation (decision 0020), unlimited by default (case-permitted),
     no deadline. An optional availability factor φ per site-hour (sensitivity 0.35,
     `demand.md`) caps it.
2. **Decision extension:** `NetworkDecision` gains allocations (cohort, site, server-hours this hour)
   and filler server-hours per site. A site's target load = (allocated Type 1 + filler) ÷ servers.
   **The actuation path is unchanged** (`SchedulerController.Apply` → `IExecutor`, decision 0024).
   `IExecutor` receives one load per site; it does not know cohorts. All attribution happens in
   **one network-side ledger** with the same structure as `JobQueueExecutor.ApplyTargetLoad`: cap
   the target at the work available, apply it, then attribute the achieved load in a fixed order.
   Type 1 goes in earliest-deadline order and filler last, so a shortfall hits filler first, then
   the Type 1 work with the latest deadline. A live executor is **"load only"**: it controls one
   container's load and does not choose which job pauses.
3. **Moves:** running a cohort's work at a site other than its origin is a **move**, recorded as a
   flow (hour, origin, destination, type, server-hours). **Moves are simulated flows only.** No
   executor transfers data or containers, and the live demo stays one site. An executor hook for
   real moves would need its own record. Step 2 has no transfer delay or cost; both are
   `TODO(source)` and get parameters later (decision 0014 §2.3).
4. **Strategies** (all heat-led hard by default, decision 0024 §6):
   - **B1-jobs:** local greedy load. Own cohorts run first in earliest-deadline-first order, then filler.
   - **B2 network greedy:** each site's load is still the local greedy. Type 1 cohorts in EDF order
     go to the eligible site with the most free heat-led capacity; ties go to the origin.
   - **LP:** rolling horizon over sites × hours with cohort variables. Lexicographic stages:
     (0) minimise dump, (1) maximise used heat, (2) minimise missed Type 1 server-hours,
     (3) maximise server-hours, (4) **minimise moved server-hours**. Stage 4 keeps work at its
     origin unless a move helps, so the map shows no arbitrary flows. This is the case's ranking,
     with deadlines after coverage. The LP knows **pending (arrived) work only**; future arrivals
     are not foreseen. An optional foresight variant must be labelled.
   - **Soft mode** (a parameter) swaps stages so that deadlines can beat dumping.
5. **Metrics:** per site and network: coverage, dumped heat, utilisation, Type 1 / Type 2
   server-hours, moved server-hours, **missed deadlines in % of jobs due**. Remaining work is
   dropped at the due hour (as `Expire` in Core). Within a cohort, missed jobs = ⌈(missed
   server-hours − 10⁻⁶) ÷ server-hours per job⌉, floored at 0, so LP residuals count no phantom misses.
   If a cohort's work is split across k sites, this is a lower bound, by at most k − 1 jobs; we say
   so. Regression test: on one site, B1-jobs gives the same missed count as `JobQueueExecutor`.
6. **Type 1 volume is a labelled scenario**, because no sourced arrival rate exists (decision 0020).
   The work unit is the measured LLM document (T-33: 0.92 Wh, about 1,090 documents per 1 kW
   server-hour). Volume is set as a share of the origin's yearly heat-led server-hours (sweep 10 %,
   30 %, 50 %), with deadline 24 h.
7. **Map export contract** (`results/network/<run>/`, one directory per strategy run, generated,
   git-ignored). Units are in the field names, and the time axis is local Europe/Zurich hours as in
   the data:
   - `run.json`: git commit, strategy, forecast label and σ/ρ/seed, φ, Type 1 share, horizon,
     SHA-256 of every input file.
   - `sites.json`: id, name, kind, latitude, longitude, canton, servers, tank, and per field
     `{value, source}` or `synthetic: true`, so the map can mark synthetic values.
   - `site-hours.json`: per site, compact arrays over the hours (`demandKwh`, `usedKwh`, `dumpedKwh`,
     `backupKwh`, `tankKwh`, `loadShare`, `type1ServerHours`, `type2ServerHours`,
     `missedServerHours`), rounded to 3 decimals.
   - `flows.json`: `[hour, fromSiteId, toSiteId, type, serverHours]`.
   - `summary.json`: metrics per strategy.

   The time axis is shared and given once. A daily aggregate file is added if the hourly one gets too
   large for a browser.
8. **Swiss test dataset** (`config/network/…`): **every number has a source or is labelled
   synthetic.** Site locations, kinds and counts are generated reproducibly (fixed seed) from
   sourced distributions where they exist (WP3 site types, When2Heat CH profiles, Open-Meteo weather
   per location). Otherwise they are labelled "synthetic test data". The reference house stays the
   scored instance.

**Build order:** B1-jobs and B2 with the export first (they already show the summer effect on the
map), then the multi-site LP.

## Alternatives considered

- **Extend `JobQueueExecutor` in Core:** this touches the scored path and decision 0010 for a
  network feature.
- **Moves with delay and cost from the start:** no sourced values, so it waits for a parameter source.

## Consequences

- Expected: on one house, B1-jobs ≈ LP. With a year-round site in the network, B2 and the LP cut
  summer deadline misses at zero dump. The LP should beat B2 when capacity is tight around deadlines.
- Type 1 volume, transfer costs and site sizes are scenarios, not findings, and must be labelled so
  on the map.

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes.** Applied:

1. Moves and shortfall are bookkeeping, not actuation. This is now stated, with one network-side
   ledger shaped like `JobQueueExecutor` and live executors as "load only". The live demo stays one
   site, and real moves need their own record.
2. LP stage 4 minimises moved server-hours (no arbitrary flows on the map).
3. The missed-deadline metric gets an epsilon, the k-site lower-bound note, drop at the due hour,
   a regression test against `JobQueueExecutor`, and "pending work only" in the LP.
4. Export: `run.json` with commit, parameters and input hashes; one directory per strategy;
   `missedServerHours`; per-field synthetic markers; units and timezone.
5. Build order follows its simpler alternative (B1-jobs, B2, export first; LP after).
