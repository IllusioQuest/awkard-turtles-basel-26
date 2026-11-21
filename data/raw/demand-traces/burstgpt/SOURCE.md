# BurstGPT (ChatGPT / GPT-4 workload trace)

- **What:** 121 consecutive days of requests to Azure OpenAI GPT-3.5 and GPT-4 from one campus
  service, as conversation log or API log. Files used: `BurstGPT_without_fails_1.csv` and `_2.csv`
  (release v2.0, 5.19 M requests). Columns: `Timestamp` (seconds from 0:00 of day 1, "calibrated
  to the local time zone"; file 2 continues file 1's clock), `Model`, `Request tokens`,
  `Response tokens`, `Total tokens`, `Log Type`.
- **URL:** https://github.com/HPMLL/BurstGPT (release https://github.com/HPMLL/BurstGPT/releases/tag/v2.0).
  Retrieved 2026-11-21.
- **Licence:** CC BY 4.0 (repository licence). Cite: Wang et al., "BurstGPT: A Real-World Workload
  Dataset to Optimize LLM Serving Systems", KDD 2025.
- **Not committed:** ~190 MB. `python3 -I scripts/fetch_demand_traces.py --only burstgpt` writes
  `llm-burstgpt-hour-of-day.csv`, `llm-burstgpt-day-index-mod7.csv`, `llm-burstgpt-daily.csv`.
- **Time zone:** source local clock (zone not named; the authors are in Guangzhou, so probably
  UTC+8, unverified). Hour-of-day is transferred to the Zurich local clock without shift
  (assumption: same working-day rhythm). The weekday of day 1 is not published, so the weekly
  file uses `day_index_mod7` (0 = first day of the trace, weekday unknown); days 4-5 are the low
  pair and probably the weekend, but this is an inference.
- **Caveats:** one user population (a university service), GPT-3.5/4 era (2023); interactive
  traffic, not batch.
