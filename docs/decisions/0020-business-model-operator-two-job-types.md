# 0020: Business model (provisional): operator model with two job types

- **Status:** proposed (provisionally fixed by Jona on 2026-11-21; team confirmation pending)
- **Date:** 2026-11-21
- **Decided by:** Jona (IllusioQuest). Builds on decisions 0018 and 0019 and partly supersedes 0018
  (contents of Type 1). Closes the ownership question left open in `docs/vision.md`.

## Context

The team needs two concrete job types for must-have 1 and one business model for the pitch.
Three findings from 2026-11-21 narrowed the options:

- **Transcription has very little demand.** All Basel physician dictation is about 546 server-hours a
  year; the reference house offers 22,661 (`docs/research/profitability-inputs.md` §4). Dictations are
  also health data. Home servers cannot easily meet the secrecy rules for health, legal and bank data
  (`docs/research/filler-demand.md` §1, revDSG Art. 8/9).
- **Webhosting** was found not to be legally viable in the form the team tested (team finding,
  2026-11-21; the details are `TODO(source)` from Nikita's T-23/T-36 work).
- **No job type earns a premium just by running in a home.** Heat-led capacity is interruptible, and
  personal data must stay out. So buyers pay commodity prices, not premiums for reliability,
  latency or trust (`filler-demand.md`, `competitors.md`). The home's advantage is cost: no cooling,
  no building, and the used heat is worth 0.8 kWh × CHF 0.133/kWh = CHF 0.106 per full-load
  server-hour (both inputs from `docs/case/key-figures.md`).

The case's scoring does not reward high income. It asks under which prices the model pays off
(`docs/case/challenge.md`, "Business value" and must-have 3).

## Decision

1. **Ownership: operator model.** DigitalHeat buys, owns and operates the servers. It pays their
   electricity and sells the computing. The host (homeowner) invests nothing and receives the
   recovered heat. What the host pays for that heat (free, or a discount on its heat-pump cost) is an
   open parameter for T-14. The backup heater stays the host's.
2. **Type 1: batch LLM service on non-personal documents** (Apertus 8B, llama.cpp; T-33). It has
   deadlines (for example 24 h), is priced per token and is served first. Example work: summarising
   or indexing public documents, evaluating open models, generating synthetic data. Personal,
   health, legal and bank data are excluded on home sites.
3. **Type 2: research simulations** as the filler. The work is non-personal and interruptible, and it
   resumes from a checkpoint or at task boundaries. It is CPU or GPU batch work with long or no
   deadlines. It fills the heat-led hours that Type 1 leaves free, on the same nodes (decision 0019),
   and is priced as a contribution above the case break-even of CHF 0.226 per server-hour. The
   scored figures keep the pinned CHF 0.66 (decision 0018). Unsold hours are not run. The measured
   representative is a Monte Carlo research kernel (T-30): cheap to build, safe for the demo,
   checkpoints at task boundaries. Codes such as GROMACS are named only as examples of what customers
   would run, because installing them on the team's laptops is a time risk.
4. **Out of the business model:** transcription as a product (personal data) and webhosting (legal
   finding). The measured transcription job (T-02) stays in the repository as a working demo job
   and live-demo fallback, run with public audio only.

**Must-have 1 per job type:**

| | Type 1: batch LLM | Type 2: research simulation |
|---|---|---|
| Work unit | one document: 3,310 input + 319 output tokens (Apertus 8B, `docs/jobs/llm-summary.md`, branch `feat/T-33-local-llm-job`) | one Monte Carlo task (T-30) |
| Runtime, laptop | 133.3 s per document (mean of 3 runs) | `TODO(source)`: T-30 measurement |
| Energy, laptop | 0.92 Wh per document (0.0614 Wh × 14.98 audio min, CPU package) | `TODO(source)`: T-30 |
| Scaling to 1 kW | 1,000 Wh ÷ 0.92 Wh ≈ 1,090 documents per server-hour (assumes energy scales linearly; T-33 method) | same method, T-30 |
| Interruption | freeze/resume of the container (decision 0009, LiveRun) | checkpoint at task boundaries |
| Deadline | 24 h (our assumption) | none (a long one may be set in T-30) |
| Arrivals per day | **unlimited jobs** (case-permitted), no sourced volume yet | **unlimited jobs** (case-permitted); fill rate 0.35 (0.2–0.6) as sensitivity (`filler-demand.md` §1) |

**Value proposition in one line:** a home is the cheapest room for patient computing, because its
heat is needed anyway.

## Alternatives considered

- **Host-owned servers** (host buys, DigitalHeat sells the computing for a commission). This needs
  a homeowner to invest about CHF 16,300 per kW (RTX 4090 profile) (`profitability-inputs.md` §2) with no precedent
  (`creative-ideas.md`, lever F). Kept as a variant in T-14, not the pitch model.
- **Transcription as Type 1 (decision 0018).** Too little demand, and personal data cannot go to
  homes.
- **Searching further for a high-premium use case.** The research found none that fits home sites
  (see Context). We compete on cost and say so.

## Consequences

- **Demand is sufficient for a pilot but not for scale.** sciCORE, the research computing centre of
  the University of Basel, uses about 79,920 server-hours a year (energy basis,
  `profitability-inputs.md` §4), 3.5 times one reference house. A network of houses needs anchor
  customers. We say so in the pitch and in the "does not pay off" statement.
- **Sources still missing:** sourced arrivals per day for both types ("unlimited jobs" until then), a
  market price per server-hour for research computing (sciCORE's own price is `TODO(source)`), and
  the webhosting legal finding (cite T-23/T-36 before the pitch).
- **This decision removes the secrecy premium.** Decision 0019 (4) expected the Type 1 premium partly
  from medical or legal secrecy, which homes now exclude. Non-sensitive batch LLM work competes with
  batch APIs at about −50 % of list price. Type 1 must therefore be priced at batch-API level unless
  fixed multi-year contracts are signed. At that price it pays only if the optimistic batched
  throughput holds (`profitability-inputs.md` §3 and §6, open check 1). Over 6 years nothing pays at
  commodity prices (decision 0019 (e)). Other limits stay as before: full-cost break-even CHF 0.956
  per sold server-hour on the RTX 4090 profile, and the GeForce licence risk. `docs/business-model.md`
  states all of this.
- **Tickets.** T-30 measures a Monte Carlo research kernel instead of option pricing. T-31 mixes Type 1 (LLM)
  with Type 2 instead of transcription. T-14 uses the operator model as its main case. T-23/T-36
  (webhosting) are no longer part of the pitch model.
- **Scored reference-house figures are unchanged** (abstract 1 kW server, the case formula).

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes.** It confirmed the
cited figures (546 vs 22,661, CHF 16,300/kW, 79,920 = 3.5×, 0.956, 0.226). Changes made because of it:

1. "Profitability unchanged" was not honest: excluding sensitive data removes the secrecy premium
   from 0019. Now stated, also in `business-model.md` under "does not pay off".
2. Added the must-have 1 table per job type (unit, runtime, energy, scaling, interruption, deadline,
   arrivals). Type 2 numbers stay `TODO(source)` until T-30.
3. The CHF 0.106 derivation is shown, and the pinned CHF 0.66 for scored figures is stated.
4. Adopted its simpler alternative: a Monte Carlo kernel is the measured Type 2 job; GROMACS is only
   an example of customer work.
