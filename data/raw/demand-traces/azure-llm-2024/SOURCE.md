# Azure LLM inference trace 2024 (one week)

- **What:** request-level samples of two production LLM inference services in Azure (`code` =
  code completion, `conv` = conversation), May 2024. Columns: `TIMESTAMP` (UTC, microseconds),
  `ContextTokens`, `GeneratedTokens`. Code: 16.8 M requests (2024-05-10 to 05-17), conv: 27.3 M
  requests (2024-05-12 to 05-18).
- **URL:** https://github.com/Azure/AzurePublicDataset/blob/master/AzureLLMInferenceDataset2024.md
  (files from the `dataset-llm-2024` release). Retrieved 2026-11-21.
- **Licence:** CC BY 4.0 (https://github.com/Azure/AzurePublicDataset/blob/master/LICENSE). Cite:
  Stojkovic et al., "DynamoLLM: Designing LLM Inference Clusters for Performance and Energy
  Efficiency", HPCA 2025.
- **Not committed:** 1.8 GB raw. `python3 -I scripts/fetch_demand_traces.py --only azure2024`
  downloads it into `data/external/azure-llm-2024/` and writes
  `data/derived/demand/llm-azure2024-hour-of-week.csv` (+ rows in `summary.csv`,
  `llm-token-quantiles.csv`).
- **Time zone:** UTC, converted to Europe/Zurich (CEST, UTC+2 in May). `hour_utc` is kept.
- **Caveats:** user region and model are not disclosed. The `code` peak lies at 19-22 h Zurich
  time (= US working day), so the absolute-time profile describes a US-dominated user base; for a
  Swiss customer base re-index on user local time (an assumption, not in the data). One week
  only: each hour-of-week is a single sample, no seasonality. The 2023 trace (one hour, Nov 2023)
  was not used because it is too short for a daily shape.
