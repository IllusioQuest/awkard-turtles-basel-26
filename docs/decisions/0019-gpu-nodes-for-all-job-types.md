# 0019: Type 2 rides on Type 1 hardware; GPU nodes, no CPU-only servers

- **Status:** proposed; still valid under 0020. Exception: Type 1 premiums from medical or legal
  secrecy (Decision 4) are excluded on home sites by 0020.
- **Date:** 2026-11-21
- **Decided by:** Ennio; team confirmation pending. Partly supersedes decision 0018 (the line "Type 2
  on CPU") and refines the team lead's hardware decision in `docs/vision.md` (GPU main scenario).

## Context

Decision 0018 put Type 1 (sovereign AI services) on GPU and Type 2 (batch compute filler) on CPU.
T-37 (`docs/research/profitability-inputs.md`) priced both against full cost. All tables below are
printed by `python3 scripts/profitability_inputs.py`, and every input is a CSV row.

## Decision

1. **Type 2 never gets hardware of its own.** It runs in the heat-led hours that Type 1 leaves free, on
   the nodes that Type 1 pays for. Type 1 has priority.
2. **The hardware class follows Type 1, so every node is a GPU node** (GPUs plus their host CPU).
   Consumer cards (RTX 4090/5090 class) are the cost basis; data-centre GPUs (L4) stay a comparison
   profile (T-15). GPU-capable Type 2 work (Monte Carlo, parameter sweeps, rendering, CUDA simulation
   codes) runs on the GPUs. CPU-only research codes run on the host CPU of the same node.
3. **Type 2 is priced as a contribution.** On hardware that is already paid for, any price above the
   case break-even (CHF 0.23 per server-hour) adds profit. The scored figures keep the pinned CHF 0.66.
4. **Type 1 needs a premium or fixed contract prices.** Location alone is worth about +10 %. The rest has
   to come from the service (Swiss German, medical or legal secrecy, compliance) or from multi-year
   contracts at a fixed price per server-hour (see (e)).

## Reasons (with sources)

**a) Dedicated Type 2 hardware does not pay, whatever its class.** At the sourced fill rate of 0.35
(`filler-demand.md` §6), the price needed is `b + N · fixed ÷ (S · φ)`:

| Dedicated Type 2 hardware | Price needed, CHF/server-h | Market reference |
|---|---:|---|
| Dense CPU servers: 0.226 + 27,229 ÷ (22,661 × 0.35) | **3.66** | pinned 0.66, Swiss spot ceiling 1.80 |
| RTX 4090 nodes: 0.226 + 16,531 ÷ (22,661 × 0.35) | **2.31** | RTX 4090 market 0.57–0.84 (`cp-mkt-runpod-4090-community`, `cp-mkt-vast-4090-median`) |

**b) Type 2 on paid-for hardware is pure contribution.** Per house and year, if all leftover hours are sold:

| Type 2 price | CHF/server-h | Contribution per hour | Per house/yr |
|---|---:|---:|---:|
| Pinned | 0.66 | 0.43 | 9,833 |
| RTX 4090 market, RunPod community (2 × USD 0.34) | 0.57 | 0.35 | 7,823 |
| RTX 4090 market, vast.ai median (2 × USD 0.50) | 0.84 | 0.61 | 13,916 |

**c) Type 1 is why the node is a GPU node.** For LLM and transcription work, GPUs need far less energy
per unit. The table below is **indicative**: the comparison uses our laptop, not the EPYC server we
reject, and different power boundaries (laptop CPU package vs GPU board rating).

| Work unit | Laptop CPU (our measurement) | GPU | GPU advantage | Comparable? |
|---|---:|---:|---:|---|
| LLM input token | 0.45 J | 0.030 J (RTX 4090, `pe-rtx4090-llama7b-pp`, verified) | 15× | yes, both unbatched prompt processing |
| LLM output token, batch 1 | 5.6 J | 2.38 J (`pe-rtx4090-llama7b-tg`, verified) | 2.4× | yes, both batch 1 |
| LLM token, batched | — | 0.21 J (`pe-rtx4090-vllm-iog-qps8`) | — | no CPU batched measurement |
| Audio minute | 0.477 Wh (large-v3) | 0.026 Wh (L4, large-v3-turbo, `pe-l4-whisper-*`) | — | no: different models |

Hardware cost per kW (also indicative, because the power bases differ). The CPU count uses CPU TDP only
(280 W, RAM unknown, `pc-cpu-brack-r6615-9354p` is a snippet whose page returned 404). The GPU node counts
100 W of host power for two desktops. Dense CPU: CHF 25,671 per kW, full-cost break-even 1.43. RTX 4090
node: CHF 16,300 per kW, break-even 0.96. On a common wall-power basis both would move: `TODO(source)`.

**d) Simulations run on GPUs, but energy efficiency is not the reason for Type 2 on GPU.**

- Swiss research computing is mostly GPU: 74 % of the CSCS Alps nodes carry GPUs (`pe-cscs-alps-gpu-nodes`).
- All of the Green500 top 10 are GPU-accelerated; the leader runs at 73.3 GFLOPS/W (`pe-green500-top-gh200`).
- NERSC: A100 GPUs raised application energy efficiency 5× on average (`pe-nersc-a100-energy-5x`, vendor).
- **Finance Monte Carlo (STAC-A2):** the best GPU node is only 1.3–1.6× more efficient than the best CPU
  node (311,045 vs 231,271 options/kWh, `pe-stac-a2-*`, press or vendor sources).
- Not every code runs on GPUs: 16 of the top 50 HPC packages lacked GPU support in 2015
  (`pe-intersect360-gpu-apps`, old, low confidence). Those run on the host CPU.

The deciding reasons for Type 2 on GPU nodes are (a) and (b): shared hardware.

**e) The price of the effort over the hardware's life.** Share of heat-led hours Type 1 must sell for the
house to break even (`N · fixed ÷ S = x (p1 − b) + (1 − x) φ2 (p2 − b)`, solved for `x`):

| Type 1 on RTX 4090 | Prices | No Type 2 | Type 2 sells 35 % of the rest | Type 2 sells all |
|---|---|---:|---:|---:|
| Batch 1, list (CHF 1.72) | year 1 | 49 % | 43 % | 28 % |
| Batch 1, list | lifetime average (tokens ×0.498, compute ×0.553) | 116 % | 117 % | 120 % |
| Batched, conservative (CHF 1.95) | year 1 | 42 % | 37 % | 23 % |
| Batched, conservative | lifetime average | 98 % | 98 % | 98 % |

At commodity prices that fall like the market trends (R5), **nothing pays over 6 years**, with or without
Type 2. Either Type 1 sells above commodity, at 1.19–1.34× in year 1 and 2.4–2.7× over the lifetime at a
fill rate of 0.35, or capacity is sold under fixed multi-year prices.

**f) The location premium is measured.** The same Azure VM costs a median **+10.0 %** more in Switzerland
North than in Frankfurt (range 10–18 % over 10 SKU/price-type pairs, `cp-region-azure-ch-premium-median`,
raw snapshot `price-snapshots/azure-region-comparison-2026-11-21.json`).

## Alternatives considered

- **CPU servers for Type 2 (decision 0018):** rejected by (a).
- **L4 data-centre GPUs:** CHF 44,179 per kW at the verified card price (`pc-gpu-bechtle-l4`), break-even
  1.65. Kept as the comparison profile.
- **Mixed fleet (GPU nodes for Type 1, CPU nodes for Type 2):** adds hardware that Type 2 cannot pay for.

## Consequences

- **T-30 (Type 2 job):** a Monte Carlo kernel with a GPU path (checkpoints at task boundaries; "CPU freeze
  is not a general CUDA checkpoint", T-38 review) and a CPU fallback. **The live demo and the laptop
  measurement use the CPU fallback**, because our laptops have no CUDA GPU (AMD iGPU, checked on Ennio's).
  The must-have does not depend on a GPU.
- The live executor needs a GPU control path. `DockerJobExecutor` controls CPU quota only and cannot report
  GPU power (T-38). This needs its own decision record when designed.
- Open checks, each of which can change the answer:
  - **Driver licence, the biggest open risk.** NVIDIA's GeForce driver licence (v. 25 Feb 2025, §2.8) says
    the software "is not licensed for datacenter deployment" (`pc-licence-geforce-no-datacenter`, opened).
    "Datacenter" is undefined, and a commercially run network of home servers is a grey area that needs
    legal advice. Alternatives without this clause: professional or data-centre cards (RTX PRO 6000 at
    CHF 22,565 per kW card only, `pc-gpu-bechtle-rtxpro6000`; L4 at CHF 44,179 per kW). They have higher
    break-evens, so the consumer-card economics are a best case until this is cleared.
  - The RTX 4090 is reportedly out of production since about September 2024 (`pc-rtx4090-production-end`,
    snippets). The profile is a cost proxy for the consumer class (RTX 5090 successor, full system price
    `TODO(source)`).
  - batched throughput with input and output tokens separated (moves the year-1 Type 1 share needed
    between 8 % and 42 %);
  - wall power of a GPU node and of a dense CPU server on the same basis;
  - water blocks, heat exchanger and installation (excluded from all prices);
  - continuous operation of consumer GPUs at water temperature, and their life (6 years assumed, `TODO(source)`);
  - GPU idle power (RTX 4090 about 22 W, forum source, `pe-rtx4090-idle`).
- Scored reference-house figures are unchanged (abstract 1 kW server).

## Review

Fresh-context review (decision-reviewer subagent, 2026-11-21): **go with changes.** It recomputed CHF 3.66
and the 49/43/28 % shares, and found no calculation errors. Changes made because of it:

1. The core argument is now "Type 2 rides on Type 1 hardware" (a, b). The CPU-vs-GPU comparisons are
   labelled indicative, and the different power bases are stated.
2. The energy table no longer claims every comparison favours the CPU. The non-comparable rows are marked.
3. A lifetime column was added. It shows that nothing pays over 6 years at falling commodity prices.
4. New open checks: the GeForce licence (since verified: the restriction is in the current text), the RTX 4090 status, water-block costs, consumer GPU life, and a
   CPU fallback for the T-30 demo.
