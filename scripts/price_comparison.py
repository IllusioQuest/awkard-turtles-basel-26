#!/usr/bin/env python3
"""T-16: our cost per unit versus market prices (LLM tokens, transcription, research Monte Carlo).

Usage: python3 scripts/price_comparison.py
Inputs: config/price-comparison.json, config/hardware/*.json, config/reference-house.json,
        data/raw/market/ai-services.csv. Output: data/derived/price_comparison.csv (+ printed table).
Method (docs/research/price-comparison.md):
    case cost   [CHF/server-h] = electricity price x server kW - recovered heat x price / heat-pump COP
                 (heat fully used, as in the case break-even)
    full cost   [CHF/server-h] = case cost + (purchase / lifetime + operations per year) / sold hours per year
    cost per unit = cost per server-hour / units per server-hour
Standard library only. Values with a TODO(source) stay empty in the output.
"""

from __future__ import annotations

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def val(p):
    return p["value"] if isinstance(p, dict) else p


def load_json(rel: str) -> dict:
    return json.loads((ROOT / rel).read_text(encoding="utf-8"))


def market_rows(ids: list[str]) -> list[dict]:
    with open(ROOT / "data/raw/market/ai-services.csv", newline="", encoding="utf-8") as f:
        rows = {r["id"]: r for r in csv.DictReader(f)}
    return [rows[i] for i in ids]


def blended(normalised: str, input_share: float) -> float:
    """CHF per 1M tokens for our measured input/output mix ('in / out' or a single value)."""
    parts = [float(x) for x in normalised.split("/")]
    return parts[0] if len(parts) == 1 else input_share * parts[0] + (1 - input_share) * parts[1]


def main() -> None:
    cfg = load_json("config/price-comparison.json")
    ref = load_json("config/reference-house.json")
    price = val(ref["ElectricityPriceChfPerKwh"])
    recovery = val(ref["HeatRecoveryShare"])
    cop = val(ref["HeatPumpEfficiency"])
    hours_year = val(cfg["hoursPerYear"])
    out: list[dict] = []

    for hw_id in cfg["hardwareProfiles"]:
        hw = load_json(f"config/hardware/{hw_id}.json")
        kw = val(hw["ServerElectricKw"])
        case_cost = price * kw - recovery * kw * price / cop
        capex_per_year = val(hw["PurchasePriceChfPerServer"]) / val(hw["LifetimeYears"]) + val(hw["OpsChfPerServerYear"])
        for scen in cfg["utilisationScenarios"]:
            sold = val(scen["soldShareOfHours"]) * hours_year
            full_cost = case_cost + capex_per_year / sold
            base = {"hardware": hw_id, "utilisation": scen["id"], "caseCostChfPerServerHour": round(case_cost, 4),
                    "fullCostChfPerServerHour": round(full_cost, 4)}
            # LLM tokens
            e = val(cfg["llm"]["energyKwhPerMillionTokens"][hw_id])
            mtok = (kw / e) if e else None
            out.append({**base, "product": "LLM batch (8B class)", "unit": "CHF per 1M tokens (91 % input)",
                        "unitsPerServerHour": round(mtok, 3) if mtok else "",
                        "ourCaseCostPerUnit": round(case_cost / mtok, 4) if mtok else "",
                        "ourFullCostPerUnit": round(full_cost / mtok, 4) if mtok else ""})
            # Transcription
            ah = val(hw.get(cfg["transcription"]["audioHoursPerServerHourFrom"]["value"]))
            out.append({**base, "product": "Transcription", "unit": "CHF per audio hour",
                        "unitsPerServerHour": ah or "",
                        "ourCaseCostPerUnit": round(case_cost / ah, 5) if ah else "",
                        "ourFullCostPerUnit": round(full_cost / ah, 5) if ah else ""})
            # Research Monte Carlo: compared per 1 kW server-hour, only where the job uses the whole server
            tasks = cfg["researchMonteCarlo"]["tasksPerServerHour"].get(hw_id)
            if tasks is None:
                continue
            out.append({**base, "product": "Research Monte Carlo", "unit": "CHF per 1 kW server-hour",
                        "unitsPerServerHour": val(tasks) if tasks else "",
                        "ourCaseCostPerUnit": round(case_cost, 4), "ourFullCostPerUnit": round(full_cost, 4)})

    market: list[dict] = []
    share = val(cfg["llm"]["inputTokenShare"])
    for r in market_rows(cfg["llm"]["marketRowIds"]):
        market.append({"product": "LLM batch (8B class)", "provider": r["provider"], "offer": r["product"],
                       "priceChfPerUnit": round(blended(r["normalised_value"], share), 4), "region": r["region"],
                       "source": r["source_url"], "confidence": r["confidence"]})
    for r in market_rows(cfg["transcription"]["marketRowIds"]):
        market.append({"product": "Transcription", "provider": r["provider"], "offer": r["product"],
                       "priceChfPerUnit": float(r["normalised_value"]), "region": r["region"],
                       "source": r["source_url"], "confidence": r["confidence"]})
    for m in cfg["researchMonteCarlo"]["marketPerServerHour"]:
        market.append({"product": "Research Monte Carlo", "provider": m["id"], "offer": m["source"],
                       "priceChfPerUnit": m["value"], "region": "CH (Zurich) / EU", "source": m["source"], "confidence": "see source"})

    dest = ROOT / "data/derived/price_comparison.csv"
    with open(dest, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["kind", "product", "unit_or_offer", "hardware_or_provider", "utilisation_or_region",
                    "units_per_server_hour", "case_cost_or_price_chf", "full_cost_chf", "case_cost_per_server_hour",
                    "full_cost_per_server_hour", "source"])
        for r in out:
            w.writerow(["ours", r["product"], r["unit"], r["hardware"], r["utilisation"], r["unitsPerServerHour"],
                        r["ourCaseCostPerUnit"], r["ourFullCostPerUnit"], r["caseCostChfPerServerHour"],
                        r["fullCostChfPerServerHour"], "config/price-comparison.json; config/hardware"])
        for m in market:
            w.writerow(["market", m["product"], m["offer"], m["provider"], m["region"], "", m["priceChfPerUnit"],
                        "", "", "", m["source"]])
    for product in ("LLM batch (8B class)", "Transcription", "Research Monte Carlo"):
        print(f"\n== {product}")
        for r in out:
            if r["product"] == product:
                print(f"  ours {r['hardware']:30s} {r['utilisation']:16s} case {r['ourCaseCostPerUnit']!s:>9} full {r['ourFullCostPerUnit']!s:>9}  ({r['unit']})")
        for m in sorted((m for m in market if m["product"] == product), key=lambda m: m["priceChfPerUnit"]):
            print(f"  mkt  {m['provider'][:30]:30s} {m['region'][:16]:16s} {m['priceChfPerUnit']:>9}")
    print(f"\nwrote {dest.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
