# 0018: Two job types: sovereign AI services (Type 1) and batch compute filler (Type 2)

- **Status:** proposed; hardware split ("Type 2 on CPU") superseded by 0019; Type 1 contents
  (transcription) and Type 2 example (Monte Carlo) superseded by 0020
- **Date:** 2026-11-21
- **Decided by:** Ennio; team confirmation pending. This is the job model inside business model v2 (T-34).

## Context

Measured on a laptop (T-02, T-33):

| Job | Energy per unit (CPU) | Units per 1 kW server-hour |
|---|---|---|
| whisper.cpp `base` | 0.0382 Wh per audio min | ≈ 436 audio h |
| whisper.cpp `large-v3` | 0.477 Wh per audio min | ≈ 27–35 audio h |
| Apertus 8B summary | 0.0614 Wh per audio min summarised (0.254 kWh per 1M tokens) | ≈ 3.9 M tokens |

The sourced Basel transcription demand covers about 0.1 % of the reference house's heat demand
(`docs/research/job-demand.md`). Premium AI work alone cannot make the heat; a filler is needed
(T-34, "filler as spot capacity").

## Decision

- **Type 1: sovereign AI services.** Mainly **transcription** (T-02) and a **batch LLM service**
  priced per token (T-33, Apertus 8B). OCR and document indexing come later. Sold as local and
  Swiss and served first. **Hardware:** Type 1 runs on GPU servers in the pitch scenario (T-15,
  T-34). The scored figures use the CPU values measured on the laptop.
- **Type 2: batch compute filler** (originally "CPU"; superseded by decision 0019: Type 2 runs on the
  same GPU nodes as Type 1). The capacity left over whenever heat is needed is sold
  for simulations and research computing: Monte Carlo (e.g. finance, physics), parameter sweeps,
  Python/C++ batch jobs. It is sold through **our own job broker**, as a queue of short, pausable
  tasks.
- **Type 2 price is heat-led:** cheap when the houses need heat and little Type 1 work is queued,
  dearer when capacity is scarce. The floor is the electricity cost (CHF 0.226 per server-hour with
  the heat used, 0.3325 without, `docs/research/computing-price.md`). The ceiling is the Swiss spot
  equivalent (CHF 1.80). The scored figures keep the pinned CHF 0.66.
- **Fill as much as possible, no unpaid work.** Hours that no customer buys are not run; the backup
  heater covers them. Running unpaid would cost CHF 0.33 of electricity per kWh of heat worth
  CHF 0.11. The scored figures assume all offered hours are sold (the case allows "unlimited
  jobs"). The sourced marketplace fill rate of 0.35 (range 0.2–0.6, `docs/research/filler-demand.md`)
  is shown as a sensitivity.
- MVP scope: privacy and isolation between Type 1 and Type 2 are left for later. Webhosting will
  probably be dropped from the business model (Ennio's view; Nikita to confirm).

## Alternatives considered

- **Type 1 only:** about 0.1 % heat coverage.
- **A heavier Type 1 model** (`large-v3`): 16× the energy per audio minute, but still far too
  little demand to make the heat.
- **Selling Type 2 on a marketplace** (vast.ai, SaladCloud): demand without our own sales effort,
  but a lower margin and a sourced fill rate of only about 0.3.

## Consequences

- Pitch line: *"Type 1 pays, Type 2 heats."*
- The year simulation needs a mixed queue (T-31). Type 2 needs a measured job (T-30) and the price
  curve (T-32).
- At CPU commodity prices the batch LLM service earns about CHF 0.25–0.50 per server-hour
  (T-33), about the same as Type 2. Type 1 earns its margin only on GPU or at a Swiss premium.

## Review

No fresh-context review: no interface or scored figure changes. A critical rating (2026-11-21,
Claude agent, confirmed by a second agent) gave **7/10**. Type 2 fills exactly the gap that T-02
measured, and Monte Carlo work fits heat-led running well. The open point is whether Type 2 buyers
exist at the scale we assume.
