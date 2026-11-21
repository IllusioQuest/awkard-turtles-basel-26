# Demand: transcription scenario, compute demand to 2030, filler marketplaces

> **Merged 2026-11-21** from `demand.md` (sourced finite transcription scenario),
> `demand.md` (R4, T-37, demand today and to 2030) and `demand.md` (filler
> marketplaces, fill rate, price; answers open question 3 of decision 0014,
> [`docs/model/routing-optimisation.md`](../model/routing-optimisation.md) §2.3, `F_{s,t}`).
> Exploration by Claude agents, 2026-11-21, not reviewed by the team; recommendations, not decisions.
>
> - Scenario file: [`config/job-scenarios/transcription-basel.json`](../../config/job-scenarios/transcription-basel.json); default run stays [`unlimited.json`](../../config/job-scenarios/unlimited.json).
> - Data: [`data/raw/market/compute-demand.csv`](../../data/raw/market/compute-demand.csv) (ids `cd-*`).
> - Our unit: 1 kW server-hour. Reference house sells **22,661** per year; 1,000 houses = **22.66 M**.
> - Throughput (decision 0018): 436 audio h per server-hour (whisper base), 27-35 (large-v3, mid 31), 3.9 M tokens.
> - USD to CHF 0.8402, EUR to CHF 0.9233 (repo rates). Assumptions marked **[A]**.
>
> **How sources were read.** WebFetch/curl were blocked for most hosts (biomedcentral, happyscribe,
> grosserrat.bs.ch, ncbi, support.salad.com, vast.ai, sparecores, akash.network). External figures come
> from **search-result snippets** (accessed 2026-11-21); only the CSCS and sciCORE figures were
> verified by opening the pages (confidence high). Confidence: `medium` = snippet quotes the primary
> page, `low` = secondary or unclear date. Check each figure on the full page before the pitch.

## 1. Short answer

- **Type 1 (sovereign AI) is large worldwide but tiny for Basel.** Sourced Basel physician dictation
  is 39-546 server-hours/yr = 0.2-2.4 % of one house; the case's "jobs per day and where it comes
  from" (must-have 1) is answered by §2. Only a Europe-wide view reaches 1,000 houses, as an upper bound.
- **Type 2 (batch filler):** public research demand is real and oversubscribed (CSCS asks 1.8-3.3x
  what it can give), but Basel's sciCORE consumes only ≈ 80,000 (energy basis) to 460,000 (node
  basis) server-hours/yr, 0.4-2 % of 1,000 houses. A broker needs customers beyond Basel research; no
  public volumes found for Swiss/European groups or finance.
- **Sellable, but not every hour:** every marketplace for distributed capacity shows more supply than
  paid demand; utilisation ≈ 25-60 % (only vendor marketing says 80 %). Recommended fill rate **φ base
  0.35, sensitivity 0.20-0.60 [A]**; 1.0 only with an anchor customer under contract (label it).
- **Price paid to the host per 1 kW server-hour [A]:** CPU CHF 0.25-1.20 (adopted central 0.66); GPU-L4
  CHF 0.85-2.20 (central 1.42). Expected filler income per offered hour on CPU = 0.35 × 0.66 =
  **CHF 0.23 ≈ case break-even 0.226**: filler alone barely covers electricity net of heat value.
- **Seasonality:** demand is roughly flat over the year (§4) while our supply is heat-led (winter);
  summer is a supply gap, not a demand gap.
- **Data rule:** filler on open marketplaces runs on an untrusted host: non-personal, non-confidential
  work only (§10).

## 2. Type 1 scenario: medical dictation in Basel-Stadt (job-demand)

This builds one finite job type from cited parts and runs it through the year simulation (decision
0010, [`docs/model/job-queue.md`](../model/job-queue.md)), for must-haves 1 and 3 (missed deadlines %).

| # | Quantity | Value | Source | Conf. |
|---|---|---|---|---|
| A1 | Primary-care physicians in practice, BS, Jan 2024 | 415 | Regierungsrat BS, report on Anzug Sarah Wyss, [grosserrat.bs.ch/dokumente/100406/000000406995.pdf](https://grosserrat.bs.ch/dokumente/100406/000000406995.pdf), accessed 2026-11-21 | medium |
| A2 | Primary care as share of all practising physicians in BS | 32.7 % | same document | medium |
| A3 | All practising physicians BS = A1 ÷ A2 | **1,269** | derived | - |
| A4 | Median dictation per physician and month | 66.71 audio min | Bax et al. 2011, *Health Econ Rev* 1:8, pediatric academic centre, London (Ontario) (2009 data), [PMC3402968](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3402968/), accessed 2026-11-21 | medium |
| A5 | Median length of one dictation | **6.5 audio min** | same paper | medium |
| A7 | Throughput of the 1 kW server | **26,178 audio min/server-h** | own measurement T-02 (`feat/T-02-transcription-job`, `docs/jobs/transcription.md`): 1000 Wh ÷ 0.0382 Wh/audio min, whisper.cpp `base`, whole-laptop gross, same energy per audio min assumed on the server | measured, one laptop |
| A8 | Deadline | **24 h** | Rev standard human transcription, max 24 h ([support.rev.com/…/18859196207629](https://support.rev.com/hc/en-us/articles/18859196207629)); GoTranscript one-day tier beside 3- and 5-day ([sonix.ai/resources/gotranscript-pricing](https://sonix.ai/resources/gotranscript-pricing/)), accessed 2026-11-21 | medium / low |
| A9 | Interruptible | yes | T-02 pause experiment (`docker pause`, identical transcript) | measured |

**Customer base (ours, unsourced):** *all* medical dictations of *all* practising physicians in BS
go to **one** reference house: a deliberate upper bound, not a forecast. Hospital physicians, law firms,
notaries, municipalities are not included (no per-person volume found; BS lawyer register exists as a
PDF, [bs.ch Anwaltsregister](https://www.bs.ch/gerichte-judikative/ueber-die-gerichte/anwaltsaufsichtskommission/anwaeltinnen-und-anwaelte), not counted).

```
physicians            = 415 / 0.327                        = 1,269
audio per day         = 1,269 × 66.71 min/month × 12/365   = 2,783 audio min/day
jobs per day          = 2,783 / 6.5 min/job                = 428.2  → 428 jobs/day (rounded down)
server-hours per day  = 428 × 6.5 / 26,178                 = 0.106
server-hours per year = 0.106 × 365                        = 38.8
```

The scenario format has one integer arrival count per day at 00:00. Real demand is weekdays only;
spreading over 365 days keeps the yearly volume but hides the weekday peak (no effect at 24 h deadline
and this volume).

**Sensitivity** (low/high run as scratch copies with only `arrivalsPerDay`, and for one run the
policy, changed; not committed):

| Case | Customer base | Audio per person | Jobs/day | Server-h/yr |
|---|---|---|---|---|
| Low | 415 primary-care physicians BS (A1) | 66.71 min/month | 140 | 12.7 |
| **Base** | 1,269 practising physicians BS (A3) | 66.71 min/month | **428** | **38.8** |
| High | 42,602 working physicians in CH (FMH Ärztestatistik 2024, [fmh.ch poster](https://report2025.fmh.ch/files/pdf33/fmh-aerztestatistikposter-2025_de.pdf), total from snippet, low) | 30 min/day (vendor-linked, [PracticeLink 2011](https://www.practicelink.com/magazine/departments/physician-tech-notes/emdat-transcription-gone-high-tech-spring-2011), low), per calendar day | 196,624 | 17,820 |

High is an extreme stress test (all Swiss physicians, 13x the measured median, one house). Deadline
variants (6 h rush, 3 or 5 working days) were not run: in the base case no job nears its deadline.

**Results** (reference house, 4 servers, 2025 Basel weather). Commands:
`dotnet build src/DigitalHeat.sln`; `dotnet run --project src/DigitalHeat.YearRun -- .` (unlimited) or
`... -- . config/job-scenarios/transcription-basel.json` (base). Output `results/reference-house/summary.json` (not committed).

| Figure | Unlimited | Low | **Base** | High, wait | High, run-and-dump |
|---|---|---|---|---|---|
| Jobs arrived / year | - | 51,100 | **156,220** | 71,767,760 | 71,767,760 |
| Completed | - | 51,100 | **156,220** | 52,200,396 | 71,767,760 |
| Missed deadlines | not modelled | 0 % | **0 %** | **27.3 %** | 0 % |
| Backlog at year end | - | 0 | 0 | 0 | 0 |
| Server-hours | 22,661 | 12.7 | **38.8** | 12,961 | 17,820 |
| Utilisation | 64.7 % | 0.04 % | **0.11 %** | 37.0 % | 50.9 % |
| Coverage of heat demand | 58.0 % | 0.03 % | **0.10 %** | 33.2 % | 33.1 % |
| Server heat used (kWh) | 18,129 | 10 | **31** | 10,369 | 10,325 |
| Heat dumped (kWh) | 0 | 0 | 0 | 0 | 3,931 |
| Backup heat (kWh) | 13,102 | 31,220 | **31,200** | 20,861 | 20,905 |
| Electricity cost (CHF) | 7,535 | 4.2 | **12.9** | 4,310 | 5,925 |
| Computing income at 0.30 CHF/server-h (CHF) | 6,798 | 3.8 | **11.6** | 3,888 | 5,346 |
| Case break-even (CHF/server-h) | 0.226 | 0.226 | 0.226 | 0.226 | 0.255 |

Total heat demand is 31,231 kWh in every run. Break-even stays 0.226 under `wait` (no dumped heat);
it rises only when heat is dumped.

**What it means.**
- **Demand, not heat, is the bottleneck.** Base needs 39 server-hours/yr vs 22,661 unlimited
  (**0.17 %**); coverage drops 58 % → 0.1 % and the backup heater supplies almost everything. A CPU
  running `base` Whisper is ≈ 436x faster than real time per kW, so one house out-transcribes a city's
  doctors. A business result, not a model error.
- **To fill 22,661 server-hours:** 22,661 × 26,178 / 365 ≈ **1.63 M audio min/day** (≈ 27,000 audio
  h/day) = median dictation of ≈ **740,000 physicians**, 17x all physicians in Switzerland.
- **Missed deadlines appear only at large demand:** High under heat-led `wait` misses 27 % of 24 h
  deadlines on days when heat-led runtime falls short of the 48.8 server-hours arriving daily
  (outside the heating season only hot water draws heat: 12 kWh/day ÷ 0.8 = 15 server-hours/day; the
  run reports yearly totals, no per-day misses). Run-and-dump removes misses but dumps 3,931 kWh and
  raises break-even to 0.255: the core deadline vs dumped-heat trade-off.
- **The server-hour price undervalues this job:** at 0.30 CHF/server-hour the base case earns CHF
  11.6/yr; the same 16,924 audio hours at the batch market rate CHF 0.13-0.30 per audio hour
  ([`ai-services.md`](ai-services.md)) would be worth ≈ CHF 2,200-5,100/yr (illustration; billing per
  audio hour is a business-model question).
- **Options for the pitch (team decides):** (a) scored comparison = unlimited run, this scenario = honest
  demand check; (b) heavier jobs per unit of value (larger Whisper, OCR, embeddings over archives);
  (c) pool many houses behind one customer base and route jobs to the house that needs heat (T-10/T-12).

**Main uncertainties.** (1) Throughput A7 dominates: one laptop, `base`, read-aloud standard German,
scaled by equal energy per audio minute; a larger Whisper is slower (not measured); reaching 1 %
utilisation (350 of 35,040 server-hours) would need ≈ 9x slower. (2) Dictation volume A4/A5: one
North American pediatric centre, abstract snippet; Swiss volumes may differ and speech recognition
already replaces part of dictation. (3) Physician count derived from a cantonal report snippet; check
the full report or FMH. (4) Deadline A8 is a human-transcription service level, not our contract.
(5) One house serving the whole canton is generous by design.

## 3. Type 1 at market level (compute-demand)

| Item | Value | CSV id |
|---|---|---|
| Europe speech and voice recognition market | USD 1.98 bn (2025), 4.56 bn (2030), CAGR 18.2 % | `cd-t1-eu-stt-2025`, `-2030`, `-cagr-mm` |
| Same, other publishers | USD 4.22 bn (2030, CAGR 16.2 %); EMEA USD 4.70 bn (2030, CAGR 14.2 %) | `cd-t1-eu-stt-2030-dbmr`, `cd-t1-emea-stt-2030-statista` |
| Medical transcription services, global | USD 130 bn (2030), CAGR 5.7 %; other publishers 4-7 %, sizes differ by 10x (**conflict between publishers, kept**) | `cd-t1-med-transcr-2030` |
| Swiss persons using generative AI | 47 % (EU 33 %); 41 % of workers use it at work | `cd-t1-ch-persons-genai` |
| Swiss SMEs with AI | 22 % (2024) to 34 % (2025) | `cd-t1-ch-sme-ai` |
| Swiss colocation (proxy for sovereign hosting) | USD 465 M (2024) to 916 M (2030), CAGR 12 % | `cd-t1-ch-colo-2030` |
| OpenRouter tokens | 194 trillion/week (Oct 2026); ≈ 25x growth in 11 months | `cd-t1-openrouter-week` |

Speech-recognition sizes differ by publisher but agree on **CAGR 14-18 % to 2030**; they are
market-research press summaries (low) incl. voice assistants and call centres, not only batch
transcription.

**Gaps (`TODO(source)`):** BFS firm-level ICT survey on AI use; Swiss sovereign-cloud / AI market size
(only an economic-impact estimate, `cd-t1-ch-econ-genai`, not spend); audio hours/yr of Swiss courts,
cantonal parliaments, Basel university hospital; Google/Microsoft token counts; Swiss physician count.

**Server-hour estimates.**
1. *Basel dictation (§2):* audio = 1,269 × 66.71 × 12 / 60 = 16,931 h; ÷ 436 = **38.8** (base) or ÷ 31 =
   **546** (large-v3) (`cd-d-basel-stt-base`, `-large`); 0.17 % and 2.4 % of one house.
2. *Europe speech spend, upper bound:* server-hours = spend in CHF ÷ price per audio hour ÷ audio hours
   per server-hour. 2025: 1.98 bn USD × 0.8402 = CHF 1.664 bn; at batch price CHF 0.1512 per audio
   hour (`ai-stt-google-v2-dynbatch` in `ai-services.csv`) = 1.10 × 10^10 audio h; ÷ 436 = **25.2 M**
   (base) or **355 M** (large-v3, ÷ 31). 2030 (CHF 3.831 bn): **58.1 M** (base)
   (`cd-d-eu-stt-2025-sh`, `-2030-sh`). That is 1.1x-15.7x of 1,000 houses, but only a loose bound:
   1.10 × 10^10 h/yr ≈ 24 audio hours per European resident, implausible as batch volume (spend
   includes voice assistants, call-centre analytics, devices, human services). **Not a volume.**
3. *Tokens:* 194 × 10^12 × 52 ÷ 3.9 × 10^6 = **2.59 × 10^9 server-hours/yr** (`cd-d-openrouter-sh`);
   1,000 houses = 0.9 % of OpenRouter. Order of magnitude only (mostly models far larger than 8B).

## 4. Type 2 at market level (compute-demand)

| Item | Value | CSV id |
|---|---|---|
| sciCORE (Basel) consumption | almost 30 M CPU-hours/yr, ≈ 800 users, > 14 M jobs | `cd-t2-scicore-cpuh` |
| sciCORE main cluster | 223 nodes, 14,528 cores | `cd-t2-scicore-cluster` |
| CSCS requested vs available node-hours | 39.5 M vs 12 M (2016), 50.5 M vs 23.5 M (2016), 19.7 M vs 11 M (2019) | `cd-t2-cscs-*` |
| ETH Zurich use of CSCS | 1.82 M node-hours (AR 2025) | `cd-t2-cscs-eth-nh` |
| Global HPC market | USD 87 bn (2030), CAGR 7.2 %; range 5-11 %; HPC for AI 27.6 % | `cd-t2-hpc-2030-gvr`, `cd-t2-hpc-ai-cagr` |

- **Oversubscription:** 39.5/12 = 3.29, 50.45/23.51 = 2.15, 19.7/11 = 1.79; mean **2.4**
  (`cd-d-cscs-oversub`). Only 2016 and 2019 found; CSCS Alps allocations are mostly GPU node-hours, so
  this shows research appetite, not CPU-filler demand.
- **sciCORE in our unit.** [A] node-hour = server-hour; cores per node = 14,528/223 = 65.15;
  30,000,000 / 65.15 = **≈ 460,000/yr** (`cd-d-scicore-sh`) = 20.3 houses = 2.0 % of 1,000 houses
  (upper bound). Energy basis, with the verified EPYC 9754 SPECpower server as proxy (128 cores at
  0.341 kW = 375 cores/kW, `pe-specpower-9754-100`): 30,000,000 / 375.4 = **≈ 80,000** server-hours
  (`cd-d-scicore-sh-energy`) = 3.5 houses; **use 80,000 as central**. The page is undated and capacity
  (14,528 × 8,760 = 127 M core-hours) is 4x consumption, so it may be stale (`TODO(source)`: annual
  report, queue waits, ETH Euler core-hours).
- **ETH at CSCS:** 1.82 M / 22,661 = 80 houses (different hardware, scale only).
- **Global HPC** is mostly hardware revenue, not convertible to server-hours without price and
  utilisation (`TODO(source)`: cloud batch/spot market in server-hours; Swiss finance Monte Carlo
  compute: no public figures).
- **Reading for the broker:** Basel research fills ≈ 20 houses; hundreds of houses need Swiss/European
  research and industry customers, and compete with subsidised free compute (sciCORE, CSCS; compare
  CHF 0.66/server-hour).

**Seasonality and time of day.** Found: NSF HPC workload analysis (`cd-t2-season-xdmod`): "strong
periodic behavior at one day and one week, very weak periodicity over one year" (2013-2017, USA,
snippet). Not found: monthly utilisation of sciCORE, Euler, CSCS (`TODO(source)`: request sciCORE
monthly statistics, private to groups). Fit: demand does not fall in winter (heat-led schedule not
penalised) but does not rise either (nothing gained for heat); summer demand exists while supply is
low, a gap served by data centres. Daily cycles matter little for queued, delay-tolerant jobs.

**Summary in server-hours/yr**

| Segment | Server-h/yr | vs 1 house (22,661) | vs 1,000 houses | Ids |
|---|---|---|---|---|
| Basel dictation, base / large-v3 | 38.8 / 546 | 0.2 % / 2.4 % | 0.0002 % / 0.002 % | `cd-d-basel-stt-*` |
| Europe speech spend 2025, base / large-v3 (upper bound) | 25.2 M / 355 M | 1,110x / 15,700x | 1.1x / 15.7x | `cd-d-eu-stt-2025-sh` |
| Europe speech spend 2030, base | 58.1 M | 2,560x | 2.6x | `cd-d-eu-stt-2030-sh` |
| OpenRouter tokens (global, 8B equiv.) | 2.59 × 10^9 | 114,000x | 114x | `cd-d-openrouter-sh` |
| sciCORE energy / node basis | 0.08 M / 0.46 M | 3.5x / 20x | 0.004x / 0.02x | `cd-d-scicore-sh-energy`, `cd-d-scicore-sh` |

Growth to 2030: speech recognition 14-18 %/yr; HPC 5-11 % (HPC for AI 27.6 %); OpenRouter ≈ 25x in 11
months (not a stable forecast); Swiss SME AI adoption 22 → 34 % in one year.

## 5. Marketplaces for distributed / consumer-hosted capacity (filler-demand)

| Network | Host earnings / prices | Utilisation / demand evidence | Interruption, data, payout | Conf. |
|---|---|---|---|---|
| **SaladCloud** (consumer PCs) | Payout per GPU in Salad's Network Monitor, not in snippets (`TODO(source)`). Third-party USD 0-100/month per host ([CashPilot](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/salad.md)). Buyer CPU USD 0.004-0.005 per vCPU-h + 0.001 per GB-h ([billing docs](https://docs.salad.com/container-engine/explanation/billing-pricing/billing.md)); RTX 4090 USD 0.16 (batch)-0.33 (high priority)/h, 2026-09-12 ([GetDeploying](https://getdeploying.com/reference/cloud-gpu/nvidia-rtx-4090)) | Container jobs only "when one is available for your hardware", else bandwidth sharing or mining; "if there is simply no demand … consider pausing Salad"; reliable chefs get priority ([getting jobs](https://support.salad.com/guides/getting-jobs/getting-container-jobs), [sporadic](https://support.salad.com/article/64-why-is-my-earning-rate-sporadic-random)); CPU workloads "low-value" ([how it earns](https://support.salad.com/article/356-how-does-my-machine-earn-salad-balance)) | Interruptible by design; ≥ 70 GB disk, virtualisation on. SOC 2 **Type I** only ([salad.com](https://salad.com/), [ClusterMAX](https://clustermax.semianalysis.com/cloudreview/saladcloud)); has a "What workloads are not a fit?" section (content not in snippet) | medium / low |
| **vast.ai** (home and DC hosts) | Host sets price; earnings = price × occupancy ([guide](https://docs.vast.ai/host/optimization-guide.md)); host keeps ≈ 85 % ([miningboard](https://miningboard.com/guides/rent-gpu-for-ai-earnings)); min payout USD 25 ([CashPilot](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/vast-ai.md)) | No official average. Vast's example **80 %** ([article](https://vast.ai/article/how-much-money-can-you-earn-renting-out-your-gpu-on-vast-ai), marketing); third-party planning **30 %**, month-1 at 30-40 % of steady state ([gmicloud](https://www.gmicloud.ai/en/blog/vast-ai-rent-my-gpu-explained)); API `usage_30d` per GPU ([docs](https://docs.vast.ai/api-reference/machines/show-gpu-metrics.md)) | Ubuntu ≥ 18.04, CUDA ≥ 12, 3 open ports/GPU, 90 % reliability ([verification](https://docs.vast.ai/host/verification-stages.md)). "Secure Cloud" = ISO 27001 DCs only; individual hosts "may have less formal security" ([compliance](https://vast.ai/compliance), [FAQ](https://docs.vast.ai/guides/reference/faq/security.md)) | medium / low |
| **Akash** (decentralised, mostly DC) | Lease price AKT/USDC by bids | GPU: 198 of 587 used in Q4 2025 → **34 %** (Messari via [TokenPost](https://www.tokenpost.kr/research/1138)); Q1 2026 ≈ 25-34 % (figures do not reconcile, [Stocktwits](https://stocktwits.com/coins/akash-network)). CPU: 5,930 of 22,629 active → **26 %** (date unknown, [capacity](https://akash.network/about/network-capacity)); Q4 2023 CPU 56 % ([recap](https://akash.network/blog/q4-2023-recap/)). Self-reported GPU 60 % ([2025 review](https://akash.network/blog/akash-2025-year-in-review/)) | Kubernetes provider; no data guarantees beyond provider choice | medium (Messari), low |
| **io.net** (crypto GPU) | Hourly token rewards **paid even when idle** ([Dextools](https://www.dextools.io/news/what-is-io-net-decentralized-gpu-ai-depin-solana-guide-2026)); 2025 redesign, rewards not aligned with demand ([tokenomics](https://io.net/p/tokenomics)); idle payouts "prove supply, not demand" ([CoinStats](https://coinstats.app/news/ebc2fa233c63c4bdce129956893feb25d5a2753398ad033fb2c80bacfab8d0dc_GPU-Provider-Rewards-How-Networks-Pay-Without-Customers/)) | Not published | - | low |
| **Golem** (crypto) | Default 0.1 GLM per thread-h "might be high given current market rates" ([FAQ](https://docs.golem.network/docs/providers/provider-faq)); third-party USD 0-50/month, CPU-only less ([CashPilot](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/golem.md)) | "earnings are minimal" ([blog](https://www.publish0x.com/crypto-musings-consumer-impacts/golem-network-provider-earning-xpxrdgn)) | Salad × Golem partnership 2026-01-13, Zug ([Barchart](https://www.barchart.com/story/news/37019027/salad-com-and-golem-network-partner-to-integrate-web2-workloads-with-decentralized-infrastructure)) | low |
| **RunPod Community** | Buyer L4 USD 0.44/h, RTX 4090 USD 0.34/h (`cp-mkt-runpod-*`, `data/raw/market/compute-prices.csv`) | **No longer accepting new hosts** ([docs](https://docs.runpod.io/hosting)); blog 2026-02-24 ([runpod.io](https://www.runpod.io/blog/an-update-on-the-community-cloud-hosting-application-process)) | No uptime warranty ([ToS](https://conductatlas.com/platform/runpod/runpod-terms-of-service/no-uptime-warranty-for-community-cloud/)) | medium |
| **Folding@home / BOINC** (no revenue) | - | Even free volunteer work ran out: ≈ 20x surge in 2020 → "No WUs available"; 2021 CPU shortage, 1-6 h waits ([F@h](https://foldingathome.org/?p=7969), [forum](https://foldingforum.org/viewtopic.php?p=347031)) | Fully interruptible | medium / low |
| **Qarnot** (heaters, FR) | Sells compute to own customers, heat to buildings; mostly finance (BNP Paribas risk) ([TechCrunch 2023](https://techcrunch.com/2023/01/09/qarnot-creates-green-data-centers-by-putting-servers-in-central-heating-boilers/amp)) | Keeps surplus work so heat is on demand; "in winter we give capacity for student projects in 3D rendering **for free**": heat-driven capacity exceeds paid demand. Peer Nerdalize (NL) bankrupt 2019 ([DCD](https://datacenterdynamics.com/en/analysis/making-the-most-of-your-waste-heat)) | Own fleet and contracts | medium |
| **Heata** (UK, hot-water cylinders) | Host gets free hot water, no cash | "Expected utilisation" ≈ 3.6 of max 4.8 kWh/day (**≈ 75 %**), contractual minimum 2.5 kWh/day ([Tom's Hardware](https://www.tomshardware.com/news/heata-server-hot-water-trial-uk)); compute sold via Civo ([DCD](https://datacenterdynamics.com/en/news/civo-partners-with-heata-to-offer-batch-processing-on-digitized-boilers)) | Host has no access to server | medium |

**Reading.** Open marketplaces fill 25-35 % (neutral data) to 60 % (self-reported). The two
heat-first operators (Qarnot, Heata) are DigitalHeat's closest analogues; both sell to their own
customers, and Qarnot still had more winter capacity than paid work.

## 6. Big-cloud spot markets

| Fact | Source | Conf. |
|---|---|---|
| AWS interruption buckets (trailing 30 days) < 5 %, 5-10 %, 10-15 %, 15-20 %, > 20 %; historically < 5 % average | [Spot Advisor](https://aws.amazon.com/ec2/spot/instance-advisor), [bid advisor](https://aws.amazon.com/ec2/spot/bid-advisor/) | medium |
| Zurich (eu-central-2) pools in our CSV: g6.xlarge 5-10 %, g6.2xlarge 10-15 %, c7i.8xlarge > 20 % | `data/raw/market/compute-prices.csv` (Vantage) | medium |
| Termination differs strongly by AZ; r7i.2xlarge > 20 % in us-west-2b | [Spare Cores 2024-04](https://sparecores.com/article/spot-instance-terminate-rates-per-az) | medium |
| Average spot lifetime before termination "almost 4 hours" | [nOps](https://www.nops.io/blog/aws-spot-facts/) | low |
| Azure eviction rate = probability of eviction next hour (7-day history); Switzerland North: 250 of 255 sizes in lowest tier (2026-09-07) | [Microsoft Learn](https://learn.microsoft.com/azure/virtual-machines/spot-vms), [DevZero](https://www.devzero.io/spot-instances/azure/Switzerland%20North) | medium / low |
| Since the 2017 change AWS spot prices "adjust slowly over days and weeks"; average prices rose | [AWS blog](https://aws.amazon.com/blogs/compute/new-amazon-ec2-spot-pricing/), [Baughman 2019](https://par.nsf.gov/servlets/purl/10119475) | medium |
| GPU market moves over months: H100 1-year rent +40 % (USD 1.70 Oct 2025 → 2.35 Mar 2026); on-demand "sold out across all GPU types" | [SemiAnalysis](https://newsletter.semianalysis.com/p/the-great-gpu-shortage-rental-capacity) | medium |
| Intraday pricing rare; Nebius preemptible re-priced up to every 15 min; no hour-of-day cycle found | [Silicon Data](https://www.silicondata.com/blog/gpu-spot-prices-dynamic-pricing-price-discovery) | low |

Swiss-region spot is the **datacentre competitor** of our filler and an upper bound for its price.

## 7. Who buys interruptible capacity, and when

- **Buyers:** batch AI inference, training/sweeps, rendering/transcoding, CI, HPC simulation, genomics
  ([DigitalOcean](https://www.digitalocean.com/resources/articles/spot-gpu-instances),
  [Google Cloud](https://cloud.google.com/blog/products/compute/google-cloud-spot-vm-use-cases-and-best-practices),
  [AWS Spot](https://aws.amazon.com/ec2/spot)); finance risk (Qarnot); Salad names Stable Diffusion and
  audio transcription.
- **Time pattern:** no source for a daily/weekly cycle of batch demand; price indices are daily or
  weekly at best ([GetDeploying](https://getdeploying.com/dataset/gpu-prices)). **[A]** flat demand
  over the day. Seasonality of *our* supply matters more: most hours in winter, when Qarnot had surplus.
- **Saturation evidence:** idle-paid rewards (io.net), RunPod closing sign-ups, Salad "pause if no
  demand", Akash 25-34 %, F@h out of free work. GPU datacentre capacity is tight (SemiAnalysis):
  demand is for **reliable** capacity; consumer supply is the surplus, and heat-led availability makes
  us less reliable than a gamer PC.

## 8. Data rules for workloads in private homes (Switzerland)

- **revDSG** (in force 2023-09-01): processing customers' personal data = processor (Art. 9): written
  contract; controller must ensure data security (Art. 8); disclosure abroad only to adequate states
  or with safeguards (Art. 16) ([PwC](https://www.pwc.ch/de/insights/regulierung/die-auftragsbearbeitervereinbarung.html),
  [EDÖB](https://WWW.EDOEB.ADMIN.CH/dam/edoeb/de/Dokumente/datenschutz/leitfaden_tom.pdf.download.pdf/TOM_DE.pdf),
  [weka.ch](https://www.weka.ch/themen/datenschutz-und-it-recht/datenschutz-umsetzen/article/auftragsbearbeitung-was-muessen-kmu-beim-einsatz-von-auslaendischen-dienstleistern-beachten/)), medium.
  A Swiss home avoids Art. 16 but not Art. 8: the homeowner's physical access is a risk (encrypted
  disks, tamper detection, no host access as at Heata).
- **Art. 321 StGB** (doctors, lawyers): disputed whether IT/cloud providers are "Hilfspersonen";
  minimal external use advised; consent lifts it ([UZH study 2019](https://www.uzh.ch/cmsssl/ius/dam/jcr:d8363d82-0129-47cc-834a-948e871f0fee/2019%20-%20Schwarzenegger%20Thouvenin%20Stiller%20George%20-%20Nutzung%20von%20Cloud-Diensten%20durch%20Anw%C3%A4ltinnen%20und%20Anw%C3%A4lte.pdf),
  [FMH guide](https://live.fmh.ch/files/pdf29/leitfaden-geheimhaltungs--und-auftragsbearbeitungsvereinbarung.pdf)), medium.
- **Banks/insurers, FINMA Circular 2018/3:** material outsourcing needs due diligence, audit rights,
  security, continuity, sub-outsourcing rules; banking secrecy applies ([FINMA](https://www.finma.ch/en/news/2017/12/20171205-mm-rs-outsourcing/),
  [Baker McKenzie](https://www.bakermckenzie.com/-/media/files/insight/publications/br_zurich_newfinmaoutsourcingcircular_jan18_german.pdf?la=en)), medium.
- **Certifications:** Salad SOC 2 Type I; vast.ai SOC 2 Type II at platform level, Secure Cloud needs
  ISO 27001 DCs; RunPod Secure = Tier 3/4 DCs. Home hosts are **excluded from every "secure" tier**.

**Consequence:** filler carries data policy `public/non-personal` (non-confidential rendering,
open-data science, open-model inference on public/synthetic data, CI). Premium sensitive jobs
(transcription for practices) need DigitalHeat's own controlled fleet with contracts and consent: a
product, not marketplace filler.

## 9. Fill rate and price for the 1 kW reference server

**Fill rate φ [A]** = share of offered server-hours that get paid filler work.

| Case | φ | Basis |
|---|---|---|
| Pessimistic | 0.20 | below Akash CPU 26 % / GPU 25 %; reliability penalty of heat-led availability |
| **Base** | **0.35** | Akash GPU 34 % (Messari), vast third-party 30 %, Akash CPU 26 % |
| Optimistic | 0.60 | Akash self-reported 60 %; Heata ≈ 75 % of max |
| Anchor contract (variant) | 0.8-1.0 | own customers under contract (Qarnot, Heata); no Swiss buyer found, `TODO(source)` |

**Price per server-hour paid to DigitalHeat [A]** (method `computing-price.md` §2, `hardware.md`); host
share of buyer price **0.5-0.85 [A]** (vast ≈ 0.85, low; Salad unknown).

| Profile | Low | Central | High | Worked basis |
|---|---|---|---|---|
| `cpu-server` | **0.25** | **0.66** (adopted, Hetzner A3) | **1.20** | Low: Salad USD 0.006-0.007 per vCPU-h incl. 2 GB × 98 vCPU (6.15 boxes × 16 threads **[A]**) = CHF 0.50-0.58 × 0.5. High: 236 vCPU/kW (CCF, `computing-price.md` A4) = CHF 1.39 × 0.85 = 1.18. Upper bound Azure CH spot 1.80 |
| `gpu-inference-l4` | **0.85** | **1.42** | **2.20** | 11.6 L4 × AWS Zurich spot CHF 0.1914 = 2.22 (cap: DC interruptible). × RunPod community/secure ratios 0.38 (low), 0.64 (L4, central), method C1 |

Expected income per **offered** server-hour = φ × price: CPU base 0.35 × 0.66 = **CHF 0.23** (range
0.05-0.72); L4 base 0.35 × 1.42 = **CHF 0.50** (range 0.17-1.32). Compare electricity CHF 0.3325 and
break-even CHF 0.226.

## 10. How to model filler

1. Replace "unlimited" `F_{s,t} = N_s` by **`F_{s,t} = φ × N_s`** (fractional servers, keeps the LP),
   `φ` a scenario parameter (`FillerFillRate`, 0.35; sensitivity 0.2/0.6/1.0). Unsold hours idle (idle
   power, `hardware.md`) or off.
2. One flat price `p^φ` per hardware profile and scenario (CPU 0.66, L4 1.42, ranges above), no hourly
   series (no source shows an intraday cycle; spot moves over days to months). Optional monthly
   variant from the T-28 Azure snapshots.
3. Realism variant (not needed for the draft): seeded two-state busy/idle Markov series per site with
   mean `φ`, so filler arrives in blocks of hours.
4. Filler data policy = `public`; premium classes keep eligibility sets `E_c`.
5. Pitch wording: "filler is sold in ≈ 1 of 3 offered hours at CHF 0.25-1.20 (CPU); filler alone does
   not pay off for the CPU profile below φ ≈ 0.35: premium work or GPU hardware does."

**Open points:** Salad host earning rates per GPU/CPU (`TODO(source)`); vast.ai `usage_30d` (needs API
key); a Swiss buyer for interruptible heat-led capacity under contract; whether a marketplace accepts a
host offline most of the summer (reliability score).
