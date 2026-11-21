# Computing price for the 1 kW reference server

> **Team decision (2026-11-21, team lead):** adopt **CHF 0.66 per server-hour** for the draft's scored
> figures (computing income, margin), keep it adjustable (sensitivity 0.25–1.80). The config change
> goes through T-06 (Jona). An automatic, snapshotted price update is proposed in T-27.


> **Recommendation for the team, not a decision.** Research note of 2026-11-21 (Claude subagent).
> Replaces nothing yet: `config/reference-house.json` still holds the unsourced example
> `ComputePriceChfPerServerHour = 0.30`. If the team adopts a value, record it in a decision record
> (economic model = review trigger, AGENTS.md §8).

## 1. Question and short answer

How should the computing income of one 1 kW server be priced, and with which value?

- **Price the server-hour, not the work unit**, for the scored figures: CHF per server-hour,
  anchored on what buyers pay for commodity CPU capacity of the same power.
- **Recommended base value (a): CHF 0.66 per server-hour** (dedicated-server equivalent, §3 A3),
  cross-checked by CHF 0.68 (cheapest Swiss spot price × cheapest community/datacentre ratio, §3 C1).
- **Sensitivity range (b): CHF 0.25 – 1.80 per server-hour**, with the "does not pay off" lines at
  CHF 0.23 – 0.33 (case formula) and CHF ≈ 0.59 (incl. hardware and operations, example
  utilisation). Slider in the dashboard: 0 – 2 CHF.
- **Work-unit pricing (transcription) is an upside, not the base:** per server-hour it is worth
  CHF 55 – 132, but it is capped by demand. Selling 0.01 – 0.4 % of one house's capacity would
  already reach the case break-even; the other > 99.6 % of server-hours need filler work at a
  capacity price.

Note: the scored **break-even price** = (electricity cost − value of heat used) ÷ server-hours sold
does **not** depend on this choice. The price only changes *computing income* and the margin
statement.

## 2. Fixed inputs

| Input | Value | Source |
|---|---|---|
| Electricity | 0.3325 CHF/kWh | `config/reference-house.json` (ElCom 2026, IWB H4) |
| Heat value | electricity ÷ 2.5 = 0.133 CHF/kWh | `docs/case/challenge.md` |
| Recoverable heat | 0.8 kWh per server-hour | `docs/case/challenge.md` |
| **Case break-even, all recovered heat used** | 0.3325 − 0.8 × 0.133 = **0.226 CHF/server-hour** | derived |
| Case break-even, all heat dumped | 0.3325 CHF/server-hour | derived (upper bound) |
| USD→CHF / EUR→CHF | 0.8402 / 0.9233 | WP1 (`docs/research/ai-services.md`, medium) |
| CPU power per vCPU at 100 % load | AWS 3.46 W, Azure 3.74 W, GCP 4.98 W | [Cloud Carbon Footprint methodology, Appendix I](https://github.com/cloud-carbon-footprint/cloud-carbon-footprint/blob/e48c659f6dafc8b783e570053024f28b88aafc79/microsite/docs/Methodology.md) (commit e48c659, accessed 2026-11-21; live docs per search snippet: 3.5 / 3.76 / 4.26), derived from SPECpower |
| Memory power | 0.392 W per GB | same |
| Transcription throughput (whisper.cpp `base`) | 436 audio h per server-hour (572 with CPU-package meter) | own measurement, branch `feat/T-02-transcription-job`, `docs/jobs/transcription.md` |
| Example year (4 servers) | 22,661 server-hours, utilisation 0.647 | year-explorer default as quoted in `docs/jobs/transcription.md` |

**Mapping "1 kW server" to cloud instances.** Cloud instances are slices of hosts. We count how many
instances draw 1 kW at full CPU load: instance power = vCPU × max W/vCPU + GB RAM × 0.392 W
(no PUE: the reference 1 kW is the server itself). Example: Azure D32as v5 = 32 × 3.74 + 128 × 0.392
= 169.9 W → 1000 ÷ 169.9 = 5.89 instances per kW → spot 5.89 × CHF 0.3056 = **CHF 1.80 per
server-hour**. This mapping is an estimate (average coefficients, not measured hardware); a newer
CPU gives more vCPU per kW, an older one fewer.

## 3. Options

Values in CHF per server-hour (= per kWh of server electricity). Row ids refer to
`data/raw/market/*.csv` (accessed 2026-11-20); other links accessed 2026-11-21.

### A. Price per server-hour, anchored on raw compute (what a buyer of capacity pays)

| # | Anchor | CHF / server-h | Source, confidence |
|---|---|---|---|
| A1 | Swiss-region **on-demand** 32-vCPU VMs × instances per kW | 7.10 – 11.09 | `cp-cpu-*-od` (AWS eu-central-2, Azure CH North, GCP europe-west6), high; mapping estimate |
| A2 | Swiss-region **spot** 32-vCPU VMs × instances per kW | **1.80 – 3.44** (Azure 1.80/1.86, GCP 2.78/2.83, AWS avg 3.44) | `cp-cpu-*-spot`, high (Azure) / medium (AWS, GCP) |
| A3 | **Dedicated bare metal**: Hetzner AX162-S, EPYC 9454P 48 cores / 96 threads, 128 GB, EUR 199/month excl. VAT, Germany/Finland. Power by CCF: 96 × 3.46 + 128 × 0.392 = 382 W → 2.62 machines per kW × EUR 199 ÷ 730 h × 0.9233 | **0.66** (0.76 if RAM power ignored) | [Hetzner launch news](https://www.hetzner.com/news/new-ax162) and product page via search snippets (host blocked), medium-low; setup fee EUR 79 ignored |
| A4 | **Home-hosted container market**: SaladCloud CPU-only, USD 0.004 – 0.005 per vCPU-h + 0.001 per GB-h (2 GB/vCPU) × 236 vCPU per kW | 1.19 – 1.39 (buyer price; host payout unknown, `TODO(source)`) | [Salad billing docs](https://docs.salad.com/container-engine/explanation/billing-pricing/billing.md) (0.004) and [Spheron blog](https://www.spheron.network/blog/saladcloud-pricing-2026-gpu-cost-vs-spheron/) (0.005), via snippets, low |
| A5 | **GPU variant** (not the reference CPU server): 2 × RTX 4090 (450 W TGP each) ≈ 1 kW, community marketplaces | 0.57 (RunPod community) – 0.84 (vast.ai median); 1.50 RunPod secure | `cp-mkt-runpod-4090-*`, `cp-mkt-vast-4090-median`, `power-gpu-nvidia-rtx4090-tgp` |

vs. break-even: every raw-compute anchor is above 0.226 and 0.3325. Electricity is a small part of
what compute is sold for; hardware, network and service dominate. That is also the risk: our
server has no SLA, sits in a home and is available when the house needs heat, not when the buyer
needs compute. A1 is not reachable for such capacity; A2 is the price of *interruptible*
datacentre capacity and an upper bound for us.

### B. Price per work unit (transcription), converted via throughput

Income per transcription server-hour = audio h per server-hour × price per audio h.

| # | Price anchor | CHF / audio h | CHF / server-h at 436 (base) | at ≈ 62 (large, estimate) |
|---|---|---|---|---|
| B1 | Commodity batch STT (AssemblyAI 0.126, Azure CH batch 0.151, Google dyn. batch 0.151, AWS Zurich batch 0.303) | 0.13 – 0.30 | **55 – 132** | 7.8 – 18.8 |
| B2 | End-user AI transcription (Amberscript EUR 49 / 25 h subscription to EUR 10 / h credits) | 1.81 – 9.23 | 789 – 4,024 | 112 – 572 |
| B3 | Swiss-German app with claimed Swiss-only processing ("Schweizerdeutsch übersetzen", CHF 36 / 10 h to CHF 4.80 / 1 h) | 3.60 – 4.80 | 1,570 – 2,093 | 223 – 298 |
| B4 | Töggl Swiss German (product test, date unknown) | 60 | — | — |

Sources: B1 `ai-stt-*` rows (high); B2 [Sonix on Amberscript pricing](https://sonix.ai/resources/amberscript-pricing/)
(competitor page, snippet, low); B3 App Store listing via search snippet (apps.apple.com blocked,
low; hosting claim is the vendor's own); B4 `ai-stt-toeggl-swissgerman` (low).
"Large" = 436 ÷ 7, from the relative speed base ~7× vs. large 1× in the
[openai/whisper README](https://github.com/openai/whisper) (A100, English); CPU ratio not measured,
`TODO(measure)`.

**Why this is capped by demand, not capacity.** The example house sells 22,661 server-hours, i.e.
22,661 × 436 ≈ **9.9 million audio hours per year** of `base` capacity. AWS's own pricing example
(a large customer) is 2 million audio minutes per month ≈ 0.4 million audio hours per year
(WP1), so one house could serve ≈ 25 such customers. Conversely, the audio volume that alone pays
the case break-even (0.226 × 22,661 = CHF 5,121 per house and year):

| Price (CHF / audio h) | Audio h / year needed | Server-hours used | Share of capacity |
|---|---|---|---|
| 0.126 | 40,646 | 93 | 0.41 % |
| 0.303 | 16,930 | 39 | 0.17 % |
| 4.80 | 1,067 | 2.4 | 0.011 % |

So money can come from transcription, but heat cannot: > 99.6 % of the server-hours a house needs
must be filled with other work. Realistic Swiss customer volumes (law firms, practices,
municipalities) are `TODO(source)` (ticket T-01). Electricity per audio hour is only
0.0023 kWh ≈ CHF 0.0008, so any per-unit price covers energy easily.

### C. Hybrid

| # | Model | CHF / server-h | Basis |
|---|---|---|---|
| C1 | Capacity on a marketplace at a **home-host discount**: cheapest Swiss spot (1.80) × community/datacentre price ratio (RunPod: 3090 0.44, 4090 0.38, A5000 0.59, L4 0.64, L40S 0.72) | **0.68 – 1.15** | A2 × `cp-mkt-runpod-*` ratios (GPU evidence applied to CPU: assumption) |
| C2 | Pessimistic: dedicated anchor A3 × lowest ratio 0.38 | **0.25** | A3 × RunPod 4090 ratio |
| C3 | Sovereign transcription for the volume that sells + filler capacity for the rest: income/server-h = (V × p_audio + (S − V ÷ 436) × p_capacity) ÷ S | C1 value + V × p_audio ÷ S | V = sold audio hours: `TODO(source)`. Example of the formula only: V = 1,067 h at CHF 4.80 adds CHF 0.23 per server-h to the capacity price |

Sovereignty premium, evidence so far: **none found for hyperscalers' Swiss regions** (AWS Zurich
batch = USD 0.006/min, the same as the us-east-1 pricing example, `ai-stt-aws-transcribe-batch-euc2`;
Azure Switzerland North batch USD 0.18/h is within the commodity band B1). A premium appears only in finished end-user products (B2–B4), which
bundle a dialect model, interface and support. `base` output does not compete there.

## 4. When it does not pay off (for the sensitivity statement)

Full cost per sold server-hour = case break-even + (hardware ÷ life + operations) ÷ (8,760 × utilisation).
Example inputs from WP2 (`data/raw/market/costs.csv`): Dell R570 CHF 8,248 (`cost-server-dell-r570`;
not shown to be a 1 kW machine), 6-year life (`lifetime-microsoft-server-accounting`), monitoring
and patching CHF 59/month (`cost-ops-smartsolution-server-basic`) → CHF 2,083 per server-year.

| Utilisation | Hardware + ops per server-h | Full-cost break-even (all heat used) | (all heat dumped) |
|---|---|---|---|
| 1.00 | 0.24 | 0.46 | 0.57 |
| 0.647 (example) | 0.37 | **0.59** | 0.70 |
| 0.30 | 0.79 | 1.02 | 1.13 |

Statement for the pitch (if the team adopts this): *"Below CHF 0.23 – 0.33 per server-hour the
computing income does not even cover the electricity net of heat value. Below about CHF 0.6 it
does not cover hardware and operations at our utilisation. Our base value CHF 0.66 is the
datacentre price of the same compute; at the pessimistic CHF 0.25 the model only just pays its
electricity."* Note that the margin over full cost at the base value is thin (0.66 vs. 0.59).

## 5. Recommendation

| | Value | Why |
|---|---|---|
| (a) Scored reference-house figures | **0.66 CHF / server-hour** (`ComputePriceChfPerServerHour`, source: this file §3 A3) | Lowest of the raw-compute anchors, traceable in one line, independent of unproven transcription demand, and matched by the hybrid cross-check C1 (0.68). Assumes the marketplace buys all offered hours ("unlimited jobs", must be stated per must-have 1). |
| (b) Sensitivity range | **0.25 – 1.80 CHF / server-hour** (slider 0 – 2) | Low end C2 (home-host discount on dedicated price), high end cheapest Swiss spot A2. Mark 0.226 / 0.3325 (case break-even) and ≈ 0.59 (full cost). |
| Upside, shown separately | transcription income per C3 | Only once a sourced customer volume exists. |

The old example 0.30 lies inside the case break-even band and has no source; keep it at most as a
named "near break-even" scenario.

## 6. Open risks

1. **Home-host discount** (biggest uncertainty): no source for what a weather-driven, SLA-free
   home server is paid per kWh of CPU. The ratios used are GPU marketplace prices; Salad host
   payouts are not published (`TODO(source)`).
2. **vCPU per kW mapping** uses average CCF coefficients, not a measured server. ± 30 % changes all
   A-values proportionally. A measured 1 kW server (vCPU count at the wall) would fix it.
3. **Demand for filler capacity** in winter peaks (12.5 servers per house) and in summer; no
   buyer has been shown. The model must say "unlimited jobs" until then.
4. **Whisper `base` quality:** German WER for `base` `TODO(source)`; for comparison, Common Voice 9
   German WER small 13.0, medium 8.5, large-v2 6.4 (paper values quoted in a
   [fine-tuned model card](https://huggingface.co/bofenghuang/whisper-large-v2-cv11-german), low).
   Not trained on Swiss German. Commercial-grade output likely needs `large` (≈ 7× slower) or a
   Swiss-German fine-tune; this lowers B but not A.
5. **Sovereignty premium** is unproven: no Swiss B2B provider with a published price and Swiss
   hosting was found; the app in B3 is a consumer product with a self-declared hosting claim.
6. **Price volatility:** spot prices are snapshots (2026-11-20); Hetzner price is the 2024 launch
   price (excl. VAT, setup fee ignored, a later price adjustment is possible).
7. **Blocked hosts:** hetzner.com, salad.com, apps.apple.com, cloudcarbonfootprint.org were not
   reachable from this session; their values come from search snippets.
