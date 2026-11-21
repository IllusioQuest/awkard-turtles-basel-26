# Ideas: service brainstorm and residential profit ideas

> **Merged 2026-11-21** from `ideas.md` (2026-11-20, one team member with Claude, an
> idea-generator and a rater subagent, both Sonnet) and `ideas.md` (Claude subagent,
> 2026-11-21). **Exploration, not confirmed by the team.** Nothing here changes a scored figure: the
> reference house stays as in [`../case/challenge.md`](../case/challenge.md); every idea is a
> **variant**.
>
> **How sources were read.** Web searches only; all facts come from **search-result snippets** of
> the named pages (accessed 2026-11-20 for Part A, 2026-11-21 for Part B), none opened in full.
> Check each on the full page before the pitch. Our own assumptions are marked **[A]**; missing
> numbers are `TODO(source)`. Prices and market data for modelling come from
> [`ai-services.md`](ai-services.md) (WP1), not from this file.

# Part A: what do the servers compute?

## A1. Current main plan (may change)

> **Update 2026-11-21 (decision 0018, proposed by Ennio):** two job types. **Type 1** = mainly
> transcription and a batch LLM service (T-33), OCR later; it earns the margin but fills only a tiny
> share of a house's capacity (T-02, [`demand.md`](demand.md)). **Type 2** = batch compute filler
> (Monte Carlo, simulations, research computing) sold through our own broker at a heat-led price; it
> makes the heat. The text below describes Type 1.

**Confidential batch AI for Swiss customers with sensitive data:** audio to text (incl. Swiss German
dialects: meetings, dictations, hearings, interviews) and document AI (OCR, indexing / embeddings,
classification, redaction). Same customers (law firms, notaries, medical practices, municipalities,
insurers), both batch with deadlines of hours to days, both split into small chunks (audio segment,
page) so pausing is cheap, both CPU, and a small version runs on a laptop for the demo (e.g.
Whisper, Tesseract). The selling point is data sovereignty, not price. Open: the trust gap (A4) and
willingness to pay (no Swiss provider with a published transcription price found).

## A2. How to think about services

Each idea answers three questions: (1) **workload shape (heat fit):** batch and pausable with long
deadlines fits heat; always-on and latency-bound only fits the small constant hot-water base load;
(2) **why us:** price (commodity, hard against big clouds), sovereignty (premium possible), green
(rarely decisive); (3) **customer group:** research, pharma, SMEs with sensitive data, public
sector, media, consumers, AI companies, finance. The scheduler depends on the workload only through
job properties (power, work amount, pausable / checkpoint cost, arrival and deadline, eligible
sites, data size, price); the service choice mainly affects the economics.

## A3. Ratings (rater subagent)

Scores 1 (bad) to 5 (good): **H** heat fit, **S** Swiss/privacy edge, **P** willingness to pay,
**C** CPU fit, **D** demo-ability on a laptop, **M** helps in summer.

| # | Idea | H | S | P | C | D | M | Total | Biggest weakness |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Confidential transcription (Swiss German) | 4 | 5 | 4 | 3 | 5 | 3 | 24 | Slow on CPU; summer demand not shown |
| 2 | Confidential document AI (OCR, redaction, search) | 4 | 5 | 4 | 4 | 4 | 3 | 24 | Swiss competitors exist; trust in home servers |
| 5 | Genomics / bioinformatics pipelines | 4 | 5 | 3 | 5 | 3 | 4 | 24 | Needs certified channels; big transfers |
| 3 | Drug-candidate screening (docking) | 5 | 3 | 3 | 4 | 3 | 5 | 23 | Pharma has own clusters and cloud |
| 16 | Open-science volunteer computing | 5 | 1 | 1 | 5 | 5 | 5 | 22 | No revenue; filler or demo job only |
| 12 | Financial risk batch | 3 | 4 | 3 | 5 | 4 | 2 | 21 | Bank audits; rigid deadline; weak in summer |
| 15 | Backup and archive verification | 4 | 4 | 2 | 4 | 4 | 3 | 21 | Commodity price; trust story |
| 8 | Media archive indexing | 5 | 3 | 2 | 2 | 3 | 5 | 20 | Pays little; needs GPU |
| 6 | Weather / climate ensembles | 4 | 1 | 2 | 5 | 3 | 4 | 19 | No sovereignty argument; free national compute |
| 7 | Video transcoding / rendering | 4 | 1 | 2 | 4 | 5 | 3 | 19 | Commodity; large file transfers |
| 14 | CI/CD and fuzzing | 2 | 3 | 3 | 5 | 4 | 2 | 19 | Builds short; only fuzzing fits |
| 20 | Digital twin / RL for heat grids | 4 | 2 | 3 | 4 | 3 | 3 | 19 | Few buyers |
| 4 | Engineering simulation (CFD, FEM) | 3 | 2 | 2 | 5 | 3 | 3 | 18 | Licence costs; no edge |
| 9 | Private fine-tuning | 4 | 4 | 4 | 1 | 2 | 3 | 18 | Not realistic on CPU |
| 22 | Household "personal cloud" | 4 | 4 | 1 | 3 | 3 | 3 | 18 | Consumers will not pay |
| 11 | Synthetic data generation | 4 | 2 | 2 | 3 | 3 | 3 | 17 | Vague customer |
| 13 | Distributed webhosting | 1 | 3 | 2 | 5 | 4 | 2 | 17 | Always-on; fights heat signal; uplink |
| 19 | Crypto mining | 5 | 1 | 1 | 1 | 4 | 5 | 17 | Not viable on CPU; at most a price-floor footnote |
| 10 | Overnight batch LLM inference | 3 | 4 | 3 | 1 | 2 | 3 | 16 | Needs GPU |
| 21 | Game servers | 1 | 2 | 2 | 4 | 3 | 1 | 13 | Always-on and latency-bound |
| 24 | Compute as art | 4 | 1 | 1 | 1 | 3 | 3 | 13 | GPU, no revenue |

Webhosting (#13) scores low as a *heat* workload but can still fit the constant hot-water base load
(≈ 0.6 servers per house all year, [`../case/key-figures.md`](../case/key-figures.md)); the webhosting
prototype was retired (decision 0026).

**Enablers** (business-model or scheduling ideas, not services): **#17 thermal-aware pricing /
compute-for-heat market** (heat signal becomes a price signal; flexible deadline = cheaper;
strongest for scheduling and creativity criteria). **#18 heat-labelled compute** (certificate "this
job heated a Basel home", auditable from simulation logs; add-on only). **#23 summer routing** (move
jobs to sites with summer heat demand: pools, hotels, farms; best answer to the 12.5 vs 0.6 servers
imbalance; needs data on such sites).

## A4. Demand evidence (low confidence, snippets, accessed 2026-11-20)

- **Transcription, plausible.** Big clouds sell Swiss German speech-to-text
  ([Azure](https://azure.microsoft.com/fr-fr/updates/release-of-swiss-german-dialect-speech-to-text/));
  research on local, privacy-preserving transcription in Swiss home care
  ([arXiv 2409.18819](https://ar5iv.arxiv.org/html/2409.18819)); OST/ZHAW privacy-aware Swiss German
  prototype ([slides](https://www.ost.ch/fileadmin/dateiliste/3_forschung_dienstleistung/institute/imp/events/technologietag_2025/technologietagost25_dintino.pdf)).
  No Swiss commercial provider with a published price found.
- **Document AI, plausible, leaning evidenced.** Swiss providers with Swiss hosting exist
  ([DeepCloud](https://www.deepcloud.swiss/en/newsroom/press-release/deepcloud-the-spinoff-of-abacus-is-taking-off-over-1-million-documents-processed-through-its-ai/),
  [SAPIENTROQ](https://sapientroq.sapient.tools/services/ai-document-automation)); a Swiss law firm
  moving to a secure cloud ([Swisscom / Balex](https://www.swisscom.ch/en/business/sme/more/testimonials/balex-ag.html)).
  Demand for the category is shown, not for a home-based provider.
- **Genomics, evidenced for sovereign compute, unclear for us:** served by certified university
  infrastructure ([BioMedIT / SPHN](https://sphn.ch/2026/07/02/biomedit-helps-shaping-europes-blueprint-for-secure-data-sharing/)).
- **Drug screening, engineering simulation:** commodity cloud markets
  ([OpenEye Orion on AWS](https://aws.amazon.com/blogs/hpc/virtual-screening-of-drug-compounds-with-orion),
  [Siemens Simcenter Cloud HPC](https://plm.sw.siemens.com/en-US/simcenter/integration-solutions/cloud-hpc/)); no Swiss locality premium found.
- **Financial risk:** regulatory driver (FINMA outsourcing rules,
  [Baker McKenzie](https://www.bakermckenzie.com/-/media/files/insight/publications/br_zurich_newfinmaoutsourcingcircular_jan18.pdf/1000)),
  but private homes add audit burden.

**Biggest risk: the trust gap.** Every high-value service needs customers to accept confidential
data on a server in a private home, while Swiss buyers use certified, audited channels. Answers to
check: **confidential computing** (AMD SEV, Intel TDX; data encrypted even from the machine's
holder); **sites with an operator** (apartment blocks, schools, hotels; locked room); **customer =
host** (a firm or practice heats its own building); **splitting work** (each server sees one
chunk); **hyper-local sovereignty** (region filter on which sites may run a job).

## A5. Parked ideas

| Other things to compute (not rated) | Why interesting | Catch |
|---|---|---|
| Sandboxes for training AI agents | CPU-heavy, short restartable episodes, large demand from AI labs; summer filler | Price only, no Swiss edge |
| Winter-peaking workloads (tax-return season, avalanche/snow forecasting, flu epidemiology, heating-bill analytics) | Demand season matches heat season; paper angle | Each market small |
| Bioacoustics (bird/bat detection, e.g. BirdNET) | Reuses audio pipeline, CPU, long deadlines, strong demo | Pays little; summer filler |
| Materials screening (batteries, catalysts, solar cells) | Long checkpointed CPU jobs, Swiss research partners | Researchers have clusters |
| Restoring archive TV footage | Strong visuals, heritage, very long deadlines | Needs GPU |
| Machine-checked proofs, formal verification | CPU, hours to days, pausable | Niche, hard to reach buyers |
| Overnight planning (routes, timetables, shifts) | Night work = high heat demand, CPU | Usually in-house |

**Pitch features:** **Alp-Cloud** (in summer compute "moves up" to colder alpine sites: hotels,
spas, pools; testable with Open-Meteo data). **The heater transcribes our pitch live** and pauses
when the slider moves to July (the must-have demo with the real service). **Compute weather
forecast** (map of when and where compute is cheap). **"Your shower transcribed a court hearing"**
(heat as work for the homeowner view, from measured energy per audio hour; no number until
measured). **Houses bid for jobs** (heat-demand-aware auctions; paper topic).

**Other directions** (change a basic assumption): heat customers with year-round or summer demand
(cheese dairies, hay drying, breweries, laundries, pools, greenhouses; check temperatures);
customer = host; grid flexibility (fast pausing as flexible load, absorbing midday solar surplus);
store winter heat as data (results that stay valuable for months); sell the scheduler as a plugin
(Kubernetes, SLURM, data centres with district heating); private household AI assistant on the
basement server (fits the hot-water base load).

# Part B: making residential houses pay off

Ideas already in Part A, [`competitors.md`](competitors.md) and [`../vision.md`](../vision.md)
(confidential computing, Alp-Cloud, heat-labelled compute, thermal-aware pricing, summer routing,
refurbished hardware as such) are not repeated; where an idea builds on one, it says so.

## B1. Starting point (our numbers)

Per 1 kW server-hour: electricity CHF 0.3325, heat value if used CHF 0.106
([`../case/key-figures.md`](../case/key-figures.md)). L4 profile: purchase CHF 34,244 per kW,
depreciation CHF 1.007 + operations 0.125 per sold hour at 5,665 sold h/yr (= 22,661 ÷ 4 servers),
full-cost break-even **CHF 1.36** ([`hardware.md`](hardware.md)). Filler marketplaces fill ≈ 35 % of
offered hours and sourced premium demand fills < 1 % ([`demand.md`](demand.md)). Competitors failed
in single homes because of seasonality and fixed costs per site ([`competitors.md`](competitors.md)
§4). So a residential idea matters only if it **raises sold hours per server**, **cuts hardware cost
per sold hour**, **lowers the marginal electricity price**, or **adds a second income that needs no
compute buyers**.

## B2. All ideas, rated

Scores 1-5: **P** profit impact (host + DigitalHeat), **F** feasibility in Switzerland, **E**
evidence now, **D** demo-ability in 5 minutes, **N** novelty. Strict rater: a 5 needs a mechanism
*and* a source.

| # | Idea (one line) | P | F | E | D | N | Σ |
|---|---|---|---|---|---|---|---|
| 1 | **20 flats every day:** multi-family houses sized by hot water | 5 | 4 | 4 | 4 | 3 | **20** |
| 2 | **Sun-compute:** filler on own PV surplus | 3 | 5 | 4 | 5 | 3 | **20** |
| 3 | **Firm + spot split** | 4 | 3 | 3 | 4 | 4 | **18** |
| 4 | **Cascade heat pump** | 3 | 4 | 3 | 3 | 4 | **17** |
| 5 | **GPU retirement home** | 5 | 3 | 3 | 2 | 3 | **16** |
| 6 | **Abwärme compliance + CO₂ credits** | 3 | 3 | 3 | 3 | 4 | **16** |
| 7 | **Heating-season compute pass** | 3 | 3 | 3 | 3 | 4 | **16** |
| 8 | **LEG cluster** | 3 | 3 | 3 | 3 | 3 | **15** |
| 9 | Larger tank (e.g. 3,000 l) instead of more servers: cheapest kWh of flexibility | 2 | 5 | 4 | 3 | 1 | 15 |
| 10 | Deadline price menu (1 h / 24 h / 7 d), builds on enabler #17 | 3 | 4 | 2 | 4 | 2 | 15 |
| 11 | Balancing / winter-reserve pool: servers as fastest sheddable load (Swissgrid pools ≥ 5 MW); builds on parked "grid flexibility" | 2 | 3 | 2 | 4 | 3 | 14 |
| 12 | Municipal anchor tenant: canton digitises archives on servers in its own social housing (customer = host) | 3 | 3 | 2 | 2 | 4 | 14 |
| 13 | Dynamic network tariffs (allowed from 2026): run in low-tariff hours, tank shifts heat | 2 | 3 | 3 | 3 | 2 | 13 |
| 14 | Weekly Legionella flush as a "hot job" (servers + booster heat tank to 60 °C) | 1 | 4 | 2 | 3 | 3 | 13 |
| 15 | Heat subscription ("Wärme-Abo") below heat-pump cost, contracting finances tank and backup | 2 | 4 | 3 | 2 | 1 | 12 |
| 16 | Confidential computing badge (attested VM) in the homeowner view | 3 | 2 | 3 | 2 | 2 | 12 |
| 17 | Gamified homeowner app ("your house rendered a film") | 1 | 5 | 1 | 4 | 1 | 12 |
| 18 | Utility as sales channel (IWB sells it where no district heating is planned) | 3 | 3 | 2 | 1 | 2 | 11 |
| 19 | Host-owned servers financed via green-mortgage discount, like PV | 2 | 2 | 2 | 1 | 3 | 10 |
| 20 | Nomadic hardware: GPU cartridges move to pools/data centre in summer | 2 | 1 | 1 | 2 | 4 | 10 |
| 21 | Winter-peaking workloads (tax season, snow forecasts); repeat of A5 | 2 | 3 | 1 | 2 | 2 | 10 |
| 22 | Insurer partner: building insurer discounts houses with monitored tank/boiler | 1 | 2 | 1 | 1 | 3 | 8 |

## B3. Top 8

**B3.1 "One house in January = 20 flats every day" (Σ 20).** The case's tension (12.5 servers in
the coldest hour vs 0.6 in summer) turns around in a multi-family building: hot water is year-round,
so 12.5 servers are needed **every day**. 12.5 × 19.2 kWh = 240 kWh/day = **20 households × 12
kWh/day** (case hot-water value per household of four; smaller flats need more of them **[A]**).
Sized to hot water, servers run ≈ 8,760 h/yr instead of 5,665. Impact (formula `hardware.md` §1):
L4 depreciation 34,244 ÷ 6 ÷ 8,760 = CHF 0.65, operations 0.08 → break-even 0.33 + 0.65 + 0.08 −
0.106 = **CHF 0.96 instead of 1.36 (−30 %)**, before space heating adds winter hours. Evidence:
Leafcloud accepts only sites with ≥ 500 GJ/yr hot water; Infomaniak runs a data centre in the
basement of the Geneva housing cooperative La Bistoquette (snippet, ABB/Infomaniak news). Risk:
central hot water must reach ≈ 60 °C (Legionella), above raw server heat, so needs B3.4; per-flat
hot-water profile `TODO(source)`; peak draw needs a larger tank. Pitch: the single house's server
bar collapses in July, the 20-flat bar stays full: "it is the cooperative, not the villa."

**B3.2 Sun-compute (Σ 20).** In non-heating hours hardware is sunk and the marginal cost of a filler
hour is electricity ([`hardware.md`](hardware.md) §3). A PV house gets only the feed-in tariff for
midday surplus: national minimum **6 Rp/kWh for systems ≤ 30 kW from 2026** (Swissolar summary,
pv-magazine), EKZ 1.2-6.2 Rp (IWB rate `TODO(source)`). Marginal cost falls from CHF 0.3325 to ≈
0.06 per server-hour; filler margin rises from 0.33 to 0.60 (CPU, CHF 0.66) or 1.09 to 1.36 (L4, CHF
1.42); with ≈ 750 surplus hours/yr **[A]** (5 h × 150 days; computable from Open-Meteo irradiance in
`data/raw/open-meteo/`) ≈ **CHF 200 per server-year** more, and summer heat charges the tank instead
of being dumped. Risk: PV is not in the reference house; filler sells only ≈ 35 % of the time; only
PV houses. Pitch: July demo day, real job starts when the sun curve crosses the house load and
pauses at sunset (same scheduler; the case's "Prepper" stretch goal).

**B3.3 Firm + spot split (Σ 18).** Buyers pay for *reliable* capacity; heat-led home capacity is the
unreliable surplus ([`demand.md`](demand.md) §7). But the year-round hot-water base is predictable:
reference house 4,380 kWh/yr ÷ 0.8 = **5,475 server-hours/yr (24 % of 22,661)**, growing with
multi-family houses (B3.1) and pools. Sell it as **reserved capacity** (fixed monthly price, φ ≈ 1),
only the winter peak as spot (φ ≈ 0.35). Impact **[A]**: at L4 CHF 1.42, moving 5,475 h from φ 0.35
to 1.0 adds 5,475 × 1.42 × 0.65 ≈ **CHF 5,000 per house-year** (about one L4 depreciation). Evidence:
Leafcloud discounts for ≥ 1-month commitments; RunPod community vs secure L4 ratio 0.64
(`computing-price.md` C1). Risk: no named buyer for reserved home capacity (`TODO(source)`); base
needs N+1 redundancy across houses.

**B3.4 Cascade heat pump (Σ 17).** The reference house keeps a backup heater anyway
([`../vision.md`](../vision.md)); make it a water-to-water heat pump sourced from the server loop
(35-45 °C) instead of outdoor air. A 35 → 65 °C lift has Carnot COP ≈ 11; at 40-50 % of Carnot,
**COP ≈ 4.5-5.5 [A]** (check manufacturer data, `TODO(source)`). In the 4-server run the backup
supplies 13,102 kWh; at COP 2.5 that is 5,241 kWh = CHF 1,743, at COP 5 CHF 871 → **≈ CHF 870/yr
saved for the host**, and 60 °C hot water answers the case owner's Legionella concern
(`deep-dive/notes-structured.md`). Evidence: high-temperature heat pumps boosting data-centre heat
(ORNL); AEE INTEC models. Risk: breaks the case simplification "no heat pump" (variant only); extra
hydraulics; loop must be liquid-cooled.

**B3.5 GPU retirement home (Σ 16).** The lowest-utilisation site should hold the lowest-capital
hardware: data centres need GPUs near full use in years 1-3, a heat-led house can use them in years
4-8. DigitalHeat leases the tail years or buys at resale. Depreciation is linear in price; at 50 % of
new price **[A]** the L4 break-even falls from CHF 1.36 to ≈ 0.86 per sold hour. Evidence: used A100
40 GB PCIe ≈ USD 4,200-6,000, −30 % in a year (AMCompute, Aliteq snippets); Qarnot used refurbished
OCP servers; used L4 prices `TODO(source)`. Risk: shorter remaining life, failure rate, lower
earnings per hour, volatile resale.

**B3.6 Abwärme compliance and CO₂ credits (Σ 16).** In Basel-Stadt a replaced heating system must
switch to renewables where feasible without extra cost, and **waste heat not from fossil processes
counts** (cantonal brochure, energy law definition of Abwärme): a compliance path for houses leaving
oil or gas, a ready customer pipeline. KliK paid **CHF 100 per certified t CO₂** for waste-heat and
heat-network programmes until 2030; a trade article mentions a server-waste-heat project at that rate
(snippet). Impact **[A]**: 18,129 kWh server heat replacing oil at ≈ 0.29 kg CO₂/kWh heat
(`TODO(source)`: BAFU factor and boiler efficiency) ≈ 5.3 t → **≈ CHF 530/yr per house** (≈ CHF 0.02
per server-hour); plus possible cantonal subsidy (up to 40 % of investment for renewable heating;
server-heat eligibility `TODO(source)`). Risk: additionality and minimum project size; heat-pump
subsidies in district-heating areas were halved in 2025, so target houses outside them.

**B3.7 Heating-season compute pass (Σ 16).** Sell the winter peak (Nov-Mar, 12.5 servers) as a
**season allocation** for non-personal batch work: open-model evaluation (e.g. Apertus), benchmark
sweeps, synthetic data on public data, research simulations; legal in homes (no personal data,
[`demand.md`](demand.md) §8) and checkpoint-friendly. Evidence: the Swiss AI Initiative distributes
compute in open calls (10-15 M GPU-hours per call; large grants > 500 k GPU-hours; Apertus training
> 6 M GPU-hours on CSCS Alps). Impact **[A]**: one 4-server L4 house offers ≈ 46 L4 cards × 5,665 h
≈ 263,000 L4-hours/yr; a seasonal contract at φ ≈ 1 lifts winter income like B3.3. Risk: academic
compute is subsidised at CSCS; L4-hours are not GH200-hours; needs an institutional anchor.

**B3.8 LEG cluster (Σ 15).** Since 1 January 2026, local electricity communities (LEG) may sell PV
power within a neighbourhood or municipality with a **40 % grid-fee discount (20 % across a
transformer)** (EKZ tariff sheet, energiezukunft.eu); ≈ 1,860 LEGs were founded in H1 2026 (Swissolar
via solarserver.de). A single-family house with servers becomes the LEG's flexible buyer of
neighbours' summer PV (extends B3.2 to houses without PV), and the LEG is the natural unit for
routing jobs between houses (T-10/T-12). Impact: LEG price and grid-fee share of the IWB H4 price
`TODO(source)`; the only assumption-free claim is that the price falls by part of the grid fee.
Risk: modest gains; metering and billing overhead; no heat network between houses.

## B4. Recommendation

- **Bet the pitch on B3.1 and B3.2.** B3.1 answers "why did residential fail, and what works?" with
  one picture from the case's own numbers; B3.2 turns the weak July must-have demo day into a strong
  one with data already in the repo and matches the Prepper stretch goal.
- B3.3 is the strongest income lever but has no buyer yet (the "next conversation"); B3.4 is the best
  answer to a Legionella question in Q&A.
- Cheap wins without a pitch slot: #9 (tank size sensitivity) and #14 (Legionella flush).
- Everything here is a variant; the scored reference house and break-even stay as the case defines.

## B5. Sources (Part B, all accessed 2026-11-21, search snippets)

- Basel-Stadt energy subsidies 2026, heat-pump rules: https://media.bs.ch/original_file/a85f31f102af942f4dc1451cbc6dd0f8cad4d582/broschuere-foerderbeitraege-energie-bs-2026-ua.pdf ; https://www.bs.ch/wsu/aue/abteilung-energie/foerderbeitraege-energie
- Basel energy law, renewable replacement, Abwärme: https://www.gesetzessammlung.bs.ch/api/de/versions/4366/pdf_file ; https://media.bs.ch/original_file/df578364dbe57ee43c6592cb12567c5bf5adacd6/flyer-antworten-auf-fragen-zum-heizungsersatz-2019-0.pdf
- KliK CHF 100/t for waste heat: https://s3.amazonaws.com/a.storyblok.com/f/246794/cd61d05ad2/fachartikel_8-haustech_industrielle-abwaerme_19.pdf ; https://www.thermdis.eawag.ch/static/templates/files/ProgrammWaermeverbuende_Vogelsanger_Aqua&Gas_2017-09.pdf
- LEG rules and adoption: https://www.energiezukunft.eu/buergerenergie/einfache-regeln-netzentgeltrabatte-und-neu-gewonnene-freiheiten ; https://www.ekz.ch/dam/ekz/angebote/strom/produzenten-und-gemeinschaften/leg/EKZ-Einsiedeln-Tarifblatt-LEG-ENP26.pdf ; https://www.solarserver.de/2026/09/02/schweiz-gruendet-1-860-lokale-stromgemeinschaften-leg-im-ersten-halbjahr-2026/
- PV feed-in 2026: https://www.pv-magazine.de/2026/01/12/swissolar-veroeffentlicht-neuregelungen-fuer-photovoltaik-anlagen-2026-auf-einen-blick/ ; https://www.ekz.ch/dam/ekz/privatkunden/strom/tarife-und-agb/Tarifdokumente/tarife-2026/ekz-rueckliefertarife-2026.pdf
- Dynamic network tariffs: https://www.uvek.admin.ch/dam/uvek/de/dokumente/dasuvek/abstimmungen/faktenblatt-5-vorlage-sichere-stromversorgung-stromnetz.pdf.download.pdf/faktenblatt-5-vorlage-sichere-stromversorgung-stromnetz.pdf ; https://www.srf.ch/news/schweiz/dynamische-stromtarife-ekz-bieten-dynamische-tarife-an-viele-andere-firmen-noch-nicht
- Balancing pools (tiko, ≥ 5 MW): https://heatpumpingtechnologies.org/?p=54362 ; https://www.swissgrid.ch/content/dam/swissgrid/about-us/newsroom/newsfeed/2026/260115-MM-Regelenergiemarkt-de.pdf
- Winter reserve ordinance: https://www.swissgrid.ch/dam/jcr:cbc77205-fb55-4be0-a229-304812908430/20260803-Swissgrid-Stellungnahme-SResV.pdf
- Infomaniak data centre in a housing cooperative: https://new.abb.com/news/de/detail/124445/abgesichert-mit-usv-von-abb-infomaniak-rechenzentrum-wird-tausende-genfer-haushalte-heizen
- Data-centre heat with high-temperature heat pumps: https://ornl.gov/publication/district-heating-utilizing-waste-heat-data-center-high-temperature-heat-pumps ; https://www.aee-intec.at/wp-content/uploads/2026/05/Zotter_G.pdf
- Used GPU prices: https://www.amcompute.com/appraisals/a100-pcie-40gb ; https://aliteq.com/a100-price ; https://hashrateindex.com/blog/used-gpu-market-pricing-deprecation-secondary-ai/
- GPU confidential computing (H100, not L4): https://developer.nvidia.com/blog/confidential-computing-on-h100-gpus-for-secure-and-trustworthy-ai/
- Swiss AI Initiative compute: https://swiss-ai.org/ ; https://aic-reo-call.epfl.ch/ ; https://arxiv.org/abs/2604.12973
