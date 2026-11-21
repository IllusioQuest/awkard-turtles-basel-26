# 0027: Datacenter-licensed GPUs only; L4 replaces RTX 4090 as hardware basis

- **Status:** proposed (decided by the team lead on 2026-11-21; team confirmation pending)
- **Date:** 2026-11-21
- **Decided by:** team lead. Supersedes the **hardware part** of decisions 0019 (point 2: "consumer
  cards are the cost basis") and 0020 (all numbers on the RTX 4090 profile). The job model of 0020
  (Type 1 batch LLM per token, served first; Type 2 research simulations as filler, pinned CHF 0.66;
  operator model) and the rule of 0019 that every node is a GPU node and Type 2 rides on Type 1
  hardware stay unchanged.

## Context

Decisions 0019 and 0020 used consumer GeForce cards (RTX 4090 profile, CHF 16,300 per kW,
`config/hardware/gpu-consumer-rtx4090.json`) as the cost basis, because it was the only profile that
cleared full cost. Both records listed NVIDIA's driver licence as the biggest open risk:

- NVIDIA, *License For Customer Use of NVIDIA GeForce Software*, v. 25 February 2025, §2.8: the
  GeForce or Titan software "(i) is licensed for use only on GeForce or Titan hardware products you
  own" and "(ii) is not licensed for datacenter deployment."
  <https://www.nvidia.com/en-us/drivers/geforce-license> (opened 2026-11-21 by T-37; CSV row
  `pc-licence-geforce-no-datacenter` in `data/raw/market/`). Re-opening the page from this session
  failed (host not reachable from the sandbox, 2026-11-21); the quote is the one recorded in that row.
- "Datacenter" is not defined in the licence. A fleet that DigitalHeat owns, operates and sells as a
  commercial compute service is, in substance, a distributed data centre, even if the boxes stand in
  basements. GPU compute without the NVIDIA driver is not a realistic option for CUDA workloads.

The team lead decided (2026-11-21) not to carry this legal risk into the pitch.

## Decision

1. **Only datacenter-licensed GPUs** (NVIDIA data-centre/professional cards whose driver licence
   permits data-centre use) are used in the business model, the pitch numbers and the economics
   defaults. Consumer GeForce cards (RTX 4090/5090 class) are **not permitted (licence)**.
2. **The L4 inference profile (`config/hardware/gpu-inference-l4.json`) is the hardware basis.**
   Refurbished L4 and T4 (`*-refurbished.json`, T-35, on main) are labelled variants (lever D).
3. The RTX 4090 profile file stays in the repository as a documented, rejected comparison; it is
   marked "not permitted (licence)" and excluded from the economics defaults (T-14).
4. **Type 1 income on L4** is computed with the 0020 rule (price per token × tokens per server-hour).
   No L4 LLM throughput is sourced yet, so the L4 energy per token reuses the RTX 4090 figures of
   T-37 (same Ada Lovelace generation) as a labelled proxy, `TODO(source)` until measured on an L4
   (`vllm bench serve` + `nvidia-smi`).
5. Scored reference-house figures stay on the case's abstract 1 kW server and do not change.

## Alternatives considered

- **Keep RTX 4090 as the basis and seek legal advice.** Cheapest hardware, but the licence text is
  explicit and the team cannot clear it before the pitch; a juror can read the clause.
- **RTX PRO 6000 (professional, CHF 22,565 per kW card only, `pc-gpu-bechtle-rtxpro6000`).** Cheaper
  per kW than L4, but there is no host system price, no throughput source and no profile file yet.
  Possible later variant.
- **CPU-only servers.** Rejected by 0019 (a); kept as the comparison profile.

## Consequences

- **The economics get worse and the pitch says so.** L4 costs CHF 34,244 per 1 kW server in the
  profile (CHF 44,179 at the newer verified card price, `profitability-inputs.md` §7) against 16,300
  for the RTX 4090. The full-cost break-even of the draft scenario (4 servers, every heat-led hour
  sold) rises from CHF 0.956 to **CHF 1.358 per sold server-hour** (0.2261 + 4 × (34,244.06 ÷ 6 +
  708) ÷ 22,661).
- **Type 1 per server-hour on L4** (proxy, see 4): 0.8375 kW of L4 boards × 3.6 MJ ÷ 0.21 J per
  token (T-37 batched, conservative) = 14.36 M tokens, i.e. **CHF 1.81** at the list price of
  CHF 0.126 per 1M tokens and **CHF 0.90** at the batch-API level (−50 %). Only the list price clears
  full cost; numbers and the required Type 1 share come from the T-14 economics bundle
  (`docs/model/economics.md`, decision 0028).
- `docs/business-model.md` switches its numbers to L4 (changed rows marked), T-14 (PR #27) uses L4
  as default and removes RTX 4090 from its defaults.
- Open checks: measured L4 LLM throughput and power (moves every Type 1 number); confirmed L4 card
  price (34,244 vs 44,179 CHF/kW); NVIDIA data-centre driver/vGPU licence terms for L4 (no per-GPU
  software fee is assumed for bare-metal CUDA use, `TODO(source)`).

## Review

See the "Review" section below once the fresh-context review has run.
