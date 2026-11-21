# Compute demand traces and time-dependent prices (T-47)

First version, 2026-11-21, Claude subagent. Covers the three job types: (1) batch LLM inference
with an 8B model (Apertus 8B), (2) Whisper large-v3 transcription, (3) research GPU/CPU compute.
For each, the question is **price** and **demand**, both over time where a source allows it.
This is collected data and a few aggregates, not a model.

Currency: USD→CHF 0.8402 and EUR→CHF 0.9233, the same rates as the rest of `data/raw/market/`
(`docs/research/ai-services.md`). Prices in CSV rows keep the published value and unit; the
CHF figure is in `normalised_value`.

## 1. What was added

| Where | What | Rows / size |
|---|---|---|
| `data/raw/market/ai-services.csv` | 8B-class LLM token prices (OpenRouter endpoints API, Public AI, kvant AI, Infomaniak), batch discounts (OpenAI, Anthropic, OpenRouter), open-weight blended price Jan vs Oct 2026, transcription (OpenAI whisper-1 / 4o-transcribe, Together Whisper, Speechmatics, Infomaniak and kvant Whisper in Switzerland) | +24 |
| `data/raw/market/compute-prices.csv` | vast.ai on-demand vs interruptible medians (own API query, 6 GPUs), Together H100 preemptible vs on-demand, Lambda (official page), Hetzner dedicated GPU, CSCS GH200 node-hour 2026, PSI share, EPFL SCITAS, GetDeploying latest on-demand vs spot medians | +30 |
| `data/raw/market/compute-demand.csv` | OpenRouter token volume (Oct 2026 month; Jan 2025 week), CSCS Clariden GPU count, PSI node-hours, the trace peak/trough figures below, Doodle transcription proxy | +11 |
| `data/raw/demand-traces/*/SOURCE.md` | One per source: Azure LLM 2024, BurstGPT, Alibaba GPU 2020, GetDeploying GPU prices (raw CSV committed, CC BY 4.0), transcription proxy | 5 sources |
| `data/derived/demand/` | Aggregates from `python3 -I scripts/fetch_demand_traces.py` | 9 files, ~120 KB |

Large raw traces (~2 GB) are downloaded to the git-ignored `data/external/` by the same script.

## 2. Method

`scripts/fetch_demand_traces.py` (stdlib only, deterministic; token quantiles use a fixed every
20th / 10th row sample) does, per source:

1. **Azure LLM 2024** (two services, one week, UTC): count requests, input and output tokens per
   UTC hour; convert the hour to Europe/Zurich (May = CEST, UTC+2); index by hour-of-week
   (Mon 00:00 = 0). Each of the 168 hours is one sample.
2. **BurstGPT** (121 days, source local clock): requests per local hour-of-day, per day, per
   `day_index mod 7`, per model and log type; burstiness from requests per minute.
3. **Alibaba GPU 2020** (9.9 weeks): successful tasks with a GPU request; arrival = task start
   read as UTC+8 local clock (README rule); duration = end − start; GPUs = `inst_num × plan_gpu/100`.
4. **GetDeploying** (committed weekly snapshot): provider-median on-demand and spot price for 8
   GPU models, spot/on-demand ratio, 12-month change.

Every `*_rel` column is divided by its series mean, so **mean = 1.0**. Peak/trough = max/min of
that column.

**Time-zone rule.** Azure has true UTC timestamps, so its profile is in true Zurich time.
Alibaba and BurstGPT only give a local clock; we put that local clock onto Zurich's local clock
(assumption: people work at the same local hours everywhere). Because the Azure `code` service
peaks at 19-22 h Zurich, i.e. during the US working day, its absolute-time profile reflects US
users; re-indexing it to a Swiss user base would be an assumption (`hour_utc` is kept for that).

## 3. Results and how to read them

### Hourly demand shapes (hour-of-week or hour-of-day, mean = 1)

| Series | File | Peak / trough | Weekend / weekday | Notes |
|---|---|---|---|---|
| Azure LLM `code` (2024) | `llm-azure2024-hour-of-week.csv` | **33.1** (Wed 21 h 3.01 vs Sat 12 h 0.09) | 0.33 | interactive coding: strongly office-hours, US time |
| Azure LLM `conv` (2024) | same | **3.56** (Tue 16 h 1.66 vs Sun 06 h 0.47) | 0.56 | chat; closest public shape to API LLM traffic |
| BurstGPT, all (2023) | `llm-burstgpt-hour-of-day.csv` | **2.48** hour-of-day | ~0.71 (days 4-5, weekday unknown) | per-model series up to 60:1 (conversation log) |
| Alibaba GPU task arrivals (2020) | `research-alibaba2020-hour-of-week.csv` | **4.03** (Thu 00 h 1.91 vs Sat 23 h 0.47); 2.47 hour-of-day | 0.78 | peak at local midnight (scheduled jobs) |

Reading: a value of 1.31 in row `conv, hour_of_week_zurich=14` (Mon 14:00 Zurich, 12:00 UTC)
means 31 % more requests than the average hour. Worked example: that hour had 213,342 requests;
the weekly mean is 162,500 per hour; 213,342 / 162,500 = 1.31.

**For DigitalHeat:** batch LLM inference and batch research jobs are not interactive, so these
shapes describe when *customers submit* work, not when it must run. The ratio says how much a
deadline-free buyer could shift: interactive chat varies about 3.5:1 over a week, coding
33:1, research GPU arrivals 4:1. Burstiness (BurstGPT): per-minute CV 2.2, the 99th-percentile
minute is 10.5× the mean minute; a home server cannot absorb such bursts, only queued batch work.

### Job durations (research GPU compute)

`job-durations.csv`, Alibaba 2020, 732,700 GPU tasks: median **10 min**, p90 3.5 h, p99 19 h,
mean 1.5 h. But GPU-hours are dominated by long jobs: tasks over 12 h are 2.5 % of tasks and
**50 % of GPU-hours**; 4-12 h another 27 %. 38 % of tasks request less than one GPU, median 1 GPU,
p90 5 GPUs (`summary.csv`). So most *GPU-hours* of research-type training need multi-hour runs
with checkpointing, while most *jobs* fit into a short heat window.

### Request sizes (LLM)

`llm-token-quantiles.csv`: Azure `conv` median 932 input / 41 output tokens (p90 3,836 / 342);
Azure `code` 1,930 / 8; BurstGPT ChatGPT conversation 487 / 191. Use these to convert a
token price into a price per request.

### Prices over time

- **GPU rental** (`gpu-price-history.csv`, weekly, Nov 2025 to Nov 2026, provider medians):
  H100 on-demand +34 %, RTX 5090 +43 %, A100 +17 %, RTX 4090 −23 %, L4 −4 %. **Spot is 36-61 % of
  on-demand** (median over the weeks: L4 0.36, T4 0.40, A100 0.54, RTX 4090 0.58, H100 0.59).
- **LLM tokens:** OpenRouter's blended open-weight price fell from USD 0.28 to 0.084 per 1M tokens
  (January to October 2026, −70 %); closed-weight from 1.21 to 0.52.
- **Volume:** OpenRouter processed 790 T tokens in October 2026 (~25× January 2026).
- No source with **hourly** price variation was found; spot prices on vast.ai move continuously but
  no public history exists.

### Key price points (CHF, from the appended rows)

| Job type | Cheapest global | Swiss / sovereign | Notes |
|---|---|---|---|
| 8B LLM, per 1M tokens (in / out) | 0.017 / 0.034 (Llama 3.1 8B, DeepInfra); typical 0.04-0.17 | kvant Qwen3-8B 0.027 / 0.106; kvant Granite/Mistral 7-8B 0.154; Public AI Apertus 8B 0.084 / 0.168 (host location not stated); Infomaniak smallest LLM (14B) 0.30 / 0.40 | batch −50 % at OpenAI, Anthropic, Mistral; −28 % on OpenRouter `:batch` |
| Whisper transcription, per audio hour | 0.076 (Together Whisper large-v3) | 0.36 (Infomaniak Whisper V3; kvant whisper-large-v3) | OpenAI whisper-1 0.30; Speechmatics 0.38-0.63; Azure batch 0.15 (existing row) |
| GPU-hour, on-demand vs interruptible | vast.ai RTX 4090 0.43 vs 0.29; RTX 3090 0.20 vs 0.14; H100 2.05 vs 1.32 | CSCS GH200 **0.67** per GPU-hour (2.69 per 4-GPU node-hour, Swiss institutions 2026); EPFL 0.41 (snippet) | Lambda GH200 2.18; Together H100 preemptible 1.67 vs 3.35 |

Swiss academic GPU-hours at CSCS cost less than a third of the same chip at Lambda: for research
compute, the Swiss competitor is subsidised academic HPC, not the commercial cloud.

## 4. Gaps and caveats

- **Apertus 8B:** Artificial Analysis lists no current API provider; Public AI's table still lists
  it at USD 0.10 / 0.20. kvant offers only Apertus 70B. The CSCS inference API hosts Apertus 8B but
  only for projects with an allocation (no public price).
- **Swisscom** AI platform prices: not public (TODO(source)). kvant's credit = CHF 1 comes from a
  search snippet of its FAQ.
- **Transcription demand:** no public trace. Only a meetings proxy (Doodle: 12 % of Swiss meetings
  on weekends, 57 % of German meetings 12-18 h). No hourly file was built, to avoid invented hours.
- **Seasonality:** none of the traces covers a full year (Azure 1 week, Alibaba 10 weeks,
  BurstGPT 4 months). Annual seasonality of research demand (e.g. academic deadlines): TODO(source).
- **ETH Euler** prices per core- or GPU-hour: not found (TODO(source)). EPFL and PSI figures are
  search snippets (low).
- **Swiss sovereign cloud market size:** no study found; a Swiss sector report says no official
  size exists.
- **vast.ai** medians are a snapshot of one query (verified hosts, 1 GPU; few offers for L4, H100);
  mostly US hosts.
- Skipped for time: Microsoft Philly trace (0.98 GB), Alibaba job/instance tables, Azure 2023
  (1 hour only) and the 2025 multimodal trace.
