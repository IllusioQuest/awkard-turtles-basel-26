# Profitability inputs: synthesis of T-37 (Basel-Stadt → Switzerland, horizon 2030)

**Status: exploration, 2026-11-21, not reviewed by the team.** Feeds T-14 (economics per actor) and T-34
(business model v2); it is **not** a second economic model. It uses T-14's surplus definition and its rule
that unsold hours are not run (decision 0018: no unpaid work).

All numbers come from CSV rows (`data/raw/market/*.csv`, schema in [`README.md`](README.md)) or repo
files. The tables in §3 and §4 are printed by one command:

```bash
python3 scripts/profitability_inputs.py     # also writes data/derived/profitability/summary.json
```

Packages: [R1 power](power-efficiency.md) · [R2 purchase cost](purchase-cost.md) ·
[R3 household capacity](household-capacity.md) · [R4 compute demand](compute-demand.md) ·
[R5 trends](trends-2030.md).

## 1. Short answer

1. **Hardware and the price we can charge decide profitability, not electricity.** Electricity plus
   the used heat costs a net CHF 0.23 per server-hour (case break-even). New hardware adds CHF 0.73–1.43
   per sold server-hour.
2. **Type 2 at the pinned CHF 0.66 does not pay its hardware on any profile.** On a dense CPU server
   it loses about CHF 17,400 per house and year even if every hour is sold.
3. **The only configuration that can clear full cost is Type 1 LLM work on consumer GPUs (RTX 4090
   class), batched and sold per token near list price.** It needs 8–42 % of heat-led hours sold in
   year 1, depending on how a batched GPU's token cost is read (§3). Over a 6-year life with falling
   token prices, it needs 17–98 %. It fails at batch-API prices unless the optimistic reading holds.
   **This is the decisive open number:** batched throughput and power of an 8B model on a real RTX 4090.
4. **Capacity dwarfs local demand.** If 1 % of Basel's 10,276 gas- and oil-heated residential buildings
   took part, they would offer 4.8 M server-hours a year. Basel physician dictation needs 546, and the
   Basel research centre sciCORE uses about 80,000. Only the global LLM token market is larger than the
   Basel network.
5. **Prices fall faster than hardware wears out.** Price per unit of compute falls about 25 % a year,
   token prices for a fixed task about 29 % a year. Over a 6-year life the average price is only about
   50–55 % of the year-1 price. Every break-even therefore needs either a fixed contract price per
   server-hour or a shorter payback.

## 2. Parameter table (inputs for T-14)

| Parameter | Value | Range / alternative | Source (CSV id or file) | Confidence |
|---|---|---|---|---|
| Sold server-hours per server and year (reference house, 4 servers) | 5,665 | — | `data/derived/reference-house/explorer-default.json` | repo result |
| Case break-even (electricity − used heat) | CHF 0.2261 per server-hour | — | same | repo result |
| Lifetime (accounting) | 6 years | 5 years; economic ≈ 3 (one analyst) | `pc-life-hyperscaler-years`, `pc-life-economic-years` | medium (verified page) / low |
| Operations per physical box | CHF 708 per year | — | `cost-ops-smartsolution-server-basic` (59 × 12) | existing row |
| Dense CPU server, 1 kW | CHF 25,671 | upper bound (TDP basis) | `pc-cpu-brack-r6615-9354p`, `pc-cpu-dell-epyc9354p-tdp` | low |
| L4 inference server, 1 kW | CHF 44,179 | repo profile 34,244 used an older card price | `pc-gpu-bechtle-l4` (CHF 3,089, verified), `cost-server-dell-r570` | high / existing |
| RTX 4090 server, 1 kW (2 desktops) | CHF 16,300 | RTX 5090 card CHF 5,543 per kW, host missing | `config/hardware/gpu-consumer-rtx4090.json`, `pc-gpu-digitec-5090-gaming-oc` | existing / low |
| CPU server wall power, load / idle | 341 W / 61.7 W (idle = 18 %) | — | `pe-specpower-9754-100`, `-idle` | high (verified) |
| Heat recovered | 0.8 (case) | vendor claim up to 0.95 | case, `pe-recovery-qarnot` | case / low |
| Type 2 price | CHF 0.66 per server-hour | floor 0.226 / 0.3325, ceiling 1.80 | `computing-price.md`, decision 0018 | team decision |
| Fill rate of offered Type 2 hours | 0.35 | 0.2–0.6 | `filler-demand.md` §6 | sourced estimate |
| LLM token price (8B class) | CHF 0.126 per 1M tokens | batch API −50 %; Swiss 70B: CHF 0.70 / 2.50 | `ai-llm-mistral-ministral3-8b`, `ai-llm-infomaniak-apertus-70b` | high / low |
| LLM energy, RTX 4090 | 0.0305 J per input token; 2.38 J per output token (batch 1) | batched: 0.21 J per token (450 W ÷ 2,148 tok/s) | `pe-rtx4090-llama7b-pp`, `-tg` (verified), `pe-rtx4090-vllm-iog-qps8` (opened) | high / medium |
| LLM energy, our laptop CPU | 0.45 J per input token; 5.6 J per output token | — | `docs/jobs/llm-summary.md` | own measurement |
| Whisper turbo on L4 | 0.026 Wh per audio minute (GPU only) | baseline config 0.034 | `pe-l4-whisper-*` | medium (verified) |
| Yearly price factor, compute | 0.754 (`2^(−1/2.46)`) | ML GPUs 2.07 y → 0.716 | `tr-hw-perfusd-epoch-all-gpu` (verified) | high, our assumption that prices follow |
| Yearly price factor, tokens / transcription (fixed task) | 0.707 | 0.1 at constant capability (10×/yr) | `tr-stt-halving-derived`, `tr-tok-a16z-10x` | low |
| Basel electricity 2030 | CHF 0.339–0.376 per kWh | — | `tr-el-basel-2030-mid`, `-high` | extrapolation |

## 3. Calculation 1: profit per house and year

**Method.** Reference house, 4 servers. The heat-led run sells `S` = 22,661 server-hours if every hour
finds a buyer. With fill rate `φ`, a share `φ` of these hours is sold and run; the rest stay off and
the backup heater covers them, so electricity and used heat both scale with `φ`:

```
surplus(φ) = φ · S · (p − b) − N · (purchase ÷ L + ops)
fill rate needed φ* = N · (purchase ÷ L + ops) ÷ (S · (p − b))
```

Here `p` is the income per sold server-hour, `b` = CHF 0.2261 the case break-even, `N` = 4 servers and
`L` = 6 years. Full-cost break-even per sold hour = `b + (purchase ÷ L + ops) ÷ 5,665`.

**Profiles** (script output):

| Profile (1 kW) | Purchase CHF | Fixed CHF/yr (depreciation + ops) | Full-cost break-even CHF/server-h |
|---|---:|---:|---:|
| cpu-dense (3.57 × R6615 with EPYC 9354P) | 25,671 | 6,807 | 1.428 |
| gpu-l4 (11.6 L4 + host, Bechtle price) | 44,179 | 8,071 | 1.651 |
| gpu-rtx4090 (2 desktops) | 16,300 | 4,133 | 0.956 |

**LLM income per server-hour on RTX 4090.** Energy per token = board power ÷ throughput: 450 W ÷ 14,770
tokens/s = 0.0305 J per input token, and 450 ÷ 188.96 = 2.38 J per output token at batch 1. Our measured
token mix is 91.2 % input (3,310 in, 319 out). Mixed energy = 0.912 × 0.0305 + 0.088 × 2.38 = 0.237 J per
token. The GPUs draw 0.9 of the 1 kW, so one server-hour yields 0.9 × 3.6 MJ ÷ 0.237 J = **13.7 M tokens**.
At CHF 0.126 per 1M tokens that is **CHF 1.72 per server-hour**.

Batched: vLLM on one RTX 4090 reaches 2,148 tokens/s at 8 requests per second (Llama-3-8B,
`pe-rtx4090-vllm-iog-qps8`), i.e. 450 W ÷ 2,148 = 0.21 J per token. The source does not say whether it
counts input tokens as well. So we compute two readings:

- **Conservative (a floor):** every token costs 0.21 J. 0.9 × 3.6 MJ ÷ 0.21 J = **15.5 M tokens**, which is
  CHF 1.95 per server-hour. This also charges the cheap input tokens at the generation rate.
- **Optimistic:** only output tokens cost 0.21 J, and input tokens keep 0.0305 J. That gives **70.1 M tokens**
  and CHF 8.84 per server-hour.

The truth lies in between. For example, if the benchmark's tokens were half input, an output token would cost
about 0.39 J and our 91 %-input mix would give about 50 M tokens per server-hour (our inference, not
sourced).

| Option | Income CHF/server-h, year 1 | Lifetime average | Surplus/house/yr, all sold | at fill 0.35 | Fill needed, year 1 | Fill needed, lifetime |
|---|---:|---:|---:|---:|---:|---:|
| Type 2 CPU, pinned price | 0.66 | 0.37 | −17,396 | −23,787 | not reachable | not reachable |
| Type 2 CPU, Swiss spot ceiling | 1.80 | 1.00 | 8,438 | −14,745 | 0.76 | not reachable |
| L4 sold at AWS spot equivalent | 2.23 | 1.23 | 13,126 | −16,391 | 0.71 | not reachable |
| RTX 4090 at home-host marketplace | 0.57 | 0.32 | −8,738 | −13,803 | not reachable | not reachable |
| Type 1 LLM, RTX 4090, batch 1, list price | 1.72 | 0.86 | 17,359 | −4,669 | 0.49 | not reachable |
| Type 1 LLM, RTX 4090, batch 1, batch-API price | 0.86 | 0.43 | −2,147 | −11,497 | not reachable | not reachable |
| Type 1 LLM, RTX 4090, batched (conservative), list price | 1.95 | 0.97 | 22,504 | −2,868 | 0.42 | 0.98 |
| Type 1 LLM, RTX 4090, batched (conservative), batch-API price | 0.97 | 0.49 | 425 | −10,596 | 0.97 | not reachable |
| Type 1 LLM, RTX 4090, batched (optimistic), list price | 8.84 | 4.40 | 178,573 | 51,755 | 0.08 | 0.17 |
| Type 1 LLM, RTX 4090, batched (optimistic), batch-API price | 4.42 | 2.20 | 78,459 | 16,716 | 0.17 | 0.37 |

**Worked example (Type 1 LLM, batch 1, list price).** Fixed cost = 4 × (16,300 ÷ 6 + 1,416) = CHF 16,532 a
year. Surplus with every hour sold = 22,661 × (1.72 − 0.2261) − 16,532 = **CHF +17,359**. Fill rate needed
= 16,532 ÷ (22,661 × 1.497) = **0.49**.

**Lifetime average.** If the price falls by factor `f` a year, the mean over `L` years is
`(1 − f^L) ÷ (L (1 − f))`. For tokens, `f` = 0.707 and L = 6 give 0.498. For compute, `f` = 0.754 gives
0.553. The "lifetime" columns use these.

**How to read.** A row is profitable if the fill rate it needs is below the fill rate we can expect
(0.35, range 0.2–0.6). "Not reachable" means it loses money even when every hour is sold.

## 4. Calculation 2: network capacity vs. demand

Capacity is from R3: suitable buildings (S1) are existing residential buildings heated by gas or oil.
Basel-Stadt has **10,276** of them, of 23,251. This was recounted by us from the cantonal GWR export
(data.bs.ch 100230) on 2026-11-21. Server-hours = S1 heat demand (665 GWh) × participation × 0.7256
server-hours per kWh (the reference-house ratio 22,661 ÷ 31,231).

| Basel-Stadt S1 participation | 1 % | 5 % | 10 % |
|---|---:|---:|---:|
| Server-hours per year | 4.8 M | 24.1 M | 48.3 M |
| Servers (1 kW) | 852 | 4,259 | 8,517 |

Switzerland: about 900,000 S1 buildings (BFS shares, rounded). That gives 204 M server-hours at 1 %
using the reference house per building, or 2.07× that using Basel's mean building.

| Demand segment | Server-hours/yr | Share of Basel 1 % | Source |
|---|---:|---:|---|
| Basel physician dictation (large-v3, CPU) | 546 | 0.01 % | `cd-d-basel-stt-large` |
| sciCORE Basel research, energy basis | 79,920 | 1.7 % | `cd-d-scicore-sh-energy` (verified input) |
| sciCORE, node basis (upper bound) | 460,490 | 9.5 % | `cd-d-scicore-sh` |
| OpenRouter token volume as RTX 4090 work, batch 1 / batched (conservative) | 738 M / 653 M | 153× / 135× | `cd-t1-openrouter-week` (194 T tokens/week) |

**Worked example.** 194 × 10¹² tokens a week × 52 = 1.01 × 10¹⁶ tokens a year. Divided by 13.7 M tokens
per server-hour (batch 1), that is 738 M server-hours. OpenRouter mostly serves models much larger than
8B, so read this only as "the token market is far bigger than our network", not as demand for an 8B
model.

**Seasonality.** Our capacity in January is 4.7× that in July (R3). Batch and HPC demand barely varies
over the year (one US dataset, R4), so summer is a supply gap, not a demand gap.

## 5. Outlook to 2030

- **Price per unit:** compute falls 0.754× and tokens 0.707× a year. A 2026 box earns about 50–55 % of
  its year-1 income on average over 6 years, and about a third (0.25–0.32) in 2030 (`tr-d-gpu-income-2030-old`).
- **Hardware:** a box bought in 2030 delivers about 3.2× the compute per kWh (`tr-d-perfw-2030`). This
  roughly cancels the price fall for new boxes but does not help installed ones.
- **Electricity:** CHF 0.339–0.376 per kWh in 2030 adds at most CHF 0.04 per server-hour.
- **Consequence:** sell capacity under multi-year contracts at a fixed price per server-hour, or plan
  for a payback within about 3 years. Without one of these, only the batched rows at list price stay
  profitable over their life, and in the conservative reading only just (98 % of hours sold needed).

## 6. Where it pays off, and what to check next

- **Can pay off:** consumer GPUs (RTX 4090/5090 class) doing batched Type 1 LLM work for Swiss customers,
  priced near list price per token. It needs 8–42 % of heat-led hours sold in year 1. Type 2 CPU filler at
  CHF 0.66 covers electricity but not hardware. It remains useful only on hardware that is already paid
  for (marginal cost = electricity − heat value = CHF 0.23).
- **Check first** (each changes the answer):
  1. Batched throughput and power of an 8B model on an RTX 4090/5090, with input and output counted
     separately (`vllm bench serve` plus `nvidia-smi` power logging). This number moves the fill rate needed
     between 8 % and 42 %.
  2. Whether Swiss customers pay near list price for a sovereign 8B service (Infomaniak's 70B is 5–20× the
     8B commodity price).
  3. Real wall power of a GPU box.
  4. Installation and heat-exchanger cost (no source yet).

## 7. Corrections made while checking the packages

- sciCORE in server-hours: R4 converted CPU-hours by node count (460,000). On an energy basis it is
  80,000, which is now the central value.
- The European speech-recognition spend is not a volume: it would imply about 24 audio hours per
  European resident. It is kept only as "the market is not small".
- R2's "cheapest fully priced 1 kW system" is the RTX 4090 profile (CHF 16,300), not the CPU server.
- The L4 card costs CHF 3,089 (Bechtle, verified), not 2,235, so the repo's L4 profile is about 29 % too
  cheap per kW (44,179 vs 34,244). The profile file is unchanged (owned by T-15); flagged here.
- Twelve key sources were opened and checked. 9 were confirmed verbatim and 3 needed wording fixes (L4
  configuration, the conditional Basel replacement duty, and "recommended" instead of "available" at
  CSCS 2019). The CSV notes say which.
