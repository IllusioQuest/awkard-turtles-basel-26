# WP1: Priced AI services and compute prices

> Snapshot of market prices for AI workloads and for compute capacity, collected 2026-11-20.
> Data: [`data/raw/market/ai-services.csv`](../../data/raw/market/ai-services.csv) (56 rows) and
> [`data/raw/market/compute-prices.csv`](../../data/raw/market/compute-prices.csv) (46 rows),
> schema in [`README.md`](README.md). Every number below has a row there (id in brackets) or a
> source link next to it. Nothing here is modelled; the "illustrations" are back-of-envelope checks
> for later work, not model inputs.

Status: first version (Claude subagent). Feeds the service × heat-fit matrix and the
computing-income side of the economic model (`docs/vision.md`, building block 1).

## Method

- **Sources, in order of preference:** machine-readable official price data (AWS Price List API,
  Azure pricing-page data API, regional tables embedded in Google Cloud pricing pages), official
  price pages, then search-result snippets of official pages (`medium`), then secondary sites
  (`low`). The `confidence` column says which one applies.
- **How pages were read:** the WebFetch tool could not resolve any host in this session, so pages
  were downloaded with `curl` through the session proxy and parsed locally. Several pages render
  prices in the browser; for those, the values come from data embedded in the page (Azure
  `data-amount` regional JSON, Google regional tables, Deepgram structured data, Vantage page data).
- **Regions:** Zurich regions first (AWS `eu-central-2`, Azure Switzerland North, GCP
  `europe-west6`), then Frankfurt / West Europe where a product is not offered in Zurich, US only
  as a reference.
- **Tiers:** the first paid tier (no free tier, no commitment) unless the row says otherwise; some
  high-volume tiers are added to show volume discounts.
- **Normalised values** = `value` × exchange rate × unit factor (e.g. per second × 3600 = per
  hour, per image × 1000 = per 1000 images). The values were computed by a small script from the
  published values; the factor is implied by `unit_basis` and `normalised_unit`.
- **Instance choice** (compute): 1-GPU servers with NVIDIA L4 / T4 / A10 (≈ 70–150 W GPU plus host),
  and 32-vCPU CPU servers, as the closest cloud equivalents of a ~1 kW heat-producing server. The
  cloud instances are slices of larger machines, so their power draw is not 1 kW; matching
  "1 server in a house" to "n cloud instances" is open (see Gaps).

## Exchange rate

| Pair | Rate | Source |
|---|---|---|
| EUR → USD | 1.0989 | ECB euro reference rate of 2026-11-17, as republished by Banca d'Italia ([page](https://www.bancaditalia.it/compiti/operazioni-cambi/cambio/cambi_rif_20261117)) |
| EUR → CHF | 0.9233 | same |
| **USD → CHF** | **0.8402** | cross rate 0.9233 / 1.0989 |

**Flag:** the Banca d'Italia page was not reachable from this session; the two rates come from a
search-result snippet of it (confidence medium). Cross-checks, both accessed 2026-11-20: the
currency table embedded in instances.vantage.sh pages gives 0.84125 CHF per USD (undated); Azure's
own CHF billing conversion (`https://azure.microsoft.com/api/v2/currencies/`) is 0.80993, i.e.
Azure bills about 4 % below the market rate in CHF. One rate is used for all rows; replace it in
one place when the team fixes a reference date.

## A. AI services priced per unit

Prices in CHF, normalised, first paid tier unless noted. "CH" = processed in a Swiss region.

### image-tagging (CHF per 1000 images)

| Provider, product | CHF | Region | Row id |
|---|---|---|---|
| Azure AI Vision Tag (0–1M) | 0.84 | **CH** (Switzerland North) | `ai-img-azure-vision-tag-chn` |
| Azure AI Vision Tag (1–10M) | 0.55 | **CH** | `ai-img-azure-vision-tag-chn-1m` |
| AWS Rekognition DetectLabels | 1.01 (0.25 at 35M+/month) | Frankfurt; **not offered in Zurich** | `ai-img-aws-rekognition-labels-euc1` |
| Google Cloud Vision Labels | 1.26 (0.84 above 5M) | not stated | `ai-img-google-vision-labels` |
| Imagga Indie / Pro (quota fully used) | 0.95 / 0.98 | not stated | `ai-img-imagga-indie` |
| Clarifai custom classifier | 1.01 | not stated, low confidence | `ai-img-clarifai-custom` |

Market price is remarkably uniform at CHF 0.8–1.3 per 1000 images, falling to 0.25–0.55 at volume.

### transcription (CHF per audio hour)

| Provider, product | CHF | Region | Row id |
|---|---|---|---|
| AssemblyAI Universal-2 (batch) | 0.13 | US or EU | `ai-stt-assemblyai-universal2` |
| Google STT V2 *dynamic batch* | 0.15 | not stated | `ai-stt-google-v2-dynbatch` |
| Azure Speech *batch* | 0.15 | **CH** | `ai-stt-azure-batch-chn` |
| OpenAI gpt-4o-mini-transcribe | 0.15 | not stated | `ai-stt-openai-4o-mini-transcribe` |
| ElevenLabs Scribe v3 | 0.17 | not stated | `ai-stt-elevenlabs-scribe-v3` |
| Deepgram Nova-3 (pre-recorded) | 0.22 | US, EU endpoint | `ai-stt-deepgram-nova3-prerec` |
| AWS Transcribe batch | 0.30 | **CH** (Zurich) | `ai-stt-aws-transcribe-batch-euc2` |
| AWS Transcribe streaming | 0.50 | **CH** | `ai-stt-aws-transcribe-stream-euc2` |
| Google STT V2 standard | 0.81 | not stated | `ai-stt-google-v2-standard` |
| Azure Speech real-time | 0.84 | **CH** | `ai-stt-azure-realtime-chn` |
| Töggl, Swiss German | 60 | Swiss provider, low confidence | `ai-stt-toeggl-swissgerman` |

Observations: (1) **deferred processing is already priced**: Google's dynamic batch is 81 % and
Azure batch 82 % cheaper than their real-time services; AWS streaming costs 67 % more than batch.
(2) The commodity price for batch transcription is CHF 0.13–0.30 per audio hour, a factor of ~6
between cheapest and Azure real-time. (3) The one Swiss-German specialist found charges about
400× the commodity price; the source is a product-test article of unknown date, so this needs
verification before anyone builds on it. No Swiss provider with a public per-minute price and
Swiss hosting was found (AlpineAI: Swiss hosting, price on request, per search result).

### embeddings (CHF per 1M tokens)

| Provider, product | CHF | Confidence | Row id |
|---|---|---|---|
| OpenAI text-embedding-3-small | 0.017 | medium | `ai-emb-openai-3-small` |
| Voyage voyage-4-lite / voyage-4 / voyage-4-large | 0.017 / 0.050 / 0.101 | high | `ai-emb-voyage-4*` |
| Mistral mistral-embed | 0.084 | low | `ai-emb-mistral-embed` |
| Cohere Embed 4 | 0.101 | low | `ai-emb-cohere-embed4` |
| Google Vertex Gemini Embedding online / batch | 0.126 / 0.101 | medium | `ai-emb-google-gemini-*` |

Spread ~7×. Batch is 20 % cheaper at Google. None of the pricing pages states a processing region.

### reranking

| Provider, product | CHF | Unit | Row id |
|---|---|---|---|
| Voyage rerank-3-lite / rerank-3 | 0.017 / 0.042 | per 1M tokens | `ai-rr-voyage-rerank3*` |
| Jina Reranker | 0.042 | per 1M tokens (low) | `ai-rr-jina-reranker` |
| Cohere Rerank 3.5 / 4 Fast | 1.68 | per 1000 searches (low) | `ai-rr-cohere-rerank` |

Units are not directly comparable: Voyage estimates USD 0.0025 per request for rerank-3, which
would be CHF 2.10 per 1000 requests, close to Cohere. Reranking is latency-bound (part of a live
search), see heat-fit below.

### ocr-documents (CHF per 1000 pages)

| Provider, product | CHF | Region | Row id |
|---|---|---|---|
| AWS Textract DetectDocumentText | 1.26 | **CH** (Zurich) | `ai-ocr-aws-textract-text-euc2` |
| Azure Document Intelligence Read | 1.26 | **CH** | `ai-ocr-azure-docint-read-chn` |
| Google Document AI Enterprise OCR | 1.26 | not stated | `ai-ocr-google-docai-ocr` |
| Azure prebuilt models (layout, invoice, ...) | 8.40 | **CH** | `ai-ocr-azure-docint-prebuilt-chn` |
| AWS Textract Tables / Forms | 12.60 / 42.01 | **CH** | `ai-ocr-aws-textract-*-euc2` |
| Google Form Parser | 25.21 | not stated | `ai-ocr-google-docai-form` |

Plain OCR is a commodity at exactly USD 1.50 per 1000 pages at all three clouds; structured
extraction costs 7–33× more.

### video-analytics

| Provider, product | CHF | Unit | Row id |
|---|---|---|---|
| AWS Rekognition stored video, labels | 6.05 | per video hour (Frankfurt) | `ai-vid-aws-rekognition-stored-euc1` |
| Google Video Intelligence, labels | 5.04 | per video hour | `ai-vid-google-videointel-labels` |
| Azure Video Retrieval ingestion | 2.52 | per video hour | `ai-vid-azure-video-retrieval` |
| AWS Rekognition StreamProcessor (event clips) | 0.41 | per video hour (US) | `ai-vid-aws-rekognition-streamproc-use1` |
| Azure Spatial Analysis on Edge | 79 | per camera per year (assumes 8760 h) | `ai-vid-azure-spatial-edge-chn` |
| Rhombus Professional license | 125 | per camera per year | `ai-vid-rhombus-professional` |
| Verkada camera license (1 year) | 209 | per camera per year (low) | `ai-vid-verkada-camera-1y` |

CCTV subscriptions bundle storage, VMS and hardware support, so they are not a pure compute
price. Eagle Eye publishes no list price. Continuous analysis of one camera through a cloud API
(CHF 5–6 per hour) would cost tens of thousands of CHF per year, which is why vendors analyse on
the camera or only short event clips (AWS pricing example: 10 s per motion event).

### rendering

| Provider | CHF | Unit | Row id |
|---|---|---|---|
| Ranch Computing (low priority) | 0.0102 | per GHz-hour | `ai-rnd-ranch-cpu-low` |
| RebusFarm | 0.0118 (CPU) / 0.0045 (GPU) | per GHz-hour / per OctaneBench-hour | `ai-rnd-rebusfarm-*` |
| GarageFarm (low priority) | 0.0202 | per GHz-hour | `ai-rnd-garagefarm-cpu-low` |

All low confidence (competitor comparison articles; vendor sites not reachable). Render farms sell
priority tiers: the cheapest tier waits until the queue is quiet, which is again a price for
deadline flexibility. For scale only: a 32-core server at 3 GHz is 96 GHz, i.e. about
CHF 1.0–1.9 per server-hour at these rates.

## B. Compute capacity per hour (CHF per hour, Linux)

### cloud-gpu (1 GPU)

| Instance | GPU | On-demand | Spot | Region | Row ids |
|---|---|---|---|---|---|
| AWS g6.xlarge | L4 | 0.91 | 0.19 (avg) | **Zurich** | `cp-gpu-aws-g6-xlarge-euc2-*` |
| AWS g6.2xlarge | L4 | 1.11 | 0.32 (avg) | **Zurich** | `cp-gpu-aws-g6-2xlarge-euc2-*` |
| GCP g2-standard-4 | L4 | 0.82 | (0.06, unreliable) | **Zurich** | `cp-gpu-gcp-g2-standard-4-euw6-*` |
| GCP g2-standard-8 | L4 | 0.98 | – | **Zurich** | `cp-gpu-gcp-g2-standard-8-euw6-od` |
| AWS g4dn.xlarge | T4 | 0.55 | 0.27 | Frankfurt (not in Zurich) | `cp-gpu-aws-g4dn-xlarge-euc1-*` |
| Azure NC4as T4 v3 | T4 | 0.55 | 0.27 | West Europe (not in CH North) | `cp-gpu-azure-nc4as-t4-weu-*` |
| Azure NV36ads A10 v5 | A10 | 3.84 | 0.71 | **CH North** | `cp-gpu-azure-nv36ads-a10-chn-*` |
| Azure NC24ads A100 v4 | A100 80 GB | 4.41 | 0.82 | **CH North** | `cp-gpu-azure-nc24ads-a100-chn-*` |
| Cohere Model Vault (managed) | L4 | 2.52 | – | not stated | `cp-gpu-cohere-vault-l4` |

Zurich carries a premium: AWS g6.xlarge on-demand is USD 1.085 in Zurich vs. 0.8048 in
us-east-1 (+35 %, Vantage page data), GCP g2-standard-4 USD 0.976 vs. 0.707 (+38 %).
The GCP spot value for g2-standard-4 in Zurich (USD 0.074/h) is printed on Google's spot page,
but the same page's monthly value implies USD 0.58/h; treat it as unverified.

### cloud-cpu (32–64 vCPU)

| Instance | vCPU / RAM | On-demand | Spot | Region |
|---|---|---|---|---|
| AWS c7i.8xlarge | 32 / 64 GiB | 1.51 | 0.47 (avg, interruptions > 20 %) | **Zurich** |
| AWS m7i.8xlarge | 32 / 128 GiB | 1.79 | – | **Zurich** |
| AWS c7i.16xlarge | 64 / 128 GiB | 3.01 | – | **Zurich** |
| Azure D32as v5 | 32 / 128 GiB | 1.65 | 0.31 | **CH North** |
| Azure D32s v5 | 32 / 128 GiB | 1.70 | 0.32 | **CH North** |
| GCP n2-standard-32 | 32 / 128 GiB | 1.71 | 0.58 | **Zurich** |
| GCP n2d-standard-32 | 32 / 128 GiB | 1.49 | 0.59 | **Zurich** |

A 32-vCPU server-hour costs CHF 1.5–1.8 on-demand and CHF 0.31–0.59 as spot in Swiss regions.

### gpu-marketplace (per GPU-hour)

| Provider, GPU | Community / cheapest | Secure / datacentre | Row ids |
|---|---|---|---|
| RunPod RTX A5000 | 0.13 | 0.23 (USD 0.27) | `cp-mkt-runpod-a5000-community` |
| RunPod RTX 3090 | 0.18 | 0.42 (USD 0.50) | `cp-mkt-runpod-3090-community` |
| RunPod RTX 4090 | 0.29 | 0.75 | `cp-mkt-runpod-4090-*` |
| RunPod L4 | 0.37 | 0.58 | `cp-mkt-runpod-l4-*` |
| RunPod L40S | 0.66 (USD 0.79) | 0.92 | `cp-mkt-runpod-l40s-secure` |
| vast.ai RTX 4090, median offer | 0.42 (floor USD 0.27) | – | `cp-mkt-vast-4090-median` (low) |
| Lambda RTX A6000 / A10 | – | 0.92 / 1.08 | `cp-mkt-lambda-*` (medium / low) |

Consumer GPUs (3090/4090) on community hosts are the closest analogue to "a server in someone's
house"; they rent for CHF 0.18–0.42 per GPU-hour.

### swiss-cloud

| Provider, product | CHF | Note | Row id |
|---|---|---|---|
| Infomaniak GPU T4 / L4 / L40S, from | 0.26 / 0.31 / 0.76 per hour | Swiss data centres; smallest flavour, rate table not visible | `cp-ch-infomaniak-*` |
| Infomaniak general-purpose, from | 0.0044 per hour | floor price only | `cp-ch-infomaniak-cpu-from` |
| Exoscale GPU A5000 / RTX Pro 6000 | 1.24 / 1.99 per GPU-hour (EUR 1.34 / 2.15) | dedicated-inference page | `cp-ch-exoscale-*` |
| Exoscale Standard Huge (8 cores) | 0.34 per hour | low confidence | `cp-ch-exoscale-cpu-huge` |

Infomaniak's L4 starting price (CHF 0.31/h) is below the hyperscalers' Zurich on-demand price
(CHF 0.82–0.91/h) and close to marketplace prices. Swisscom and other Swiss clouds: no public
hourly price found in the time box.

## Throughput figures (for "price per unit → income per server-hour" later)

| Workload | Published throughput | Hardware | Source |
|---|---|---|---|
| Transcription, Whisper large-v2, faster-whisper fp16, batch 8 | 13 min audio in 17 s ≈ **46× real time** (int8: 16 s ≈ 49×) | RTX 3070 Ti 8 GB | [faster-whisper README](https://github.com/SYSTRAN/faster-whisper#benchmark), v1.1.0 |
| Transcription, Whisper small, int8, batch 8 | 13 min in 51 s ≈ 15× real time | Intel i7-12700K, 8 threads (CPU only) | same |
| Embeddings, bge-m3, 512-token passages, TEI | ≈ 190 texts/s (A5000), ≈ 277 (L40S); ≈ 97,000 tokens/s on the A5000 | single GPU | [RunPod benchmark](https://www.runpod.io/blog/gpu-embedding-workloads-benchmark), list prices of 2026-09-10 |
| Embeddings, MiniLM-L6-v2, TEI | ≈ 650,000 tokens/s | RTX 2000 Ada | same |
| Image classification, ResNet-50, offline | ≈ 12,872 images/s (not an official MLPerf result) | 1× L4 | [StorageReview L4 test](https://storagereview.com/review/nvidia-l4-gpu-review-low-power-inferencing-wizard), via search snippet |
| Video transcode (indicative only) | ffmpeg 134 FPS | AWS g6.xlarge (L4) | [Vantage g6.xlarge](https://instances.vantage.sh/aws/ec2/g6.xlarge) |

Typical job sizes stated by the sources: AWS pricing examples use 2.5M images/month and
2M audio minutes/month; the faster-whisper benchmark uses one 13-minute file; RunPod's bulk mode
targets "10 million documents tonight" in 512-token chunks; AWS's camera example processes 10 s of
video per motion event. No throughput figure was found for OCR, reranking, rendering or video
analytics.

**Illustration (not a model input):** 46 audio hours per GPU-hour × CHF 0.13–0.30 per audio hour
(batch market price) gives CHF 6–14 of service value per GPU-hour, against CHF 0.19–0.91 to rent
an L4 in Zurich. Embeddings: ~350M tokens per GPU-hour × CHF 0.017–0.126 per 1M tokens gives
CHF 6–44. Images: ResNet-50 × CHF 0.84 per 1000 images gives an absurd CHF 39,000 per hour.
Conclusion for the model: **unit prices are far above raw compute cost, so income per server-hour
will be limited by how much work can be sold (demand, sales, quality, trust), not by throughput.**
The defensible computing-income anchor for interruptible capacity is the **spot/marketplace
price** (CHF ~0.13–0.6 per GPU-hour, CHF 0.3–0.6 per 32-vCPU hour), with service prices as an
upside if the team sells finished services.

## Where data is processed (sovereignty)

- **Swiss region available:** AWS Zurich has Transcribe, Textract and EC2 (L4, CPU), but **not
  Rekognition and not T4 (g4dn)**. Azure Switzerland North has Vision, Speech, Document
  Intelligence and A10 / A100 VMs, but **not T4 VMs**. GCP Zurich has L4 (g2) and n2/n2d.
- **Swiss providers:** Infomaniak (Swiss data centres, prices in CHF), Exoscale (Swiss-founded,
  zones include Geneva). Töggl and AlpineAI for Swiss-German transcription (low confidence).
- **EU option:** Deepgram (EU endpoint) and AssemblyAI (US or EU, same price).
- **Not stated** on the pricing pages: OpenAI, ElevenLabs, Voyage, Cohere, Mistral, Jina, Imagga,
  Clarifai, Google Vision / Document AI / Video Intelligence (Google has regional endpoints for some
  APIs, not verified here), RunPod and vast.ai (host location varies).
- A Swiss region of a US hyperscaler keeps data in Switzerland but not under a Swiss provider;
  whether that matters for a customer (e.g. a law firm) is a question for the team, not answered here.

## Heat-fit: the 3 most promising workload categories

Heat-led computing means jobs run when heat is wanted and pause otherwise, so the work must be
**interruptible**, **deadline-flexible** (hours to a day) and ideally **sensitive** enough that
local processing earns a premium.

1. **Batch transcription** (meetings, interviews, call and medical/legal recordings, Swiss German).
   Interruptible per file or segment; the market already sells deferred batch at 80 % below
   real-time, so the price signal for flexibility exists; audio is often personal data; good
   published GPU and CPU throughput. Market: CHF 0.13–0.30 per audio hour commodity, possibly much
   more for Swiss German.
2. **Document OCR and extraction** (law firms, offices, archives, invoices). Natural checkpoint per
   page; deadlines of hours to days; highly sensitive content; runs on CPU or GPU; all three clouds
   price plain OCR at CHF 1.26 per 1000 pages and structured extraction at CHF 8–42. Gap: no
   throughput figure yet.
3. **Bulk embedding / indexing for search and RAG** (re-indexing document stores, archives). Each
   item takes milliseconds, so pausing costs nothing; batch is priced lower already (Google −20 %);
   corpora are often confidential. Low unit price (CHF 0.017–0.126 per 1M tokens) but very high
   throughput.

Runner-up: **rendering** (interruptible per frame, priority tiers already priced, not sensitive,
sources low confidence). Poor fit: **live video analytics and reranking** (latency-bound,
continuous), and **real-time transcription**; archived-video search would fit like batch work.

## Gaps and blocked sources

- **Blocked hosts** (proxy refused): developers.openai.com and cdn.openai.com (OpenAI prices via
  snippet only), www.clarifai.com, docs.cohere.com, lambda.com (lambda.ai redirects there),
  console.vast.ai, portal.exoscale.com and calculator.exoscale.com, rhombus.com, rebusfarm.net,
  garagefarm.net, ranchcomputing.com, toeggl.ch, swisscom.ch; exchange-rate sources
  (ecb.europa.eu, snb.ch, bancaditalia.it, xe.com and others). The WebFetch tool failed DNS for all
  hosts; curl worked for the allowed ones.
- **Client-rendered prices not captured:** Cohere embed/rerank (page lists only Model Vault),
  Mistral, Jina, vast.ai live offers, Infomaniak per-flavour rates, Exoscale price list.
- **Unreliable:** GCP spot table for g2 (hourly vs. monthly mismatch); AWS spot values are Vantage
  averages, not AWS data; Azure spot values are current prices and move.
- **Missing:** official Clarifai general-model price, Eagle Eye price, Swiss transcription
  providers with public prices, Swisscom cloud prices, throughput for OCR / reranking / rendering /
  video analytics, power draw per instance (needed to compare with a ~1 kW home server).
- **Next steps if time allows:** allow the blocked hosts and re-fetch; measure our own throughput
  for transcription and OCR on the demo laptop (also a must-have groundwork task).
