# R1: Power and efficiency per job (T-37)

**Status: exploration (2026-11-21), not reviewed.** Raw values: [`data/raw/market/power-efficiency.csv`](../../data/raw/market/power-efficiency.csv)
(ids `pe-*`). Hardware composition and prices: [`hardware.md`](hardware.md). Our laptop values:
[`../jobs/transcription.md`](../jobs/transcription.md), [`../jobs/llm-summary.md`](../jobs/llm-summary.md).
Confidence is `low` wherever only a search snippet was read (stated in the CSV notes).

## 1. Wall power of servers (load / idle / paused)

| System | Full load | 50 % | 10 % | Active idle | Ids |
|---|---:|---:|---:|---:|---|
| 1x EPYC 9754 (128 cores), Lenovo SR635 V3, SPECpower | 341 W | 202 W | 120 W | 61.7 W | `pe-specpower-9754-100/-50/-10/-idle` |
| same CPU, Dell R7615 (snippet) | about 341 W | | | 74 W | `pe-specpower-9754-dell-idle` |
| 1x EPYC 9654, QCT (snippet) | 360 W | | | about 73 W | `pe-specpower-9654-quanta` |
| NVIDIA L4 card | TDP 72 W (measured 50.9 to 72.3 W while running Whisper, GPU only) | | | TODO(source) | `pe-l4-tdp`, `pe-l4-whisper-power` |
| RTX 4090 card | board rating 450 W | | | 22 W (forum, low) | `pe-rtx4090-tgp`, `pe-rtx4090-idle` |
| RTX 5090 card | board rating 575 W | | | 30 W (forum, low; field reports 45 to 100 W) | `pe-rtx5090-tgp` |

How to read: SPECpower gives wall power of a whole server. A 1 kW class CPU box is therefore a
2-socket machine (one 128-core socket draws only 341 W). **Idle is 18 % of full load**
(61.7 / 341, `pe-specpower-9754-idle` / `-100`), so a "paused" job on an idle-but-on server still
produces about 60 W and 10 % of the heat; true suspend (S3) or power-off figures: TODO(source).
Whole GPU boxes (L4 server, RTX 4090 workstation) wall power: no measured source found;
use host 162.5 W + cards from `hardware.md` as an estimate, flagged as such.

## 2. GPU energy per work unit

**Formula (Whisper):** Wh per audio minute = P[W] / RTFx / 60, with RTFx = audio seconds per wall
second. **Formula (LLM):** kWh per 1M tokens = P[W] / (tokens/s) x 1e6 / 3.6e6.

**Worked example, L4 Whisper turbo** (configuration bf16, batch 4, verified on the page; the baseline fp16
batch-1 configuration gives 34x at 68.6 W, i.e. 0.0336 Wh per audio minute): P = 64.9 W (`pe-l4-whisper-power`),
RTFx = 41.1 (`pe-l4-whisper-rtfx`): 64.9 / 41.1 = 1.58 Wh per audio hour = **0.0263 Wh per audio minute**.
This matches the source's own 1.58 Wh/h (`pe-l4-whisper-wh-per-audio-hour`). GPU power only, host
not included; the model is **large-v3-turbo, not large-v3**, on short LibriSpeech clips.

Derived values (P = board rating, so these are **upper bounds**, not measurements):

| Job | Basis | Result | Our CPU value | GPU factor |
|---|---|---:|---:|---:|
| Whisper large-v3, RTX 4090 | 450 W (`pe-rtx4090-tgp`), RTFx 25.1 (`pe-rtx4090-whisper-rtfx`) | 0.299 Wh/audio min | 0.477 (large-v3, CPU package) | 1.6x |
| same, if RTFx 70 (unverified claim) | 450 W / 70 / 60 | 0.107 Wh/audio min | 0.477 | 4.5x |
| Whisper turbo, L4 | measured 64.9 W, 41.1x | 0.0263 Wh/audio min | 0.0382 (base, whole laptop) | 1.45x (different models) |
| LLM 7B Q4 output, RTX 4090, batch 1 | 450 W, 188.96 tok/s (`pe-rtx4090-llama7b-tg`): 450/188.96 = 2.38 J/token | 0.66 kWh/1M output | 1.56 | 2.4x |
| LLM output, RTX 4090, batch 16 (blog, low) | 450 W, 2140 tok/s (`pe-rtx4090-vllm-b16`): 0.21 J | 0.058 kWh/1M output | 1.56 | about 27x |
| LLM prompt processing, RTX 4090 | 450 W, pp512 14,770 tok/s (`pe-rtx4090-llama7b-pp`): 0.0305 J | 0.0085 kWh/1M input | 0.125 | 15x |

Reading: GPUs win clearly for LLM tokens (2x at batch 1, 15 to 27x for batched or prompt work).
For Whisper the gain is only 1.5 to 4.5x against our CPU large-v3 number, and not comparable like
for like (turbo vs large-v3, short vs long audio, GPU-only vs CPU package vs whole laptop).
Caveat: llama.cpp rows are Llama 2 7B Q4_0 (not Apertus 8B Q4_K_M); our values are CPU package
only, the 4090 bound is the full board rating.

Gaps: L4 and RTX 4090 measured power for Whisper large-v3 and for 8B LLMs, L4 LLM throughput
and any MLPerf Power result for 8B (none found; L40S 1642 tok/s offline, `pe-mlperf-l40s-llama8b`, no power),
A100/H100 references (TODO(source)), RTX 5090 Whisper (llama.cpp 7B: 300 tok/s vs 189 tok/s on 4090).

## 3. CPU Monte Carlo / batch (Type 2)

EPYC 9754 1P: 12,765,755 ssj_ops at 341 W = 37,436 ssj_ops/W at full load; SPECpower overall
score (incl. low loads) 30,939 ssj_ops/W (`pe-specpower-9754-score`); 2P 33,300 (`pe-specpower-9754-2p-score`).
Per kWh: 12,765,755 ops/s x 3600 / 0.341 kWh = 1.35e11 ssj_ops per kWh at full load.
**Scaling to our laptop (Ryzen 7 250): no SPECpower or SPECrate-per-watt source found,
TODO(source).** Do not scale "work per kWh" from ssj_ops (Java business workload) to Monte Carlo
without a measured laptop run of the same kernel on both classes; recommend measuring our own
Monte Carlo kernel on the laptop and, if available, a cloud EPYC instance.

## 4. Auxiliary energy and heat recovery

- Circulation pump: 3 to 45 W for domestic pumps (Grundfos ALPHA2 family, `pe-pump-grundfos-alpha2l`,
  low). Example: 20 W pump on a 1 kW server = 2 % of its electricity, which is **not recoverable
  as server heat** but ends up as water heat anyway (pump heat enters the circuit); count it as
  load. Wilo data: TODO(source).
- Share of server power recovered as water heat: Qarnot claims up to 95 % with direct cold-plate
  cooling at about 65 C (`pe-recovery-qarnot`, vendor claim, low). The case uses 0.8 (80 %);
  95 % is the vendor optimum, so 0.8 is conservative for water cooling. Heata, Cloud&Heat:
  no recovery percentage found, TODO(source).
- Heat-exchanger and tank losses: TODO(source); the heat model of the repo already covers tank loss.
