# Data

All input data for the project and how to get it again from the original source.
Decision record: [`docs/decisions/0001-data-storage.md`](../docs/decisions/0001-data-storage.md).

## Layout

```
data/
  raw/        source data, unchanged, small enough to commit; each source has a SOURCE.md
  derived/    produced by scripts from raw/ (reproducible); never edited by hand
```

| Path | What it is | How to get the original | Committed? |
|---|---|---|---|
| [`raw/open-meteo/`](raw/open-meteo/SOURCE.md) | Hourly weather Basel 2025: temperature + solar irradiance | `python3 scripts/fetch_data.py` (Open-Meteo archive API, no login) | yes (~300 KB) |
| [`raw/elcom/`](raw/elcom/SOURCE.md) | Electricity price 2026, IWB Basel, category H4, incl. price components | `python3 scripts/fetch_data.py` (ElCom GraphQL API) | yes (snapshot) |
| `raw/market/` | Researched prices (AI services, compute, costs, energy), one CSV per topic | collected by hand, schema in [`docs/research/README.md`](../docs/research/README.md) | yes |
| [`raw/market/price-snapshots/`](raw/market/price-snapshots/SOURCE.md) | Dated Azure Retail Prices snapshots, Switzerland North VM sizes for the computing price (T-28); never overwritten | `python3 scripts/fetch_data.py --prices` (Azure Retail Prices API, no login) | yes (~12 KB each) |
| [`raw/market/hardware.csv`](raw/market/SOURCE.md) | Hardware power, GPU card price and Whisper throughput benchmarks for the T-15 hardware profiles (`config/hardware/`) | collected by hand, see `raw/market/SOURCE.md` (T-15) | yes |
| `raw/sites/` | Researched site types and heat-demand profile values; [`when2heat/`](raw/sites/when2heat/SOURCE.md): hourly CH heat profiles 2022 (extract) | collected by hand, see `docs/research/README.md` (WP3); extract command in its `SOURCE.md` | yes (~1 MB) |
| [`raw/demand-traces/`](raw/demand-traces/) | T-51 demand shapes: one `SOURCE.md` per trace ([Azure LLM 2024](raw/demand-traces/azure-llm-2024/SOURCE.md), [BurstGPT](raw/demand-traces/burstgpt/SOURCE.md), [Alibaba GPU 2020](raw/demand-traces/alibaba-gpu-2020/SOURCE.md), [transcription proxy](raw/demand-traces/transcription-proxy/SOURCE.md)); aggregates in `derived/demand/` | `python3 -I scripts/fetch_demand_traces.py` (downloads ~2 GB into `data/external/`, git-ignored) | SOURCE.md only; aggregates yes (~120 KB) |
| [`raw/demand-traces/getdeploying-gpu-prices/`](raw/demand-traces/getdeploying-gpu-prices/SOURCE.md) | Weekly GPU rental price history (on-demand, spot, reserved), 115 GPU models, CC BY 4.0 | download URL in its `SOURCE.md`; new dated file per refresh | yes (1.1 MB) |
| [`raw/housedata/`](raw/housedata/SOURCE.md) | Real house from the organisers: floor plans, monthly gas 2021–2026, 15-min electricity 2021–2025 (anonymised) | from the organisers; local copy in `data/external/housedata/` | SOURCE.md only (personal data) |
| [`raw/official/`](raw/official/README.md) | Official hourly profile CSV provided by the organisers with the case | from the organisers (not public) | yes, once we have it |
| [`raw/swiss-grid/`](raw/swiss-grid/SOURCE.md) | 10 km grid over CH (409 points + 26 cantonal capitals): hourly weather 2025 and archived forecasts (1–3 days ahead), gzipped | `python3 scripts/fetch_swiss_grid.py grid\|weather\|forecast` (geo.admin.ch, Open-Meteo, no login) | yes (~40 MB, by team choice) |
| [`raw/elcom/municipalities-2026/`](raw/elcom/SOURCE.md) | ElCom 2026 price, all municipalities, categories H1–H8 → `derived/elcom/price-per-municipality-2026.csv` | `python3 scripts/fetch_elcom_municipalities.py` | yes (~6 MB) |
| [`raw/buildings/`](raw/buildings/SOURCE.md) | GWR building register + BFE hectare heat demand map → `derived/buildings/` (per municipality × building class, per grid cell) | `python3 scripts/build_buildings_heat.py --download` | sources **no** (~1 GB), aggregates yes |
| `derived/` | Outputs of our scripts (profiles, results) | rerun the producing script | decided per file, see [`derived/README.md`](derived/README.md) |
| Alibaba GPU trace 2020 | Job durations and resource requests (inspiration) | see "Large datasets" below | **no** (several GB) |
| MLPerf Inference results | Power per inference benchmark | see "Large datasets" below | **no**; only extracted numbers with source |

## Rules

1. **Raw stays raw.** Files in `raw/` are never edited by hand. To update a file, re-fetch it and
   update its `SOURCE.md` (date, parameters).
2. **Every source has a `SOURCE.md`** with: URL, query parameters, retrieval date, licence/terms,
   what the columns mean, known caveats.
3. **Derived data must be reproducible** by one documented command from `raw/`.
4. **No large files in git.** Anything over a few MB is fetched by script or documented here, and
   only the numbers we extract go into the repo (with source and date).
5. **The official organiser CSV wins** over our self-fetched data if the two ever differ. Scored
   reference-house figures should come from the official CSV once it is in `raw/official/`.

## Fetch and validate

```bash
python3 scripts/fetch_data.py           # download Open-Meteo + ElCom into data/raw/, then validate
python3 scripts/fetch_data.py --check   # only validate what is already in data/raw/
python3 scripts/fetch_data.py --prices  # new Azure price snapshot + data/derived/computing-price.json
python3 -m unittest discover -s scripts/tests   # offline tests of the price derivation
```

`--check` also verifies that `data/derived/computing-price.json` is reproduced exactly from the
snapshot it records. To derive from a pinned (older) snapshot without network:
`python3 scripts/fetch_prices.py --snapshot data/raw/market/price-snapshots/<file>.json`.

Python 3.9+ with the standard library only. The check rebuilds the case's heating-demand formula
and compares it with the expected values from the case. Last run (2026-11-20):

| Check | Result | Expected (case) |
|---|---|---|
| hours | 8,760 | 8,760 |
| coldest hour | −5.6 °C | −5.6 °C |
| heating days (daily mean < 12 °C) | 206 | 206 |
| space heating | 26,851 kWh/yr | "about 27,000" |
| hot water | 4,380 kWh/yr | 12 kWh × 365 |
| electricity price, IWB Basel H4 2026 | 33.25 Rp./kWh | from ElCom |

Open-Meteo rate-limits repeated requests (HTTP 429). If that happens, wait a minute; the
existing file is only overwritten after a successful download.

## Large datasets (not committed)

Where big data lives: **at its original source.** Everyone downloads it with a documented command
into the git-ignored `data/external/` and processes it locally; only small extracted results (with
source and the command that produced them) are committed. Reasons: GitHub rejects files over
100 MB and gets slow for repositories over ~1 GB, and Git LFS's free quota (1 GB) is too small to
be worth the setup. If the team ever needs to share large intermediate files, use a shared drive or
a Hugging Face dataset and document the link here.

- **Alibaba GPU cluster trace 2020**: https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020.
  The repository's README describes the tables and links to a download script for the data files
  (hosted on Alibaba Cloud OSS, several GB). Download into a git-ignored folder (`data/external/`)
  if needed, and commit only the statistics we derive (e.g. job-duration distribution), with source.
- **MLPerf Inference: Datacenter** results with power: https://mlcommons.org/benchmarks/inference-datacenter/.
  The results tables can be viewed and exported on the site. Note the exact round (e.g. v5.x),
  system, benchmark and scenario for every number we use.
