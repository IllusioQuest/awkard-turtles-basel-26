# Job: local LLM batch service (Apertus 8B, llama.cpp)

Ticket T-33. Type 1 service (decision 0018): confidential batch LLM work, priced per token. It is
measured here on the first use case, meeting minutes from a transcript. **Status: measured on one
laptop (CPU). Type 1 runs on GPU servers in the pitch scenario; these CPU figures are the
measured, scored baseline.**

## What the job is

- [llama.cpp](https://github.com/ggml-org/llama.cpp) release **b12600**, built for CPU
  (`jobs/llm/Dockerfile`, tools `llama-completion` and `llama-bench`).
- Model **`swiss-ai/Apertus-8B-Instruct-2509`** (Swiss AI Initiative, Apache-2.0), GGUF `Q4_K_M`
  from [unsloth/Apertus-8B-Instruct-2509-GGUF](https://huggingface.co/unsloth/Apertus-8B-Instruct-2509-GGUF)
  (5,057,885,440 bytes, SHA256 `87e7f0eb…a7944d`, checked against Hugging Face, accessed 2026-11-21).
- Queue runner `jobs/llm/run_queue.sh`, using the same host contract as the transcription job
  (`in/doc_NNN.txt` + `.minutes`, `out/pass_NNN/*.md`, `out/log.csv`, `progress.json`). Greedy
  decoding, fixed seed, output limit 400 tokens, context 8192 tokens.
- **Work unit: one audio minute summarised.** The token figures below are the basis for per-token
  pricing.
- Input: the `base` transcript of the 14.98-minute test recording from T-02 (2,040 words,
  3,310 prompt tokens). Prompt (German): summary in at most five sentences, key points, open tasks.

## Reproduce

```bash
L=data/external/llm
mkdir -p $L/models $L/work/in
curl -L -o $L/models/Apertus-8B-Instruct-2509-Q4_K_M.gguf \
  https://huggingface.co/unsloth/Apertus-8B-Instruct-2509-GGUF/resolve/main/Apertus-8B-Instruct-2509-Q4_K_M.gguf
docker build -t digitalheat/llm-summary jobs/llm
# input: concatenated transcript from the T-02 job, plus its audio minutes
cat data/external/transcription/work/out/pass_000/chunk_*.txt > $L/work/in/doc_000.txt
echo 14.981 > $L/work/in/doc_000.minutes
python3 scripts/measure_job.py energy --no-battery --image digitalheat/llm-summary --cpus 8 --memory 8g \
  --env THREADS=8 --mount "$PWD/$L/models:/models:ro,Z" --mount "$PWD/$L/work:/work:Z" \
  --progress-file $L/work/progress.json --clear $L/work/out --idle-seconds 60 --reps 3 \
  --out data/raw/measurements/llm-summary-apertus-8b-q4km/energy-<date> run
```

## Results (2026-11-21, AMD Ryzen 7 250, 8 threads, `--cpus 8 --memory 8g`, charger connected)

The charger was connected, so the battery meter was not valid; only the CPU package (RAPL) is
measured. Raw data: `data/raw/measurements/llm-summary-apertus-8b-q4km/energy-2026-11-21/`.

| Per run (14.98 audio min, 3,310 prompt + 319 generated tokens) | Mean (3 runs) |
|---|---|
| Runtime | 133.3 s (132.0–134.9) |
| CPU package power | 25.1 W |
| Reading the prompt | 57 tokens/s (59.6 s) |
| Generating | 4.4 tokens/s (71.3 s) |

| Per audio minute summarised | Value | Std. dev. |
|---|---|---|
| Runtime | **8.9 s** | 0.1 s |
| Energy, CPU package, gross | **0.0614 Wh** | 0.0006 Wh |
| Energy, CPU package, net (idle 3.5 W subtracted) | 0.0528 Wh | 0.0005 Wh |

**Per token** (assumes constant package power across phases): reading ≈ 25.1 W × 59.6 s ÷ 3,310
≈ **0.45 J per input token**; generating ≈ 25.1 W × 71.3 s ÷ 319 ≈ **5.6 J per output token**. That is
about **0.125 kWh per 1M input tokens and 1.56 kWh per 1M output tokens**. Output tokens cost about
12× as much energy as input tokens.

## Scaling to the 1 kW reference server

Same assumption as T-02: energy per unit stays the same on the server. CPU package only; the
whole machine is higher (×1.31 in the T-02 `base` runs).

```
audio minutes summarised per server-hour = 1000 Wh ÷ 0.0614 Wh ≈ 16,300 (≈ 270 audio hours)
tokens per server-hour, pure generation  = 1 kWh ÷ 1.56 kWh per 1M ≈ 0.64 M output tokens
tokens per server-hour, pure reading     = 1 kWh ÷ 0.125 kWh per 1M ≈ 8 M input tokens
```

Worked example. Summarising the transcript of a one-hour meeting (≈ 13,000 input + 400 output
tokens, i.e. 4 × the test input) takes about 13,000 × 0.45 J + 400 × 5.6 J ≈ 8.1 kJ ≈ **2.2 Wh** on
this CPU. That is about 7 % of the 33 Wh needed to transcribe the same hour with `large-v3` (60 × 0.477 Wh,
`docs/jobs/transcription.md`).

### Income per server-hour (does it pay on CPU?)

Market prices for small hosted models (accessed 2026-11-21; USD → CHF 0.8402 as in
`docs/research/ai-services.md`):

| Provider, model | Input / 1M tokens | Output / 1M tokens | Source |
|---|---|---|---|
| Mistral, Ministral 3 8B | USD 0.15 (CHF 0.126) | USD 0.15 (CHF 0.126) | [model card](https://docs.mistral.ai/models/model-cards/ministral-3-8b-25-12); batch −50 % ([pricing](https://mistral.ai/pricing)) |
| Public AI, Apertus 8B Instruct | ≈ USD 0.10 | ≈ USD 0.20 | [Artificial Analysis](https://artificialanalysis.ai/models/apertus-8b-instruct/providers), search snippet, not verified |
| Infomaniak (Swiss), Apertus v1.5 70B | CHF 0.70 | CHF 2.50 | [Infomaniak prices](https://www.infomaniak.com/en/hosting/ai-services/prices), search snippet, not verified |

With the measured mix (91 % input tokens: 0.92 Wh per 3,629 tokens, i.e. 0.254 kWh per 1M tokens),
one server-hour processes 1 ÷ 0.254 ≈ 3.9 M tokens. At the Ministral price:

```
3.9 M tokens × CHF 0.126 per 1M ≈ CHF 0.50 per server-hour   (batch price: ≈ CHF 0.25)
pure generation: 0.64 M × CHF 0.126 ≈ CHF 0.08 per server-hour
```

**Result:** at commodity prices, a CPU server earns about what Type 2 earns (pinned CHF 0.66) or
less. With batch pricing it lands near the case break-even (CHF 0.23). So the LLM service pays only
if (a) it runs on GPU (Type 1 is on GPU in the pitch scenario, ticket T-15), or (b) we charge a
Swiss premium. Infomaniak's 70B price is about 5–20× the commodity 8B price.

## Quality (by eye)

Correct German, structure as requested, the summary follows the story. There are content errors,
caused partly by the `base` transcript. The "open tasks" section is weak because the input is a
short story, not a meeting.
