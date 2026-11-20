# External references and data sources

> Index of everything the case points to and where it lives in this repository.
> Data storage rules and fetch commands: [`data/README.md`](../../data/README.md).
> Do not invent numbers that should come from these sources.

| What | Source | Used for | In repo |
|---|---|---|---|
| Case presentation (PDF) | [Dropbox: DigitalHeat.pdf](https://www.dropbox.com/scl/fi/6byhvibtqd73fmtkax6b7/DigitalHeat.pdf?rlkey=inpoz7g3kumwogq624yhx5z6y&dl=0) | Background, slides of the case owner | **missing**, see below |
| Hourly reference profile (CSV) | Provided by the organisers with the case | Reference house, all scored figures | **missing**, goes to [`data/raw/official/`](../../data/raw/official/README.md) |
| Weather, Basel 2025 | [Open-Meteo historical weather API](https://open-meteo.com/en/docs/historical-weather-api) | Heating demand, solar (stretch goal) | [`data/raw/open-meteo/`](../../data/raw/open-meteo/SOURCE.md) |
| Electricity price | [ElCom electricity prices](https://www.strompreis.elcom.admin.ch), 2026, IWB, Basel, H4 | Electricity cost, value of heat | [`data/raw/elcom/`](../../data/raw/elcom/SOURCE.md): 33.25 Rp./kWh |
| AI inference power | [MLPerf Inference: Datacenter](https://mlcommons.org/benchmarks/inference-datacenter/) | Energy per job | link only (large, see `data/README.md`) |
| Job durations / resource requests | [Alibaba GPU cluster trace 2020](https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020) | Inspiration for job arrivals and runtimes | link only (several GB) |
| Compute prices | Public on-demand and spot prices of large cloud providers (AWS, Azure, GCP, ...) | Computing income, competitiveness | to research |
| Market prices for AI services | e.g. image tagging APIs (from the deep dive) | Business model | to research |

## Case PDF: manual step needed

The Dropbox link cannot be fetched from the cloud agent environment (Dropbox redirects to a
download host whose name changes on every request). Someone on the team please:

1. Download the PDF from the link above.
2. Save it unchanged as `docs/case/DigitalHeat.pdf` and commit it.
3. Ask an agent to extract the content into `docs/case/DigitalHeat.md` (text plus a short
   description of each figure), so agents can read it without the PDF.

## Rules for using these sources

- Every number taken from a source gets the **URL and access date** next to it.
- The reference house uses exactly the parameters in [`challenge.md`](challenge.md). If the
  official CSV and our own profile differ, the official CSV wins.
- Sanity check for any heating-demand profile: about **27,000 kWh/year** on **206 heating days**
  (our own computation: 26,851 kWh, 206 days).
