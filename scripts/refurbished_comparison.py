#!/usr/bin/env python3
"""Refurbished vs new hardware per 1 kW server, including wear (ticket T-35).

Prints the Markdown tables of docs/research/refurbished-hardware.md. Inputs, nothing typed in here:
  config/hardware/*.json                       hardware profiles (T-15 format)
  config/hardware/wear/refurbished-wear.json   failure rates, factors, work-per-kWh ratios
  config/reference-house.json                  electricity price, heat-pump efficiency
  data/derived/reference-house/explorer-default.json   sold server-hours (4-server heat-led run)
  data/derived/reference-house-v1/sites/reference-house/hourly.csv   load per hour (start count)

Formulas (per case server = 1 kW, one server-hour = 1 kWh electricity, H = sold hours per year):
  k (wear multiplier)           = k_age (refurbished parts only) x k_temp x k_cycle
  wear CHF per server-year W    = GPU cards x AFR_gpu x k x (card price + repair h x labour)
                                  + hosts or boxes x repairs/yr x k x (repair h x labour + part share x box price)
  cost per sold server-hour C   = electricity + purchase / life / H + ops / H + W / H
  full-cost break-even B        = C - heat value (0.8 kWh x electricity price / heat-pump efficiency)
  break-even per unit of work   = B / E, E = work per kWh relative to the new profile of the class
  work-equivalent income        = E x income of the new reference profile (same buyer price per unit of work)
  Coffin-Manson cycles to fail  N_f(dT) = N_ref x (dT_ref / dT)^n

Usage: python3 scripts/refurbished_comparison.py
"""
import csv
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ORDER = ["cpu-server", "cpu-server-refurbished", "gpu-inference-l4",
         "gpu-inference-l4-refurbished", "gpu-inference-t4-refurbished"]
# Coffin-Manson inputs, documented with sources in refurbished-wear.json ThermalCycleFactor.
N_REF_CYCLES, DT_REF_K, DT_ONOFF_K, EXPONENT = 4535, 100, 60, 1.9


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def v(obj, name):
    return obj[name]["value"]


def is_gpu(p):
    return v(p, "HostElectricKw") > 0


def wear_chf_per_year(p, w, g, k_age, k_temp, k_cycle):
    labour = v(g, "LabourChfPerHour")
    k_env = k_temp * k_cycle
    k_part = (k_age if w["Refurbished"] else 1) * k_env
    server_repair = lambda price: v(g, "ServerRepairHours") * labour + v(g, "ServerRepairPartShareOfUnitPrice") * price
    if is_gpu(p):
        gpu = v(p, "UnitsPerServer") * v(g, "GpuAnnualFailureRate") * k_part * (
            w["GpuPartPriceChf"] + v(g, "GpuRepairHours") * labour)
        host = v(g, "ServerRepairsPerServerYear") * k_env * server_repair(v(p, "HostPurchasePriceChf"))  # host is new
        return gpu + host
    return v(p, "UnitsPerServer") * v(g, "ServerRepairsPerServerYear") * k_part * server_repair(v(p, "UnitPurchasePriceChf"))


def evaluate(p, w, ref_profile, g, price, heat_chf, hours, k_age=None, k_temp=None, life=None):
    k_age = v(g, "AgeFactorRefurbished") if k_age is None else k_age
    k_temp = v(g, "HotWaterTemperatureFactor") if k_temp is None else k_temp
    life = v(p, "LifetimeYears") if life is None else life
    e = v(w, "WorkPerKwhRelativeToReference")
    d = {"dep": v(p, "PurchasePriceChfPerServer") / life / hours,
         "ops": v(p, "OpsChfPerServerYear") / hours,
         "wear": wear_chf_per_year(p, w, g, k_age, k_temp, v(g, "ThermalCycleFactor")) / hours,
         "el": v(p, "ServerElectricKw") * price, "e": e}
    d["cost"] = d["el"] + d["dep"] + d["ops"] + d["wear"]
    d["be"] = d["cost"] - heat_chf
    d["be_work"] = d["be"] / e
    d["spot"] = v(p, "SpotPriceChfPerServerHour")
    d["spot_work"] = e * v(ref_profile, "SpotPriceChfPerServerHour")
    d["home_work"] = e * v(ref_profile, "HomeHostPriceChfPerServerHour")
    return d


def starts_per_server_year(server_count):
    """Whole-server starts if fractional load were realised by switching servers on and off."""
    path = ROOT / "data" / "derived" / "reference-house-v1" / "sites" / "reference-house" / "hourly.csv"
    with open(path, encoding="utf-8") as f:
        load_ = [float(r["actual_load_per_server"]) for r in csv.DictReader(f)]
    active = [math.ceil(server_count * x - 1e-9) for x in load_]
    starts = sum(max(0, b - a) for a, b in zip([active[0]] + active, active))
    return starts / server_count, min(load_)


def main():
    ref = load(ROOT / "config" / "reference-house.json")
    result = load(ROOT / "data" / "derived" / "reference-house" / "explorer-default.json")["Summary"]
    wear = load(ROOT / "config" / "hardware" / "wear" / "refurbished-wear.json")
    g = wear["Global"]
    price = ref["ElectricityPriceChfPerKwh"]["value"]
    heat_chf = 0.8 * price / ref["HeatPumpEfficiency"]["value"]
    hours = result["ServerHours"] / result["ServerCount"]
    profiles = {p["Id"]: p for p in (load(f) for f in (ROOT / "config" / "hardware").glob("*.json")
                                     if not f.name.endswith(".schema.json"))}
    w_by_id = {w["ProfileId"]: w for w in wear["Profiles"]}
    res = {i: evaluate(profiles[i], w_by_id[i], profiles[w_by_id[i]["ReferenceProfileId"]], g, price, heat_chf, hours)
           for i in ORDER}

    print(f"Inputs: electricity {price} CHF/kWh, heat value {heat_chf:.3f} CHF per server-hour, "
          f"sold server-hours per server-year H = {hours:,.1f}\n")
    rows = [
        ("Purchase price per 1 kW server (CHF)", lambda i, d: v(profiles[i], "PurchasePriceChfPerServer"), 0),
        ("Life used for depreciation (years)", lambda i, d: v(profiles[i], "LifetimeYears"), 2),
        ("Depreciation per sold server-hour (CHF)", lambda i, d: d["dep"], 3),
        ("Operations per sold server-hour (CHF)", lambda i, d: d["ops"], 3),
        ("Expected wear (failures) per sold server-hour (CHF)", lambda i, d: d["wear"], 3),
        ("Electricity per server-hour (CHF)", lambda i, d: d["el"], 3),
        ("**Cost per sold server-hour incl. wear (CHF)**", lambda i, d: d["cost"], 3),
        ("**Full-cost break-even, heat used (CHF per server-hour)**", lambda i, d: d["be"], 3),
        ("Work per kWh relative to new (E)", lambda i, d: d["e"], 2),
        ("**Break-even per new-equivalent unit of work (B / E)**", lambda i, d: d["be_work"], 3),
        ("Rental income at spot, per GPU/vCPU-hour (CHF per server-hour)", lambda i, d: d["spot"], 2),
        ("Work-equivalent income at spot (E x new spot)", lambda i, d: d["spot_work"], 2),
        ("Work-equivalent income at home-host price (E x new home-host)", lambda i, d: d["home_work"], 2),
    ]
    print("| Per 1 kW server | " + " | ".join(f"`{i}`" for i in ORDER) + " |")
    print("|---|" + "---:|" * len(ORDER))
    for label, f, digits in rows:
        print(f"| {label} | " + " | ".join(f"{f(i, res[i]):,.{digits}f}" for i in ORDER) + " |")

    print("\nSensitivity of the break-even (CHF per sold server-hour, heat used):\n")
    cases = [("central", {}), ("k_age = 3", {"k_age": 3}), ("k_temp = 2", {"k_temp": 2}),
             ("k_age = 3 and k_temp = 2", {"k_age": 3, "k_temp": 2}), ("refurbished life 2 years", "life2"),
             ("GPU AFR 0.03 instead of 0.09", "afr")]
    print("| Case | " + " | ".join(f"`{i}`" for i in ORDER) + " |")
    print("|---|" + "---:|" * len(ORDER))
    for name, kw in cases:
        cells = []
        for i in ORDER:
            p, w = profiles[i], w_by_id[i]
            args, gg = {}, g
            if kw == "afr":
                gg = dict(g, GpuAnnualFailureRate=dict(g["GpuAnnualFailureRate"], value=0.03))
            elif kw == "life2":
                if w["Refurbished"]:
                    # Recompute the value-weighted life with 2 years for the refurbished part.
                    used = v(p, "PurchasePriceChfPerServer") - v(p, "HostPurchasePriceChf")
                    host = v(p, "HostPurchasePriceChf")
                    args["life"] = v(p, "PurchasePriceChfPerServer") / (used / 2 + host / 6)
            else:
                args = kw
            d = evaluate(p, w, profiles[w["ReferenceProfileId"]], gg, price, heat_chf, hours, **args)
            cells.append(f"{d['be']:.3f}")
        print(f"| {name} | " + " | ".join(cells) + " |")

    per_server, min_load = starts_per_server_year(result["ServerCount"])
    n_f = N_REF_CYCLES * (DT_REF_K / DT_ONOFF_K) ** EXPONENT
    print(f"\nThermal cycling: N_f({DT_ONOFF_K} K) = {N_REF_CYCLES} x ({DT_REF_K}/{DT_ONOFF_K})^{EXPONENT} = {n_f:,.0f} cycles; "
          f"reference run min load per server {min_load:.3f}, whole-server starts {per_server:.2f} per server-year "
          f"-> {per_server / n_f:.4%} of fatigue life per year; 1 start/day -> {365 / n_f:.1%}; hourly -> {8760 / n_f:.0%}")


if __name__ == "__main__":
    main()
