# R5: Trends to 2030 (ticket T-37)

Data: [`data/raw/market/trends-2030.csv`](../../data/raw/market/trends-2030.csv) (ids `tr-*`).
Accessed 2026-11-21. Context: electricity CHF 0.3325/kWh, hardware life about 5 years, Type 2 price
pinned CHF 0.66 per server-hour, Type 1 CPU income CHF 0.50 per server-hour
([`docs/jobs/llm-summary.md`](../jobs/llm-summary.md)), GPU server cost and income in
[`hardware.md`](hardware.md). Everything below is a **projection from historic rates, not a
forecast**; the scored figures are unchanged.

## 1. Hardware performance per watt and per CHF

| Series | Doubling time | Factor per year | Factor 2026 to 2030 | CSV id |
|---|---:|---:|---:|---|
| GPU FLOP/s per W, since 2008 (Epoch AI, opened) | 2.4 y (CI 2.1-2.7) | 1.335 | **3.17** | `tr-hw-perfw-epoch-gpu-2008`, `tr-d-perfw-2030` |
| ML hardware FLOP/s per W, FP32 (Epoch 2023) | 3.0 y | 1.260 | 2.52 | `tr-hw-perfw-epoch-ml-2023` |
| GPU FLOP/s per W (Sun 2019, cited by Epoch) | 3-4 y | about 1.22 | about 2.4 | `tr-hw-perfw-sun2019` |
| All GPUs FLOP/s per USD | 2.46 y | 1.326 | **3.09** | `tr-hw-perfusd-epoch-all-gpu`, `tr-d-perfusd-2030` |
| ML GPUs FLOP/s per USD | 2.07 y | 1.398 | 3.82 | `tr-hw-perfusd-epoch-ml-gpu` |
| Top GPUs FLOP/s per USD | 2.95 y | 1.265 | 2.56 | `tr-hw-perfusd-epoch-top-gpu` |
| AI-chip performance per USD since 2023 | 1.7 y (CI 1.4-2.3) | 1.503 | 5.11 | `tr-hw-perfusd-epoch-aichip-2023` |
| CPU/HPC system efficiency 2008-2023 (Springer, snippet) | 2.29 y | 1.354 | 3.36 | `tr-cpu-springer-2024`, `tr-d-cpu-2030` |

Formula: factor over `n` years = `2^(n / T_double)`. Example: `2^(4 / 2.4) = 3.17`.
Koomey's historic 1.57 y rate is obsolete; after 2000 it is about 2.5-2.6 y
(`tr-cpu-koomey-1.57`, `tr-cpu-koomey-post2000`, low confidence).

Reading: a 1 kW server bought in 2030 delivers roughly 2.5-3.5 times the compute of one bought in
2026, for the same electricity. Epoch's rates are for FP32 or AI-format peak FLOP/s, not for
CPU LLM throughput. **Gap:** a SPECpower series for CPU servers: `TODO(source)`.

## 2. Hardware prices

Only price-performance rates were found (table above); they say compute per CHF rises about
3x in four years, not that list prices fall. Epoch's own top-GPU rate (2.95 y) is the slowest, and
the AWS on-demand price per vCPU of the newer m7i (USD 0.0504) is about 5 % above m6i (0.048),
`tr-cloud-cpu-m7i`, `tr-cloud-cpu-m6i` (undated snapshots, low). GPU list-price series and
forecasts: `TODO(source)`. Cloud CPU price history per vCPU-hour: `TODO(source)`.

## 3. Price per token / per inference

| Observation | Rate | CSV id |
|---|---|---|
| Epoch AI, price for GPT-4-level GPQA Diamond | 40x per year; range 9x to 900x across six milestones | `tr-tok-epoch-40x`, `-min`, `-max` |
| a16z "LLMflation", constant capability | 10x per year; GPT-3 USD 60 per 1M tokens (2021) to USD 0.06 (2024) | `tr-tok-a16z-10x`, `tr-tok-a16z-gpt3` |
| Epoch, later average (search snippet) | about 13x per year | `tr-tok-epoch-13x-snippet` (low) |
| OpenAI STT: whisper-1 USD 0.006 per min (2023) to gpt-4o-mini-transcribe about USD 0.003 (2025) | halving in about 2 y, 0.707x per year | `tr-stt-whisper1-2023`, `tr-stt-4omini-2025`, `tr-stt-halving-derived` |

**Caveat that matters for us.** The 10-900x rates are for the price of a *fixed capability level*,
which falls mainly because newer, smaller models reach it. They do not describe the price of one
fixed, already-served model, and Epoch warns the fastest rates may not persist. Our job (summarise
a transcript) has a fixed quality bar, so some of the decline applies, but a 2026 CPU box running
an 8B model competes with 2030 models served on 2030 hardware. The only like-for-like series we
found is STT (one data pair, a different model), so the Type 1 projection uses it as the central
case and shows the 10x line only as the extreme.

**Projection, Type 1 on CPU (fixed 2026 box):**
`income_2030 = income_2026 x p^4`, with `p` = yearly price factor.
- Central, `p = 0.707` (`tr-stt-halving-derived`): `0.50 x 0.25 = CHF 0.125` per server-hour
  (batch price: 0.0625), `tr-d-t1-cpu-2026`, `tr-d-t1-cpu-2030`. This is below electricity alone
  (CHF 0.33) and below the Type 2 pin (0.66).
- Extreme, `p = 0.1` (a16z): `0.50 x 10^-4 = CHF 0.00005`. Not credible for a fixed model; it shows
  that token-priced income has no floor in these trends.

Consequence: Type 1 income per server-hour **falls** with every trend, unless priced per
server-hour (as the pinned Type 2 price is) or sold at a Swiss-sovereignty premium (Infomaniak 70B
is 5-20x commodity 8B, per `llm-summary.md`).

## 4. Electricity and heat in Basel / Switzerland

Basel H4 history 2011-2027 is already in `energy-prices.csv` (`retail-iwb-basel-h4-YYYY`) and in
[`costs-and-energy.md`](costs-and-energy.md); not repeated.

| Item | Value | CSV id |
|---|---|---|
| Basel CAGR 2020-2026 | `(0.3325/0.27647)^(1/6) - 1 = 3.12 %` per year (flat 2020-21, jump 2023-24, then slightly down) | `tr-el-basel-cagr-2020-2026` |
| Basel 2026 to 2027 (published) | 0.3342 / 0.3325 = +0.51 % | `tr-el-basel-2027-step` |
| ElCom national median 2027 | 26.5 Rp./kWh, about -4 %; energy component 12.11 to 11.35, network 10.73 to 10.94 | `tr-el-elcom-median-2027`, `-energy-`, `-grid-` |
| Basel 2030, continued 2020-26 growth | `0.3325 x 1.0312^4 = CHF 0.376` | `tr-el-basel-2030-high` |
| Basel 2030, 2027 step continues | `0.3342 x 1.0051^3 = CHF 0.339` | `tr-el-basel-2030-mid` |

ElCom (press release 2026-09-08) states that the wholesale rise since March 2026 (Iran war) shows
in tariffs only in later years and that network costs trend upward. Wholesale snapshots:
Swiss base future June 2026 EUR 102.10 per MWh (`tr-el-ch-spot-jun2026`, snippet, low), against
SwissIX 2025 mean 10.2 Rp./kWh (`spot-swissix-mean-2025`). So the 2027 dip is probably not the
direction for 2028-2030; the range 0.339-0.376 is a plausible bracket, not a forecast.
**Gaps:** BFE Energieperspektiven 2050+ household price path (Technical Report, BFE pubdb 10783,
price tables not read): `TODO(source)`. Swiss futures Cal-2027 to Cal-2030 (EEX data paid, ElCom
PDF text not extractable): `TODO(source)`. Gas and heat-pump tariff trends: `TODO(source)`. Heating
oil: CHF 104.58 per 100 L in 2024, 115.03 in 2023, 99.84 average 2019-2023 (`tr-heat-oil-2024`,
snippet, low; kWh conversion `TODO(source)`). The heat value follows the electricity price in the
model (0.3325 / 2.5), so it moves with the tariff, not with oil.

## 5. Synthesis: which trend dominates by 2030

Worked example, GPU server (1 kW, `gpu-inference-l4` from `hardware.md`: break-even CHF 1.359,
income at spot CHF 2.23 per server-hour).

Assumption (ours, `TODO(source)` for a GPU rental price series): market price per unit of compute
falls as price-performance rises, factor `2^(-4/2.46) = 0.324`. Compute per kW rises by 3.17.

| Per server-hour | 2026 | 2030, new hardware | 2030, unchanged 2026 box |
|---|---:|---:|---:|
| Income | 2.23 | `2.23 x 3.17 x 0.324 = 2.29` | `2.23 x 0.324 = 0.72` |
| Electricity | 0.3325 | 0.376 (+0.043) | 0.376 |
| Break-even (heat used) | 1.359 | `1.359 + 0.0435 = 1.40` | 1.40 |
| Margin | +0.87 | +0.89 | **-0.68** |

Ids: `tr-d-gpu-income-2030-new`, `-old`, `tr-d-gpu-cost-2030`. Depreciation and operations are held
constant (card price unchanged, no forecast found).

Reading:
1. **Falling price per unit of compute / per token dominates.** It moves income by a factor of 3
   (or 4 for the CPU token case) over four years, while electricity at 0-3 % per year moves cost by
   under 0.05 CHF per server-hour (about 3 %).
2. **Hardware gains only help if you buy new.** They roughly cancel the price decline for a
   2030 box; they do nothing for a 2026 box already installed. With about 5-year life, the model
   should either price the 2026 box at declining income or assume a mid-life refresh.
3. Electricity is the smallest of the three in the central case; it matters only in a
   wholesale-shock path (0.376 vs 0.339 is a 0.04 CHF spread).

What would change this: a price per server-hour that stays flat (contractual, as the Type 2 pin
assumes), or a sovereignty premium. Both are business-model choices, not trends.

## Gaps summary

BFE price path; Swiss Cal-2027..2030 futures; gas and heat-pump trends; GPU rental and list-price
series; SPECpower; CPU cloud price history; a second STT price pair (e.g. other vendors). The
Epoch 1.7 y vs 2.2 y price-performance figures disagree (opened page vs search snippet).
