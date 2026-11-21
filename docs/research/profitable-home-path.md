# A candidate path to profitable home compute (T-41)

Status: exploratory recommendation, 2026-11-21; not an accepted business-model change or a verified investment case. Extends [the free-heat audit](free-heat-viability.md). The user explicitly keeps private homes as the target. Three parallel audits checked revenue, physical hardware and break-even arithmetic.

## What the case actually asks

[The case](../case/challenge.md) asks which workloads fit, how to schedule them against heat demand over a year, and when they pay off. It explicitly leaves server count and summer operation to the team. It requires a reference-house annual balance, two job types and a real scheduler-controlled job; it does not require free heat, full heating coverage, four servers, or profitability at every price. The scoring prioritises coverage, then dumping, then utilisation/deadlines. A small profitable variant therefore complements rather than replaces the scored case comparison.

The fixed case server remains 1 kW electrical / 0.8 kW recovered heat. A smaller physical machine is an explicitly labelled hardware variant. No scored inputs are changed here.

## Recommended experiment: sell small reserved compute capacity, recover base-load heat

Size one physical node to the home's year-round hot-water load, and obtain a paid capacity commitment before purchasing it. Offer permitted, independently schedulable GPU batches (e.g. public-data inference or rendering), with measured performance and explicit availability terms. Avoid pricing all work as one cheap 8B token API: a buyer purchasing GPU capacity runs its own applications, so our unknown Apertus throughput is no longer converted directly into sales income. Performance, service quality and demand still have to be demonstrated.

This has a residential precedent: [Heata](https://www.heata.co/free-hot-water) describes servers in homes providing hot water, and its [compute offering](https://www.heata.co/compute) lists distributed batch/AI/CI workloads, cloud control planes and interchangeable home workers. This supports the deployment concept, not a claim about Heata's profits or our achievable prices. Do not infer that its privacy controls or service guarantees already exist in DigitalHeat. Sources accessed 2026-11-21.

## Correct the physical hardware assumption first

The existing L4 profile normalises 11.6313 GPUs onto one CHF 8,248 host. [Dell's R570 specifications](https://www.dell.com/support/manuals/de-ch/poweredge-r570/r570_ism_pub/technische-daten-der-gpu?guid=guid-0e6b3720-5463-4d19-9956-fae48b1c0c4f&lang=de-de) support at most **four L4 cards** (accessed 2026-11-21). The existing profile is not an installable bill of materials; a dense host could change both price and power.

An illustrative four-card R570-class candidate uses the existing host price and power estimate, but respects that card-count limit. The exact GPU-ready risers, CPU/RAM configuration, power supplies and cooling are still unquoted. This is a calculation boundary, not a purchase recommendation.

| Input | Value | Source / limit |
|---|---:|---|
| Cards | 4 L4 | Dell supported maximum; actual configuration must be quoted |
| Card price | CHF 3,089 each | dated T-37 Bechtle quote in [profitability inputs](profitability-inputs.md); not guaranteed available |
| Host price | CHF 8,248.03 | existing `config/hardware/gpu-inference-l4.json`; GPU-ready build unverified |
| Candidate acquisition | CHF 20,604.03 | host + 4 cards; excludes heat integration |
| Estimated active power | 0.4505 kW | existing host estimate 0.1625 + 4 × L4 0.072 kW TDP; wall power unmeasured |
| Recovered heat | 0.3604 kW | active power × case 80%; liquid integration unverified |
| Hot-water demand | 0.5 kW | case 12 kWh/day ÷ 24; constant hourly simplification |
| Depreciation / operations | CHF 3,434.005 / 708 per year | six-year accounting life and existing per-host ops; not proof of physical life |
| Electricity | CHF 0.3325/kWh | reference-house ElCom snapshot |
| Comparison GPU price | from CHF 0.2924/GPU-hour | [Infomaniak L4](https://www.infomaniak.com/de/hosting/gpu), accessed 2026-11-21; buyer retail comparison, not DigitalHeat net sales or equivalent SLA |

Under the case's constant hot-water demand, 0.3604 kW fits below 0.5 kW every hour. Hence this variant has no seasonal heat bottleneck at the estimated active power. At 8,760 active hours it delivers at most 3,157.104 kWh useful heat, worth **CHF 419.90/year** to the host: `3157.104 / 2.5 × 0.3325`, using the case heat-pump COP of 2.5. DigitalHeat pays/reimburses all node electricity and supplies the heat free. At 75% operation the benefit is about CHF 315/year, before any household-side costs. Full operation covers about 10% of the house's total yearly heat requirement. Real draw timing, losses, temperatures and water hygiene must be engineered and measured.

## Conditional economics and the contract boundary

Use the existing actor split: free heat is a host benefit, not operator income. Apply T-35's wear formula per installed node per **calendar year**, not a fixed CHF 0.473 per sold hour (that rate used a different card count and utilisation).

```
annual_wear_chf = 4 × assumed_gpu_failure_rate × (3089 + 160)
                + 0.16 × (1.5 × 160 + 0.10 × 8248.03)
annual_profit_chf = 4 × paid_node_hours × net_chf_per_gpu_hour
                 − paid_node_hours × 0.4505 × 0.3325
                 − 20604.03 / 6 − 708 − annual_wear_chf − missing_costs_chf
break_even_paid_node_hours = (20604.03 / 6 + 708 + annual_wear_chf + missing_costs_chf)
                          / (4 × net_chf_per_gpu_hour − 0.4505 × 0.3325)
```

For this bound the whole node runs at full estimated power while sold and is off while unsold. Real idle consumption and partial-card utilisation must replace this favourable simplification. Paid hours are contracted/billable equivalent full-node hours, not assumed demand. T-35's 3% and 9% failure inputs are stress scenarios; neither is a measured L4 replacement rate. Both price replacements at the later card quote here.

A capacity reservation can be paid while unused; that produces less heat than an active job. The table assumes the contracted work actually runs for the stated hours. Do not infer heat output from invoices alone.
Reservations can also require idle availability rather than shutdown: contract terms and measured idle watts must determine that cost. The assumed 0.16 host repairs/year has an unverified source observation period too. Neither repair scenario is an evidenced lower or upper bound.

| Outcome at the retail comparison price, before missing costs | 3% repair scenario | 9% repair scenario |
|---|---:|---:|
| Annual repair allowance | CHF 560 | CHF 1,340 |
| Break-even paid equivalent node hours/year | 4,611 (53%) | 5,376 (61%) |
| Operator remainder if all 8,760 hours sold | **+CHF 4,231/year** | **+CHF 3,452/year** |
| Operator remainder if 75% sold | +CHF 1,998/year | +CHF 1,218/year |

Worked example, 9% scenario, full sales: income `4 × 8760 × 0.2924 = CHF 10,245.70`; electricity `8760 × 0.4505 × 0.3325 = CHF 1,312.17`; depreciation CHF 3,434.01, ops CHF 708 and repair allowance CHF 1,340.01 leave CHF 3,451.51. This is room for integration and a margin, not verified final profit.

At 75% sales, an illustrative 15% revenue deduction (discount, platform or sales fees; a sensitivity, not a sourced fee) leaves only **CHF 65/year** in the 9% scenario. The route needs contracted net revenue sufficient to cover installed costs. Direct sales may reduce platform deductions but have their own sales/support costs. It cannot be justified by setting a marketplace fill slider high.

Unpriced: cooling conversion, installation, connectivity, service visits beyond the ops allowance, platform/software, insurance, financing, taxes where applicable, uptime redundancy and idle electricity. A one-off integration quote must be annualised over its own evidenced life, not silently assigned the server's six years. The retail price does not establish service equivalence or willingness to pay for interruptible home capacity.
Tax bases also need reconciliation: repair labour includes VAT while hardware and retail quotes may use different bases. L4 is passively air-cooled; supported card count does not validate water integration. Consumer GPU substitution has no validated host fit, power, cooling, licensing or buyer price in this estimate.

## Levers ranked by what they can actually change

1. **Smaller node, year-round heat use:** removes the summer heat constraint in the case variant. Four GPUs is a candidate for measurement, not a mandated team hardware choice.
2. **Paid capacity commitment before installation:** converts a volume assumption into an obligation to pay. A deposit helps financing but is not extra profit. Specify permitted data, checkpoint/deadline behaviour, outages and fallback costs in the offer.
3. **Sell the service the customer needs:** reserved GPU batches, rendering or managed processing may avoid the cheap-token price ceiling. Quote only after running their representative job; include software licences, support and sales effort. No premium is assumed just because a home is Swiss.
4. **Reduce actual installed cost:** quote a supported used host, GPU-specific warranty and a thermal package. A cheaper host must maintain GPU support and efficiency. Generic server warranties need not cover GPUs or cooling modifications. Refining the unsupported replacement estimate is legitimate; claiming a lower failure rate without evidence is not.
5. **Discounted rather than free heat:** a fee can share the host benefit. For this small node, the total maximum heat-pump-equivalent value is only CHF 420/year at full use; preserving a host saving leaves less than that for the operator. It cannot repair a large compute deficit.
6. **Existing customer's hardware / service-fee model:** could reduce DigitalHeat's capital requirement, but the customer still pays depreciation and takes hardware risk. Evaluate total system surplus and both actors; do not make hardware disappear from costs.

Not a solution by itself: a larger tank without a summer heat sink, job routing that leaves home hardware idle, leasing that only moves capex, free filler work, or a lower wear parameter chosen to force profit. Cloud gaming has additional latency, licence and uptime requirements and is not assigned hypothetical income here.

## Next proof and handoff

Recommend a private-home base-load pilot, conditional on three concrete items: **a complete thermal/GPU system quote; measured whole-node watts, useful heat and customer-job performance; and a signed minimum-volume/price offer**. Reuse Core and T-14 for the measured variant, keeping the scored 1 kW case unchanged. A supported node with at most four L4s is the first candidate, not an approved purchase.

T-14/T-15: resolve R570 card-count mismatch and keep annual repairs separate from hourly electricity. T-18: show the smaller host benefit and operator remainder together with the contract/measurement conditions. T-34: retain private homes, but treat a move from token sales to reserved capacity as a proposed team decision, with a short ADR/review before implementation. No customer has been contacted, no hardware ordered, and no profit is claimed as measured.

## Review

A fresh-context reviewer checked this note against the hardware and wear configuration. Arithmetic passed, including both 75% rows and the CHF 65 sensitivity. Adopted clarifications: explicit COP and electricity payer, lower host benefit at part utilisation, uncertain host repair frequency, VAT consistency, reservation idle power, and no assumption that direct sales are free or necessary. This is an exploratory boundary calculation; no shared model implementation changed.
