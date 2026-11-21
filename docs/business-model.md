# Business model (provisional, 2026-11-21)

> **Status:** provisionally fixed by Jona, team confirmation pending. Decision:
> [0020](decisions/0020-business-model-operator-two-job-types.md), building on
> [0018](decisions/0018-two-job-types-strategy.md) and [0019](decisions/0019-gpu-nodes-for-all-job-types.md).
> Ticket: [T-34](tickets/T-34-business-model-v2.md). This page explains the model and does no new
> modelling. Every number links to the file it comes from.
>
> **Hardware basis changed on 2026-11-21 (decision [0027](decisions/0027-datacenter-licensed-gpus-only.md)):**
> NVIDIA's GeForce driver licence excludes "datacenter deployment", so consumer cards (RTX 4090) are
> not permitted. All numbers below are now on the datacenter-licensed **L4** profile
> (`config/hardware/gpu-inference-l4.json`). Rows and sentences that changed are marked *(changed: L4, decision 0027)*.
> The economics come from the T-14 model ([`model/economics.md`](model/economics.md)).

## In one sentence

**DigitalHeat puts its own servers in homes, pays their electricity, and sells patient computing.
The homeowner gets the heat.** A home is the cheapest room for computing that can wait, because its
heat is needed anyway.

## Actors and money flows (operator model)

| Actor | Gives | Gets |
|---|---|---|
| **Host** (homeowner) | space in the basement, connection to the heating system | heat; keeps the backup heater |
| **DigitalHeat** | servers, installation, operations, electricity | income from computing, optional heat fee `h` from the host |
| **Compute customer** | money per token (Type 1) or per server-hour (Type 2) | results within the deadline, computed in Switzerland |

The heat fee `h` (CHF per kWh of heat the house uses) is open. It ranges from 0 (free heat) to
CHF 0.133 (heat-pump-equivalent value, [`key-figures.md`](case/key-figures.md)). It only moves money
between the two actors: the system surplus stays the same, but DigitalHeat's own electricity
break-even moves from CHF 0.3325 (free heat) to 0.2261 (full heat value) per server-hour.

## Two job types

| | **Type 1: batch LLM service** | **Type 2: research simulations** |
|---|---|---|
| Role | pays; served first | fills the remaining heat-led hours |
| Example work | summarising or indexing public documents, evaluating open models, synthetic data (Apertus 8B) | simulations with checkpoints (for example molecular dynamics), parameter sweeps |
| Data | non-personal only | non-personal |
| Deadline | yes (for example 24 h) | long or none |
| Interruption | pause/resume (freeze) | checkpoint or task boundary |
| Price | per token; batch-API level unless fixed multi-year contracts | per server-hour, a contribution above CHF 0.226 (scored figures: pinned 0.66) |
| Per server-hour on L4 *(changed: L4, decision 0027)* | 14.36 M tokens (proxy: RTX 4090 energy per token of T-37, batched, conservative; L4 not yet measured) × CHF 0.126 per 1M (list) = **CHF 1.81**; batch-API level **CHF 0.90** ([`model/economics.md`](model/economics.md) §1.2) | CHF 0.66 |
| Measured | laptop, T-33: 133.3 s and 0.92 Wh per document ([`jobs/llm-summary.md`](jobs/llm-summary.md), on branch `feat/T-33-local-llm-job`) | Monte Carlo research kernel, T-30 (open) |
| Demand source | "unlimited jobs" (case-permitted); sourced volume `TODO(source)` | "unlimited jobs"; reference: sciCORE Basel ≈ 79,920 server-h/yr ([`profitability-inputs.md`](research/profitability-inputs.md) §4) |

Both run on the same GPU nodes (decision 0019), datacenter-licensed L4 cards (decision 0027) *(changed: L4, decision 0027)*. Hours that nobody buys are not run, and the backup
heater covers them (decision 0018).

## Numbers for the reference house (4 servers, draft baseline)

From [`results/reference-house.md`](results/reference-house.md): 58.0 % of the heat demand covered,
0 kWh dumped, 22,661 server-hours, 18,129 kWh of heat used (= CHF 2,411 ÷ 0.133). Case break-even
**CHF 0.2261 per server-hour**.

Full cost adds hardware and operations. On the L4 profile *(changed: L4, decision 0027)* that is 4 × (34,244 ÷ 6 + 708) =
**CHF 25,661 a year** and a full-cost break-even of **CHF 1.358 per sold server-hour**
(`config/hardware/gpu-inference-l4.json`; on the former RTX 4090 basis it was CHF 16,532 and 0.956,
[`profitability-inputs.md`](research/profitability-inputs.md) §3).

System surplus per house and year, if every heat-led hour is sold at one price `p`:
`surplus = 22,661 × (p − 0.2261) − 25,661` *(changed: L4, decision 0027)*

| Price `p`, CHF/server-h | What it stands for | Surplus CHF/yr |
|---:|---|---:|
| 0.66 | pinned Type 2 price *(changed: L4, decision 0027)* | −15,829 |
| 0.90 | Type 1 LLM on L4, batched (conservative proxy), batch-API price *(changed: L4, decision 0027)* | −10,289 |
| 1.358 | full-cost break-even on L4 *(changed: L4, decision 0027)* | 0 |
| 1.60 | Type 1 LLM on L4, batch 1 (proxy), list price *(changed: L4, decision 0027)* | +5,536 |
| 1.81 | Type 1 LLM on L4, batched (conservative proxy), list price *(changed: L4, decision 0027)* | +10,206 |

The RTX 4090 market rows (CHF 0.57 and 0.84) and the RTX 4090 Type 1 rows (1.72, 1.95) are removed:
that hardware is not permitted (decision 0027). *(changed: L4, decision 0027)*

Mixed sales *(changed: L4, decision 0027)*: with Type 2 selling every remaining heat-led hour at CHF 0.66, the system breaks even
when Type 1 takes about 61 % of the sold hours, and DigitalHeat (which gives the heat away free) when
it takes about 70 % (by hand: `x = ((25,661 + 2,411) ÷ 22,661 − 0.434) ÷ (1.583 − 0.434)` = 0.70 with 0.434 = 0.66 − 0.226 and 1.583 = 1.81 − 0.226; without the 2,411 of free heat it is 0.61). The
year simulation of T-14 gives the same picture as a share of server capacity: DigitalHeat and the
host both gain from a Type 1 share of **67 %** (4 servers) or **39 %** (1 server, sized for base
load), 72 % / 46 % if Type 2 sells only 35 % of the rest. **At the batch-API token price no Type 1
share pays** ([`model/economics.md`](model/economics.md) §3). The host gains in every case (free heat,
CHF 2,411 a year with 4 servers).

## Why it can work where others failed

Nerdalize went bankrupt, Cloud&Heat gave up residential sites and Qarnot left room radiators
([`competitors.md`](research/competitors.md)). They sized for the winter peak, ran always on, used new
hardware and had no demand. Our answers: heat-led running with no unpaid work, a server count
chosen from the year simulation instead of the winter peak, LLM work that pays first and research work that fills, and our own Swiss
customers.

## When it does not pay off

- **No secrecy premium.** Sensitive data stays out of homes, so Type 1 competes with batch APIs at
  about half the list price. There it pays only if the optimistic batched GPU throughput holds,
  which is not yet measured ([`profitability-inputs.md`](research/profitability-inputs.md) §3, §6).
  The rows at CHF 1.60 and 1.81 above assume list price *(changed: L4, decision 0027)*. On L4 the batch-API price (CHF 0.90)
  never covers full cost with the conservative throughput; the optimistic batched reading (about
  4.5× more tokens) would.
- **Commodity prices only.** Below CHF 1.358 per sold server-hour the L4 profile loses money *(changed: L4, decision 0027)*,
  and both the pinned Type 2 price (0.66) and Type 1 at batch-API level (0.90) are below that.
- **Falling prices.** Token and compute prices fall about 25–29 % a year. Over a 6-year life nothing pays
  at commodity prices; we need fixed multi-year contract prices or a shorter payback (decision 0019 (e),
  [`profitability-inputs.md`](research/profitability-inputs.md) §5).
- **Licensed hardware costs more** *(changed: L4, decision 0027)*. NVIDIA's GeForce driver licence excludes "datacenter
  deployment", and a fleet run by DigitalHeat counts as one, so we use L4 data-centre cards
  (decision 0027). That raises the yearly fixed cost per house from CHF 16,532 to 25,661 (4 servers),
  or to CHF 32,285 at the newer verified L4 card price (CHF 44,179 instead of 34,244 per kW,
  [`profitability-inputs.md`](research/profitability-inputs.md) §7). Then the full-cost break-even is
  CHF 1.651, and Type 1 at list price (1.81) clears it by only about 10 %.
- **Scale.** sciCORE fills about 3.5 reference houses. A network needs anchor customers.
- **Not yet priced:** water blocks, heat exchanger, installation, house electrical connection,
  upload bandwidth, maintenance visits.

## Open points

- Heat fee `h` for the host (T-14).
- Type 1 arrivals per day and their source; the market price of research computing (sciCORE price
  `TODO(source)`).
- The measured Type 2 representative (T-30).
- Measured L4 throughput and power for the 8B model *(changed: L4, decision 0027)*: every Type 1 number on L4 is a proxy until then.
