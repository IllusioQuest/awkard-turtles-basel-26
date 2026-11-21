# Paper skeleton (T-22)

> **Status:** skeleton, 2026-11-21 (Jona / Claude). Not reviewed by the team. Section texts are
> notes, not prose. Every number below comes from a file in this repository; the file is named next
> to it. Figures marked **TODO** do not exist yet. The structure follows the paper outline in
> [`docs/vision.md`](../vision.md), updated for the business model in decision
> [0020](../decisions/0020-business-model-operator-two-job-types.md).
> LaTeX draft: [`latex/`](latex/README.md) (`latexmk -pdf main.tex`).

## Working title

*Heat-led computing in homes: scheduling, sizing and economics of servers that heat a house*

Alternatives: "When does a server pay for heating a home?" · "The house as the cheapest room for
patient computing".

## Draft abstract (placeholders in [brackets])

Servers turn almost all of their electricity into heat, while homes pay for heat. We study when
distributed servers in residential buildings can deliver useful heat *and* useful computing. For a
reference house in Basel (2025 hourly weather, 31,231 kWh heat demand a year), heat demand
needs 12.5 servers in the coldest hour but 0.6 in summer. We formulate heat-led job routing as a
rolling-horizon linear programme that contains the greedy heat-following scheduler as a special
case. A demonstrator shows that the same scheduler code pauses and resumes real containerised
workloads (LLM batch inference, research simulation) on a laptop. With 4 servers the house covers
58.0 % of its heat demand with no heat dumped at 64.7 % utilisation. Electricity net of the used
heat costs CHF 0.226 per server-hour, but full cost on consumer GPUs is CHF 0.956. We show that
[hardware staging by load duration] and [routing] change this, and state the prices below which the
model does not pay off.

## Contributions (candidates, team to choose 3–4)

1. **A formal model** of heat-led job routing across sites as an LP / MILP with rolling horizon,
   with the greedy heat-following scheduler as its one-site, one-hour special case
   ([`model/routing-optimisation.md`](../model/routing-optimisation.md) §8.1).
2. **An economic decomposition** per server-hour: case break-even (electricity − used heat) vs.
   full-cost break-even, "system surplus first, split second" between host and operator
   ([`business-model.md`](../business-model.md), [`vision.md`](../vision.md)).
3. **The tension between the case's ranking and the operator's money** (§5.4 of the routing model):
   at a filler price above the electricity price, running all servers and dumping heat maximises
   surplus; heat-led operation is a choice with a price.
4. **Sizing by load duration** (proposed, not computed yet): the n-th server in a house runs far fewer
   hours than the first, so peak servers should be the cheapest hardware.
5. **A demonstrator** in which one scheduler drives both the year simulation and real containers
   (decisions 0005, 0009), with measured workloads.
6. **Capacity vs. demand at city scale**: 1 % of Basel's gas- and oil-heated residential buildings
   would offer 4.8 M server-hours a year, far more than local research or dictation demand
   ([`research/profitability-inputs.md`](../research/profitability-inputs.md) §4).

## Sections

### 1. Introduction
- Problem: heat demand and compute load do not match in time (12.5 vs. 0.6 servers,
  [`case/key-figures.md`](../case/key-figures.md)).
- Why homes: competitors failed or retreated exactly there (Nerdalize bankrupt 2018, Cloud&Heat left
  residential sites, Qarnot left room radiators; [`research/competitors.md`](../research/competitors.md)).
- Research questions: (RQ1) how many servers per house and with which scheduling? (RQ2) which work fits
  which season? (RQ3) under which electricity, compute and hardware prices does it pay off, and for whom?
- Contributions (above).

### 2. Related work
- Heat-reusing compute companies: Qarnot, Heata, Deep Green, Leafcloud, Cloud&Heat, Nerdalize, Thermify,
  Windcloud ([`research/competitors.md`](../research/competitors.md)).
- Swiss projects: IWB + Neutral-IT, IWB + DeepSquare, K51, Infomaniak D4, EPFL Heating Bits
  ([`research/digitalheat-company.md`](../research/digitalheat-company.md) §4).
- Data-centre heat reuse into district heating; demand response and carbon-aware scheduling of
  data centres; spot / preemptible capacity markets ([`research/demand.md`](../research/demand.md)).
  `TODO(source)`: academic references (to collect).

### 3. Case and reference house
- Reference house exactly as in [`case/challenge.md`](../case/challenge.md): Basel 2025 hourly weather,
  heating formula, 206 heating days, 26,851 kWh space heat + 4,380 kWh hot water, 35 kWh lossless tank,
  ElCom IWB H4 CHF 0.3325/kWh, heat valued at price ÷ 2.5.
- Server: 1 kW electric, 0.8 kWh recoverable heat per hour.

### 4. System model
- Sites, servers, tank, heat balance, jobs (cohorts with deadlines, filler), data-locality rules
  (decision 0013), links between sites ([`model/routing-optimisation.md`](../model/routing-optimisation.md) §1–4).
- Job catalog: Type 1 batch LLM, Type 2 research simulation (decision 0020); descriptor schema
  (decision 0009).

### 5. Scheduling

*Draft text (explanatory, not yet formal; numbers from `docs/model/network-scheduler.md`).*

Every strategy answers the same question each hour: how much should each site's servers run, and
which work should they do? All strategies share one heat balance per site (the case's lossless tank,
80 % recovery, backup heater for the rest) and one actuation path, so the scheduler that computes the
year is the one that drives a real container in the demonstrator.

We compare five strategies of increasing intelligence. **B0** runs every server all the time and dumps
what the house cannot take. **B1** is the heat-led greedy rule: run exactly what the house needs plus
what fits into the tank, never dump. **B2** keeps B1's load at every site but moves paid work (Type 1,
with a 24-hour deadline) that its home site cannot finish in time to the site with the most free
heat-led capacity. The **LP** plans all sites over a rolling 48-hour window and decides, in a strict
order of priorities that mirrors the case's ranking, how to minimise dumped heat, maximise used heat,
avoid missed deadlines, maximise server-hours and, last, keep work at home unless moving helps. A
**full-year LP** with perfect knowledge of the whole year serves only as an upper bound.

The LP is a linear programme because our work splits into small units (documents, simulation tasks)
and servers can run at part load; this keeps it fast (seconds per hour for 33 sites,
[`model/routing-complexity.md`](../model/routing-complexity.md)). Integer servers or non-splittable
jobs would make it a MILP (future work). Weather-based demand forecasts are perfect foresight unless
a forecast error is added; we label every result accordingly.

Two control strategies (decision 0029) separate *local timing* from *the network*: **B1-reserve** and
**B2-reserve** stop filler work from filling the tank when tomorrow's paid work will need the room.

### 6. Economics
- Marginal value of a server-hour: `p − 0.2261` if its heat is used, `p − 0.3325 − 0.8·π_dump` if
  dumped (§5.2).
- Full cost: depreciation and operations per sold hour; hardware profiles CPU, L4, RTX 4090
  (full-cost break-even 1.428 / 1.651 / 0.956 CHF per server-hour,
  [`research/profitability-inputs.md`](../research/profitability-inputs.md) §3).
- Operator model, heat fee `h` and the split of the system surplus ([`business-model.md`](../business-model.md)).
- Price trends to 2030 and lifetime averages (§5 of the same file).

### 7. Demonstrator
- Architecture: `IScheduler.Decide(HeatSignal)` → `SchedulerController.Apply` → `IExecutor`
  (simulated or `DockerJobExecutor`), decisions 0005 and 0009.
- Measured workloads: whisper.cpp ([`jobs/transcription.md`](../jobs/transcription.md)), Apertus 8B
  ([`jobs/llm-summary.md`](../jobs/llm-summary.md)), research simulation (T-30, `TODO`).
- Measurement method and scaling to 1 kW; limits (CPU package vs. wall power).

### 8. Evaluation

*Draft text; tables in `docs/model/network-scheduler.md`, data in `data/derived/network/experiments/`.*

**8.1 Reference house.** Four servers cover 58.0 % of the house's heat demand with no dumped heat at
64.7 % utilisation ([`results/reference-house.md`](../results/reference-house.md)). On a single house
the greedy rule B1 is optimal: it reaches the full-year upper bound exactly, and the LP cannot improve
on it. Planning on imperfect forecasts costs little (coverage 58.05 % → 57.82 % at σ24 = 20 %).
*Message: on one house, intelligence does not pay; heat physics decides.*

**8.2 Sizing** (TODO: full 1–13 server sweep).

**8.3 Strategy comparison on a Swiss test network.** On 33 synthetic sites in nine cities, all
heat-led strategies cover the same 83.4 % of demand with no dumped heat, because coverage is set by
how much heat the available work produces, not by where it runs. The strategies differ in missed
deadlines: with 30 % paid work, B1 misses 15.7 % of jobs, while B2 misses none by moving about
103,000 server-hours to sites with summer heat demand. The LP results are pending
(TODO: fill from `A_type1_share-lp.csv`).

**8.4 Why the network helps.** In summer a house can only absorb heat for hot water. Paid work above
that cannot run at home without dumping heat. If the network contains year-round heat sinks (hotels
with spas, indoor pools), the work moves there and no deadline is missed. Remove them and moving still
happens, but 13.8 % of jobs are missed because every house is short at the same time. Timing at home
does not substitute for the network: keeping tank room for tomorrow's work saves only 0.07 points of
missed deadlines, though it reduces the work that has to move by 37 %.

**8.5 Sensitivity.** Paid-work volume (10–70 %): B1's missed deadlines grow from 0 to 29.5 %, B2 stays
at 0 % while moved server-hours grow roughly linearly. Filler availability φ (1.0 → 0.2): coverage
falls from 83 % to 21 % for every strategy, which shows that a shortage of sellable work, not the
algorithm, limits the delivered heat. Forecast error for the LP: TODO (`C_forecast_error-lp.csv`).
Economic sensitivities (compute price, hardware, electricity, heat fee): see §6.

**8.6 Capacity vs. demand** (§4 of `profitability-inputs.md`).

*Key statements: (i) on one house greedy is optimal (upper-bound proof); (ii) value arises in the
network, with deadlines and year-round sinks; (iii) forecast errors cost little; (iv) limits in §9.*

### 9. Limits and threats to validity
- When it does not pay off ([`business-model.md`](../business-model.md)).
- Model simplifications from the case: lossless tank, 80 % recovery, no heat pump, abstract 1 kW server,
  flat electricity price.
- Perfect foresight; unlimited-jobs assumption; marketplace fill rates from search snippets;
  laptop measurements scaled linearly to 1 kW; GeForce licence; installation costs unpriced.

### 10. Conclusion and outlook
- District heating (out of scope in the case), grid flexibility, PV ("Prepper"), multi-family buildings
  ([`research/ideas.md`](../research/ideas.md)).

### Appendix: reproducibility
- One command per figure (table below), committed inputs with `SOURCE` files, fixed seeds,
  SHA-256 of outputs as in `scripts/export_reference_house.py`.

## Figures and tables

| ID | Content | Produced by | Status |
|---|---|---|---|
| T1 | Key figures of the reference house (servers, coverage, dump, utilisation, missed deadlines, break-even) | `dotnet run --project src/DigitalHeat.YearRun -- .` → `results/reference-house/summary.json` | exists (missed deadlines open) |
| T2 | Job catalog with measured runtime and energy | `config/jobs/*.json`, `scripts/measure_job.py` | Type 1 exists; Type 2 T-30 |
| T3 | Assumptions with sources | `config/reference-house.json`, `docs/assumptions.md` | exists |
| T4 | Strategy comparison B0/B1/B2/LP/UB (reference house) | `dotnet run --project src/DigitalHeat.NetworkRun -- .` → `results/network/reference-house-comparison.json` | exists |
| T5 | Network strategies: missed deadlines and moves vs. Type 1 share (A), φ (B), network mix (D), forecast error (C) | `python3 scripts/network_experiments.py [--lp --jobs 4]` → `data/derived/network/experiments/` | greedy exists; LP pending |
| F1 | Year heatmap: heat demand vs. server capacity | year explorer (`prototypes/year-explorer/`) or T-13 export | exists (explorer); paper plot **TODO** |
| F2 | Sizing curve: coverage, utilisation, full-cost break-even over 1–13 servers | `scripts/export_reference_house.py` with a full sweep | partial (1, 3, 4, 5, 13 servers) |
| F3 | Load duration: hours per year of the n-th server | derived from F2 | **TODO** |
| F4 | January and July day: demand, tank, server load, real job paused | `DigitalHeat.LiveRun` replay output | runs exist; plot **TODO** |
| F5 | Money per server-hour: electricity, heat value, case and full-cost break-even | case values + `scripts/profitability_inputs.py` | data exists; plot **TODO** |
| F6 | Surplus per house vs. compute price for each hardware profile | `scripts/profitability_inputs.py` | data exists; plot **TODO** |
| F7 | Capacity vs. demand at city scale | `scripts/profitability_inputs.py` (§4) | data exists; plot **TODO** |
| F9 | Map: summer flows from houses to hotels/pools (B2 vs LP, split view) | `NetworkRun … --export` (decision 0025 §7) → Ennio's map | export exists; map in progress (Ennio) |
| F10 | Missed deadlines vs. Type 1 share; with vs. without year-round sinks | `data/derived/network/experiments/A_*.csv`, `D_*.csv` | data exists; plot **TODO** |
| F8 | Win-win zone: host and operator vs. heat fee and compute price | T-14 | **TODO** |

## Open questions for the team

1. Venue and format: short workshop paper or full paper? Candidates: ACM e-Energy, ACM BuildSys,
   Energy Informatics (journal). To check: deadlines, page limits.
2. Which 3–4 contributions carry the paper (list above)?
3. Is the LP (T-11/T-12) part of this paper, or a second paper?
4. Authors and order.
