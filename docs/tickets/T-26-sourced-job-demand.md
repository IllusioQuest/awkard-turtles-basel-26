# T-26: Sourced job demand scenario and computing price

- **Tier:** 1 Draft  ·  **Area:** Data / model inputs
- **Depends on:** T-02 (measured throughput), T-05 (finite job queue)
- **Suggested owner:** Claude  ·  **Owner:** Team lead / Claude  ·  **Status:** in progress (two research subagents)

## Goal

Replace "unlimited jobs" and the example computing price (0.30 CHF per server-hour) with sourced
values, so that "missed deadlines in % of jobs" and the computing income are real figures (must-haves 1 and 3).

## Why now

T-02 measured about 436 audio hours per 1 kW server-hour. Demand, not capacity, limits the business;
"unlimited jobs" hides that.

## Tasks

- [x] Research a sourced transcription demand scenario: job size (audio minutes per job), deadline
      (turnaround), arrivals per day for a realistic customer base; low/high range.
      → `config/job-scenarios/transcription-basel.json`, `docs/research/job-demand.md` (branch `data/job-demand-scenario`).
- [x] Research and recommend how to price computing income (per server-hour vs. per audio hour with demand cap),
      with value and sensitivity range → `docs/research/computing-price.md` (branch `data/computing-price`).
- [ ] Team lead decides; then hand the values to T-06 (Jona) for the scored figures and update `config/reference-house.json` via T-06.

## Done when

- Missed deadlines and computing income in the reference-house results come from sourced inputs; the default scenario choice is agreed with the team.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Results (2026-11-21, merged to main)

- **Demand** (`docs/research/job-demand.md`, `config/job-scenarios/transcription-basel.json`): all
  1,269 practising doctors in Basel-Stadt dictating into one house = 428 jobs/day (6.5 audio min,
  24 h deadline). Result: 0 % missed deadlines, but 39 server-hours/year, utilisation 0.11 %,
  heat coverage 0.1 % (vs. 58.0 % with unlimited jobs). Throughput ≈ 436 audio h per 1 kW
  server-hour (T-02) makes transcription far too light to make heat on its own. Sources partly from
  search snippets; verify before the pitch.
- **Computing price** (`docs/research/computing-price.md`): recommended CHF 0.66 per server-hour
  (Hetzner-equivalent; spot route 0.68), range 0.25–1.80. Transcription per audio hour is worth
  CHF 55–132 per server-hour but fills only 0.01–0.4 % of a house's capacity.
- **Team lead's decisions:** price **0.66**, adjustable (→ T-06 updates `config/reference-house.json`;
  automatic snapshotted update proposed in T-28). **Demand model: to be agreed with Ennio** (see below).

**Recorded 2026-11-21:** filler load sold as spot capacity is under consideration (`docs/vision.md`, section "Under consideration"); price 0.66 pinned, 0.687 live cross-check (T-28).

## Question for Ennio / Ennio's agent (T-02 owner)

The team lead asked to agree the demand model for the scored figures with you. Options:

1. **Premium + filler (Claude's recommendation):** transcription with the sourced Basel demand as
   premium load, plus an interruptible filler load sold as capacity at the market price (0.66 CHF/h)
   that delivers the heat. Needs a small extension of the T-05 queue (finite + unlimited job types
   in one scenario) → coordinate with Jona.
2. **Heavier transcription:** a larger Whisper model (better quality, much more compute per audio
   minute). Can you measure `small`/`medium` on your laptop with the T-02 script? That changes the
   throughput and therefore how much of the capacity real demand can fill.
3. **Only sourced transcription** (coverage 0.1 %, honest "demand is the bottleneck" story).

Please write your answer here or in `docs/status.md` (section "Open coordination").

**Ennio's answer (2026-11-21):** option 1, as two job types: **Type 1** sovereign AI services
(transcription, local LLM T-29, later OCR/indexing) served first, **Type 2** interruptible batch
compute (Monte Carlo, simulations) as the heat-producing filler, priced by free capacity. Recorded
as decision 0015 (proposed, team confirmation pending). Scored figures keep the pinned 0.66 and
state "Type 2 demand unlimited"; follow-ups T-30 (Type 2 job), T-31 (mixed queue), T-32 (heat-led price).
