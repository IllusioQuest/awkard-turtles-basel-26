# 0015: Two job types: sovereign AI services (Type 1) and interruptible batch compute (Type 2)

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Ennio (team member); team confirmation pending

## Context

T-02 measured whisper.cpp at 0.0382 Wh per audio minute (`base`, whole laptop) and 0.477 Wh
(`large-v3`, CPU package), i.e. about 436 and 27–35 audio hours per 1 kW server-hour
(`docs/jobs/transcription.md`, PR #15). The reference house sells about 22,661 server-hours a year
and would need 0.6 million (`large-v3`) to 9.9 million (`base`) audio hours to fill them; the
sourced Basel scenario fills 0.1 % of the heat demand (`docs/research/job-demand.md`). **Demand,
not heat or compute, is the bottleneck.** `docs/vision.md` already lists "filler load sold as spot
capacity" as under consideration (commit 31c5e9c); T-26 asks Ennio to choose the demand model.

## Decision

Ennio's answer to T-26 (option 1, made concrete): DigitalHeat sells **two job types**.

- **Type 1, sovereign AI services:** transcription (T-02) and local LLM inference (T-29, Apertus 8B
  via llama.cpp; no measurement yet), later OCR and document indexing. Marketed as local, Swiss,
  data stays in the canton/house (placement policy per decision 0013). Priced per work unit,
  served first, has deadlines. **Earns the margin.**
- **Type 2, interruptible batch compute:** whenever heat is needed and Type 1 work is short, the
  remaining capacity is sold for simulations and research computations (Python/C++, Monte Carlo,
  parameter sweeps). Priced per server-hour, **dynamically, by how much free capacity we have**.
  **Makes the heat.**

Conditions attached by the review below (part of the decision):

1. **Type 2 is a work-unit task queue, not a VM rental:** customers submit many short, idempotent
   tasks in an approved container runtime; a task can be paused (`docker pause`, decision 0009) or
   dropped and re-queued at any time, with no deadline guarantee. Small input/output data only.
2. **Isolation:** Type 2 code runs sandboxed, without access to the home network or the internet
   (only the job broker), and never on a server that holds Type 1 data at the same time. The
   Type 1 privacy claim is only made with this separation in place.
3. **Pricing follows the heat signal:** the free-capacity price has a floor at the marginal cost
   (CHF 0.226 per server-hour when the heat is used, 0.3325 when it would be dumped,
   `docs/research/computing-price.md` §2) and a ceiling at the cheapest Swiss spot equivalent
   (CHF 1.80). **Scored figures keep the pinned CHF 0.66 per server-hour** and state "Type 2 demand
   unlimited" (team-lead price decision, `computing-price.md`); dynamic pricing is a variant until
   a demand response has a source (`TODO(source)`).
4. Finance Monte Carlo only with non-confidential or synthetic inputs; regulated bank workloads
   are out of scope (FINMA outsourcing audit burden, `research/service-brainstorm.md` #12).

## Alternatives considered

- **Type 1 only** (T-26 option 3): honest, but 0.1 % heat coverage fails the case's main ranking.
- **Heavier Type 1 jobs** (T-26 option 2): `large-v3` measured; still 0.6 million audio hours a year
  needed. Kept as the default Type 1 quality level, not as the heat source.
- **Sell Type 2 through an existing home-container marketplace** (e.g. SaladCloud, buyer price
  CHF 1.19–1.39 per server-hour, host payout `TODO(source)`): lower sales risk, less margin;
  remains an option for the Type 2 channel.

## Consequences

- Heat coverage and utilisation can reach the unlimited-jobs comparison (58.0 %, 0 kWh dumped,
  4 servers) **only if Type 2 demand exists**; the claim moves from "transcription demand" to
  "buyers of interruptible home CPU", which is still unsourced (`computing-price.md` §6, risks 1 and 3).
- Margin at the pinned price is thin: CHF 0.66 vs. ≈ 0.59 full cost at 64.7 % utilisation. Type 2
  roughly covers its costs; Type 1 provides the profit. Pitch line: *"Type 1 pays, Type 2 heats."*
- Needs: a mixed queue in the year simulation (finite Type 1 + unlimited Type 2, T-31), a measured
  Type 2 job with sandbox (T-30), and a heat-led price model (T-32).
- Summer: Type 2 does not fix low summer utilisation; heat need, not demand, limits it there.

## Review

Not reviewed by a fresh-context subagent: before the draft, AGENTS.md §8 asks for reviews only of
hard-to-reverse decisions; this one changes no interface or scored figure. A critical rating was
done by a Claude agent (2026-11-21) at Ennio's request: **7/10, sound with conditions.**

- **Strengths:** fills exactly the gap T-02 measured; Type 2 tasks (Monte Carlo, sweeps) are the
  best possible heat fit (embarrassingly parallel, cheap to pause, tiny data); the split
  "Type 1 pays, Type 2 heats" is simple to pitch and consistent with the pinned 0.66 price.
- **Weaknesses:** no sourced buyer for SLA-free, weather-driven home CPU; the Type 1 data-safety
  claim conflicts with running third-party code on the same machines; dynamic pricing "by free
  capacity" without a floor can sell below marginal cost or encourage dumping heat.
- **Changed because of it:** conditions 1–4 above; dynamic pricing tied to the heat signal and kept
  out of scored figures; tickets T-30 to T-32.
