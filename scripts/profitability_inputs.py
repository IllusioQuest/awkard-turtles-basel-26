#!/usr/bin/env python3
"""Profitability inputs for DigitalHeat (ticket T-37): every number from a CSV row or config file.

Reads sourced values by id from data/raw/market/*.csv, the hardware profiles in config/hardware/,
and the reference-house year result (data/derived/reference-house/explorer-default.json), then
prints the tables of docs/research/profitability-inputs.md and writes
data/derived/profitability/summary.json.

    python3 scripts/profitability_inputs.py

Standard library only, deterministic. Formulas are in the docstrings and in the document.
This is a calculation of inputs for the economics of T-14 (decision 0017 on its branch), not a
second economic model: it uses the same surplus definition (income + used-heat value
- electricity - depreciation - operations) and the same rule that unsold hours are not run.
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MARKET = ROOT / "data" / "raw" / "market"
OUT = ROOT / "data" / "derived" / "profitability" / "summary.json"

# Values that are not in a CSV row, each with its source.
TYPE2_PINNED_CHF_PER_SERVER_HOUR = 0.66   # docs/research/computing-price.md, team decision 2026-11-21
FILL_RATE_BASE = 0.35                     # docs/research/demand.md §9 (range 0.2-0.6)
FILL_RATE_LOW, FILL_RATE_HIGH = 0.20, 0.60
LLM_INPUT_TOKENS, LLM_OUTPUT_TOKENS = 3310, 319  # docs/jobs/llm-summary.md, measured token mix
BATCH_API_DISCOUNT = 0.5                  # mistral.ai/pricing, noted in ai-llm-mistral-ministral3-8b
USD_TO_CHF = 0.8402                       # docs/research/ai-services.md (exchange-rate table)
# Own laptop measurements (AMD Ryzen 7 250, CPU package, RAPL), docs/jobs/llm-summary.md and transcription.md
LAPTOP_J_PER_INPUT_TOKEN = 0.45
LAPTOP_J_PER_OUTPUT_TOKEN = 5.6
LAPTOP_WH_PER_AUDIO_MIN_LARGE_V3 = 0.477
MONTHS_PER_YEAR = 12
SECONDS_PER_HOUR = 3600
J_PER_KWH = 3.6e6
TOKENS_PER_MILLION = 1e6
WEEKS_PER_YEAR = 52
PARTICIPATION = (0.01, 0.05, 0.10)


def load_rows() -> dict[str, dict]:
    rows: dict[str, dict] = {}
    for path in sorted(MARKET.glob("*.csv")):
        with path.open(newline="") as f:
            for r in csv.DictReader(f):
                if r.get("id"):
                    rows[r["id"]] = r
    return rows


ROWS = load_rows()


def v(row_id: str, field: str = "value") -> float:
    """Numeric value of a CSV row (raises if the id is missing: no silent defaults)."""
    return float(ROWS[row_id][field])


def profile(name: str) -> dict[str, float]:
    d = json.loads((ROOT / "config" / "hardware" / f"{name}.json").read_text())
    return {k: x["value"] for k, x in d.items() if isinstance(x, dict) and "value" in x}


def lifetime_average(yearly_factor: float, years: int) -> float:
    """Mean price level over `years` if the price is multiplied by `yearly_factor` each year:
    (1 - f^L) / (L (1 - f)); year 1 = 1.0."""
    if yearly_factor == 1:
        return 1.0
    return (1 - yearly_factor ** years) / (years * (1 - yearly_factor))


def main() -> None:
    house = json.loads((ROOT / "data/derived/reference-house/explorer-default.json").read_text())["Summary"]
    servers = house["ServerCount"]
    sold_h = house["ServerHours"]                    # 22,661 server-hours per year (4 servers)
    elec_chf, heat_chf = house["ElectricityCostChf"], house["HeatValueChf"]
    case_be = (elec_chf - heat_chf) / sold_h          # CHF 0.2261 per server-hour
    sold_h_per_server = sold_h / servers

    ops_per_box = v("cost-ops-smartsolution-server-basic") * MONTHS_PER_YEAR   # CHF 708 per box-year
    life = int(v("pc-life-hyperscaler-years"))                                # 6 years

    # --- hardware profiles per 1 kW server ---------------------------------------------------
    cpu_boxes = 1 / (v("pc-cpu-dell-epyc9354p-tdp") / 1000)                    # TDP basis, like hardware.md
    l4 = profile("gpu-inference-l4")
    l4_cards = (1 - l4["HostElectricKw"]) / (v("hw-power-nvidia-l4-tdp") / 1000)
    rtx = profile("gpu-consumer-rtx4090")
    profiles = {
        "cpu-dense (R6615, EPYC 9354P)": dict(
            price=cpu_boxes * v("pc-cpu-brack-r6615-9354p"), ops=cpu_boxes * ops_per_box,
            basis=f"{cpu_boxes:.2f} boxes x CHF {v('pc-cpu-brack-r6615-9354p'):,.0f} (pc-cpu-brack-r6615-9354p, TDP pc-cpu-dell-epyc9354p-tdp)"),
        "gpu-l4 (11.6 L4 + host, Bechtle price)": dict(
            price=l4_cards * v("pc-gpu-bechtle-l4") + v("cost-server-dell-r570"), ops=ops_per_box,
            basis=f"{l4_cards:.2f} x CHF {v('pc-gpu-bechtle-l4'):,.0f} (pc-gpu-bechtle-l4) + host CHF {v('cost-server-dell-r570'):,.0f}"),
        "gpu-rtx4090 (2 desktops)": dict(
            price=rtx["PurchasePriceChfPerServer"], ops=rtx["OpsChfPerServerYear"],
            basis="config/hardware/gpu-consumer-rtx4090.json"),
    }
    for p in profiles.values():
        p["fixed_per_year"] = p["price"] / life + p["ops"]
        p["full_be"] = case_be + p["fixed_per_year"] / sold_h_per_server

    # --- income per sold server-hour ---------------------------------------------------------
    gpu_share = rtx["UnitsPerServer"] * rtx["UnitElectricKw"]                 # 0.9 of the 1 kW
    w_gpu = v("pe-rtx4090-tgp")
    j_in = w_gpu / v("pe-rtx4090-llama7b-pp")                                  # J per input token
    j_out_b1 = w_gpu / v("pe-rtx4090-llama7b-tg")                              # J per output token, batch 1
    j_out_b16 = w_gpu / v("pe-rtx4090-vllm-iog-qps8")                          # batched, vLLM request rate 8/s
    share_in = LLM_INPUT_TOKENS / (LLM_INPUT_TOKENS + LLM_OUTPUT_TOKENS)

    def tokens_per_server_hour(j_out: float) -> float:
        j_mix = share_in * j_in + (1 - share_in) * j_out
        return gpu_share * J_PER_KWH / j_mix

    tok_b1, tok_b16 = tokens_per_server_hour(j_out_b1), tokens_per_server_hour(j_out_b16)
    # conservative reading: the batched figure counts all tokens, so every token costs j_out_b16
    tok_b16_cons = gpu_share * J_PER_KWH / j_out_b16
    price_tok = v("ai-llm-mistral-ministral3-8b", "normalised_value")         # CHF per 1M tokens
    f_compute = 2 ** (-1 / v("tr-hw-perfusd-epoch-all-gpu"))                  # 0.754 per year
    f_token = v("tr-stt-halving-derived")                                     # 0.707 per year
    options = [
        ("Type 2 CPU, pinned price", "cpu-dense (R6615, EPYC 9354P)", TYPE2_PINNED_CHF_PER_SERVER_HOUR, f_compute),
        ("Type 2 CPU, Swiss spot ceiling", "cpu-dense (R6615, EPYC 9354P)", profile("cpu-server")["SpotPriceChfPerServerHour"], f_compute),
        ("L4 sold at AWS spot equivalent", "gpu-l4 (11.6 L4 + host, Bechtle price)", l4["SpotPriceChfPerServerHour"], f_compute),
        ("RTX 4090 at home-host marketplace", "gpu-rtx4090 (2 desktops)", rtx["HomeHostPriceChfPerServerHour"], f_compute),
        ("Type 1 LLM on RTX 4090, batch 1, list price", "gpu-rtx4090 (2 desktops)", tok_b1 / TOKENS_PER_MILLION * price_tok, f_token),
        ("Type 1 LLM on RTX 4090, batch 1, batch-API price", "gpu-rtx4090 (2 desktops)", tok_b1 / TOKENS_PER_MILLION * price_tok * BATCH_API_DISCOUNT, f_token),
        ("Type 1 LLM on RTX 4090, batched (conservative), list price", "gpu-rtx4090 (2 desktops)", tok_b16_cons / TOKENS_PER_MILLION * price_tok, f_token),
        ("Type 1 LLM on RTX 4090, batched (conservative), batch-API price", "gpu-rtx4090 (2 desktops)", tok_b16_cons / TOKENS_PER_MILLION * price_tok * BATCH_API_DISCOUNT, f_token),
        ("Type 1 LLM on RTX 4090, batched (optimistic), list price", "gpu-rtx4090 (2 desktops)", tok_b16 / TOKENS_PER_MILLION * price_tok, f_token),
        ("Type 1 LLM on RTX 4090, batched (optimistic), batch-API price", "gpu-rtx4090 (2 desktops)", tok_b16 / TOKENS_PER_MILLION * price_tok * BATCH_API_DISCOUNT, f_token),
    ]

    def surplus(fill: float, price: float, prof: dict) -> float:
        """Per house and year: fill x sold_h x (price - case break-even) - servers x fixed."""
        return fill * sold_h * (price - case_be) - servers * prof["fixed_per_year"]

    def fill_needed(price: float, prof: dict) -> float:
        """Fill rate at which the surplus is zero: servers x fixed / (sold_h x (price - case_be))."""
        m = price - case_be
        return float("inf") if m <= 0 else servers * prof["fixed_per_year"] / (sold_h * m)

    results = []
    for label, pname, price, f in options:
        prof = profiles[pname]
        avg = lifetime_average(f, life)
        results.append(dict(
            option=label, profile=pname, price_year1=price, price_lifetime_avg=price * avg,
            lifetime_avg_factor=avg, full_be=prof["full_be"],
            surplus_fill_1=surplus(1, price, prof), surplus_fill_base=surplus(FILL_RATE_BASE, price, prof),
            fill_needed_year1=fill_needed(price, prof), fill_needed_lifetime=fill_needed(price * avg, prof)))

    # --- network capacity vs demand (Basel-Stadt) -------------------------------------------
    cap = {p: v(f"hh-cap-bs-all-{int(p * 100)}") for p in PARTICIPATION}
    tokens_year = v("cd-t1-openrouter-week") * 1e12 * WEEKS_PER_YEAR
    demand = [
        ("Basel physician dictation, large-v3 (CPU)", v("cd-d-basel-stt-large")),
        ("sciCORE Basel research, energy basis", v("cd-d-scicore-sh-energy")),
        ("sciCORE Basel research, node basis (upper bound)", v("cd-d-scicore-sh")),
        ("OpenRouter token volume on RTX 4090, batched (conservative)", tokens_year / tok_b16_cons),
        ("OpenRouter token volume on RTX 4090, batch 1", tokens_year / tok_b1),
    ]

    # --- print -----------------------------------------------------------------------------
    print(f"Reference house: {servers} servers, {sold_h:,.0f} sold server-hours/yr "
          f"({sold_h_per_server:,.0f} per server), electricity CHF {elec_chf:,.0f}, used heat CHF {heat_chf:,.0f}, "
          f"case break-even CHF {case_be:.4f}/server-hour. Lifetime {life} y, ops CHF {ops_per_box:.0f} per box-year.\n")
    print("| Profile (1 kW) | Purchase CHF | Basis | Fixed CHF/yr (depr. + ops) | Full-cost break-even CHF/server-h |")
    print("|---|---:|---|---:|---:|")
    for n, p in profiles.items():
        print(f"| {n} | {p['price']:,.0f} | {p['basis']} | {p['fixed_per_year']:,.0f} | {p['full_be']:.3f} |")
    print(f"\nLLM on RTX 4090: {j_in:.4f} J/input token, {j_out_b1:.3f} J/output token (batch 1), "
          f"{j_out_b16:.3f} (batched); input share {share_in:.3f}; GPU share of 1 kW {gpu_share:.2f} -> "
          f"{tok_b1 / 1e6:.1f} M tokens/server-h (batch 1), {tok_b16_cons / 1e6:.1f} M (batched, conservative), {tok_b16 / 1e6:.1f} M (batched, optimistic). "
          f"Token price CHF {price_tok:.4f}/1M. Yearly price factors: compute {f_compute:.3f}, tokens {f_token:.3f}.\n")
    print("| Option | Income CHF/server-h, year 1 | Lifetime average | Surplus per house/yr, all hours sold | Surplus at fill 0.35 | Fill rate needed, year-1 price | Fill rate needed, lifetime price |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    fmt = lambda x: "not reachable" if x > 1 else f"{x:.2f}"
    for r in results:
        print(f"| {r['option']} | {r['price_year1']:.2f} | {r['price_lifetime_avg']:.2f} | {r['surplus_fill_1']:,.0f} | "
              f"{r['surplus_fill_base']:,.0f} | {fmt(r['fill_needed_year1'])} | {fmt(r['fill_needed_lifetime'])} |")
    print("\n| Basel-Stadt capacity (S1, gas/oil buildings) | Server-hours/yr |\n|---|---:|")
    for p, c in cap.items():
        print(f"| {p:.0%} participation | {c:,.0f} |")
    print("\n| Demand segment | Server-hours/yr | Share of Basel 1 % capacity |\n|---|---:|---:|")
    for name, d in demand:
        print(f"| {name} | {d:,.0f} | {d / cap[0.01]:.2%} |")

    # --- GPU vs CPU (decision 0019) ----------------------------------------------------------
    # Energy per work unit: laptop CPU (own measurement, docs/jobs/*.md) vs GPU (CSV rows).
    energy = [
        ("LLM input token (J)", LAPTOP_J_PER_INPUT_TOKEN, j_in, "pe-rtx4090-llama7b-pp"),
        ("LLM output token, batch 1 (J)", LAPTOP_J_PER_OUTPUT_TOKEN, j_out_b1, "pe-rtx4090-llama7b-tg"),
        ("LLM token, batched, conservative (J)", LAPTOP_J_PER_OUTPUT_TOKEN, j_out_b16, "pe-rtx4090-vllm-iog-qps8"),
        ("Whisper, Wh per audio minute (large-v3 CPU vs large-v3-turbo L4)", LAPTOP_WH_PER_AUDIO_MIN_LARGE_V3,
         v("pe-l4-whisper-power") / v("pe-l4-whisper-rtfx") / 60, "pe-l4-whisper-power, -rtfx"),
    ]
    gpu_prof, cpu_prof = profiles["gpu-rtx4090 (2 desktops)"], profiles["cpu-dense (R6615, EPYC 9354P)"]
    cpu_type2_needed = case_be + servers * cpu_prof["fixed_per_year"] / (sold_h * FILL_RATE_BASE)
    gpu_type2_needed = case_be + servers * gpu_prof["fixed_per_year"] / (sold_h * FILL_RATE_BASE)  # dedicated GPU node
    gpu_type2_market = [("pinned Type 2 price", TYPE2_PINNED_CHF_PER_SERVER_HOUR)] + [
        (rid, rtx["UnitsPerServer"] * v(rid) * USD_TO_CHF) for rid in ("cp-mkt-runpod-4090-community", "cp-mkt-vast-4090-median")]

    def type1_share_needed(p1: float, p2: float, fill2: float) -> float:
        """Share x of heat-led hours sold as Type 1 so that the house breaks even, Type 2 filling a share
        fill2 of the rest: N*fixed = S*[x(p1-b) + (1-x) fill2 (p2-b)]  ->  x."""
        m1, m2 = p1 - case_be, fill2 * (p2 - case_be)
        return (servers * gpu_prof["fixed_per_year"] / sold_h - m2) / (m1 - m2)

    swiss_premium = v("cp-region-azure-ch-premium-median") / 100
    needed_price = case_be + servers * gpu_prof["fixed_per_year"] / (sold_h * FILL_RATE_BASE)
    type1_variants = [("batch 1, list price", tok_b1 / TOKENS_PER_MILLION * price_tok),
                      ("batched (conservative), list price", tok_b16_cons / TOKENS_PER_MILLION * price_tok)]

    print("\n| Energy per work unit | Laptop CPU (measured) | GPU | GPU advantage | GPU source |\n|---|---:|---:|---:|---|")
    for name, cpu_val, gpu_val, src in energy:
        print(f"| {name} | {cpu_val:.4g} | {gpu_val:.4g} | {cpu_val / gpu_val:.1f}x | `{src}` |")
    print(f"\nType 2 alone on dedicated CPU servers, fill {FILL_RATE_BASE}: price needed CHF {cpu_type2_needed:.2f} per server-hour "
          f"= {cpu_type2_needed / TYPE2_PINNED_CHF_PER_SERVER_HOUR:.1f}x the pinned CHF {TYPE2_PINNED_CHF_PER_SERVER_HOUR}; "
          f"Swiss spot ceiling CHF {profile('cpu-server')['SpotPriceChfPerServerHour']}. "
          f"Dedicated RTX 4090 node for Type 2 only: CHF {gpu_type2_needed:.2f} (RTX 4090 market 0.57-0.84).")
    print("\n| Type 2 price on a GPU node | CHF/server-h | Contribution over case break-even | Per house/yr if all leftover hours sold |\n|---|---:|---:|---:|")
    for name, p in gpu_type2_market:
        print(f"| {name} | {p:.2f} | {p - case_be:.2f} | {sold_h * (p - case_be):,.0f} |")
    avg_t, avg_c = lifetime_average(f_token, life), lifetime_average(f_compute, life)
    print("\n| Type 1 on RTX 4090 | Prices | Type 1 CHF/server-h | Type 1 share needed, no Type 2 | Type 2 sells 35 % of the rest | Type 2 sells all of the rest |\n|---|---|---:|---:|---:|---:|")
    for name, p1 in type1_variants:
        for label, a1, a2 in (("year 1", 1.0, 1.0), ("lifetime average", avg_t, avg_c)):
            cells = [type1_share_needed(p1 * a1, TYPE2_PINNED_CHF_PER_SERVER_HOUR * a2, f2) for f2 in (0, FILL_RATE_BASE, 1)]
            print(f"| {name} | {label} | {p1 * a1:.2f} | " + " | ".join(f"{c:.2f}" for c in cells) + " |")
    print(f"\nSwiss premium: same Azure VM costs {swiss_premium:.1%} more in Switzerland North than Frankfurt (median). "
          f"Type 1 price needed at fill {FILL_RATE_BASE} without Type 2: CHF {needed_price:.2f} per server-hour.")
    print("\n| Type 1 variant | Commodity CHF/server-h | Needed, year 1 | Needed over lifetime (falling prices) | With +10 % Swiss premium |\n|---|---:|---:|---:|---:|")
    for name, p1 in type1_variants:
        avg = lifetime_average(f_token, life)
        print(f"| {name} | {p1:.2f} | {needed_price / p1:.2f}x | {needed_price / (p1 * avg):.2f}x | {1 + swiss_premium:.2f}x |")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(dict(
        reference_house=dict(servers=servers, sold_server_hours=sold_h, case_break_even=case_be),
        gpu_vs_cpu=dict(cpu_type2_price_needed=cpu_type2_needed, dedicated_gpu_type2_price_needed=gpu_type2_needed,
                        type2_on_gpu_contribution={n: p - case_be for n, p in gpu_type2_market},
                        type1_price_needed_fill_base=needed_price, swiss_region_premium=swiss_premium),
        profiles=profiles, options=results, basel_capacity=cap, demand=dict(demand),
        tokens_per_server_hour=dict(batch1=tok_b1, batched_conservative=tok_b16_cons, batched_optimistic=tok_b16),
        yearly_price_factor=dict(compute=f_compute, tokens=f_token), lifetime_years=life,
        fill_rate=dict(base=FILL_RATE_BASE, low=FILL_RATE_LOW, high=FILL_RATE_HIGH)), indent=2) + "\n")


if __name__ == "__main__":
    main()
