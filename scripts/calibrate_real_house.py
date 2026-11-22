#!/usr/bin/env python3
"""Calibrate a Fleet house to the organisers' real house (measured gas use), T-82.

The real house heats with gas. Its monthly gas meter readings (data/external/housedata, not committed:
personal data of a real household) are turned into useful heat with a boiler efficiency, then the two
house parameters of the case formula are fitted by least squares on the months that have both a
reading and 2025 weather:

    heat_m = design_heating_kw * S_m + hot_water_kwh_per_day * days_m
    S_m    = sum over the month's hours on heating days of max(0, (20 - T) / 25.6)   (case formula per kW)

The result is written as a Fleet house file (two fitted numbers plus the case defaults), which the
Fleet simulation then runs with the unchanged Core heat model. The month table and the fit quality go
to the house file's spec block. No consumption values beyond monthly totals leave this script.

Usage: python3 scripts/calibrate_real_house.py [--efficiency 0.86] [--out config/fleet/houses/real-house.json]
"""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GAS = ROOT / "data/external/housedata/energy/Data_Gas_2021-2026_anonymisiert.csv"
WEATHER = ROOT / "data/raw/open-meteo/basel-2025-hourly.csv"
REFERENCE = ROOT / "config/reference-house.json"
METER = "MESSPUNKT_004"   # main gas meter (the two other meters read about 200 kWh/year each)
YEAR = "2025"             # the only year with weather in the repo
EFFICIENCY_SOURCE = ("Regulation (EU) No 813/2013, Annex II 1(a): seasonal space heating energy efficiency of fuel "
                     "boiler space heaters <= 70 kW shall not fall below 86 % from 26 September 2015 "
                     "(https://www.legislation.gov.uk/eur/2013/813/annex/II/adopted, accessed 2026-11-22). Minimum for new "
                     "boilers; the house's boiler type and age are unknown, so the real efficiency may be lower.")


def param(p, key):
    return p[key]["value"]


def month_basis(p):
    """Per month of 2025: S_m (kWh per kW of design heat) and number of days, with the case formula."""
    rows = []
    with WEATHER.open(encoding="utf-8") as f:
        lines = [l for l in f]
    start = next(i for i, l in enumerate(lines) if l.startswith("time,"))
    reader = csv.DictReader(lines[start:])
    tcol = next(c for c in reader.fieldnames if c.startswith("temperature_2m"))
    for r in reader:
        rows.append((r["time"], float(r[tcol])))
    by_day = defaultdict(list)
    for t, temp in rows:
        by_day[t[:10]].append(temp)
    base, divisor, threshold = param(p, "BaseTemperatureC"), param(p, "FormulaDivisorC"), param(p, "HeatingDayThresholdC")
    s, days = defaultdict(float), defaultdict(int)
    for day, temps in by_day.items():
        if len(temps) != 24:
            raise SystemExit(f"{day}: expected 24 hourly values")
        month = int(day[5:7])
        days[month] += 1
        if sum(temps) / 24 < threshold:  # case heating-day rule
            s[month] += sum(max(0.0, (base - x) / divisor) for x in temps)
    return s, days


def gas_months():
    gas = defaultdict(float)
    with GAS.open(encoding="utf-8") as f:
        for r in csv.reader(f, delimiter=";"):
            if len(r) > 11 and r[0] == METER and r[8] == "KWH" and r[1].startswith(YEAR):
                gas[int(r[1][4:6])] += float(r[11])  # column "Verbrauch Medium Einfachtarif (kWh/m3)"
    return gas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--efficiency", type=float, default=0.86)
    ap.add_argument("--out", default="config/fleet/houses/real-house.json")
    a = ap.parse_args()
    p = json.loads(REFERENCE.read_text(encoding="utf-8"))
    s, days = month_basis(p)
    gas = gas_months()
    months = [m for m in range(1, 13) if gas.get(m, 0) > 0]
    heat = {m: gas[m] * a.efficiency for m in months}
    # Least squares for heat = d*S + w*days (two unknowns, normal equations).
    sxx = sum(s[m] ** 2 for m in months); sxy = sum(s[m] * days[m] for m in months); syy = sum(days[m] ** 2 for m in months)
    bx = sum(s[m] * heat[m] for m in months); by = sum(days[m] * heat[m] for m in months)
    det = sxx * syy - sxy ** 2
    d = (bx * syy - by * sxy) / det
    w = (sxx * by - sxy * bx) / det
    table, sse, sst = [], 0.0, 0.0
    mean = sum(heat.values()) / len(months)
    for m in range(1, 13):
        model = d * s[m] + w * days[m]
        row = {"month": m, "gas_kwh": round(gas.get(m, 0.0), 1), "heat_kwh": round(heat.get(m, 0.0), 1) if m in heat else None,
               "model_kwh": round(model, 1)}
        if m in heat:
            sse += (heat[m] - model) ** 2; sst += (heat[m] - mean) ** 2
            row["error_pct"] = round(100 * (model - heat[m]) / heat[m], 1)
        table.append(row)
    r2 = 1 - sse / sst
    year_model = d * sum(s.values()) + w * sum(days.values())
    print(f"months fitted: {months}; design_heating_kw = {d:.3f}, hot_water_kwh_per_day = {w:.3f}, R^2 = {r2:.4f}")
    print(f"modelled 2025 heat demand: {year_model:.0f} kWh (case reference house: 31,231 kWh)")
    for r in table:
        print(r)

    house = {
        "house_id": "real-house", "lat_deg": 47.56, "lon_deg": 7.59, "cell_id": "cap-bs", "bfs_municipality_nr": 2701,
        "canton": "BS", "weather_ref": "data/raw/open-meteo/basel-2025-hourly.csv",
        "design_heating_kw": round(d, 3), "formula_divisor_c": param(p, "FormulaDivisorC"),
        "hot_water_kwh_per_day": round(w, 3), "tank_kwh": param(p, "TankCapacityKwh"), "tank_initial_kwh": 0,
        "electricity_price_chf_per_kwh": param(p, "ElectricityPriceChfPerKwh"), "server_count": None,
    }
    spec = {
        "description": ("T-82 variant (not a scored figure): the organisers' real house, calibrated to its measured gas use. "
                        f"Generated by scripts/calibrate_real_house.py (efficiency {a.efficiency})."),
        "sources": {
            "measured_gas": f"Hackathon organisers, Housedata (anonymised), meter {METER}, monthly kWh {YEAR}; local copy "
                            "data/external/housedata (not committed), see data/raw/housedata/SOURCE.md",
            "boiler_efficiency": EFFICIENCY_SOURCE,
            "method": "least squares on months with a reading: heat = design_heating_kw x S_m + hot_water_kwh_per_day x days_m, "
                      "S_m from the case formula on Basel 2025 weather (config/reference-house.json constants)",
            "design_heating_kw": "fitted (see method)",
            "hot_water_kwh_per_day": ("fitted (see method); includes all summer gas use (hot water and boiler/circulation losses "
                                      "cannot be separated from monthly data)"),
            "position_weather_price": "assumed Basel: the address is anonymised; case weather, cell cap-bs and IWB H4 price as the reference house",
            "tank": "35 kWh lossless, case value (the house's storage is unknown)",
        },
        "fit": {"months": months, "efficiency": a.efficiency, "r_squared": round(r2, 4),
                "modelled_year_heat_kwh": round(year_model, 1), "table": table},
    }
    out = ROOT / a.out
    out.write_text(json.dumps({"spec": spec, "seed": None, "houses": [house]}, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"-> {a.out}")


if __name__ == "__main__":
    main()
