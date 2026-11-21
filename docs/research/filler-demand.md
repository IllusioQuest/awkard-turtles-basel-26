# Filler demand: can interruptible home compute be sold, how often, at which price?

> Status: **research note, exploration** (Claude subagent, 2026-11-21), answers open question 3 of
> decision 0014 ([`docs/model/routing-optimisation.md`](../model/routing-optimisation.md) §2.3,
> `F_{s,t}`). Not reviewed by the team. Recommendation, not a decision.
>
> **How sources were read.** WebFetch was blocked for every host tried (support.salad.com, vast.ai,
> sparecores.com, akash.network). All external figures come from **search-result snippets** of
> the named pages, accessed **2026-11-21**. Confidence: `medium` = snippet quotes the primary page,
> `low` = secondary source or unclear date. Every figure must be checked on the full page before
> the pitch. Assumptions are marked **[A]**.

## 1. Short answer

- **Sellable: yes, but not at every hour.** Every marketplace that buys distributed/consumer
  capacity shows **more supply than paid demand**. Measured or reported utilisation lies between
  **≈ 25 % and ≈ 60 %**; only vendor marketing claims 80 %.
- **Recommended fill rate** (share of offered server-hours that get paid filler work):
  **base 0.35, sensitivity 0.20 – 0.60 [A]**. A value of 1.0 is only defensible with an anchor
  customer under contract (Qarnot/Heata model) and must be labelled as such.
- **Price paid to the host per 1 kW server-hour [A]:** CPU profile **CHF 0.25 – 1.20** (keep the
  adopted 0.66 as central value); GPU-L4 profile **CHF 0.85 – 2.20** (central 1.42).
- **Data rule:** filler on open marketplaces runs on an *untrusted host*. It must be
  **non-personal, non-confidential** work. Personal data needs a processor contract and security
  (revDSG Art. 8/9); health, legal and bank data add professional or banking secrecy, which home
  servers cannot easily meet. Marketplaces' own "secure" tiers exclude homes (ISO 27001 data centres).
- **Model it as** `F_{s,t} = φ × N_s` (availability factor) times a flat price per profile, not
  "always available at 0.66". With φ = 0.35 the expected filler income per offered hour is
  0.35 × 0.66 = **CHF 0.23 (CPU)**, which is the case break-even of CHF 0.226. That is the honest
  headline: on the CPU profile, filler alone barely covers electricity net of heat value.

## 2. Marketplaces for distributed / consumer-hosted capacity

| Network | What hosts get | Utilisation / demand evidence | Interruption, hardware, data, payout | Conf. |
|---|---|---|---|---|
| **SaladCloud** (consumer PCs) | Host payout per GPU model shown in Salad's Network Monitor ("average earning rate", "top 25 %"); figures not in snippets, `TODO(source)`. Third-party: USD 0–100/month per host ([CashPilot guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/salad.md)). Buyer prices: CPU USD 0.004–0.005 per vCPU-h + 0.001 per GB-h ([Salad billing docs](https://docs.salad.com/container-engine/explanation/billing-pricing/billing.md)); RTX 4090 USD 0.16 (batch) – 0.33 (high priority) per h, 2026-09-12 ([GetDeploying](https://getdeploying.com/reference/cloud-gpu/nvidia-rtx-4090)) | Container jobs only "when one is available for your hardware"; otherwise fallback to bandwidth sharing or crypto mining; "if there is simply no demand for your hardware, consider pausing Salad" and set a demand alert; "the most reliable chefs with the best hardware will get priority" ([getting container jobs](https://support.salad.com/guides/getting-jobs/getting-container-jobs), [earning rate sporadic](https://support.salad.com/article/64-why-is-my-earning-rate-sporadic-random)). CPU workloads are "low-value" ([how does my machine earn](https://support.salad.com/article/356-how-does-my-machine-earn-salad-balance)) | Interruptible by design (host can stop any time, buyer re-queues). Container jobs need ≥ 70 GB free disk and virtualisation on. SOC 2 **Type I** only ([salad.com](https://salad.com/), [SemiAnalysis ClusterMAX](https://clustermax.semianalysis.com/cloudreview/saladcloud)); Salad's page has a section "What workloads are not a fit?" (content not in snippet) | medium (mechanics), low (numbers) |
| **vast.ai** (marketplace, home and DC hosts) | Host sets price; earnings = price × occupancy ([host optimisation guide](https://docs.vast.ai/host/optimization-guide.md)). Host keeps ≈ 85 % (third-party, [miningboard](https://miningboard.com/guides/rent-gpu-for-ai-earnings)); minimum payout USD 25 ([CashPilot vast guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/vast-ai.md)) | No official platform average. Vast's own example assumes **80 %** ([vast.ai article](https://vast.ai/article/how-much-money-can-you-earn-renting-out-your-gpu-on-vast-ai), marketing); third-party planning value **30 %**, month-1 earnings 30–40 % of steady state while reputation builds ([gmicloud](https://www.gmicloud.ai/en/blog/vast-ai-rent-my-gpu-explained)). API exposes `usage_30d` per GPU model ([docs](https://docs.vast.ai/api-reference/machines/show-gpu-metrics.md)) | Ubuntu ≥ 18.04, dedicated machine, CUDA ≥ 12, 3 open ports per GPU, 90 % reliability for verification ([verification stages](https://docs.vast.ai/host/verification-stages.md)). "Secure Cloud" = ISO 27001 data centres only; "individual hosts may have less formal security measures" ([compliance](https://vast.ai/compliance), [security FAQ](https://docs.vast.ai/guides/reference/faq/security.md)) | medium / low |
| **Akash Network** (decentralised, mostly DC providers) | Lease price in AKT/USDC set by bids | GPU: avg 198 GPUs used of 587 in Q4 2025 → **34 %** (Messari via [TokenPost](https://www.tokenpost.kr/research/1138)); Q1 2026 ≈ 25–34 % (figures do not reconcile, [Stocktwits](https://stocktwits.com/coins/akash-network)). CPU: live capacity page 5,930 of 22,629 CPUs active → **26 %** (snapshot date unknown, [network capacity](https://akash.network/about/network-capacity)); Q4 2023 CPU 56 % ([Akash recap](https://akash.network/blog/q4-2023-recap/)). Self-reported 60 % GPU ([2025 review](https://akash.network/blog/akash-2025-year-in-review/)) | Provider runs Kubernetes; no data guarantees beyond provider choice | medium (Messari), low (others) |
| **io.net** (crypto GPU network) | Hourly token rewards **paid even when idle** ([Dextools](https://www.dextools.io/news/what-is-io-net-decentralized-gpu-ai-depin-solana-guide-2026)); 2025 redesign because inflationary rewards were not aligned with demand ([io.net tokenomics](https://io.net/p/tokenomics)); analysis: idle payouts "prove supply, not demand" ([CoinStats](https://coinstats.app/news/ebc2fa233c63c4bdce129956893feb25d5a2753398ad033fb2c80bacfab8d0dc_GPU-Provider-Rewards-How-Networks-Pay-Without-Customers/)) | Utilisation not published | — | low |
| **Golem** (CPU/GPU, crypto) | Provider sets price; default 0.1 GLM per thread-hour "might be high given current market rates" ([provider FAQ](https://docs.golem.network/docs/providers/provider-faq)); third-party USD 0–50/month per device, CPU-only earns less ([CashPilot Golem](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/golem.md)) | "earnings are minimal" (personal blog, [publish0x](https://www.publish0x.com/crypto-musings-consumer-impacts/golem-network-provider-earning-xpxrdgn)) | Salad × Golem partnership 2026-01-13, Zug ([Chainwire/Barchart](https://www.barchart.com/story/news/37019027/salad-com-and-golem-network-partner-to-integrate-web2-workloads-with-decentralized-infrastructure)) | low |
| **RunPod Community Cloud** | Buyer: L4 USD 0.44/h, RTX 4090 USD 0.34/h (`cp-mkt-runpod-*`, `data/raw/market/compute-prices.csv`) | **No longer accepting new hosts** ([RunPod docs](https://docs.runpod.io/hosting)); no shutdown timeline (blog 2026-02-24, [runpod.io](https://www.runpod.io/blog/an-update-on-the-community-cloud-hosting-application-process)) | No uptime warranty for Community Cloud ([ToS excerpt](https://conductatlas.com/platform/runpod/runpod-terms-of-service/no-uptime-warranty-for-community-cloud/)) | medium |
| **Folding@home / BOINC** (no revenue) | — | Even free volunteer work ran out: ≈ 20× demand surge in 2020 → "No WUs available"; 2021 CPU shortage with 1–6 h waits between work units ([F@h update](https://foldingathome.org/?p=7969), [forum](https://foldingforum.org/viewtopic.php?p=347031)) | Fully interruptible | medium (official), low (forum) |
| **Qarnot** (heaters, FR) | Sells compute to own customers, heat to buildings; most paying customers in finance (BNP Paribas risk calculations) ([TechCrunch 2023](https://techcrunch.com/2023/01/09/qarnot-creates-green-data-centers-by-putting-servers-in-central-heating-boilers/amp)) | Keeps a surplus of work so heat is available on demand; "in winter we give capacity for student projects in 3D rendering **for free**" → heat-driven capacity exceeds paid demand in winter ([DCD](https://datacenterdynamics.com/en/analysis/making-the-most-of-your-waste-heat)). Peer Nerdalize (NL) went bankrupt 2019 (same) | Own fleet, own contracts | medium |
| **Heata** (UK, hot-water cylinders) | Host gets free hot water, no cash; Heata pays electricity | "Expected utilisation" gives ≈ 3.6 of max 4.8 kWh/day (**≈ 75 %**), contractual minimum 2.5 kWh/day ([Tom's Hardware](https://www.tomshardware.com/news/heata-server-hot-water-trial-uk)); compute sold via Civo batch processing ([DCD](https://datacenterdynamics.com/en/news/civo-partners-with-heata-to-offer-batch-processing-on-digitized-boilers)) | Host has no access to the server | medium |

**Reading.** Open marketplaces fill 25–35 % (neutral data) to 60 % (self-reported). The two
heat-first operators (Qarnot, Heata) are the closest analogues to DigitalHeat; both sell to their
own customers, and Qarnot still had more winter capacity than paid work.

## 3. Big-cloud spot markets

| Fact | Value | Source | Conf. |
|---|---|---|---|
| AWS interruption buckets (trailing 30 days) | < 5 %, 5–10 %, 10–15 %, 15–20 %, > 20 %; historically < 5 % average over all pools | [AWS Spot Instance Advisor](https://aws.amazon.com/ec2/spot/instance-advisor), [bid advisor](https://aws.amazon.com/ec2/spot/bid-advisor/) | medium |
| Zurich (eu-central-2) pools in our CSV | g6.xlarge 5–10 %, g6.2xlarge 10–15 %, c7i.8xlarge > 20 % | `data/raw/market/compute-prices.csv` (Vantage) | medium |
| Measured termination differs strongly by AZ; r7i.2xlarge > 20 % in us-west-2b | — | [Spare Cores 2024-04](https://sparecores.com/article/spot-instance-terminate-rates-per-az) | medium |
| Average spot lifetime before termination | "almost 4 hours" | [nOps](https://www.nops.io/blog/aws-spot-facts/) | low |
| Azure eviction rate = probability of eviction in the next hour (7-day history); Switzerland North: 250 of 255 sizes in the lowest tier (2026-09-07) | — | [Microsoft Learn](https://learn.microsoft.com/azure/virtual-machines/spot-vms), [DevZero](https://www.devzero.io/spot-instances/azure/Switzerland%20North) | medium / low |
| Price volatility: since the 2017 change AWS spot prices "adjust slowly over days and weeks", average prices rose afterwards | — | [AWS blog](https://aws.amazon.com/blogs/compute/new-amazon-ec2-spot-pricing/), [Baughman et al. 2019](https://par.nsf.gov/servlets/purl/10119475) | medium |
| GPU market moves on months, not hours: H100 1-year rent +40 % (USD 1.70 Oct 2025 → 2.35 Mar 2026), on-demand "sold out across all GPU types" | — | [SemiAnalysis](https://newsletter.semianalysis.com/p/the-great-gpu-shortage-rental-capacity) | medium |
| Intraday pricing is rare; Nebius preemptible re-priced up to every 15 min; no measured hour-of-day cycle found | — | [Silicon Data blog](https://www.silicondata.com/blog/gpu-spot-prices-dynamic-pricing-price-discovery) | low |

Spot capacity in Swiss regions is the **datacentre competitor** of our filler and an upper bound
for its price: it is interruptible too, but with SLAs on everything else.

## 4. Who buys interruptible capacity, and when

- **Buyers:** batch AI inference, batch training/sweeps, rendering/transcoding, CI tests, HPC
  simulations, genomics ([DigitalOcean](https://www.digitalocean.com/resources/articles/spot-gpu-instances),
  [Google Cloud blog](https://cloud.google.com/blog/products/compute/google-cloud-spot-vm-use-cases-and-best-practices),
  [AWS Spot](https://aws.amazon.com/ec2/spot)); finance risk runs (Qarnot). Salad names Stable
  Diffusion and audio transcription as typical container jobs.
- **Time pattern:** no source found for a daily or weekly demand cycle of batch/interruptible work;
  index snapshots are daily or weekly at best ([GetDeploying](https://getdeploying.com/dataset/gpu-prices)).
  **[A]** Assume flat demand over the day (batch queues are global and deadline-tolerant).
  Seasonality of *our* supply matters more: we offer most hours in winter, exactly when Qarnot had
  surplus capacity.
- **Saturation evidence:** idle-paid rewards (io.net), RunPod closing new host sign-ups, Salad's
  "pause if no demand", Akash 25–34 %, F@h running out of free work. GPU datacentre capacity, in
  contrast, is tight (SemiAnalysis). Demand is for **reliable** capacity; unreliable consumer
  supply is the surplus side. Our heat-led availability makes us *less* reliable than a gamer PC.

## 5. Data rules for workloads on servers in private homes (Switzerland)

- **revDSG** (in force 2023-09-01): DigitalHeat processing customers' personal data is a
  processor (Art. 9): written contract, and the controller must ensure the processor guarantees
  data security (Art. 8); disclosure abroad only to adequate states or with safeguards (Art. 16)
  ([PwC](https://www.pwc.ch/de/insights/regulierung/die-auftragsbearbeitervereinbarung.html),
  [EDÖB TOM guide](https://WWW.EDOEB.ADMIN.CH/dam/edoeb/de/Dokumente/datenschutz/leitfaden_tom.pdf.download.pdf/TOM_DE.pdf),
  [weka.ch](https://www.weka.ch/themen/datenschutz-und-it-recht/datenschutz-umsetzen/article/auftragsbearbeitung-was-muessen-kmu-beim-einsatz-von-auslaendischen-dienstleistern-beachten/)), medium.
  A home site in Switzerland avoids Art. 16 but not Art. 8: physical access by the homeowner is a
  security risk to be mitigated (encrypted disks, tamper detection, no host access as at Heata).
- **Professional secrecy, Art. 321 StGB** (doctors, lawyers): disputed whether IT/cloud providers
  are "Hilfspersonen"; literature advises minimal use of external providers with protected data;
  consent of the patient/client lifts it ([UZH study on lawyers' cloud use 2019](https://www.uzh.ch/cmsssl/ius/dam/jcr:d8363d82-0129-47cc-834a-948e871f0fee/2019%20-%20Schwarzenegger%20Thouvenin%20Stiller%20George%20-%20Nutzung%20von%20Cloud-Diensten%20durch%20Anw%C3%A4ltinnen%20und%20Anw%C3%A4lte.pdf),
  [FMH guide](https://live.fmh.ch/files/pdf29/leitfaden-geheimhaltungs--und-auftragsbearbeitungsvereinbarung.pdf)), medium.
- **Banks/insurers, FINMA Circular 2018/3 Outsourcing:** material functions require due diligence,
  audit rights, security, business continuity, rules for sub-outsourcing; banking secrecy still
  applies ([FINMA](https://www.finma.ch/en/news/2017/12/20171205-mm-rs-outsourcing/),
  [Baker McKenzie](https://www.bakermckenzie.com/-/media/files/insight/publications/br_zurich_newfinmaoutsourcingcircular_jan18_german.pdf?la=en)), medium.
- **Marketplace certifications:** Salad SOC 2 Type I, vast.ai SOC 2 Type II at platform level, but
  vast's Secure Cloud needs ISO 27001 data centres; RunPod Secure Cloud = Tier 3/4 DCs. Home hosts
  are **excluded from every "secure" tier** found.

**Consequence for the model:** filler class φ carries data policy `public/non-personal` (rendering
of non-confidential content, open-data science, open-model batch inference on public or synthetic
data, CI). Premium sensitive jobs (transcription for practices) need DigitalHeat's own controlled
fleet with contracts and consent; that is a product, not marketplace filler.

## 6. Fill rate and price for the 1 kW reference server

**Fill rate φ [A]** = share of offered server-hours that get paid filler work.

| Case | φ | Basis |
|---|---|---|
| Pessimistic | 0.20 | below Akash CPU 26 % / GPU 25 %; reliability penalty of heat-led availability |
| **Base** | **0.35** | Akash GPU 34 % (Messari), vast third-party 30 %, Akash CPU 26 % |
| Optimistic | 0.60 | Akash self-reported 60 %; Heata planning ≈ 75 % of max |
| Anchor contract (variant) | 0.8 – 1.0 | own customers under contract (Qarnot, Heata); no source for a Swiss buyer, `TODO(source)` |

**Price per server-hour paid to DigitalHeat [A].** Method of `computing-price.md` §2 and
`hardware.md`. Host share of marketplace buyer price **0.5 – 0.85 [A]** (vast ≈ 0.85, low; Salad
unknown). USD→CHF 0.8402.

| Profile | Low | Central | High | Worked basis |
|---|---|---|---|---|
| `cpu-server` | **0.25** | **0.66** (adopted, Hetzner A3) | **1.20** | Low: Salad buyer USD 0.006–0.007 per vCPU-h incl. 2 GB × 98 vCPU (6.15 boxes × 16 threads **[A]**) = CHF 0.50–0.58 × 0.5. High: 236 vCPU per kW (CCF, `computing-price.md` A4) = CHF 1.39 × 0.85 = 1.18. Upper bound Azure CH spot 1.80 |
| `gpu-inference-l4` | **0.85** | **1.42** | **2.20** | 11.6 L4 × AWS Zurich spot CHF 0.1914 = 2.22 (cap: datacentre interruptible). × RunPod community/secure ratios 0.38 (low) and 0.64 (L4, central), method C1 |

Expected filler income per **offered** server-hour = φ × price: CPU base 0.35 × 0.66 = **CHF 0.23**
(range 0.05 – 0.72); L4 base 0.35 × 1.42 = **CHF 0.50** (range 0.17 – 1.32). Compare electricity
CHF 0.3325 and case break-even CHF 0.226 per server-hour.

## 7. How to model it

1. Replace the default "unlimited" `F_{s,t} = N_s` by **`F_{s,t} = φ × N_s`** (servers, fractional:
   keeps the LP) with `φ` a scenario parameter (`FillerFillRate`, 0.35, sensitivity 0.2/0.6/1.0).
   Unsold hours are idle (idle power, `hardware.md`) or off.
2. **Price:** one flat `p^φ` per hardware profile and scenario (CPU 0.66, L4 1.42, ranges above),
   no hourly series: no source shows an intraday cycle, and spot prices move over days to months.
   Optional monthly variant from the T-28 Azure snapshots.
3. **Variant for realism (not needed for the draft):** a seeded two-state (busy/idle) Markov
   series per site with mean `φ`, so filler arrives in blocks of hours; deterministic by seed.
4. Data policy of filler = `public`; premium classes keep their own eligibility sets `E_c`.
5. State in the pitch: "filler is sold in ≈ 1 of 3 offered hours at CHF 0.25–1.20 (CPU); filler
   alone does not pay off for the CPU profile below φ ≈ 0.35 — premium work or GPU hardware does."

## 8. Open points

- Salad Network Monitor host earning rates per GPU/CPU (`TODO(source)`, needs support.salad.com).
- vast.ai `usage_30d` per GPU model (needs an API key; vast.ai host blocked).
- A Swiss buyer willing to take interruptible, heat-led capacity under contract (anchor customer).
- Whether a marketplace accepts a host that is offline most of the summer (reliability score).
