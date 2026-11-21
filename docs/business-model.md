# Business model (provisional, 2026-11-21)

> **Status:** provisionally fixed by Jona, team confirmation pending. Decision:
> [0020](decisions/0020-business-model-operator-two-job-types.md), building on
> [0018](decisions/0018-two-job-types-strategy.md) and [0019](decisions/0019-gpu-nodes-for-all-job-types.md).
> Ticket: [T-34](tickets/T-34-business-model-v2.md). This page explains the model and does no new
> modelling. Every number links to the file it comes from.

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
| Measured | laptop, T-33: 133.3 s and 0.92 Wh per document ([`jobs/llm-summary.md`](jobs/llm-summary.md), on branch `feat/T-33-local-llm-job`) | Monte Carlo research kernel, T-30 (open) |
| Demand source | "unlimited jobs" (case-permitted); sourced volume `TODO(source)` | "unlimited jobs"; reference: sciCORE Basel ≈ 79,920 server-h/yr ([`profitability-inputs.md`](research/profitability-inputs.md) §4) |

Both run on the same GPU nodes (decision 0019). Hours that nobody buys are not run, and the backup
heater covers them (decision 0018).

## Numbers for the reference house (4 servers, draft baseline)

From [`results/reference-house.md`](results/reference-house.md): 58.0 % of the heat demand covered,
0 kWh dumped, 22,661 server-hours, 18,129 kWh of heat used (= CHF 2,411 ÷ 0.133). Case break-even
**CHF 0.2261 per server-hour**.

Full cost adds hardware and operations. On the RTX 4090 profile that is 4 × (16,300 ÷ 6 + 1,416) =
**CHF 16,532 a year** and a full-cost break-even of **CHF 0.956 per sold server-hour**
([`profitability-inputs.md`](research/profitability-inputs.md) §3).

System surplus per house and year, if every heat-led hour is sold at one price `p`:
`surplus = 22,661 × (p − 0.2261) − 16,532`

| Price `p`, CHF/server-h | What it stands for | Surplus CHF/yr |
|---:|---|---:|
| 0.57 | RTX 4090 market, RunPod community (decision 0019 (b)) | −8,738 |
| 0.66 | pinned Type 2 price | −6,698 |
| 0.84 | RTX 4090 market, vast.ai median (decision 0019 (b)) | −2,619 |
| 1.72 | Type 1 LLM, batch 1, list price (T-37) | +17,359 |
| 1.95 | Type 1 LLM, batched (conservative), list price (T-37) | +22,504 |

Mixed sales: Type 1 must fill 28–49 % of the heat-led hours in year 1, depending on how much of the
rest Type 2 sells (decision 0019 (e)).

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
  The rows at CHF 1.72 and 1.95 above assume list price.
- **Commodity prices only.** Below CHF 0.956 per sold server-hour the RTX 4090 profile loses money,
  and Type 2 market prices (0.57–0.84) are below that.
- **Falling prices.** Token and compute prices fall about 25–29 % a year. Over a 6-year life nothing pays
  at commodity prices; we need fixed multi-year contract prices or a shorter payback (decision 0019 (e),
  [`profitability-inputs.md`](research/profitability-inputs.md) §5).
- **GeForce licence.** NVIDIA's consumer driver licence excludes "datacenter deployment". If a home
  network counts as one, professional cards raise the break-even (decision 0019).
- **Scale.** sciCORE fills about 3.5 reference houses. A network needs anchor customers.
- **Not yet priced:** water blocks, heat exchanger, installation, house electrical connection,
  upload bandwidth, maintenance visits.

## Open points

- Heat fee `h` for the host (T-14).
- Type 1 arrivals per day and their source; the market price of research computing (sciCORE price
  `TODO(source)`).
- The measured Type 2 representative (T-30).
