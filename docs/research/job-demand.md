# Job demand: a sourced finite scenario for transcription

> Status: **exploration, first version** (Claude agent, 2026-11-21). Not reviewed by the team.
> Scenario file: [`config/job-scenarios/transcription-basel.json`](../../config/job-scenarios/transcription-basel.json).
> The default reference-house run stays [`unlimited.json`](../../config/job-scenarios/unlimited.json).

The case asks "how many jobs arrive per day and where that number comes from" (must-have 1) and
"missed deadlines in % of jobs" (must-have 3). This note builds one finite job type, medical
dictation transcription, from cited parts and runs it through the existing year simulation
(decision 0010, [`docs/model/job-queue.md`](../model/job-queue.md)).

**How sources were read.** WebFetch and curl were blocked for every host tried in this session
(biomedcentral.com, happyscribe.com, grosserrat.bs.ch, ncbi.nlm.nih.gov). All external
figures below therefore come from **search-result snippets** of the named pages (confidence
`medium` where the snippet quotes the page directly, `low` otherwise). Each one has to be checked
against the full page before the pitch.

## 1. Assumption chain (base case)

| # | Quantity | Value | Source | Conf. |
|---|---|---|---|---|
| A1 | Primary-care physicians in practice, Basel-Stadt, Jan 2024 | 415 | Regierungsrat BS, report on Anzug Sarah Wyss, [grosserrat.bs.ch/dokumente/100406/000000406995.pdf](https://grosserrat.bs.ch/dokumente/100406/000000406995.pdf), accessed 2026-11-21 | medium |
| A2 | Share of all practising ("niedergelassene") physicians in BS that are primary care | 32.7 % | same document | medium |
| A3 | All practising physicians in BS = A1 ÷ A2 | **1,269** | derived | — |
| A4 | Median dictation per physician and month | 66.71 audio min | Bax et al. 2011, "The effect of seniority and education on departmental dictation utilization", *Health Econ Rev* 1:8, pediatric academic centre, London (Ontario), [PMC3402968](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3402968/), accessed 2026-11-21 | medium |
| A5 | Median length of one dictation (one letter) | **6.5 audio min** | same paper | medium |
| A6 | Months per day | 12 / 365 | calendar | — |
| A7 | Throughput of the 1 kW reference server | **26,178 audio min / server-hour** | own measurement, T-02 (branch `feat/T-02-transcription-job`, `docs/jobs/transcription.md`): 1000 Wh ÷ 0.0382 Wh per audio minute, whisper.cpp `base`, whole-laptop gross; assumes the same energy per audio minute on the server | measured, one laptop |
| A8 | Deadline | **24 h** | Rev standard human transcription: estimate shown at checkout, maximum 24 h ([support.rev.com/…/18859196207629](https://support.rev.com/hc/en-us/articles/18859196207629)); GoTranscript one-day tier next to 3-day and 5-day tiers ([sonix.ai/resources/gotranscript-pricing](https://sonix.ai/resources/gotranscript-pricing/)), both accessed 2026-11-21 | medium / low |
| A9 | Interruptible | yes | T-02 pause experiment (`docker pause`, identical transcript) | measured |

**Customer-base assumption (ours, not sourced):** *all* medical dictations of *all* practising
physicians in canton Basel-Stadt go to **one** reference house. This is deliberately generous: it
is an upper bound for what one house could plausibly be offered in Basel, not a market forecast.
Hospital physicians, law firms, notaries and municipalities are **not** included (no published
per-person dictation volume was found for them; the BS lawyer register exists as a PDF list,
[bs.ch Anwaltsregister](https://www.bs.ch/gerichte-judikative/ueber-die-gerichte/anwaltsaufsichtskommission/anwaeltinnen-und-anwaelte),
but was not counted).

### Calculation

```
physicians            = 415 / 0.327                        = 1,269
audio per day         = 1,269 × 66.71 min/month × 12/365   = 2,783 audio min/day
jobs per day          = 2,783 / 6.5 min/job                = 428.2  → 428 jobs/day (integer, rounded down)
server-hours per day  = 428 × 6.5 / 26,178                 = 0.106 server-hours/day
server-hours per year = 0.106 × 365                        = 38.8 server-hours/year
```

The scenario format supports one integer arrival count per local day, the same every day, arriving
at 00:00. The real pattern is weekdays only (practices are closed at weekends); spreading the
monthly median over all 365 days keeps the yearly volume right but hides the weekday peak. With a
24 h deadline and demand this small, the weekday peak would not change the result.

## 2. Sensitivity range (described, also run as scratch variants)

| Case | Customer base | Audio per person | Jobs/day | Server-h/year |
|---|---|---|---|---|
| Low | 415 primary-care physicians BS (A1) | 66.71 min/month (A4) | 140 | 12.7 |
| **Base** | 1,269 practising physicians BS (A3) | 66.71 min/month (A4) | **428** | **38.8** |
| High | 42,602 working physicians in Switzerland, all sectors (FMH Ärztestatistik 2024, [fmh.ch report2025 poster](https://report2025.fmh.ch/files/pdf33/fmh-aerztestatistikposter-2025_de.pdf), total from a search snippet, low) | 30 min/day (vendor-linked trade article, [PracticeLink 2011](https://www.practicelink.com/magazine/departments/physician-tech-notes/emdat-transcription-gone-high-tech-spring-2011), low), taken per calendar day | 196,624 | 17,820 |

The high case is an extreme stress test: every physician in Switzerland, dictating 13× the
measured median, all sent to one house. It is not a plausible market for one house; it shows what
it would take to fill the servers. Deadline variants (6 h rush, 3 or 5 working days) are listed by
the same providers; they were not run, because in the base case no job comes close to its deadline.

Not committed: the low and high variants were run from scratch copies of the base file with only
`arrivalsPerDay` (and for one run the policy) changed.

## 3. Results (reference house, 4 servers, 2025 Basel weather)

Command:

```bash
dotnet build src/DigitalHeat.sln
dotnet run --project src/DigitalHeat.YearRun -- .                                                # unlimited
dotnet run --project src/DigitalHeat.YearRun -- . config/job-scenarios/transcription-basel.json  # base
```

Output: `results/reference-house/summary.json` (not committed, overwritten by each run).

| Figure | Unlimited (default) | Low | **Base** | High, wait | High, run-and-dump |
|---|---|---|---|---|---|
| Jobs arrived / year | — | 51,100 | **156,220** | 71,767,760 | 71,767,760 |
| Completed | — | 51,100 | **156,220** | 52,200,396 | 71,767,760 |
| Missed deadlines | not modelled | 0 % | **0 %** | **27.3 %** | 0 % |
| Backlog at year end | — | 0 | **0** | 0 | 0 |
| Server-hours | 22,661 | 12.7 | **38.8** | 12,961 | 17,820 |
| Utilisation | 64.7 % | 0.04 % | **0.11 %** | 37.0 % | 50.9 % |
| Coverage of heat demand | 58.0 % | 0.03 % | **0.10 %** | 33.2 % | 33.1 % |
| Server heat used (kWh) | 18,129 | 10 | **31** | 10,369 | 10,325 |
| Heat dumped (kWh) | 0 | 0 | **0** | 0 | 3,931 |
| Backup heat (kWh) | 13,102 | 31,220 | **31,200** | 20,861 | 20,905 |
| Electricity cost (CHF) | 7,535 | 4.2 | **12.9** | 4,310 | 5,925 |
| Computing income at 0.30 CHF/server-h (CHF) | 6,798 | 3.8 | **11.6** | 3,888 | 5,346 |
| Case break-even (CHF/server-h) | 0.226 | 0.226 | **0.226** | 0.226 | 0.255 |

Total heat demand is 31,231 kWh in every run. The break-even stays at 0.226 CHF/server-hour under
`wait` because, with no dumped heat, each server-hour saves the same heat value; it rises only when
heat is dumped.

## 4. What it means

- **Demand, not heat, is the bottleneck.** The base case needs 39 server-hours per year; the house
  could use 22,661 under the unlimited comparison. Even all practice dictations of Basel-Stadt
  fill **0.17 %** of that. Coverage drops from 58 % to 0.1 %, and the backup heater supplies almost
  everything. This is a real business result, not a model error: a CPU running `base` Whisper is
  about 436× faster than real time per kW, so one house can transcribe far more audio than a city's
  doctors produce.
- **What it would take:** filling the unlimited 22,661 server-hours needs
  22,661 × 26,178 / 365 ≈ **1.63 million audio minutes per day** (≈ 27,000 audio hours/day), the
  median dictation volume of about **740,000 physicians**, 17× all physicians in Switzerland.
- **Missed deadlines appear only when demand is large.** In the high case under heat-led `wait`,
  27 % of jobs miss the 24 h deadline. The cause is days on which heat-led runtime falls short of
  the 48.8 server-hours of work arriving daily: outside the heating season only hot water draws
  heat, 12 kWh/day ÷ 0.8 kWh per server-hour = 15 server-hours/day. (The run reports yearly
  totals only; per-day misses are not in the output.)
  Running and dumping heat removes the misses but dumps 3,931 kWh and raises the break-even to
  0.255 CHF/server-hour. That is the core trade-off between deadlines and dumped heat.
- **The server-hour price undervalues this job.** At the model's 0.30 CHF/server-hour, the base
  case earns CHF 11.6/year. The same 16,924 audio hours priced at the batch market rate of
  CHF 0.13–0.30 per audio hour ([`ai-services.md`](ai-services.md)) would be worth about
  CHF 2,200–5,100/year. This is an illustration only; billing per audio hour is a business-model
  question for the team.
- **Consequences for the pitch (for the team to decide):** either (a) report the unlimited run as the
  scored comparison and this scenario as the honest demand check; (b) pick heavier jobs per unit
  of value (larger Whisper models, GPU-free OCR, embeddings over archives) so the same audio needs
  more server-hours; or (c) pool many houses' servers behind one large customer base and route jobs
  to the house that needs heat (T-10/T-12).

## 5. Main uncertainties

1. **Throughput (A7) dominates everything.** It is one laptop, `base` model, read-aloud standard
   German, scaled to 1 kW by equal energy per audio minute. A larger Whisper model is slower per
   audio minute (not measured here) and would raise server-hours proportionally; to reach even
   1 % utilisation in the base case (350 of 4 × 8,760 = 35,040 server-hours) it would need to be
   about 9× slower (350 / 38.8).
2. **Dictation volume (A4, A5)** comes from one North American pediatric centre (2009) and was read
   from an abstract snippet. Swiss practice volumes may differ, and speech recognition at the
   practice already replaces part of dictation.
3. **Physician count (A1–A3)** is derived from two figures in a cantonal report snippet; the true
   number of practising physicians in BS should be checked in the full report or the FMH statistics.
4. **Deadline (A8)** is a human-transcription service level, not a contract we have.
5. **Customer base** (one house serves the whole canton) is our assumption and generous by design.
