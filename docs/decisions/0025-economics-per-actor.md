# 0025: Economics per actor: system surplus, split, levers and win-win grid

- **Status:** proposed (renumbered from 0017, which main took for the always-on catalog)
- **Date:** 2026-11-21
- **Decided by:** team lead / Claude (subagent), ticket T-14; team confirms later

## Context

Business model v2 (T-34): "Swiss sovereign GPU cloud that heats homes". The team lead requires
that the **reference house** becomes profitable for **both** host and DigitalHeat, and that the
model shows honestly under which combination of levers this holds and where not:
A (size for base load), B (premium sovereign AI near Swiss on-demand price), D (refurbished
hardware, profiles from T-35), G (route work instead of dumping heat). Filler is interruptible
spot capacity with a fill rate φ (`docs/research/filler-demand.md`: 0.35, range 0.2–0.6).
Constraints: one model in the C# core (AGENTS §9a), every number in config with a source (§5),
scored case figures unchanged (§5.1), output in the T-13 bundle conventions.

## Decision

1. **Demand enters the existing simulation, not a second model.** Sellable work per server and
   hour is capped at `a = p + φ·(1 − p)` (premium share `p` of offered capacity, served first;
   filler fills φ of the rest), i.e. the deterministic expectation of `F = φ·N` of decision 0014.
   This is a new `DemandLimitedExecutor : IExecutor` (achieved load = min(target, a)) passed to
   the unchanged `YearSimulation.Run` with the unchanged `GreedyHeatScheduler`. Heat used, backup,
   dump and server-hours therefore come from the same hourly tank balance. Premium is served
   first in every hour: premium load = `min(actual, p)`, filler = the rest; premium capacity the
   heat-led schedule leaves unused is reported as unserved (changed after review).
2. **System surplus per year** (pure function, `Economics.Evaluate`):
   `surplus = I_premium + I_filler + V_heat − E − D − W − O` with
   `E` = server kWh × electricity price, `V_heat` = heat used × price of replaced heat
   (heat pump: electricity ÷ 2.5 as in the case; gas: fuel price ÷ boiler efficiency;
   oil: litre price ÷ (kWh per litre × efficiency)), `D` = N × purchase ÷ lifetime,
   `W` = N × purchase × wear share per year (`TODO(source)` until T-35, value 0),
   `O` = N × operations per server-year (hardware profile), installation per site ÷ lifetime
   (`TODO(source)`, value 0; the bundle reports the affordable installation budget; added after
   review). Labelled variants from `creative-ideas.md` (CO₂ credits, own-PV electricity) are off in
   the base case. Unsold hours: server off (idle draw
   of GPUs is `TODO(source)`).
3. **Split.** Operator model: DigitalHeat owns the hardware, pays electricity, operations and
   wear, keeps all compute income, charges the host a heat fee per kWh (default 0 = free heat,
   the market model in `competitors.md` lesson 2). Host = `V_heat − fee·heat`. Host-owned model:
   host pays hardware, wear and electricity and receives `(1 − c)·I`; DigitalHeat keeps
   commission `c·I` and pays operations. Both splits sum to the surplus (tested).
4. **Break-evens side by side:** case break-even (scored formula, electricity − heat-pump heat
   value per server-hour, unchanged in `YearSummary`) and full-cost break-even
   `(E + D + W + O − V_heat) ÷ S` = average income per sold server-hour needed. Two derived
   answers for the levers: **required premium share** (smallest `p` with surplus ≥ 0, and with
   operator-DigitalHeat ≥ 0) and **break-even hardware price factor** (purchase price at which
   surplus = 0, as a share of the profile's price; this is how D is judged until T-35 profiles
   exist, which then plug in via `config/hardware/*.json`).
5. **Lever table + win-win grid** written by `DigitalHeat.YearRun --economics <config> --output
   <dir>`: `run.json` (T-13 provenance), `levers.csv` (profile × server count (A) × premium share
   (B) × policy heat-led/always-on (G) × heat alternative), `required-premium.csv`, `win-win.csv`
   (filler price × electricity price × fill rate, both models), `parameters.json`. The legacy and
   v1 `summary.json` are not touched.
   Parameters: `config/economics/reference-house.json`, every value `{value, unit, source}`.

## Alternatives considered

- Scale heat used linearly with the fill rate after an unlimited run: simpler, but ignores the
  tank and overstates coverage under caps; rejected because the capped executor costs ~20 lines.
- Hourly random fill (Markov busy/idle, filler-demand §7.3): more realistic, needs a seed and
  adds variance; later variant.
- Computing the economics in the dashboard / Python: would be a second model (§9a).

## Consequences

- Economics is reusable for any site: it takes a `YearResult`, a hardware profile and prices.
- Premium demand has no source; `p` is swept and the result is stated as "required premium
  share", not as a forecast. Lever G is shown for one house only (always-on + dump vs heat-led);
  the network value of routed work needs T-10/T-12.
- The new export files are additive to contract v1; `summary.json` is unchanged.

## Review

Fresh-context review (separate Claude session with only the packet, 2026-11-21): **go with
changes.** Applied:

1. The proportional split `S·p/a` did not mean "premium first": now per hour premium =
   `min(actual, p)`, with an `unserved_premium_server_hours` column; the docs say that a premium share
   holds year-round only with always-on (G vs. SLA).
2. Full cost lacked installation: added `InstallationChfPerSite` (`TODO(source)`, 0) amortised over
   the lifetime (no discounting, stated) and an affordable-installation column.
3. Scored figures must not drift: the capped runs write only to the economics bundle; a test checks
   that the uncapped run (φ = 1) reproduces the scored server-hours, heat value and case break-even.
4. Stated that with heat fee 0 the operator win-win reduces to DigitalHeat > 0.

Not adopted: its simpler alternative (grid over filler price × fill rate only, one model), because
the electricity axis and both models were explicitly requested and cost no extra simulation.
