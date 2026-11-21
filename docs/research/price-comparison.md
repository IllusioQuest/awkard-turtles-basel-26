# Price comparison: our cost per unit vs. known providers (T-16)

> Status: first version, 2026-11-21. Reproduce with `python3 scripts/price_comparison.py`
> (inputs `config/price-comparison.json`, `config/hardware/*.json`, `config/reference-house.json`,
> `data/raw/market/ai-services.csv`; output `data/derived/price_comparison.csv`). Market prices are
> list prices from WP1/T-37 research (many from search snippets, confidence per row in the CSV).
> L4 throughput comes from third-party benchmarks with assumed card power (`l4-throughput.md`).

## Method

Per 1 kW server-hour (the case's reference server):

- **Case cost** = electricity − value of the heat used = 0.3325 − 0.8 × 0.3325 ÷ 2.5 = **0.2261 CHF**
  (heat fully used, as in the case break-even).
- **Full cost** = case cost + (purchase price ÷ lifetime + operations per year) ÷ sold server-hours
  per year. Sold hours: 64.7 % of the year (reference house, 4 servers, `docs/results/reference-house.md`)
  or 35 % (fill rate of filler marketplaces, `filler-demand.md`).
- **Cost per unit** = cost per server-hour ÷ units per server-hour.

Units per server-hour (all scaled to 1 kWh of a 1 kW server):

| Workload | CPU server (measured on a laptop) | L4 server (benchmarks) |
|---|---|---|
| LLM 8B, our mix (91 % input) | 3.9 M tokens (`jobs/llm-summary.md`, T-33) | 25 M tokens, range 14.4–93 M (`l4-throughput.md`) |
| Transcription | 436 audio h, whisper.cpp **base** (T-02) | 470 audio h, whisper **large-v3-turbo** (benchmark) |
| Monte Carlo (2D Ising) | 244,000 tasks (T-30) | not applicable: the job runs on the host CPU, the cards would idle |

**Worked example (L4, LLM, reference house):** full cost 1.3585 CHF per server-hour ÷ 25 M tokens =
**0.054 CHF per 1 M tokens**; case cost 0.2261 ÷ 25 = 0.009 CHF.

## Results

### LLM batch, 8B class (CHF per 1 M tokens, 91 % input / 9 % output)

| | Cost / price | Region |
|---|---:|---|
| **Ours, L4, full cost, 64.7 % sold** | **0.054** (range 0.015–0.094) | CH, in the host's building |
| Ours, L4, full cost, 35 % sold | 0.093 (range 0.025–0.161) | CH |
| Ours, CPU, full cost, 64.7 % / 35 % | 0.63 / 1.12 | CH |
| Ours, case cost (electricity − heat), L4 / CPU | 0.009 / 0.057 | CH |
| DeepInfra Llama 3.1 8B (OpenRouter) | 0.018 | US |
| kvant Qwen3 8B | 0.034 | **CH** |
| OpenAI gpt-5-nano batch | 0.034 | not stated |
| Anthropic Claude Haiku 5.5 batch | 0.057 | global |
| Public AI Apertus 8B | 0.092 | not stated |
| Mistral Ministral 3 8B | 0.126 | EU |
| kvant Granite / Mistral 7B | 0.154 | **CH** |
| Amazon Bedrock Llama 3.1 8B | 0.185 | not stated |
| Infomaniak Ministral 14B (smallest offered) | 0.309 | **CH** |

### Transcription (CHF per audio hour)

| | Cost / price | Model / region |
|---|---:|---|
| **Ours, L4, full cost, 64.7 % / 35 % sold** | **0.003 / 0.005** | large-v3-turbo, CH |
| Ours, CPU, full cost, 64.7 % / 35 % | 0.006 / 0.010 | whisper.cpp base (lower quality), CH |
| Azure batch, Google dynamic batch, OpenAI 4o-mini | 0.151 | Azure: Switzerland North |
| ElevenLabs Scribe v3 | 0.168 | not stated |
| Deepgram Nova-3 | 0.217 | US / EU endpoint |
| AWS Transcribe batch | 0.303 | **CH (Zurich)** |

### Research Monte Carlo (CHF per 1 kW server-hour, CPU)

| | Cost / price |
|---|---:|
| Ours, CPU server, case cost | 0.226 |
| **Ours, CPU server, full cost, 64.7 % / 35 % sold** | **2.49 / 4.41** |
| Hetzner dedicated (adopted price) | 0.66 |
| Home-host marketplace level | 0.69 |
| Azure Switzerland North spot | 1.80 |
| Azure Switzerland North on-demand | 9.73 |

## How to read it

- **Running costs are tiny everywhere.** Electricity minus the heat value is 0.009 CHF per 1 M tokens
  or 0.0005 CHF per audio hour. What decides the price is **hardware and utilisation**.
- **Transcription: 30–100 times below the market** even at full cost on an L4. This is the strongest
  price argument, but demand is small (`job-demand.md`: all Basel doctors fill 0.1 % of one house).
- **LLM: competitive on an L4, not on a CPU.** At 64.7 % sold hours an L4 server costs 0.054 CHF per
  1 M tokens: cheaper than Swiss offers except kvant's Qwen3 8B (0.034) and in the range of global
  batch APIs. At 35 % sold hours (0.093) it is above kvant, OpenAI and Anthropic batch but below
  Mistral, Bedrock and Infomaniak. A CPU server (0.63) is not competitive.
- **Research computing on CPU servers does not beat the market** at full cost (2.49 CHF vs 0.66–1.80):
  the cheapest cloud capacity is cheaper than new CPU servers in a basement. It only pays as filler
  on hardware that Type 1 work already pays for (decision 0019/0020).
- **What is not compared:** quality (model size, accuracy), latency and service guarantees (we sell
  interruptible batch work), and data location (several market prices do not state a region; ours
  stays in Switzerland, in the host's building). These are the arguments for a premium, not price.

## Limits

- Hardware throughput: L4 values assume 72 W per card and an unsourced host share; Whisper on the CPU
  uses the small `base` model. Laptop measurements are scaled linearly to 1 kW.
- Market prices change fast (open-weight token prices fell about 70 % in nine months, WP1 row
  `ai-llm-or-openweight-blended-2026-10`); all are dated 2026-11-21.
- Sold-hour shares are scenarios (64.7 % with unlimited jobs; 35 % from marketplace fill rates).
