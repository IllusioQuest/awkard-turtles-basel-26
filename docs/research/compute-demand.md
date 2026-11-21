# R4: Demand for compute, today and to 2030

> Status: exploration (Claude agent, 2026-11-21), ticket T-37 package R4. Not reviewed by the team.
> Data: [`data/raw/market/compute-demand.csv`](../../data/raw/market/compute-demand.csv) (ids `cd-*`).
> Method of reading sources: the research agent read search snippets only. The CSCS and sciCORE figures
> were then **verified by opening the pages** (2026-11-21, confidence raised to high); the rest remain
> snippets (confidence `low`/`medium`, see CSV).
> Conversion: USD to CHF 0.8402, EUR to CHF 0.9233 (repo rates).
> Our unit: 1 kW server-hour. Reference house sells **22,661** per year; 1,000 houses = **22.66 M**.
> Throughput from decision 0018: 436 audio h (whisper base), 27-35 audio h (large-v3, mid 31),
> 3.9 M tokens per server-hour.

## 1. Short answer

- **Type 1 (sovereign AI)** is large in the world but, for Basel alone, tiny: sourced Basel physician
  dictation is 39-546 server-hours per year, i.e. 0.2-2.4 % of one house. Only a Europe-wide view reaches
  the scale of 1,000 houses, and only as an upper bound.
- **Type 2 (batch filler)**: public research demand is real and **oversubscribed** (CSCS asks 1.8-3.3 times
  what it can give), but the Basel centre sciCORE consumes only about 80,000 (energy basis) to 460,000 (node basis)
  server-hour equivalents per year, 0.4-2 % of 1,000 houses. A broker therefore needs customers beyond Basel research (Swiss and European
  research groups, finance), for which we found **no public volumes**.
- **Seasonality**: no site-level monthly data found for Switzerland. The one analysed dataset shows strong
  daily and weekly cycles and very weak yearly periodicity, so demand is roughly flat over the year
  while our supply is heat-led (winter). Summer is a supply gap, not a demand gap.

## 2. Type 1: sovereign AI services

| Item | Value | CSV id |
|---|---|---|
| Europe speech and voice recognition market | USD 1.98 bn (2025), 4.56 bn (2030), CAGR 18.2 % | `cd-t1-eu-stt-2025`, `-2030`, `-cagr-mm` |
| Same, other publishers | USD 4.22 bn (2030, CAGR 16.2 %); EMEA USD 4.70 bn (2030, CAGR 14.2 %) | `cd-t1-eu-stt-2030-dbmr`, `cd-t1-emea-stt-2030-statista` |
| Medical transcription services, global | USD 130 bn (2030), CAGR 5.7 %; other publishers 4-7 %, sizes differ by 10x | `cd-t1-med-transcr-2030` |
| Swiss persons using generative AI | 47 % (EU 33 %); 41 % of workers use it at work | `cd-t1-ch-persons-genai` |
| Swiss SMEs with AI | 22 % (2024) to 34 % (2025) | `cd-t1-ch-sme-ai` |
| Swiss colocation (proxy for sovereign hosting) | USD 465 M (2024) to 916 M (2030), CAGR 12 % | `cd-t1-ch-colo-2030` |
| OpenRouter tokens | 194 trillion per week (Oct 2026); about 25x growth in 11 months | `cd-t1-openrouter-week` |

Market sizes for speech recognition differ by publisher but agree on **CAGR 14-18 % to 2030**. They are
market-research press summaries (confidence low) and include voice assistants and call centres, not only
batch transcription.

**Gaps (`TODO(source)`)**: BFS firm-level ICT survey on AI use; Swiss sovereign-cloud or Swiss AI market
size (only an economic-impact estimate, `cd-t1-ch-econ-genai`, which is not spend); audio hours per year
of Swiss courts, cantonal parliaments, Basel university hospital (not found; the Basel physician figure
in `docs/research/job-demand.md` remains the only sourced volume); Google or Microsoft token counts; Swiss
physician count for scaling beyond Basel.

### Server-hour estimates

1. **Basel physician dictation (sourced, from job-demand.md).** Audio hours = 1,269 physicians x 66.71 min
   x 12 / 60 = 16,931 h. Server-hours = 16,931 / 436 = **38.8** (base) or 16,931 / 31 = **546** (large-v3).
   `cd-d-basel-stt-base`, `cd-d-basel-stt-large`. Against one house: 0.17 % and 2.4 %.
2. **Europe speech-recognition spend as an upper bound.** Formula: server-hours = market spend in CHF /
   price per audio hour / audio hours per server-hour. Worked example 2025: 1.98 bn USD x 0.8402 =
   CHF 1.664 bn; at the batch price CHF 0.1512 per audio hour (`ai-stt-google-v2-dynbatch` in
   `ai-services.csv`) gives 1.10 x 10^10 audio h; / 436 = **25.2 M server-hours** (base) or **355 M**
   (large-v3, / 31). 2030 (CHF 3.831 bn): **58.1 M** (base). `cd-d-eu-stt-2025-sh`, `cd-d-eu-stt-2030-sh`.
   This is **1.1x to 15.7x of 1,000 houses today**, but only a loose upper bound: 1.10 x 10^10 audio hours
   per year would be about 24 hours of transcribed audio per European resident, which is not plausible
   as batch-transcription volume. The spend includes voice assistants, call-centre analytics, devices and
   human services. **Do not use it as a volume; it only shows the market is not small.**
3. **Tokens.** 194 x 10^12 tokens/week x 52 / 3.9 x 10^6 = **2.59 x 10^9 server-hours per year**
   (`cd-d-openrouter-sh`). 1,000 houses = 0.9 % of OpenRouter alone. Order of magnitude only, since
   OpenRouter serves mostly models much larger than an 8B model.

## 3. Type 2: batch compute filler

| Item | Value | CSV id |
|---|---|---|
| sciCORE (Basel) consumption | almost 30 M CPU-hours per year, about 800 users, over 14 M jobs | `cd-t2-scicore-cpuh` |
| sciCORE main cluster | 223 nodes, 14,528 cores | `cd-t2-scicore-cluster` |
| CSCS requested vs available node-hours | 39.5 M vs 12 M (2016), 50.5 M vs 23.5 M (2016), 19.7 M vs 11 M (2019) | `cd-t2-cscs-*` |
| ETH Zurich use of CSCS | 1.82 M node-hours (AR 2025) | `cd-t2-cscs-eth-nh` |
| Global HPC market | USD 87 bn (2030), CAGR 7.2 %; range 5-11 %; HPC for AI 27.6 % | `cd-t2-hpc-2030-gvr`, `cd-t2-hpc-ai-cagr` |

- **Oversubscription (direct demand-excess signal):** requested / available = 39.5/12 = 3.29, 50.45/23.51 =
  2.15, 19.7/11 = 1.79; mean **2.4** (`cd-d-cscs-oversub`). Only 2016 and 2019 found; CSCS Alps
  allocations are mostly GPU node-hours, so this shows research appetite, not demand for CPU filler.
- **sciCORE in our unit.** Assumption [A]: one node-hour is one server-hour. Cores per node = 14,528 / 223 =
  65.15; server-hours = 30,000,000 / 65.15 = **about 460,000 per year** (`cd-d-scicore-sh`) = **20.3
  reference houses**, **2.0 % of 1,000 houses**. This node basis is an **upper bound**. On an energy basis
  (our unit is 1 kWh), with the verified EPYC 9754 SPECpower server as proxy (128 cores at 0.341 kW =
  375 cores per kW, `pe-specpower-9754-100`): 30,000,000 / 375.4 = **about 80,000 server-hours**
  (`cd-d-scicore-sh-energy`), i.e. 3.5 reference houses. Use 80,000 as the central value. The sciCORE page is undated and cluster capacity
  (14,528 cores x 8,760 h = 127 M core-hours) is 4x the consumed hours, so the figure may be stale; check
  with sciCORE (`TODO(source)`: annual report, queue waiting times, ETH Euler core-hours).
- **ETH at CSCS:** 1.82 M node-hours / 22,661 = 80 houses (different hardware, scale only).
- **Global HPC market** is mostly hardware revenue; it cannot be converted to server-hours without a
  price and utilisation per core-hour. `TODO(source)`: cloud batch/spot market in server-hours, Swiss
  finance Monte Carlo/risk compute (no public figures found).

**Reading for the broker:** Basel research is enough for about 20 houses. Selling the output of
hundreds of houses needs Swiss-wide or European research and industry customers, and a free-compute
competitor exists (sciCORE and CSCS are subsidised for researchers; compare with CHF 0.66 per server-hour).

## 4. Seasonality and time of day

- Evidence found: NSF HPC workload analysis (`cd-t2-season-xdmod`): "strong periodic behavior at one day and
  one week, very weak periodicity over one year" (2013-2017, USA, search snippet).
- Not found: month-by-month utilisation of sciCORE, Euler or CSCS; academic-semester or deadline peaks.
  `TODO(source)`: request monthly usage statistics from sciCORE (monthly reports exist, private to groups).
- **Fit with heat-led supply:** demand for batch work does not fall in winter, so the winter-heavy
  heat schedule is not penalised; but demand does not rise in winter either, so nothing is gained
  for heat. In summer, demand exists while our supply is low; this is a supply gap served by
  data centres. Daily cycles (weekday peaks) matter little for queued, delay-tolerant batch jobs.

## 5. Summary in server-hours per year

| Segment | Server-hours/yr | vs 1 house (22,661) | vs 1,000 houses | Ids |
|---|---|---|---|---|
| Basel dictation, base / large-v3 | 38.8 / 546 | 0.2 % / 2.4 % | 0.0002 % / 0.002 % | `cd-d-basel-stt-*` |
| Europe speech spend, upper bound 2025, base / large-v3 | 25.2 M / 355 M | 1,110x / 15,700x | 1.1x / 15.7x | `cd-d-eu-stt-2025-sh` |
| Europe speech spend, upper bound 2030, base | 58.1 M | 2,560x | 2.6x | `cd-d-eu-stt-2030-sh` |
| OpenRouter tokens (global, 8B equivalent) | 2.59 x 10^9 | 114,000x | 114x | `cd-d-openrouter-sh` |
| sciCORE (Basel research), energy basis / node basis | 0.08 M / 0.46 M | 3.5x / 20x | 0.004x / 0.02x | `cd-d-scicore-sh-energy`, `cd-d-scicore-sh` |

Growth to 2030: speech recognition 14-18 % per year; HPC 5-11 % (HPC for AI 27.6 %); OpenRouter
tokens about 25x in 11 months (not a stable forecast); Swiss SME AI adoption 22 to 34 % in one year.
