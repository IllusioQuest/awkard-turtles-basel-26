# Vision (draft)

> **Status: draft**, written from a discussion with one team member on 2026-11-20. To be confirmed
> by the whole team; then record it as a decision. Agents: treat this as the current direction, but
> do not present it as final.

## Guiding question

**Under which conditions does it pay to move computing load to where heat is needed, with which
services and which scheduling, for the host of the servers *and* for DigitalHeat?**

The case owner wants a working business idea, not only a demonstrator. Our answer must show
where the model works, for whom, and where it does not.

## Residential houses must pay off (meta-insight, 2026-11-21)

DigitalHeat has not worked with single-family houses yet, but chose a simple residential house as
the case reference and speaks of homes throughout. Competitors failed or retreated exactly there
(`research/competitors.md`). Our task is therefore to **make residential houses profitable**, not to
move away from them. Business model v2 (ticket T-34): sovereign Swiss GPU cloud that heats homes,
sized for year-round heat, refurbished hardware, compute-heavy premium AI, routing instead of dumping.

## Who must win

| Actor | Example | Wins when ... |
|---|---|---|
| **Host** (has the servers in the building) | homeowner (main example); also hotel/spa, farm, office, shop | heat costs less than the alternative (heat pump), after their share of costs |
| **DigitalHeat** (operator / platform) | the case owner | computing income + heat sales cover hardware, operations and electricity, with a margin |
| **Compute customer** (buys the computing) | law firm, municipality, company with sensitive data | price ≤ cloud alternative, or a premium is justified by data sovereignty / locality |

A result is only "it pays off" if host **and** DigitalHeat are both positive. The central visual
is a **win-win zone**: the range of electricity and computing prices in which both win.

**System surplus first, split second.** Money flows between actors only divide the total; if the
total is negative, no ownership model makes everyone win. So the economic model first computes the
**system surplus per year** = computing income + value of heat actually used − electricity −
hardware depreciation − operations, and only then splits it with ownership parameters (who pays
electricity, heat fee, commission). Things that must be visible in that calculation:

- Low summer utilisation (12.5 servers needed in the coldest hour, 0.6 in summer) makes hardware
  expensive per server-hour sold.
- The backup heater is still needed, so the host saves running costs of heat, not necessarily the
  investment in a heat pump.
- Computing income needs a sourced job demand (or the explicit statement "unlimited jobs").
- Show the **case break-even** (scored: electricity cost only) and **our full-cost break-even**
  side by side, clearly labelled.

## Business model (provisionally fixed, 2026-11-21)

Operator model, two job types: Type 1 = batch LLM service on non-personal documents, Type 2 =
research simulations. Transcription and webhosting are out of the business model. See
[`business-model.md`](business-model.md) and decision
[0020](decisions/0020-business-model-operator-two-job-types.md). The sections below are background.

## Ownership models (both are allowed, as a parameter)

- **Operator model:** DigitalHeat owns and operates the servers, sells the computing and gives the
  host cheap or free heat (possibly for a fee per kWh of heat).
- **Host-owned model:** the host buys the servers, DigitalHeat sells the computing through its
  platform and keeps a commission.
- Hybrids (leasing, revenue share) are combinations of the same money flows.

The economic model therefore tracks **money flows per actor**; an ownership model only decides
who pays which cost and who receives which income.

## Building blocks

The three layers the case owner suggested, plus what we add:

1. **Market analysis (marketing layer).** Which services earn money? Each service is rated on
   price **and** on how well it fits heat production: interruptible?, deadline flexibility, latency
   needs, data sensitivity (sovereignty premium), resulting income per server-hour. Result: a
   **service × heat-fit matrix**.
2. **Economic model (financial layer).** Money flows per actor over time, break-even, sensitivity
   to prices, ownership models, win-win zone.
3. **System model (physical layer).** Sites as **parameter lists** (location, heat demand model or
   time series, hot water with daily shape, storage, space, allowed jobs), servers, storage, jobs,
   network between sites (latency, eligibility, data rules).
4. **Scheduling algorithms.** Distributed load balancing under heat constraints, always compared
   with simple baselines:
   - baseline 1: servers always on
   - baseline 2: greedy heat-following, hour by hour
   - ours: forecast-based (weather 24–72 h ahead), uses storage and job deadlines, moves jobs
     between eligible sites
   - all of them behind the **same scheduler interface** that also drives the real job
   - using the actual 2025 weather as the "forecast" is **perfect foresight** and must be labelled
     so; a realistic variant adds forecast error
5. **Customer tool.** "From when does it pay off for me?" For a host: enter your site, get
   servers, heat covered, savings, and the conditions under which it stops paying off.
6. **Network simulation.** Many sites over long periods (several years), with two kinds of future:
   short-term weather forecasts inside the scheduler, and long-term scenarios (electricity and
   compute prices, climate change, hardware ageing).
7. **Demonstrator.** A real job on a laptop, paused and resumed by the same scheduler code.

**UI later (not for the draft):** a configurator for customers (hosts), an admin tool for
DigitalHeat (tune parameters, see sites), and flow simulations and heat maps on a map.

## Paper outline (later)

1. Problem: heat demand and computing load do not match in time (12.5 vs. 0.6 servers).
2. Market: service × heat-fit matrix.
3. System model and formal problem statement.
4. Algorithms, with baselines.
5. Economics: actors, ownership models, win-win zone.
6. Evaluation: reference house (case figures), network of sites over several years, future
   scenarios, sensitivity analysis.
7. Customer tool.
8. Limits: when it does not pay off.
9. Demonstrator.

## Hackathon vs. later

Built in tiers. Each tier must be complete before the next one starts; the architecture is generic
from the start, so nothing is thrown away.

| Tier | Contents |
|---|---|
| **1 · Draft (2026-11-21 15:00)** | All must-haves on the reference house: ≥ 2 job types (one measured on a laptop), a real job paused/resumed by the scheduler, a greedy heat-following scheduler with the tank, the yearly balance with all case key figures and the case break-even price. |
| **2 · Pitch, core** | Forecast-based scheduler vs. the two baselines on the case's ranking (coverage, then heat dumped, then utilisation and missed deadlines); market matrix with 4–6 sourced services (WP1); system surplus and one ownership model (operator), with a slider for the host/DigitalHeat split; case vs. full-cost break-even; homeowner view in one minute. |
| **3 · Pitch, stretch** | Win-win chart for both ownership models; a small neighbourhood with one year-round site (e.g. hotel/spa) receiving summer jobs; break-even tool with sliders. |
| **4 · Later (paper)** | Network of many sites over many years, distributed/optimisation methods and scalability, realistic forecast errors, future scenarios, any location and building type, full market study. |

Pitch must still open with the case's key figures for the reference house (see
[`case/key-figures.md`](case/key-figures.md)).

## Data needed

- Phases 1–2: [`research/README.md`](research/README.md) (WP1 services and compute prices, WP2
  costs and energy, WP3 sites and profiles).
- Larger evaluations: multi-year and multi-location weather (Open-Meteo, from 1940), job arrivals
  (Alibaba trace), a generated neighbourhood of sites (from WP3).
- Future scenarios: Swiss climate scenarios (e.g. CH2018), electricity and compute price
  scenarios. Not yet in the research plan.

**Hardware (decided 2026-11-21):** GPU inference servers (L4 profile) are the main pitch scenario; CPU and consumer-GPU profiles are shown as comparison (`research/hardware.md`). Finding: hardware cost, not electricity, decides profitability. **Per job type (decision 0019, proposed):** no CPU-only servers. Every heater is a GPU node (consumer class RTX 4090/5090 as cost basis, L4 as comparison); Type 1 has priority, and Type 2 fills the leftover heat-led hours on the same node (GPU-capable codes on the GPU, CPU-only codes on the host CPU). Reasons and sources: `decisions/0019-gpu-nodes-for-all-job-types.md`. Scored reference-house figures keep the abstract 1 kW server.

## Two job types (proposed, decision 0018, 2026-11-21)

The job model of business model v2 (T-34); makes the "filler load" below concrete.

- **Type 1, sovereign AI services (pays):** mainly transcription (T-02) and a batch LLM service priced
  per token (T-33, Apertus 8B); OCR and indexing later. Served first.
- **Type 2, batch compute filler (heats):** leftover heat-led capacity sold through our own job
  broker for simulations and research computing (Monte Carlo, parameter sweeps, Python/C++ batch).
  Heat-led price between electricity cost (0.226 / 0.3325) and Swiss spot (1.80); scored figures
  keep 0.66. Fill as much as possible, no unpaid work; unsold hours go to the backup heater.
  Scored figures assume all hours sold; fill rate 0.35 (`research/demand.md`) as sensitivity.

## Under consideration: filler load sold as spot capacity (2026-11-21)

> Made concrete as Type 2 by decision 0018 (proposed); kept here as background.

Real demand for our premium service (confidential transcription) fills only a tiny share of a
house's server capacity: about 436 audio hours per 1 kW server-hour (T-02) against 428 dictation
jobs/day from all doctors in Basel-Stadt, which gives 0.1 % heat coverage instead of 58 %
(`research/demand.md`). We are therefore considering a **two-tier load model**:

- **Premium load:** sensitive batch jobs with deadlines (transcription, document AI), priced per
  work unit, high margin, served first.
- **Filler load:** remaining capacity sold as **interruptible spot compute** at the market price,
  paused or released whenever the heat is not needed. It produces most of the heat.

Prices: CHF 0.66 per server-hour fixed for the scored figures (Hetzner-equivalent), with the
self-updating Azure-based value (0.687 on 2026-11-21, T-28) shown as a live cross-check
(`research/computing-price.md`). Open: who buys interruptible home capacity (no source yet for
willingness to pay without service guarantees); queue support for mixing finite premium jobs with
unlimited filler (T-05 extension); team confirmation with Ennio (T-26).

## Open questions

- What a host pays or receives in each ownership model (heat fee? free heat? commission?).
- Which 4–6 services go into the first matrix.
- Stack and architecture: decided, see `AGENTS.md` §12.
- What the draft submission (2026-11-21 15:00) must contain (tier 1 assumes: the must-haves).

## Review

Fresh-context review (2026-11-20), verdict **go with changes**. Applied all three points: the
plan is split into tiers (the pitch column was too large for 3 people and ~33 h), the system
surplus is computed before splitting it between actors, and "perfect foresight" is labelled and
the scheduler runs behind one interface. Its simpler alternative (one house, operator model,
split slider) became tier 2.
