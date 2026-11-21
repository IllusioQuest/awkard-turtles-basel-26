# GetDeploying GPU rental price history (weekly)

- **File:** `weekly-2026-11-21.csv`, unchanged download of
  https://getdeploying.com/dataset/gpu-prices/weekly.csv, retrieved 2026-11-21 (17,197 rows,
  115 GPU models, 99 providers, weeks 2025-05-05 to 2026-11-16; most models from 2025-11-17).
- **Licence:** CC BY 4.0. Attribution: GetDeploying (2026). GPU rental price history [dataset].
  https://getdeploying.com/dataset/gpu-prices. Accessed November 21, 2026.
- **Columns:** `gpu_slug`, `date` (Monday of the week), `billing_type` (ON_DEMAND, SPOT,
  RESERVATION, ...), `reservation_months`, `min_price`, `max_price`, `median_price`,
  `provider_median_price` (median of each provider's median, so every provider counts once),
  `provider_count`, `offering_count`. Prices in USD per GPU per hour.
- **Derived:** `python3 -I scripts/fetch_demand_traces.py --only gpuprices` writes
  `data/derived/demand/gpu-price-history.csv` (8 GPUs, on-demand vs spot, CHF at 0.8402).
- **Caveats:** listings of public price pages, not transaction prices; provider coverage changes
  week to week (a jump can be a provider entering); the publisher says history before 2026 was
  partial and manually updated; the latest week may be partial. To refresh, download a new dated
  file next to this one; never overwrite.
