# 0029: Reserve heat-led capacity for Type 1 (B1-reserve, B2-reserve, LP-persist)

- **Status:** accepted for B1-reserve/B2-reserve as a control experiment; LP-persist dropped (see Result)
- **Date:** 2026-11-21
- **Decided by:** Jona (approved the approach in chat), drafted by Claude (T-12)

## Context

T-12 step 3 validation (decision 0025 strategies on `config/network/switzerland-test.json`, Type 1
share 0.3). In a 72-hour July window, B1 missed 27.6 % of Type 1 deadlines, B2 0 % with 1,444 moved
server-hours, and the LP 0 % with **no** moves. Cause: in summer a site's heat-led budget (hot-water
drain plus free tank room) is the scarce resource. B1 spends the free tank room on filler as soon as
the day's Type 1 work is done; when the next cohort arrives at 00:00, the tank is full and only the
drain (≈ 8.6 server-hours/day at a single-family house) is left for ≈ 17.5 server-hours of Type 1.
The LP kept the tank nearly empty only by solver tie-breaking (it knows pending work only), not by
design. So part of B2's benefit is "do not spend scarce summer heat on filler too early", which is a
local scheduling effect, not a network effect. The paper must separate the two.

## Decision

1. **B1-reserve** (local, no moves): as B1-jobs, but filler may not fill the tank beyond a reserve
   kept free for the next Type 1 cohort. Expected next cohort = the total server-hours of the site's
   own cohorts that arrived in the last 24 h (persistence, no scenario knowledge). Expected own
   heat-led capacity during that cohort's deadline window = mean heat demand of the last 24 observed
   hours ÷ recoverable heat per server-hour × deadline hours (persistence; the scheduler remembers the
   last 24 h of demand and of arrived cohorts itself, because completed cohorts leave the pending list). Reserve (kWh) = min(tank capacity, recoverable kWh per
   server-hour × max(0, expected cohort − expected capacity)). Filler this hour is capped so that the
   tank after the hour keeps at least the reserve free. Sites without Type 1 behave exactly like B1-jobs.
   Load stays heat-led and capped at available work; dump stays 0 under heat-led hard.
2. **B2-reserve:** B2's move rule plus the same filler reserve, so the comparison B1 → B1-reserve →
   B2 → B2-reserve isolates "local timing" from "moving work".
3. **LP-persist** (labelled foresight variant, allowed by decision 0025 §4): the rolling-horizon work
   LP additionally plans **expected** cohorts: copies of each origin's latest cohort, arriving every
   24 h inside the window, same size, deadline and eligibility (persistence). They enter stage 2
   (missed work) and the work balance like real cohorts but can only be allocated from their expected
   arrival hour, so they never appear in the current hour's allocations. Lexicographic order unchanged
   (dump, used heat, missed work, server-hours, moves). The plain LP (pending work only) stays as is.
4. No interface change: all three are `INetworkScheduler` implementations; one heat balance
   (`HeatBalance.Step`), one ledger, actuation unchanged (decision 0024/0025). YearRun untouched.

## Alternatives considered

- Fix the LP by a time-weighted filler penalty: ad hoc weight without source; persistence cohorts
  express the actual reason (future Type 1 work) and are explainable.
- Use the scenario's true arrival rate: perfect knowledge of demand; persistence is realistic and
  needs no extra input.

## Consequences

Coverage may drop slightly where a reserve stays unused (heat that could have been stored is not),
so coverage, dump, missed deadlines and moved server-hours are all reported side by side.
Persistence over-reserves after a large cohort and under-reserves before a larger one; this is
reported, not tuned. The reserve depends on daily cohorts (decision 0025 §1); other arrival patterns
would need a new estimate.

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes**. Applied:
1. Capacity estimate uses the mean demand of the last 24 hours over the cohort's deadline window, not
   "this hour × 24" (which flips with the hot-water profile); the wrong "as B2" wording is removed.
2. Information sets are labelled: the greedy strategies use no forecast; the LP uses the heat forecast
   (perfect foresight in scenario runs). Since LP-persist is dropped, no mixed comparison remains.
3. Simpler alternative adopted: only B1-reserve and B2-reserve now; LP-persist not built.
4. Tests: summer case (reserve helps one cohort, then the shortfall returns) and winter case (reserve 0,
   B1-reserve equals B1 hour by hour).

## Result (2026-11-21)

- Toy test: with daily Type 1 work above the hot-water drain, the reserve saves one cohort, then the
  tank is full of Type 1 heat and the shortfall returns. Local timing only delays it.
- Full year on `switzerland-test` (Type 1 share 0.3, φ = 1): **B1-reserve = B1** (83.42 % coverage,
  0 kWh dumped, 15.69 % missed) and **B2-reserve = B2** (0 % missed, 102,754 moved server-hours).
- Conclusion: in this model, B2's gain over B1 is entirely a network effect. The LP's 0 % without moves
  in a 72-hour July window came from the empty starting tank (window artefact), not from planning.
  LP-persist would only improve such transients and is not built.
