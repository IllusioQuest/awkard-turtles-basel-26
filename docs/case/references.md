# External references and data sources

> Index of everything the case points to and where it lives in this repository.
> Data storage rules and fetch commands: [`data/README.md`](../../data/README.md).
> Do not invent numbers that should come from these sources.

| What | Source | Used for | In repo |
|---|---|---|---|
| Case presentation (PDF) | [Dropbox: DigitalHeat.pdf](https://www.dropbox.com/scl/fi/6byhvibtqd73fmtkax6b7/DigitalHeat.pdf?rlkey=inpoz7g3kumwogq624yhx5z6y&dl=0) | Background, slides of the case owner | in repo: [`DigitalHeat.pdf`](DigitalHeat.pdf) (unchanged), text and figure descriptions in [`DigitalHeat.md`](DigitalHeat.md) |
| Hourly reference profile (CSV) | Provided by the organisers with the case | Reference house, all scored figures | **missing**, goes to [`data/raw/official/`](../../data/raw/official/README.md) |
| Weather, Basel 2025 | [Open-Meteo historical weather API](https://open-meteo.com/en/docs/historical-weather-api) | Heating demand, solar (stretch goal) | [`data/raw/open-meteo/`](../../data/raw/open-meteo/SOURCE.md) |
| Electricity price | [ElCom electricity prices](https://www.strompreis.elcom.admin.ch), 2026, IWB, Basel, H4 | Electricity cost, value of heat | [`data/raw/elcom/`](../../data/raw/elcom/SOURCE.md): 33.25 Rp./kWh |
| AI inference power | [MLPerf Inference: Datacenter](https://mlcommons.org/benchmarks/inference-datacenter/) | Energy per job | link only (large, see `data/README.md`) |
| Job durations / resource requests | [Alibaba GPU cluster trace 2020](https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020) | Inspiration for job arrivals and runtimes | link only (several GB) |
| Compute prices | Public on-demand and spot prices of large cloud providers (AWS, Azure, GCP, ...) | Computing income, competitiveness | to research |
| Market prices for AI services | e.g. image tagging APIs (from the deep dive) | Business model | to research |

## Related literature

Papers collected by the team for the paper and related-work section; not yet summarised.

- https://qed.usc.edu/papers/HungGY15.pdf
- https://ieeexplore.ieee.org/document/9445675
- https://www.sciencedirect.com/science/article/abs/pii/S0167739X21000169
- https://www.sciencedirect.com/science/article/abs/pii/S0360544225028749
- https://pmc.ncbi.nlm.nih.gov/articles/PMC8073740

## Rules for using these sources

- Every number taken from a source gets the **URL and access date** next to it.
- The reference house uses exactly the parameters in [`challenge.md`](challenge.md). If the
  official CSV and our own profile differ, the official CSV wins.
- Sanity check for any heating-demand profile: about **27,000 kWh/year** on **206 heating days**
  (our own computation: 26,851 kWh, 206 days).

## Residential population composition (T-59)

- BFS, Eigentuemertypen der Gebaeude 2020, published 2022-05-19, accessed 2026-11-21: https://statistik.admin.ch/de/news/2022-0305. Linked overview workbook asset 22444710; 2020 residential buildings excluding ZH/VS. Analysis/calculation path: docs/research/swiss-residential-node-mix.md; source manifest: data/raw/bfs-building-ownership-2020/source.json; PR #49. Counts are not dwelling, household, server or hotel/spa shares.

## Server sizing (T-70)

- Stoft, S. (2002). *Power System Economics: Designing Markets for Electricity*. IEEE Press / Wiley, ch. 3 (screening curves and load-duration curves). Method reference for the marginal-value sizing rule in docs/results/server-sizing.md; no number is taken from it.
