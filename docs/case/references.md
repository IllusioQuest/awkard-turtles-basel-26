# External references and data sources

> Index of everything the case points to. **Nothing below is stored in the repository yet.**
> Before any of it is downloaded or committed, the team agrees how data is stored (see the open
> point in [`../../AGENTS.md`](../../AGENTS.md)). Do not invent numbers that should come from these sources.

| What | Source | Used for | Status |
|---|---|---|---|
| Case presentation (PDF) | [Dropbox: DigitalHeat.pdf](https://www.dropbox.com/scl/fi/6byhvibtqd73fmtkax6b7/DigitalHeat.pdf?rlkey=inpoz7g3kumwogq624yhx5z6y&dl=0) | Background, slides of the case owner | link only |
| Hourly reference profile (CSV) | Provided by the organisers with the case (weather, heating demand, hot water) | Reference house, all scored figures | not in repo yet |
| Weather, Basel 2025 | [Open-Meteo historical weather API](https://open-meteo.com/en/docs/historical-weather-api), 47.56 N, 7.59 E, 2025-01-01 to 2025-12-31, hourly, Europe/Zurich, `temperature_2m` + solar irradiance | Heating demand, solar (stretch goal) | link only |
| Electricity price | [ElCom electricity prices](https://www.strompreis.elcom.admin.ch), 2026, IWB, Basel, category H4, total CHF/kWh incl. all fees | Electricity cost, value of heat (price ÷ 2.5) | link only, value not yet looked up |
| AI inference power | [MLPerf Inference: Datacenter](https://mlcommons.org/benchmarks/inference-datacenter/) (results with power measurements) | Energy per job for job types | link only |
| Job durations / resource requests | [Alibaba GPU cluster trace 2020](https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020) | Inspiration for job arrivals and runtimes | link only |
| Compute prices | Public on-demand and spot prices of the large cloud providers (AWS, Azure, GCP, ...) | Computing income, competitiveness | to research |
| Market prices for AI services | e.g. image tagging APIs (from the deep dive) | Business model, "we can do it better" | to research |

## Rules for using these sources

- Every number taken from a source gets the **URL and access date** next to it (in code comments,
  config or the assumptions list).
- The reference house must use exactly the parameters in [`challenge.md`](challenge.md). If the
  official CSV and a self-computed profile differ, the official CSV wins.
- Sanity check for any heating-demand profile: about **27,000 kWh/year** on **206 heating days**.
