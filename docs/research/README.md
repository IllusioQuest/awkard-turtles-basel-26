# Research plan: data collection

> Goal: collect the numbers that the financial layer (phase 1) and the market layer (phase 2) need,
> with sources, **before** we model anything. Three work packages, one per team member and their
> agents. They are independent and can run in parallel.
>
> Status: WP2 has a first version; other package assignments are listed below.
> Write your name next to a package when you take it.

| WP | Topic | Phase | Owner | Output |
|---|---|---|---|---|
| [WP1](#wp1-priced-ai-services-and-compute-prices) | Priced AI services and compute prices | 2 (market) + 1 (income) | *open* | `docs/research/ai-services.md`, `data/raw/market/ai-services.csv`, `data/raw/market/compute-prices.csv` |
| [WP2](#wp2-costs-and-energy-prices) | Costs and energy prices | 1 (financial) | Codex (GPT-6 Astra); status: first version | `docs/research/costs-and-energy.md`, `data/raw/market/costs.csv`, `data/raw/market/energy-prices.csv` |
| [WP3](#wp3-sites-and-heat-demand-profiles) | Sites and heat-demand profiles | 2 (customers) + 3 (physics input) | Claude subagent (**in progress**, started 2026-11-20 23:00) | `docs/research/sites-and-profiles.md`, `data/raw/sites/site-types.csv` |

Not planned yet (later or on request): competitors in heat-from-compute (Qarnot, Heata, Deep Green,
Cloud&Heat, Nerdalize), regulation (data protection, Legionella), server power curves.

## Rules for all packages

- **Every number goes into a CSV row** with source URL and access date (schema below). The
  Markdown file explains context, comparability and gaps; it does not hold numbers without a row.
- **Comparable units.** Always record the provider's original unit (`unit_basis`) **and**, where
  possible, a normalised value (e.g. CHF per 1,000 images, CHF per audio hour, CHF per server-hour).
  Write down the conversion (exchange rate with date, assumptions).
- **Prefer official price pages and primary sources.** Secondary sources (blogs, news) only with
  `confidence: low`.
- **Snapshot, don't fetch at runtime.** Prices change; the CSV is the dated snapshot we use.
- **Time box: about 2 hours per package** for a first usable version. Breadth before depth: a rough
  value for every row beats a perfect value for one.
- **Do not model yet.** Collect and describe. Note interesting observations ("cheapest provider is
  X times cheaper than Y") in the Markdown file.
- Data sovereignty: note for every service **where the data is processed** (country/region), as this
  is part of our possible selling point.

## CSV schema (all files in `data/raw/market/`)

One row per price point. Columns:

| Column | Example | Meaning |
|---|---|---|
| `id` | `ai-img-aws-rekognition-labels` | stable, unique, lowercase |
| `category` | `image-tagging` | see the list per package |
| `provider` | `AWS` | company |
| `product` | `Rekognition DetectLabels` | product / SKU / instance type |
| `value` | `1.00` | number as published |
| `currency` | `USD` | ISO code |
| `unit_basis` | `per 1000 images` | the provider's unit, verbatim |
| `normalised_value` | `0.89` | optional, in `normalised_unit` |
| `normalised_unit` | `CHF per 1000 images` | optional |
| `tier` | `first 1M images/month` | volume tier, region, spot/on-demand, ... |
| `region` | `eu-central-2 (Zurich)` | where it is offered / processed |
| `source_url` | `https://aws.amazon.com/rekognition/pricing/` | exact page |
| `accessed` | `2026-11-21` | YYYY-MM-DD |
| `confidence` | `high` | high = official price page; medium = official but unclear; low = secondary |
| `notes` | `free tier excluded` | anything needed to interpret the row |

`data/raw/sites/site-types.csv` (WP3) uses its own columns, defined in WP3.

---

## WP1: Priced AI services and compute prices

**Question:** For which AI workloads is there a market price today, how high is it, and what does
an equivalent server-hour cost at cloud providers? This gives the computing income and the
"we can do it cheaper / more locally" comparison.

**A. AI services priced per unit** (`ai-services.csv`), categories:
- `image-tagging`: AWS Rekognition, Google Cloud Vision, Azure AI Vision, Clarifai, Imagga
- `transcription`: AWS Transcribe, Google Speech-to-Text, Azure Speech, OpenAI (Whisper / transcribe
  models), ElevenLabs Scribe, Deepgram, AssemblyAI; a Swiss provider if one exists
- `embeddings`: OpenAI, Cohere, Voyage, Mistral, Google; per million tokens
- `reranking`: Cohere, Voyage, Jina
- `ocr-documents`: AWS Textract, Google Document AI, Azure Document Intelligence
- `video-analytics`: cloud video AI per minute and CCTV analytics subscriptions per camera per year
  (e.g. Verkada, Rhombus, Eagle Eye; whatever publishes prices)
- `rendering`: render farms, per GHz-hour or node-hour

**B. Compute capacity per hour** (`compute-prices.csv`), categories:
- `cloud-cpu`, `cloud-gpu`: AWS, Azure, GCP; on-demand **and** spot; prefer the Zurich regions;
  pick 2–3 instance types each that are comparable to a ~1 kW server (e.g. a small GPU server with
  NVIDIA L4 / T4, a CPU server with 32–64 vCPU)
- `gpu-marketplace`: RunPod, Lambda, vast.ai (consumer and datacentre GPUs)
- `swiss-cloud`: Infomaniak, Exoscale, Swisscom, others that publish prices (sovereignty anchor)

**Also record, per workload category, in the Markdown file:** a typical job size (e.g. images
per batch, audio hours) and, if published, throughput figures (images/s, real-time factor) that
let us translate "price per unit" into "income per server-hour" later.

**Done when:** ≥ 3 providers per category in A, ≥ 2 instance types × 3 clouds in B, conversion
to CHF documented, a short summary of the 3 most promising workload categories with reasons.

## WP2: Costs and energy prices

**Owner: Codex (GPT-6 Astra)** · **Status: first version** · Snapshot: 2026-11-20.
See [findings and gaps](costs-and-energy.md).

**Question:** What does it cost to install and run a heat-producing server in a building, and what
does the heat it replaces cost? This gives the cost side of the money flows.

Categories in `costs.csv`:
- `hardware-server`: purchase price of servers comparable to the 1 kW reference (CPU server,
  GPU server with e.g. L4/L40S/RTX), with power draw; list prices or reputable retailers (CH/EU)
- `hardware-lifetime`: typical depreciation period and lifetime of servers and GPUs
- `installation`: heat exchanger / liquid cooling, hydraulics, integration into a heating system;
  quotes or published project costs where available
- `operations`: maintenance, internet connectivity, insurance, monitoring (rough values are fine)
- `heat-alternative`: heat pump investment and running costs (CH), its efficiency (COP / annual
  performance factor); oil and gas boiler as comparison

Categories in `energy-prices.csv`:
- `electricity-retail`: ElCom history for IWB Basel H4 (all years available) plus 3–5 other
  Swiss operators for comparison (the existing 2026 value is in `data/raw/elcom/`)
- `electricity-spot`: Swiss day-ahead prices (e.g. EPEX / energy-charts.info), yearly mean and
  example hourly profile source
- `fuel-heating`: heating oil and natural gas prices for households in Switzerland (e.g. BFS
  price index), CO₂ levy
- `feed-in`: what a Basel PV owner gets for feeding in solar power (for the optional solar case)

**Done when:** a value with source for every category, price history for Basel electricity, and a
short note on which costs are missing from the case's break-even formula (it only uses electricity
cost and heat value).

## WP3: Sites and heat-demand profiles

**Question:** Which kinds of sites could host servers, and how does their heat demand look over the
year and over the day? The goal is **generality**: a site in the simulation should be a list of
parameters (plus, optionally, a time series), so that any location and building type can be
simulated.

Two parts:

**A. A generic site description.** Propose the parameter list that describes a site, for example:
type, location (lat/lon), space-heating model and its parameters, hot-water demand per day and its
daily shape, required supply temperature, storage size, space for servers, seasonal pattern,
data-sovereignty constraints (which jobs may run), price of the heat it replaces. Write it as a
table in the Markdown file; do not implement it.

**B. Profiles and values per site type** (`data/raw/sites/site-types.csv`; one row per value with
`site_type, parameter, value, unit, source_url, accessed, confidence, notes`):
- `residential`: daily shape of space heating and hot water (standard load profiles, e.g.
  VDI 4655, BDEW standard load profiles, SIA 2024 usage profiles), night vs. day, weekday vs. weekend
- `hotel-spa`, `indoor-pool`: yearly and daily heat demand, hot-water share
- `agriculture-drying`: grain/maize drying season, temperatures and energy per tonne
- `office-law-firm`, `retail-grocery`: heat demand, and the data/compute need (e.g. CCTV, documents)
- others that fit (laundries, greenhouses, multi-family houses)

**Also:** how many of each site type exist in Basel / Switzerland, if a public source has it
(e.g. BFS building register), to size the market later.

**Done when:** the generic parameter list exists, and each site type has at least yearly demand,
seasonality and a source for its daily shape.

---

## Prompt to hand a package to an agent

> Read `AGENTS.md` and `docs/research/README.md`. Do work package WPn as described there, on a new
> branch `data/research-wpn`. Collect values with sources into the CSV files using the schema,
> write the Markdown summary, keep to the time box, do not model anything. Open a PR when done.
