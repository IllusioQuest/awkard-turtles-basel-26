# Free heat: paid demand versus operator revenue (T-42)

Follow-up: [a candidate profitable private-home path](profitable-home-path.md) checks a smaller,
physically sized GPU node and reserved capacity sales. It is conditional, not a verified profit.

**Status:** conditional audit, 2026-11-21. Three read-only subagent audits independently checked
demand, achievable revenue and costs. This note does not change the case model, T-14 economics
code or the provisional business model. Its numerical T-14 snapshot is [PR #27](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/27)
at commit `60c9b197`; that PR is not yet on `main`. [Decision 0020](../decisions/0020-business-model-operator-two-job-types.md)
sets the main case: DigitalHeat owns the servers and pays their electricity; the host keeps the
backup heat pump. [T-14](../tickets/T-14-economics-actors.md) owns the actor-split calculation.

## Answer

**A profitable free-heat offer is possible only conditionally; the present evidence does not
establish a sellable sweet spot.** No paid DigitalHeat Type 1 or Type 2 hours, signed customer,
contract price, or repeatable public-document job arrival rate are documented in the repository.
That is a missing observation, **not a claim that market demand is zero**. The heat-led capacity
of 22,661 server-hours for four case servers is a technical maximum under unlimited work, not
booked sales (year-model result, retired 2026-11-22; Fleet: [`fleet-simulation.md`](../model/fleet-simulation.md) §16).

At the **unmeasured L4 throughput proxy** used in T-14, an 8B batch-API competitor price is too
low for the operator to break even, even if all heat-led hours are sold. The published 8B list
price can make some *modelled* rows positive, but it is not a DigitalHeat sale. Newer L4 purchase
quotes and expected wear narrow or erase that modelled margin. Mixed Type 1/Type 2 L4 rows also
assume a GPU-compatible filler job that has not been measured. Thus T-18 may illustrate a
potential CHF 2,411/year host heating benefit **if heat is free**, but may not call it a realised
bill saving or imply that DigitalHeat can finance it today.

## What is observed and what is a scenario

| Input | Evidence | Use in this audit |
|---|---|---|
| Heat-led output | `data/derived/reference-house-v1/summary.json`: four servers, 22,661 server-hours, 18,129 kWh useful heat, CHF 2,411 heat-pump-equivalent value | Technical maximum with unlimited jobs. The CHF amount is the host's *possible* avoided heating expense at zero heat fee. |
| Type 1 work | [T-33 laptop measurement](../jobs/llm-summary.md): one public-domain transcript, Apertus 8B, 3,629 tokens in 133.3 s, CPU-package energy only | A real demonstration, **not** an L4 throughput measurement or a recurring public-document order. Its [job descriptor](../../config/jobs/llm-summary-apertus-8b.json) says no customer contract. |
| Type 1 price | [Mistral Ministral 3 8B](https://docs.mistral.ai/inference/pricing): USD 0.15 per million input and output tokens list; [batch processing](https://docs.mistral.ai/inference/batch-processing) advertises 50% discount (pages checked 2026-11-21). T-14 converts at its sourced 0.8402 CHF/USD to CHF 0.126/M list or 0.063/M batch. | These are competitor **buyer** prices for a different model than the measured Apertus 8B, before sales fees or a demonstrated buyer. T-14's 14.3563 M tokens per L4 server-hour transfers a different 8B model's RTX 4090 benchmark to L4 hardware. CHF 1.81/h list or 0.90/h batch is hypothetical gross income, not realised net revenue. |
| Type 2 volume and price | [Filler research](filler-demand.md) proposes 0.35 paid fill (0.20–0.60), and T-14 pins CHF 0.66/server-hour. [sciCORE reports](https://scicore.unibas.ch/using-scicore/hpc-resources/) almost 30 M CPU-hours/year; its [fee page](https://scicore.unibas.ch/using-scicore/user-fees/) lists subsidised user rates (checked 2026-11-21). | The fill is an analogy, not Swiss orders; CHF 0.66 is a team scenario based on a 1 kW dedicated CPU server, not a realised L4 GPU-job payout. T-30's measured kernel runs on a laptop CPU. The L4 node profile assigns 0.8375 of its 1 kW to GPUs; no GPU Type 2 workload or concurrent CPU/GPU job proves full-power heat or income. sciCORE usage/fees do not establish paid portable work. |
| Hardware and wear | Current T-14 [L4 profile](../../config/hardware/gpu-inference-l4.json) uses CHF 34,244 per abstract 1 kW server. [T-37 purchase research](profitability-inputs.md) records a later Bechtle quote of CHF 3,089/card, implying CHF 44,179/kW; [T-35](refurbished-hardware.md) models CHF 0.473 expected wear per sold L4 server-hour. | Both higher purchase price and wear are **sensitivities**. The Bechtle listing changes and was out of stock at the recorded quote; a purchasable liquid-cooled 1 kW chassis is unpriced. |

Consumer RTX 4090 cost cases in T-37 are excluded from the proposed deployment by T-14's
[decision 0027](https://github.com/IllusioQuest/awkard-turtles-basel-26/blob/60c9b197/docs/decisions/0027-datacenter-licensed-gpus-only.md).
The L4 is a *normalised* 1 kW profile with fractional cards; physical packaging, cooling and
power draw remain unverified.

## Calculation path: free heat is a transfer to the host

With no heat fee, T-14's operator split is:

```
DigitalHeat CHF/year = Type 1 sold hours × realised Type 1 CHF/hour
                    + Type 2 sold hours × realised Type 2 CHF/hour
                    − server electricity CHF − hardware depreciation CHF
                    − operations CHF − wear CHF − installation CHF
Host heating benefit CHF/year = useful server heat kWh × heat-pump-equivalent CHF/kWh
```

The case values useful heat at `0.3325 ÷ 2.5 = 0.133 CHF/kWh` ([case](../case/challenge.md),
[ElCom snapshot](../../data/raw/elcom/iwb-basel-h4-2026.json)). That value belongs to the host
when heat is free; it is **not** also DigitalHeat cash income. At a nonzero heat fee `h`, the host
benefit becomes `useful heat × (0.133 − h)` and DigitalHeat gains `useful heat × h`, before any
contract-specific costs. T-14 already implements this split.

**Worked example from T-14's generated `levers.csv`:** one L4-equivalent server, heat-led, Type 1
share 0.50 of installed capacity, Type 2 fill cap 0.35 of the rest, list token price CHF 0.126/M.
The unchanged scheduler sells 4,380 Type 1 plus 1,432.75 Type 2 server-hours. T-14 records
CHF 7,922.95 + 945.62 income, CHF 1,932.74 electricity, CHF 5,707.34 depreciation and CHF 708
operations. With free heat, DigitalHeat gets **+CHF 520.49/year** under the older CHF 34,244
hardware profile; the host gets CHF 618.48 in heat-pump-equivalent benefit. Updating *only* the
purchase-price sensitivity to CHF 44,179/kW adds `(44,179 − 34,244) ÷ 6 = CHF 1,656/year`, so
the operator becomes **−CHF 1,135/year** before wear and installation. At the competitor batch
price CHF 0.063/M, the same T-14 row is already **−CHF 3,441/year** with the cheaper hardware.
Source: PR #27 `data/derived/economics-reference-house/levers.csv`, pinned at `60c9b197`;
purchase and wear sources are in the table above. Amounts are rounded to CHF 1.
This is a mathematical price-and-fill scenario, not a physically verified L4 Type 2 run or
evidence that either job type has been sold.

## The conditional sales corridor

T-14's **older-price, zero-wear** run at Type 2 fill 0.35 and Type 2 price CHF 0.66 gives these
operator outcomes. The 0.35 cap is applied to installed hourly capacity before the heat cap;
it does **not** mean exactly 35% of the heat-led remaining hours are sold. `p` is a Type 1 share of
installed hourly capacity, not an observed buyer share. **The mixed rows are financially and
physically unvalidated placeholders:** the CPU-only T-30 filler has not been shown to run and
heat a full 1 kW L4-equivalent GPU node. Only `p = 1` avoids that Type 2 mismatch, and it still
depends on unmeasured L4/Apertus throughput and hypothetical gross sales price.

| L4-equivalent servers | Type 1 price | `p` | DigitalHeat CHF/year in T-14 | With later L4 purchase-price sensitivity only |
|---:|---|---:|---:|---:|
| 1 | batch-API level, CHF 0.063/M | 1.00 | −2,051 | −3,707 |
| 1 | list, CHF 0.126/M | 0.50 | +520 | −1,135 |
| 1 | list, CHF 0.126/M | 0.75 | +2,948 | +1,292 |
| 4 | batch-API level, CHF 0.063/M | 1.00 | −12,700 | −19,324 |
| 4 | list, CHF 0.126/M | 0.75 | +1,055 | −5,569 |
| 4 | list, CHF 0.126/M | 1.00 | +7,795 | +1,172 |

The later price sensitivity is `T-14 operator CHF − server_count × (44,179.1064 − 34,244.06) ÷ 6`;
it changes no dispatch or income. At full Type 1 sales, the implied minimum *realised* price is
about **CHF 1.39 per sold hour with one server** or **CHF 1.76 with four**, before wear/installation:
`0.3325 + N × (44,179.1064 ÷ 6 + 708) ÷ sold_server_hours`, with 7,631.25 or 22,661.03
sold hours from the T-14 run. The L4 batch-API proxy is CHF 0.90/h, below either threshold;
the list proxy is CHF 1.81/h, only just above the four-server threshold.

T-35's **modelled** CHF 0.473 wear per sold L4 server-hour would subtract another CHF 10,719
from the four-server, all-Type-1 list row, turning its later-price **+CHF 1,172** into roughly
**−CHF 9,547/year**. It is a sensitivity, not a measured failure bill: T-35 extrapolates its
9% GPU annual failure rate from Meta's H100-training [job interruptions](https://ar5iv.labs.arxiv.org/html/2407.21783),
which do not count L4 replacements. T-35's lower 3% sensitivity reduces the wear allowance to
about CHF 0.178 per sold hour; the same four-server case is still about **−CHF 2,862/year**
(`1,172 − 22,661.03 × (0.473 − 0.295)`), before installation. For that list-price case to stay
above zero, wear would need to fall below about CHF 0.052/hour, an 89% reduction from T-35's
central allowance. At the batch-API price, even zero wear does not make the operator profitable.
Integration, liquid cooling, insurance, financing, platform fees, idle power and price decline remain unpriced or
unmeasured. Each can only reduce the operator margin shown here.

## Demand gap and next proof

The local Type 1 job has no qualified buyers, documents/day, recurrence, signed price or contract.
The Type 2 research kernel is measured on a laptop, not a customer L4 workload. T-31's mixed
year run is a **technical queue fixture** with medical dictation as Type 1, explicitly outside
decision 0020's home business case. Its nominal `soldCapacityShare = 0.35` yields 9,720.56 Type 2
hours out of 22,622.24 in its all-sold comparison, or **42.97%**: the cap uses installed hourly
capacity, not 35% of heat-led residual hours. Do not label that result as 35% of available work
sold or use its CHF income as a sales forecast ([T-31 draft PR #25](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/25)).

**To decide whether free heat is financeable:** obtain a price and minimum paid volume from at
least one public-document Type 1 buyer, measure Apertus 8B token throughput and whole-node power
on a permitted L4 build, obtain an installable hardware/cooling quote, and establish billable
research-compute hours compatible with site/data rules. Then rerun T-14 with *net* realised
prices, actual paid arrivals and T-35 wear, and compare the host and operator cash results.
Until then, use “possible heating-cost reduction if heat is free” in T-18, not “you save CHF X”.
