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
| [`raw/official/`](raw/official/README.md) | Official hourly profile CSV provided by the organisers with the case | from the organisers (not public) | yes, once we have it |
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
```

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

- **Alibaba GPU cluster trace 2020**: https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020.
  The repository's README describes the tables and links to a download script for the data files
  (hosted on Alibaba Cloud OSS, several GB). Download into a git-ignored folder (`data/external/`)
  if needed, and commit only the statistics we derive (e.g. job-duration distribution), with source.
- **MLPerf Inference: Datacenter** results with power: https://mlcommons.org/benchmarks/inference-datacenter/.
  The results tables can be viewed and exported on the site. Note the exact round (e.g. v5.x),
  system, benchmark and scenario for every number we use.
