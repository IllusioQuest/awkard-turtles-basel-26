# Pitch claim sheet — reference-house opening

Status: source gate for T-67, not the final graphic. All six opening figures must describe **one** fixed-case C# annual run, with a visible scenario label. The case requires these six figures before the live demonstration: [`challenge.md`](../case/challenge.md) § Pitch and [`key-figures.md`](../case/key-figures.md). Do not combine values from different demand scenarios.

| Opening claim | Current 4-server unlimited-work comparison | Evidence and display rule |
|---|---:|---|
| Servers per house | 4 | [`reference-house.md`](../results/reference-house.md), draft comparison; a scenario choice, not a proved optimum. |
| Total heat demand covered by servers | 58.0% | Same comparison; denominator is space heating **plus** hot water. |
| Heat dumped per house | 0 kWh | Same comparison; this is model output, not measured waste heat. |
| Server utilisation | 64.7% | Same comparison; server running hours ÷ (4 × 8,760). |
| Missed deadlines | **undefined** | Unlimited work has no finite job count or deadlines. The finite transcription-only diagnostic reports 0%, but belongs to a different scenario and cannot fill this cell. |
| Case break-even compute price | CHF 0.2261 per server-hour | Same comparison; `(electricity cost − value of heat actually used) ÷ sold server-hours`. It excludes hardware, integration and operations. |

The numeric source of the comparison is [`explorer-default.json`](../../data/derived/reference-house/explorer-default.json), reproduced by `python scripts/export_reference_house.py` using the fixed weather and ElCom files. Worked example: `(CHF 7,534.79 electricity − CHF 2,411.13 used-heat value) ÷ 22,661.03 sold server-hours = CHF 0.2261/hour`. The scenario assumes every offered hour can be sold. The value of used heat is the case's `CHF 0.3325/kWh ÷ 2.5` heat-pump comparison, not a cash payment by a customer.

**Publication gate:** use a finite or mixed C# scenario with one coherent set of six figures, including a defined deadline denominator, or visibly say “not defined” for the unlimited comparator. T-31 is an in-review technical scenario, so its numbers require a separate source and label before replacing this row. The film's finance segment must additionally distinguish case-only balance from full operator profit; [`reference-house.md`](../results/reference-house.md) documents missing costs. The live process and Basel map transfer have their own source gates in [`film-treatment-proposal.md`](film-treatment-proposal.md).
