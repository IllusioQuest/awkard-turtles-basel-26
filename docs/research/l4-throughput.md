# NVIDIA L4 throughput and power: LLM, Whisper, Monte Carlo

Research note for decision 0027 (L4 replaces RTX 4090 as hardware basis; L4 LLM throughput was
unsourced and proxied by RTX 4090 figures). Profile: [`config/hardware/gpu-inference-l4.json`](../../config/hardware/gpu-inference-l4.json).
CPU comparison values: [`docs/jobs/llm-summary.md`](../jobs/llm-summary.md),
[`docs/jobs/transcription.md`](../jobs/transcription.md), [`docs/jobs/research-sim.md`](../jobs/research-sim.md).

**Status: research summary, not a measurement.** All sources accessed 2026-11-21. Most hosts were
not reachable from the sandbox (huggingface.co, arxiv.org, e2enetworks.com, servethehome.com,
storagereview.com, jarvislabs.ai, spheron.network, substack.com all failed DNS); github.com was
reachable. Values marked "snippet" were read from search-result snippets only and are low
confidence until someone opens the page. Following the `docs/research/README.md` rule, the values
should also get rows in `data/raw/market/` (`hw-perf-*`); that is a follow-up, not done here.

## 1. Card and host basics

| Quantity | Value | Unit | Source | Accessed | Confidence |
|---|---|---|---|---|---|
| Board power limit (TDP) | 72 | W | NVIDIA L4 page <https://www.nvidia.com/en-us/data-center/l4/>; already in repo as `hw-power-nvidia-l4-tdp` | 2026-11-21 | high |
| Typical draw under use | 50–65 (nvidia-smi sometimes > 70) | W | ServeTheHome L4 review <https://www.servethehome.com/nvidia-l4-review-the-versatile-ai-inference-card-pny/2> (snippet; workload not named, memory not fully loaded) | 2026-11-21 | low |
| Memory bandwidth | 300 | GB/s | NVIDIA L4 page; PNY <https://www.pny.com/nvidia-l4> (snippet) | 2026-11-21 | high |
| FP16 tensor, dense | 121 (242 with sparsity) | TFLOPS | Lenovo Press LP1717 <https://lenovopress.lenovo.com/lp1717-nvidia-l4-24gb-pcie-gen4-passive-gpu> (snippet) | 2026-11-21 | medium |
| FP8 tensor, dense | 242.5 (485 with sparsity) | TFLOPS | NVIDIA L4 page / StorageReview table (snippet) | 2026-11-21 | medium |
| FP32 (non-tensor) | 30.3 | TFLOPS | NVIDIA L4 page / Cisco datasheet copy (snippet) | 2026-11-21 | high |
| Host power with L4 cards | not found | W | No measured wall power for a host with 1–4 L4 found. StorageReview's DL145 Gen11 review logs whole-server power (iLO) next to L4 board power, but the values were not in the snippets: <https://www.storagereview.com/review/nvidia-rtx-pro-4500-edge-review-hpe-proliant-dl145-gen11-l4>. Chassis note from the same search: DL145 Gen11 takes up to three L4 (<https://www.servnetuk.com/datasheet/hpe-proliant-dl145-gen11>, snippet). | 2026-11-21 | — |

**Host share used below** (unchanged from the profile, not re-sourced): one 1 kW "server" = host
0.1625 kW (Xeon 6507P TDP + memory estimate, `cpu-server.json`) + (1 − 0.1625) ÷ 0.072 =
**11.63 L4 cards** at 72 W each. All "per kWh" figures below are per kWh of that whole 1 kW server
(host included), with the cards at their 72 W board limit. If the cards really draw 50–65 W, the
same work costs less energy, so the per-kWh values below are on the **conservative** side for the
card part; the host estimate is the bigger unknown (`TODO(source)`, measure with `nvidia-smi` plus
a wall meter).

**Conversion formula (all workloads):**

```
units per kWh(server) = cards per server × units per card-second × 3600 s ÷ 1 kWh
                      = 11.63 × r × 3600          (r = card throughput, units/s)
```

Worked example: 589.3 tok/s per card → 11.63 × 589.3 × 3600 ≈ 24.7 M tokens per kWh.

## 2. LLM inference, 7–8B models

No MLPerf L4 entry for Llama 3.1 8B or GPT-J was found in the snippets (MLPerf v5.1 Llama 3.1 8B
results seen were L40S and H100; Google cites an L4 GPT-J v3.1 offline result only as performance
per dollar, <https://cloud.google.com/blog/products/compute/performance-per-dollar-of-gpus-and-tpus-for-ai-inference>).
NVIDIA's TensorRT-LLM perf overview has no L4 rows. Apertus 8B: no L4 figure found (same
architecture class as Llama 3.1 8B, so the Llama/Qwen values are the closest proxy).

| # | Setup | Value | Unit | Source | Accessed | Confidence |
|---|---|---|---|---|---|---|
| L1 | Qwen2.5-7B-Instruct, vLLM 0.29.0, 1× L4 (Colab, 23 GB), 512 in / 256 out per request, **BF16**; concurrency 1 / 4 / 16 / 32 / 64 | 17.5 / 67.2 / 222.4 / 386.9 / 589.3 | tok/s (label "tok/s"; most likely output tokens as in `vllm bench serve`, not stated) | Aman12x/vllm-serving-bench <https://github.com/Aman12x/vllm-serving-bench> (page opened) | 2026-11-21 | medium |
| L2 | same, **FP8** | 28.9 / 112.0 / 382.5 / 668.8 / 955.8 | tok/s | same | 2026-11-21 | medium |
| L3 | same, **AWQ 4-bit** | 50.8 / 193.0 / 534.9 / 862.8 / 1080.9 | tok/s | same | 2026-11-21 | medium |
| L4 | same, p95 time to first token at concurrency 1 (512-token prompt): BF16 / FP8 / AWQ | 167 / 111 / 175 | ms | same | 2026-11-21 | medium |
| L5 | Bench360: 7–8B models (Mistral-7B-v0.3, Qwen2.5-7B, Llama-3.1-8B), batch 128 on L4: vLLM / SGLang / LMDeploy / TGI | 401.9 ± 21.0 / 513.2 ± 29.4 / 198.6 / 166.5 | tokens/s (TPS; which model and in/out split not in snippet) | arXiv 2511.16682 v1 <https://arxiv.org/html/2511.16682v1> (snippet) | 2026-11-21 | low |
| L6 | Bench360: on L4 and A10, throughput saturates between concurrency 16 and 32 | — | — | same | 2026-11-21 | low |
| L7 | Llama 3 8B, Hugging Face transformers, GCP g2 (1× L4), batch 1 (precision not stated) | 12.75 | output tok/s | Parseur <https://parseur.com/blog/blog-llama3-performance-cost> (snippet, 2024) | 2026-11-21 | low |
| L8 | Qwen2.5-7B-Instruct, L4, per-user decode | 17 | tok/s | RunPod guide <https://www.runpod.io/articles/guides/best-gpu-for-llm-inference> (snippet; setup not stated) | 2026-11-21 | low |
| L9 | Mistral 7B, vLLM 0.5.3, AWS g6.xlarge (1× L4), batch 32, peak | 1389 | tok/s | dev.to <https://dev.to/johalputt/benchmark-llama-32-7b-vs-mistral-7b-vs-gemma-2-7b-inference-speed-on-nvidia-l4-and-amd-mi300-3pcb> (snippet; article names a non-existent "Llama 3.2 7B" and misstates L4 memory; **not used**) | 2026-11-21 | very low |
| L10 | Bandwidth ceiling, batch 1, 16 GB BF16 8B model: 300 GB/s ÷ 16 GB | 18.75 (37.5 at 8 bit) | output tok/s | derived; same figure in <https://dev.to/viswaretas_kotra/how-many-tokens-per-second-should-an-llm-serve-on-one-gpu-27b8> (snippet) | 2026-11-21 | high (as a bound) |

Consistency: L1 at concurrency 1 (17.5 tok/s) and L7/L8 (12.75–17) sit just below the
bandwidth ceiling L10 (18.75), so the batch-1 figures agree. L1 BF16 at 32 (386.9) and L5 vLLM
at 128 (401.9) agree within 4 %, which supports L1 being the same kind of figure as Bench360's TPS.

**Prompt processing (input tokens).** No direct L4 prefill throughput was found. Derived lower
bound from L4: one 512-token prompt is processed within the p95 TTFT, so prefill ≥ 512 ÷ 0.167 s
= **3,066 tok/s (BF16)** and ≥ 512 ÷ 0.111 s = **4,613 tok/s (FP8)** per card (TTFT includes
queueing and scheduling, so the true rate is higher). Compute ceiling: 121 TFLOPS ÷ (2 × 7.6 × 10⁹
FLOP per token) ≈ 7,960 tok/s (BF16 dense, 100 % utilisation; not reachable in practice).

**Measured power:** none for LLM inference on L4 found (Bench360 and Watt Counts,
arXiv 2604.09048, report L4 J/token in figures that the snippets did not show).

### Units per kWh (1 kW L4 server, host included)

| Quantity | Basis | Per card (tok/s) | Per kWh of server |
|---|---|---|---|
| Output, batch 1, BF16 | L1 c = 1 | 17.5 | 0.73 M tokens |
| Output, batched, BF16 | L1 c = 64 | 589.3 | **24.7 M tokens** |
| Output, batched, FP8 | L2 c = 64 | 955.8 | 40.0 M tokens |
| Output, batched, low end | L5 vLLM b = 128 | 401.9 | 16.8 M tokens |
| Input (prefill), BF16 lower bound | L4 TTFT | 3,066 | **128 M tokens** |
| Input (prefill), FP8 lower bound | L4 TTFT | 4,613 | 193 M tokens |
| Input, compute ceiling | datasheet | 7,960 | 333 M tokens (bound, not a value) |

In the L1/L2 runs each request also carries 512 input tokens per 256 output tokens; the batched
output figures therefore already include the prefill of 2 input tokens per output token. If the
label "tok/s" in L1 is total tokens rather than output tokens, the output rates are a third of
the above (BF16 c = 64: 8.2 M output + 16.5 M input tokens per kWh).

**Comparison:**
- Our CPU measurement (laptop, llama.cpp-class, `llm-summary.md`): 8 M input and 0.64 M output
  tokens per kWh. The L4 server does about **16× more input** and **39× more output** tokens per
  kWh batched (BF16 c = 64 vs CPU batch 1); at batch 1, L4 output is about the same as the CPU
  (0.73 vs 0.64 M per kWh).
- Decision 0027 proxy (RTX 4090 batched, 0.21 J per token for every token): 14.36 M tokens per
  server-hour. The sourced L4 figures are **above** the proxy: 24.7 M output tokens (plus 49 M input
  tokens) per kWh at BF16, or 24.7 M total tokens under the most pessimistic reading of the label.
  The proxy is therefore conservative, by a factor of about 1.7 (pessimistic reading) to 5 (output
  reading, all tokens counted).

**Recommended (economics default):** treat a 2 : 1 input : output batch job at **25 M tokens per
kWh, all tokens counted** (L1 BF16 c = 64, pessimistic reading of the label). Range **17 M** (L5,
Bench360 vLLM) to **120 M** (L2 FP8 c = 64, output reading, input counted: 40 M output + 80 M
input). Separately priced: output 25 M per kWh (range 13–40 M), input 128 M per kWh (range
128–193 M). Our own summary job is 91 % input, so it sits nearer the input figure; the
recommendation stays conservative until measured. `TODO(source)`: one hour on GCP g2-standard-4
Zurich with `vllm bench serve` + `nvidia-smi` power log (as in decision 0027 §4) replaces all of
this.

## 3. Whisper transcription

RTFx = audio duration ÷ processing time = audio hours per GPU-hour.

| # | Setup | Value | Unit | Source | Accessed | Confidence |
|---|---|---|---|---|---|---|
| W1 | whisper-large-v3-turbo, 1× L4, transformers bf16, batch 4, SDPA attention, greedy (8.93 % WER on their set) | 41.1 (repo row: 40.4, peak of batch 1–32) | × real time | E2E Networks blog (2026-03-27) <https://www.e2enetworks.com/blog/benchmarking-asr-models-nvidia-l4-parakeet-whisper-nemotron> (snippet; already `hw-perf-e2e-l4-whisper-turbo-b4`) | 2026-11-21 | low |
| W2 | same model, beam 4 (8.11 % WER) | 26.9 | × real time | same (snippet) | 2026-11-21 | low |
| W3 | Parakeet bf16 batch 8 on L4 (not Whisper, for scale) | 238.9 | × real time | same (snippet) | 2026-11-21 | low |
| W4 | whisper-large-v3 on 1× L4, bf16, HF Inference Endpoints (vLLM, torch.compile, CUDA graphs, FP8 KV cache); "nearly 8×" RTFx vs transformers | ≈ 100 (secondary press quote) | × real time | HF blog 2025-05-13 <https://huggingface.co/blog/fast-whisper-endpoints>; 100× quote from <https://deepnewz.com/ai-modeling/hugging-face-deploys-whisper-large-v3-8x-faster-transcriptions-0-80-hr-on-nvidia-85d8d3cd> (snippet) | 2026-11-21 | low |
| W5 | whisper-large-v3, faster-whisper int8_float16, L4 | 12–16 | × real time | <https://vexascribe.com/faster-whisper> (snippet, no source given) | 2026-11-21 | very low |
| W6 | whisper-large-v3, faster-whisper int8_float16, 1× L4: RTF ≈ 0.50 (≈ 2×) | 2 | × real time | <https://yobitel.com/knowledge-base/whisper> (snippet; contradicts W1, W4, W5; **not used**) | 2026-11-21 | very low |

`whisper base` on L4: no figure found. Power: none measured for Whisper on L4 found; cards taken at
72 W.

### Units per kWh

| Basis | RTFx per card | Audio hours per kWh of server |
|---|---|---|
| W1 large-v3-turbo greedy batch 4 (repo value 40.4) | 40.4 | **470** |
| W2 large-v3-turbo beam 4 | 26.9 | 313 |
| W4 large-v3 optimised (vLLM) | ≈ 100 | ≈ 1,160 |
| W5 large-v3 faster-whisper int8 | 12–16 | 140–186 |

**Comparison with our CPU measurements** (`transcription.md`, laptop, energy per minute assumed
constant on a server): `base` 436 audio h per kWh (whole laptop); `large-v3` 35 (package only) to
27 (whole-laptop estimate). So for the large models the L4 server is about **13–17× better**
(turbo 470 vs large-v3 CPU 27–35) and roughly equal to CPU `base` while using a much better model.

**Recommended:** **470 audio hours per kWh** (large-v3-turbo, matches the profile's 469.9 per
server-hour). Range **313–1,160** (beam search to optimised vLLM serving). For full large-v3 with
faster-whisper use 140–186 (very low confidence).

## 4. Monte Carlo / research simulation (Type 2)

No comparable L4 (or Ada-generation) Ising benchmark was found. Published single-GPU Metropolis
figures are for 2010-era cards (e.g. up to 7.9 spin flips per ns on one GPU, arXiv 1007.3726, snippet;
multi-GPU records ≈ 1.15 × 10⁵ lattice updates per ns on large clusters, arXiv 2502.18624,
snippet). These are not convertible to our job: different hardware generation, algorithm and
lattice size (our L = 32 lattice is far too small to fill a GPU). For scale only: our pure-Python
job does 32 × 32 × 12,000 ≈ 12.3 M spin updates per task in 0.858 s with 8 workers, i.e.
≈ 0.014 updates per ns; an optimised GPU code would be orders of magnitude faster, but no
L4-sourced factor exists, so **none is used**.

**Consequence for the model:** our Type 2 job (`research-sim.md`) is a CPU job and on an L4
server it runs on the **host CPU**. Assuming energy per task stays as measured (T-02 method):

```
tasks per kWh of host CPU energy   = 1000 Wh ÷ 0.00409 Wh ≈ 244,000 (measured, laptop, gross)
tasks per server-hour on the host  = 0.1625 kW × 244,000 ≈ 39,700
```

The L4 cards contribute nothing to this job; while only Type 2 runs, they idle (idle draw
`TODO(source)`), so a server running only Type 2 produces about 0.16 kW of heat plus card idle, not
1 kW. This matters for the decision 0019/0020 rule "Type 2 rides on Type 1 hardware": to fill heat
demand with Type 2 alone, the GPU job would need a GPU port of the simulation, measured on an L4.

**Recommended:** **244,000 tasks per kWh** (host CPU, range 244,000–311,000 from the two measured
runs in `research-sim.md`), applicable only to the host share (≈ 0.16 kW of the 1 kW server). No GPU
Monte Carlo value.

## 5. Summary of recommendations (1 kW L4 server, per kWh, host included)

| Workload | Recommended | Range | Main source |
|---|---|---|---|
| LLM 7–8B, 2 : 1 batch job, all tokens | 25 M tokens | 17–120 M | L1/L2 (vllm-serving-bench), L5 (Bench360) |
| LLM output tokens (batched) | 25 M | 13–40 M | L1, L2 |
| LLM input tokens | 128 M | 128–193 M (ceiling 333 M) | L4 TTFT, datasheet |
| LLM output, batch 1 | 0.73 M | 0.53–1.2 M | L1, L7, L2 |
| Whisper large-v3-turbo | 470 audio h | 313–1,160 | W1, W2, W4 |
| Monte Carlo (our Ising job) | 244,000 tasks per kWh of host CPU only | 244,000–311,000 | `research-sim.md` (no L4 figure) |

Key caveats: no measured L4 power for any workload (72 W board limit assumed); host share is an
estimate; all L4 LLM values are third-party and partly from snippets. Batch 1 range: 0.53 M from L7
(12.75 tok/s) to 1.2 M from L2 FP8 (28.9 tok/s).
