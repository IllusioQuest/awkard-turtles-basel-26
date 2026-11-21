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

The team lead decided (2026-11-21) not to carry this legal risk into the pitch. This is **risk
avoidance, not a legal finding**: we do not claim that home servers are a data centre in law, only
that we cannot show they are not.

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
   T-37 as a labelled **proxy, direction of error unknown** (same Ada Lovelace generation, but a
   much smaller memory system, and both J/token figures divide board power ratings, not measured
   power). `TODO(source)`: replace it by one hour on a rented cloud L4 in Zurich (Google Cloud
   g2-standard-4, CHF 0.82/h, `cp-gpu-gcp-g2-standard-4-euw6-od`) with `vllm bench serve` +
   `nvidia-smi`.
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
  (`docs/model/economics.md`, decision 0028). Sanity bound: the AWS Zurich spot
  price of the same L4 capacity is CHF 2.23 per server-hour (`gpu-inference-l4.json`), so a buyer
  can rent equivalent capacity for about 1.2× our list-price token income.
- **The L4 profile uses the cheaper card price** (CHF 2,235, low confidence). At the verified
  CHF 3,089 (`pc-gpu-bechtle-l4`, `profitability-inputs.md` §7) one server costs CHF 44,179 and the
  full-cost break-even of the draft scenario is **CHF 1.651**; Type 1 at list (1.81) then clears it
  by only about 10 %. The profile file belongs to T-15/T-35 and is not changed here; the team must
  pick the default card price (see Review).
- `docs/business-model.md` switches its numbers to L4 (changed rows marked), T-14 (PR #27) uses L4
  as default and removes RTX 4090 from its defaults.
- Open checks: measured L4 LLM throughput and power (moves every Type 1 number); confirmed L4 card
  price (34,244 vs 44,179 CHF/kW); NVIDIA data-centre driver licence and CUDA licence terms for
  bare-metal L4 use (no per-GPU software fee is assumed, `TODO(source)`: nvidia.com was not
  reachable from this session).

## Review

Fresh-context review (separate Claude Code session with only the packet, 2026-11-21): **go with
changes.** Points and what we did:

1. *Card price:* the default L4 profile uses the low-confidence CHF 2,235; the verified CHF 3,089
   gives CHF 44,179 per server and a full-cost break-even of 1.651, which list-price Type 1 clears by
   only ~10 %. **Partly adopted:** stated in Consequences, `business-model.md` and
   `docs/model/economics.md`; the profile default is not switched in this PR because the file is
   owned by T-15/T-35 and it would move every figure — team decision.
2. *Throughput proxy:* "same generation" is weak; label it "direction of error unknown", add the
   AWS spot sanity bound and measure one hour on a cloud L4. **Adopted** (label, bound, measurement
   named as the `TODO(source)`); the measurement itself is not done yet.
3. *Licence of the replacement:* cite NVIDIA's data-centre/CUDA licence and say the decision avoids
   a risk, not a legal finding. **Adopted** the wording; the citation stays `TODO(source)` because
   nvidia.com was not reachable from this session.
