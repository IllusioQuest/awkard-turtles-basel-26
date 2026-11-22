#!/usr/bin/env python3
"""Compare the hardware profiles in config/hardware/ (ticket T-15).

Prints the Markdown comparison table used in docs/research/hardware.md. Every input is read from
the profile files, config/reference-house.json and the reproducible reference-house result
data/derived/reference-house/explorer-default.json (4 servers, heat-led); nothing is typed in here.

Formulas (per case server = 1 kW electric load, one server-hour = 1 kWh electricity):
  sold server-hours per server-year  H = ServerHours / ServerCount        (explorer-default)
  depreciation per sold server-hour   = PurchasePrice / Lifetime / H      (straight line, no residual)
  operations per sold server-hour     = OpsPerServerYear / H
  electricity per server-hour         = ServerElectricKw x ElectricityPrice
  cost per sold server-hour           = electricity + depreciation + operations
  heat value per server-hour (used)   = ServerElectricKw x HeatRecoveryShare x ElectricityPrice / HeatPumpEfficiency
  net cost per sold server-hour       = cost - heat value                 (winter: heat used)
  heat per CHF                        = recoverable heat kWh per server-hour / cost per server-hour
  audio hours per kWh                 = audio hours per server-hour / ServerElectricKw
  full-cost break-even                = net cost per sold server-hour

Usage: python3 scripts/hardware_comparison.py
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def v(profile, name):
    return profile[name]["value"]


def fmt(x, digits=2):
    return "TODO" if x is None else f"{x:,.{digits}f}"


def main():
    ref = load(ROOT / "config" / "reference-house.json")
    result = load(ROOT / "data" / "derived" / "reference-house" / "explorer-default.json")["Summary"]
    price = ref["ElectricityPriceChfPerKwh"]["value"]
    efficiency = ref["HeatPumpEfficiency"]["value"]
    hours_sold = result["ServerHours"] / result["ServerCount"]
    hours_installed = result["Hours"]

    profiles = [load(p) for p in sorted((ROOT / "config" / "hardware").glob("*.json"))
                if not p.name.endswith(".schema.json")]

    print(f"Inputs: electricity {price} CHF/kWh, heat-pump efficiency {efficiency}, "
          f"sold server-hours per server-year {hours_sold:,.1f} of {hours_installed} "
          f"(explorer-default, utilisation {result['Utilisation']:.3f})\n")
    rows = [
        ("Purchase price per 1 kW server (CHF)", lambda p, d: v(p, "PurchasePriceChfPerServer"), 0),
        ("Depreciation per sold server-hour (CHF)", lambda p, d: d["dep"], 3),
        ("Operations per sold server-hour (CHF)", lambda p, d: d["ops"], 3),
        ("Electricity per server-hour (CHF)", lambda p, d: d["el"], 3),
        ("**Cost per sold server-hour (CHF)**", lambda p, d: d["cost"], 3),
        ("Heat value if used (CHF per server-hour)", lambda p, d: d["heat_chf"], 3),
        ("**Full-cost break-even, heat used (CHF per server-hour)**", lambda p, d: d["cost"] - d["heat_chf"], 3),
        ("Recoverable heat per CHF of cost (kWh/CHF)", lambda p, d: d["heat_kwh"] / d["cost"], 2),
        ("Transcription throughput (audio h per kWh)", lambda p, d: d["audio_per_kwh"], 0),
        ("Income at spot (CHF per server-hour)", lambda p, d: v(p, "SpotPriceChfPerServerHour"), 2),
        ("Income at home-host marketplace price (CHF per server-hour)", lambda p, d: v(p, "HomeHostPriceChfPerServerHour"), 2),
        ("Income at on-demand price (CHF per server-hour)", lambda p, d: v(p, "OnDemandPriceChfPerServerHour"), 2),
        ("Idle power (kW per server)", lambda p, d: v(p, "IdleElectricKw"), 3),
    ]
    derived = []
    for p in profiles:
        kw = v(p, "ServerElectricKw")
        audio = v(p, "TranscriptionAudioHoursPerServerHour")
        d = {
            "dep": v(p, "PurchasePriceChfPerServer") / v(p, "LifetimeYears") / hours_sold,
            "ops": (v(p, "OpsChfPerServerYear") or 0) / hours_sold,
            "el": kw * price,
            "heat_kwh": kw * v(p, "HeatRecoveryShare"),
        }
        d["heat_chf"] = d["heat_kwh"] * price / efficiency
        d["cost"] = d["el"] + d["dep"] + d["ops"]
        d["audio_per_kwh"] = None if audio is None else audio / kw
        derived.append(d)

    print("| Per 1 kW server | " + " | ".join(f"`{p['Id']}`" for p in profiles) + " |")
    print("|---|" + "---:|" * len(profiles))
    for label, f, digits in rows:
        print(f"| {label} | " + " | ".join(fmt(f(p, d), digits) for p, d in zip(profiles, derived)) + " |")
    print("\nTranscription models: " + "; ".join(f"`{p['Id']}` {p['TranscriptionModel']}" for p in profiles))
    return 0


if __name__ == "__main__":
    sys.exit(main())
