# Service brainstorm: what do the servers compute?

> Brainstorm from 2026-11-20 (one team member with Claude, an idea-generator subagent and a rater
> subagent, both Sonnet). **Not confirmed by the team.** Demand evidence is low confidence: the
> rater only read search-result snippets, no page was opened in full. Prices and market data for
> modelling come from [`ai-services.md`](ai-services.md) (WP1), not from this file.

## Current main plan (may change)

> **Update 2026-11-21 (decision 0015, proposed by Ennio, team confirmation pending):** two job
> types. **Type 1** = the confidential batch AI below plus a local LLM (T-29); it earns the margin
> but its demand fills only a tiny share of a house's capacity (T-02, `job-demand.md`).
> **Type 2** = interruptible batch compute (Monte Carlo, parameter sweeps, research simulations)
> sold by the server-hour at a heat-led dynamic price; it produces most of the heat. Matches
> enablers #17 (thermal-aware pricing) and the "sandboxes for AI agents" idea below. The trust gap
> now has a second side: third-party code in a private home (sandbox, no network, separated from
> Type 1 data). The text below describes Type 1.

**Confidential batch AI for Swiss customers with sensitive data:**

- **Audio to text** (transcription, incl. Swiss German dialects): meetings, dictations, hearings,
  interviews.
- **Document AI:** OCR, indexing / embeddings, classification and redaction of documents and
  archives.

Why: same customers (law firms, notaries, medical practices, municipalities, insurers), at least two
job types, both batch with deadlines of hours to days, both split into small chunks (audio segment,
page) so pausing is cheap, both run on CPU, and a small version runs on a laptop for the live demo
(e.g. Whisper, Tesseract). The selling point is data sovereignty, not price.

Open: the **trust gap** (see below) and willingness to pay (no Swiss provider with a published
transcription price found yet).

## How to think about services

Each service idea answers three separate questions:

1. **Workload shape (heat fit):** batch and pausable with long deadlines fits heat; always-on and
   latency-bound only fits the small constant hot-water base load.
2. **Why us:** price (commodity, hard against big clouds), sovereignty (premium possible), green
   (nice extra, rarely decisive).
3. **Customer group:** research, pharma, SMEs with sensitive data, public sector, media, consumers,
   AI companies, finance.

The scheduler depends on the workload only through a few job properties (power, work amount,
pausable / checkpoint cost, arrival and deadline, eligible sites, data size, price). The choice of
service mainly affects the economics, not the scheduler.

## Ratings (rater subagent)

Scores 1 (bad) to 5 (good): **H** heat fit, **S** Swiss/privacy edge, **P** willingness to pay,
**C** CPU fit, **D** demo-ability on a laptop, **M** helps in summer.

| # | Idea | H | S | P | C | D | M | Total | Biggest weakness |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Confidential transcription (Swiss German) | 4 | 5 | 4 | 3 | 5 | 3 | 24 | Slow on CPU for large models; summer demand not shown |
| 2 | Confidential document AI (OCR, redaction, search) | 4 | 5 | 4 | 4 | 4 | 3 | 24 | Swiss competitors exist; trust in home servers |
| 5 | Genomics / bioinformatics pipelines | 4 | 5 | 3 | 5 | 3 | 4 | 24 | Needs certified channels; large data transfers |
| 3 | Drug-candidate screening (docking) | 5 | 3 | 3 | 4 | 3 | 5 | 23 | Pharma has own clusters and cloud |
| 16 | Open-science volunteer computing | 5 | 1 | 1 | 5 | 5 | 5 | 22 | No revenue; filler or demo job only |
| 12 | Financial risk batch | 3 | 4 | 3 | 5 | 4 | 2 | 21 | Bank audits; rigid overnight deadline; weak in summer |
| 15 | Backup and archive verification | 4 | 4 | 2 | 4 | 4 | 3 | 21 | Commodity price; trust story |
| 8 | Media archive indexing | 5 | 3 | 2 | 2 | 3 | 5 | 20 | Pays little; needs GPU |
| 6 | Weather / climate ensembles | 4 | 1 | 2 | 5 | 3 | 4 | 19 | No sovereignty argument; free national compute |
| 7 | Video transcoding / rendering | 4 | 1 | 2 | 4 | 5 | 3 | 19 | Commodity; large file transfers |
| 14 | CI/CD and fuzzing | 2 | 3 | 3 | 5 | 4 | 2 | 19 | Builds short and latency-sensitive; only fuzzing fits |
| 20 | Digital twin / RL for heat grids | 4 | 2 | 3 | 4 | 3 | 3 | 19 | Few buyers |
| 4 | Engineering simulation (CFD, FEM) | 3 | 2 | 2 | 5 | 3 | 3 | 18 | Licence costs dominate; no real edge |
| 9 | Private fine-tuning | 4 | 4 | 4 | 1 | 2 | 3 | 18 | Not realistic on CPU |
| 22 | Household "personal cloud" | 4 | 4 | 1 | 3 | 3 | 3 | 18 | Consumers will not pay |
| 11 | Synthetic data generation | 4 | 2 | 2 | 3 | 3 | 3 | 17 | Vague customer |
| 13 | Distributed webhosting | 1 | 3 | 2 | 5 | 4 | 2 | 17 | Always-on, fights the heat signal; home uplink and uptime |
| 19 | Crypto mining | 5 | 1 | 1 | 1 | 4 | 5 | 17 | Not viable on CPU; at most a price-floor footnote |
| 10 | Overnight batch LLM inference | 3 | 4 | 3 | 1 | 2 | 3 | 16 | Needs GPU |
| 21 | Game servers | 1 | 2 | 2 | 4 | 3 | 1 | 13 | Always-on and latency-bound |
| 24 | Compute as art | 4 | 1 | 1 | 1 | 3 | 3 | 13 | GPU, no revenue |

Note on #13: webhosting scores low here as a *heat* workload. It can still fit the constant
hot-water base load (≈ 0.6 servers per house all year, see [`../case/key-figures.md`](../case/key-figures.md)),
and a teammate is prototyping it (`docs/webhosting-*.md`).

**Enablers** (business-model or scheduling ideas, not services):

- **#17 Thermal-aware pricing / compute-for-heat market:** the heat signal becomes a price signal;
  flexible deadline = cheaper. Strongest enabler for the scheduling and creativity criteria.
- **#18 Heat-labelled compute:** certificate "this job heated a Basel home", auditable from our
  simulation logs. Add-on only.
- **#23 Summer routing:** move jobs to sites with summer heat demand (pools, hotels, farms). Best
  answer to the 12.5 vs 0.6 servers imbalance; needs data on such sites.

## Demand evidence (low confidence, snippets only, accessed 2026-11-20)

- **Transcription — plausible.** Big clouds sell Swiss German speech-to-text
  ([Azure](https://azure.microsoft.com/fr-fr/updates/release-of-swiss-german-dialect-speech-to-text/));
  research on local, privacy-preserving transcription in Swiss home care
  ([arXiv 2409.18819](https://ar5iv.arxiv.org/html/2409.18819)); OST/ZHAW privacy-aware Swiss German
  transcription prototype ([slides](https://www.ost.ch/fileadmin/dateiliste/3_forschung_dienstleistung/institute/imp/events/technologietag_2025/technologietagost25_dintino.pdf)).
  No Swiss commercial provider with a published price found.
- **Document AI — plausible, leaning evidenced.** Swiss providers with Swiss hosting exist
  ([DeepCloud](https://www.deepcloud.swiss/en/newsroom/press-release/deepcloud-the-spinoff-of-abacus-is-taking-off-over-1-million-documents-processed-through-its-ai/),
  [SAPIENTROQ](https://sapientroq.sapient.tools/services/ai-document-automation)); a Swiss law firm
  moving to a secure cloud while digitising ([Swisscom / Balex](https://www.swisscom.ch/en/business/sme/more/testimonials/balex-ag.html)).
  Demand for the category is shown, not for a home-based provider.
- **Genomics — evidenced for sovereign compute, unclear for us.** Served by certified university
  infrastructure ([BioMedIT / SPHN](https://sphn.ch/2026/07/02/biomedit-helps-shaping-europes-blueprint-for-secure-data-sharing/)).
- **Drug screening, engineering simulation — commodity cloud markets** (e.g.
  [OpenEye Orion on AWS](https://aws.amazon.com/blogs/hpc/virtual-screening-of-drug-compounds-with-orion),
  [Siemens Simcenter Cloud HPC](https://plm.sw.siemens.com/en-US/simcenter/integration-solutions/cloud-hpc/)).
  No Swiss locality premium found.
- **Financial risk — regulatory driver** (FINMA outsourcing rules,
  [Baker McKenzie summary](https://www.bakermckenzie.com/-/media/files/insight/publications/br_zurich_newfinmaoutsourcingcircular_jan18.pdf/1000)),
  but a network of private homes adds audit burden.

## The biggest risk: the trust gap

Every high-value service depends on customers accepting confidential data on a server in a private
home, while Swiss buyers of sensitive compute use certified, audited channels. Possible answers, to
be checked:

- **Confidential computing** (confidential VMs such as AMD SEV or Intel TDX): data stays encrypted
  even from whoever physically holds the machine.
- **Sites with an operator:** apartment blocks, schools, hotels; servers in a locked room.
- **Customer = host:** the firm or practice runs the servers in its own building and heats it.
- **Splitting work:** each server sees only one chunk (audio segment, page).
- **Hyper-local sovereignty:** data never leaves the municipality or canton (a region filter on
  which sites may run a job).

## Further ideas (parked)

**Other things to compute** (not rated yet):

| Idea | Why interesting | Catch |
|---|---|---|
| Sandboxes for training AI agents (run code, run tests, score) | CPU-heavy, short restartable episodes, large current demand from AI labs; good summer filler | Price only, no Swiss edge |
| Workloads that peak in winter (tax-return season, avalanche and snow forecasting, flu epidemiology, heating-bill analytics) | Demand season matches heat season; paper angle | Each market small |
| Bioacoustics: bird and bat detection in forest recordings for biodiversity monitoring | Reuses the audio pipeline, CPU (e.g. BirdNET), long deadlines, strong demo | Pays little; summer filler |
| Materials screening (batteries, catalysts, solar cells) | Long checkpointed CPU jobs, Swiss research partners | Researchers have clusters |
| Restoring archive TV footage | Strong visuals, heritage, very long deadlines | Needs GPU |
| Machine-checked proofs and formal verification | CPU, hours to days, pausable, growing demand | Niche, hard to reach buyers |
| Overnight planning (delivery routes, timetables, shift plans) | Night work = high heat demand, CPU | Usually done in-house |

**Pitch features around the main plan:**

- **Alp-Cloud:** in summer, compute "moves up" to colder alpine sites (hotels, spas, pools), like
  cows to the summer pastures. Answer to the summer gap; testable with Open-Meteo data.
- **The heater transcribes our pitch live** and pauses when the slider moves to July; this is the
  must-have demo with the real service.
- **Compute weather forecast:** a map of when and where compute will be cheap, driven by the weather
  forecast and heat demand (thermal-aware pricing as a visual).
- **"Your shower transcribed a court hearing":** heat translated into work for the homeowner view,
  computed from measured energy per audio hour (no number until measured).
- **Houses bid for jobs** (heat-demand-aware auctions): paper topic for distributed scheduling.

**Other directions** (change a basic assumption, not the workload):

- Heat customers with year-round or summer demand: cheese dairies, hay drying, breweries, laundries,
  pools, greenhouses (check required temperatures).
- Customer = host: a firm's own confidential AI heats its own building (removes the trust gap).
- Grid flexibility: sell fast pausing as flexible load to the grid; absorb midday solar surplus.
- Store winter heat as data: compute results that stay valuable for months, sell all year.
- Sell the scheduler as a plugin (Kubernetes, SLURM, data centres with district heating).
- Private household AI assistant on the basement server (fits the hot-water base load).
