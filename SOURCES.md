# Sources

Every external source we use, what we take from it and where it goes in. Grouped by topic, the
ones behind the main result (the reference house in the Fleet model) first. Exact URLs, access
dates and licences are kept next to the data: in each `data/raw/*/SOURCE.md`, in the
`source_url` and `accessed` columns of `data/raw/market/*.csv` (row IDs given below), and in the
`source` field of every value in `config/`. Status words: **sourced** (external, citable),
**measured** (our own measurement), **scenario value** (our choice, labelled), `TODO(source)` (no source yet).

## 1. The case and the house

| Source | What we take | Used in |
|---|---|---|
| DigitalHeat case text and slides (organisers) | reference house: heat formula `max(0, 10 kW × (20 − T) ÷ 25.6)` on heating days (mean < 12 °C), 12 kWh/day hot water, 35 kWh lossless tank, 1 kW server, 80 % heat recovery, heat value = electricity ÷ 2.5, must-haves and scoring | `docs/case/challenge.md`, `config/reference-house.json`, `config/fleet/houses/reference.json`, Core `HeatDemand`, every Fleet run |
| Organisers' real house (anonymised `Housedata.zip`) | monthly gas 2021–2026, 15-min electricity 2021–2025, floor plans | `data/raw/housedata/SOURCE.md` (data local only), `scripts/calibrate_real_house.py` → `config/fleet/houses/real-house.json` |
| EU Regulation 813/2013, Annex II 1(a) ([legislation.gov.uk](https://www.legislation.gov.uk/eur/2013/813/annex/II/adopted)) | gas boilers ≤ 70 kW: seasonal efficiency ≥ 86 % since 2015 → gas × 0.86 = heat | real-house calibration |
| EU Regulation 814/2013, Annex III, load profile L ([EUR-Lex PDF](https://eur-lex.europa.eu/legal-content/EN/TXT/PDF/?uri=CELEX:32013R0814)) | time-of-day shape of hot-water draws (24 tappings, 11.655 kWh/day), scaled to 12 kWh/day | `config/hot-water-profile-l.json`, voice demo only (labelled variant) |
| Deep-dive with the case owner | team's notes of the session (job types, site types, pitch ideas) | `docs/deep-dive/notes-verbatim.md` |

## 2. Weather and location

| Source | What we take | Used in |
|---|---|---|
| Open-Meteo Historical Weather API ([docs](https://open-meteo.com/en/docs/historical-weather-api)), Basel 47.56 N 7.59 E, 2025 hourly, CC BY 4.0 | outdoor temperature (and irradiance, unused) | `data/raw/open-meteo/`, reference and real house, voice demo |
| Open-Meteo archive and Previous Runs API ([docs](https://open-meteo.com/en/docs/previous-runs-api)) on a 10 km Swiss grid | hourly weather per cell 2025, archived forecasts lead 0–3 days | `data/raw/swiss-grid/`, all multi-house Fleet runs, forecasts for pricing |
| swisstopo / geo.admin.ch ([terms](https://www.geo.admin.ch/en/general-terms-of-use-fsdi)) | grid cells, municipality and canton per cell | `data/raw/swiss-grid/grid-10km.csv`, house generators |

## 3. Energy prices and buildings

| Source | What we take | Used in |
|---|---|---|
| ElCom electricity prices 2026 ([strompreis.elcom.admin.ch](https://www.strompreis.elcom.admin.ch)) | IWB Basel H4 33.25 Rp./kWh; per-municipality and cantonal prices | `data/raw/elcom/`, `config/reference-house.json`, generated houses |
| BFS Building and Dwelling Register extract ([housing-stat.ch](https://www.housing-stat.ch/)) and BFE heat-demand map ([bfe.admin.ch](https://www.bfe.admin.ch/waerme-kaeltenachfrage-wohnen-und-dl)) | buildings and single-family heat demand per municipality and 10 km cell | `data/raw/buildings/`, `data/derived/buildings/`, design heat of generated houses |
| BFS "Gebäude nach Eigentümertyp 2020" ([release](https://statistik.admin.ch/de/news/2022-0305)) | building owner mix | `data/raw/bfs-building-ownership-2020/`, residential population (T-59) |
| When2Heat heating profiles CH 2022 (Open Power System Data, [doi](https://doi.org/10.25832/when2heat/2023-07-27)) | hourly heat profiles for non-house sites | `data/raw/sites/when2heat/`, network research sites |

## 4. Job demand (how much work arrives)

| Source | What we take | Used in |
|---|---|---|
| Grosser Rat Basel-Stadt ([PDF](https://grosserrat.bs.ch/dokumente/100406/000000406995.pdf)) | 1,269 practising physicians in Basel-Stadt | transcription volume 428 dictations/day (`config/job-scenarios/transcription-basel.json`) |
| Bax et al. 2011, Health Econ Rev 1:8 ([PMC](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3402968/)) | 66.71 dictated minutes per physician and month, median 6.5 min per dictation | transcription volume and size |
| OpenRouter weekly tokens (via press, `cd-t1-openrouter-week`) | 194 × 10¹² tokens/week (× 1 % Swiss share × 1 % market share: **scenario values**) | LLM volume (`config/fleet/job-types/llm-inference-apertus-8b.json`) |
| sciCORE Basel ([HPC resources](https://scicore.unibas.ch/using-scicore/hpc-resources)) | ≈ 30 million CPU-hours/year → ≈ 80,000 1 kW server-hours (`cd-d-scicore-sh-energy`, via SPECpower) | research volume |
| Azure LLM inference trace 2024 ([AzurePublicDataset](https://github.com/Azure/AzurePublicDataset), CC BY 4.0) | weekly arrival shape, tokens per request | LLM and (as a labelled proxy) transcription arrivals |
| Alibaba PAI GPU cluster trace 2020 ([clusterdata](https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020)) | job durations, weekly arrival shape | research job sizes and arrivals |
| BurstGPT (CC BY 4.0) | burstiness check (CV 2.2 per minute) | stated as a simplification |
| Team decision 2026-11-21 | deadlines uniform 2 h – 3 d after arrival (**scenario value**) | all job types (`docs/model/fleet-simulation.md` §5.1) |

Raw traces: `data/raw/demand-traces/*/SOURCE.md`; reduced shapes: `data/derived/demand/`. Row
details: `data/raw/market/compute-demand.csv`; research notes: `docs/research/demand.md`,
`docs/research/compute-demand-traces.md`.

## 5. Prices we charge and compete with

| Source | What we take | Used in |
|---|---|---|
| Infomaniak / kvant Whisper V3 | CHF 0.36 per audio hour (transcription price) | `config/fleet/job-types/transcription-whisper-large-v3.json` |
| Public AI, Apertus 8B | CHF 0.084 in / 0.168 out per 1M tokens (LLM price) | LLM job type |
| CSCS academic pay-per-use (`docs/research/compute-price-elasticity.md`) | CHF 0.82 per 1 kW server-hour competitor price | Type 2 pricing `p_ref` |
| Jin, Peng, Wang 2026; Brand et al. 2025; Jiang et al. 2016, via Hull & Sattath 2026 ([arXiv](https://arxiv.org/pdf/2611.13131)) | price elasticity −0.8 (range −0.5 … −1.2, low confidence) | Type 2 customer acceptance |
| Baughman et al. 2019 | AWS spot price SD 7.9 % | Type 2 price noise |
| Hetzner AX162-S (dedicated server) | CHF 0.66 per server-hour anchor | `config/reference-house.json` `ComputePriceChfPerServerHour`, `docs/research/computing-price.md` A3 |
| Azure Retail Prices API, Switzerland North | VM prices → market-tracking price C1 | `data/raw/market/price-snapshots/`, `data/derived/computing-price.json` |
| AWS g6.xlarge spot/on-demand, RunPod L4 (`cp-gpu-aws-g6-xlarge-euc2-*`, `cp-mkt-runpod-l4-community`) | GPU rental comparison prices | `config/hardware/gpu-inference-l4.json`, server sizing |
| GetDeploying GPU price history (CC BY 4.0) | weekly GPU rental prices | `data/raw/demand-traces/getdeploying-gpu-prices/`, optional `p_ref(t)` |
| AI service price list (83 rows) | LLM, transcription and API prices for comparison | `data/raw/market/ai-services.csv`, `scripts/price_comparison.py` |

## 6. Hardware, energy and costs

| Source | What we take | Used in |
|---|---|---|
| NVIDIA L4 datasheet (`hw-power-nvidia-l4-tdp`) | 72 W per card | L4 GPU profile (`config/hardware/gpu-inference-l4.json`) |
| Bechtle Switzerland, PNY L4 (`hw-cost-bechtle-pny-l4`) | CHF 2,235 per card | L4 profile purchase cost |
| Digitec, Dell PowerEdge R570 (`cost-server-dell-r570`) | CHF 8,248 host | L4 and CPU profiles |
| E2E Networks Whisper benchmark on L4 (`hw-perf-e2e-l4-whisper-turbo-b4`) | 40.4 audio h per GPU-hour → 469.9 per 1 kW server-hour | transcription energy per audio minute |
| vLLM L4 throughput (`docs/research/l4-throughput.md` §2) | 25 M tokens per 1 kW server-hour | LLM energy per token |
| CoreWeave annual report, SEC ([10-K](https://www.sec.gov/Archives/edgar/data/1769628/000176962826000104/crwv-20251231.htm)) | 6-year accounting life | hardware cost per year (`FleetRun post`) |
| Smart Solution server support (`cost-ops-smartsolution-server-basic`) | CHF 59 per server-month operations | hardware cost per year |
| SPECpower (EPYC 9754, `pe-specpower-9754-100`) | cores per kW | sciCORE conversion |
| Further rows: `data/raw/market/hardware.csv`, `costs.csv`, `purchase-cost.csv`, `power-efficiency.csv`, `energy-prices.csv`, `household-capacity.csv`, `trends-2030.csv` | hardware, cost and trend research | `docs/research/hardware.md`, `profitability-inputs.md`, `trends-2030.md` |

## 7. Our own measurements

| Measurement | Result | Where |
|---|---|---|
| Whisper base and large-v3 on the laptop (CPU, RAPL) | 4.62 s and 70.3 s per audio minute | `docs/jobs/transcription.md`, `data/raw/measurements/transcription-*` |
| Apertus 8B summary on the laptop | 8.9 s per audio minute summarised, 25.1 W package | `docs/jobs/llm-summary.md`, `data/raw/measurements/llm-summary-*` |
| Ising Monte Carlo research job | 4.09e-6 kWh per task (CPU, **placeholder** for GPU) | `docs/jobs/research-sim.md`, `data/raw/measurements/research-sim-ising` |
| Voice demo container | CPU time (cgroup) and energy (RAPL share) per request; freeze verified with a cgroup probe | `docs/demo/voice-request.md` |

## 8. Models, software and test data

| Source | Used for |
|---|---|
| whisper.cpp v1.9.5 ([github](https://github.com/ggml-org/whisper.cpp)), models `ggml-base`, `ggml-large-v3` ([Hugging Face](https://huggingface.co/ggerganov/whisper.cpp)) | transcription job, voice demo |
| Whisper paper, Radford et al. ([arXiv 2212.04356](https://arxiv.org/abs/2212.04356)); Swiss German WER ([arXiv 2404.19310](https://arxiv.org/html/2404.19310v2)) | model quality figures in `docs/jobs/transcription.md` |
| llama.cpp b12600 ([github](https://github.com/ggml-org/llama.cpp)); Apertus 8B Instruct, Swiss AI Initiative, Apache-2.0, GGUF by unsloth ([Hugging Face](https://huggingface.co/unsloth/Apertus-8B-Instruct-2509-GGUF)) | LLM job, voice demo |
| LibriVox "Der Schuß" (public domain, [archive.org](https://archive.org/download/schuss_hk_librivox/schuss_2_puschkin_64kb.mp3)) | transcription test audio |
| Google OR-Tools | LP scheduler (network research) |
| Blender (Debian package) | landscape renderer |
| CARTO dark-matter basemap ([style](https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json)) | online map background in the dashboard (offline fallback: plain background) |
| React, Vite, Recharts, MapLibre GL, .NET, xUnit | application code (open source) |

## Where a juror's number comes from

| Number in the pitch | Source chain |
|---|---|
| 31,231 kWh heat demand, 206 heating days | case formula (§1) on Open-Meteo Basel 2025 (§2) |
| CHF 0.3325/kWh, heat value CHF 0.133/kWh | ElCom IWB Basel H4 (§3) ÷ 2.5 (case) |
| 77,840 jobs per year | §4 volumes × 0.1 (scenario value), seeded Poisson arrivals |
| CHF 13,360 income | §5 prices × work done in the Fleet run |
| CHF 25,661 hardware per year | §6 L4 profile: purchase ÷ 6 years + operations |
| Real house 10.13 kW, 38.8 kWh/day | organisers' gas data × 0.86 (§1), fitted to the case formula |

Results and their full assumption table: `docs/model/fleet-simulation.md` §16. Older reference
list: `docs/case/references.md`.
