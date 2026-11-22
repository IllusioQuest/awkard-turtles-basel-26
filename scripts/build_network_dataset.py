#!/usr/bin/env python3
"""Build the Swiss network test dataset (T-12 step 3, decision 0025 §8).

Writes
  config/network/switzerland-test.json                        site list (every number sourced or synthetic)
  data/derived/network/switzerland-test/<siteId>-demand.csv   hourly demand for "file" sites
  data/derived/network/switzerland-test/site-totals.csv       annual totals per site (for checking)

Standard library only, deterministic (SEED), offline: it reads only committed files
(data/raw/open-meteo/*, data/raw/sites/*, config/reference-house.json).

Demand models (method, not data; see config/network/README.md):
  reference-house-case  the case formula itself ("case-linear"), computed by the C# loader.
  residential-sfh       space: case formula (10 kW design load) on the city's weather;
                        hot water: 4 persons x 50 m2/person x 13.5 kWh/(m2 a) (SIA 2024 via BFE),
                        When2Heat CH SFH monthly and hourly shares.
  residential-mfh       space: case formula with the case's design load per m2 scaled to the
                        building's floor area; hot water: persons x 30 m2 x 19.8 kWh/(m2 a)
                        (SIA 2024 via BFE), When2Heat CH MFH hourly shares, flat over months.
  hotel-spa             95 kWh/(m2 a) heat (SIA 380/1 target, BFE hotel guide), 42 % hot water;
                        space shaped by case-formula degree-hours on the city's weather, hot water
                        by When2Heat CH commercial hot-water profile (proxy).
  indoor-pool           627 MWh/a (Hallenbad Allschwil after renovation, BFE), flat over the year
                        (weakly weather dependent per BFE guide; no open hourly profile).
"""
from __future__ import annotations

import csv
import json
import random
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SEED = 20251121
SCENARIO_ID = "switzerland-test"
OUT_CONFIG = ROOT / "config" / "network" / f"{SCENARIO_ID}.json"
OUT_DIR = ROOT / "data" / "derived" / "network" / SCENARIO_ID
SITE_TYPES = ROOT / "data" / "raw" / "sites" / "site-types.csv"
WHEN2HEAT = ROOT / "data" / "raw" / "sites" / "when2heat" / "when2heat-ch-2022.csv"
REFERENCE = ROOT / "config" / "reference-house.json"
GEOCODING = ROOT / "data" / "raw" / "open-meteo" / "geocoding-2026-11-21"
WEATHER = "data/raw/open-meteo/{slug}-2025-hourly.csv"

BFE_RESIDENTIAL = "https://pubdb.bfe.admin.ch/de/publication/download/9686"
BFE_HOTEL = "https://pubdb.bfe.admin.ch/de/publication/download/1389"
BFE_POOL = "https://pubdb.bfe.admin.ch/de/publication/download/816"
WHEN2HEAT_DOI = "https://doi.org/10.25832/when2heat/2023-07-27"
CASE = "docs/case/challenge.md"
ELCOM = "data/raw/elcom/iwb-basel-h4-2026.json"
GEO_SRC = "Open-Meteo geocoding API, data/raw/open-meteo/geocoding-2026-11-21/{slug}.json (accessed 2026-11-21)"

# City -> canton code (the geocoding response gives the canton name as admin1).
CITIES = [  # slug, canton code, kinds placed there
    ("basel", "BS", ["reference-house-case", "residential-sfh", "residential-mfh", "hotel-spa", "indoor-pool"]),
    ("zurich", "ZH", ["residential-sfh", "residential-mfh", "hotel-spa", "indoor-pool"]),
    ("bern", "BE", ["residential-sfh", "residential-mfh", "hotel-spa", "indoor-pool"]),
    ("geneva", "GE", ["residential-sfh", "residential-mfh", "hotel-spa", "indoor-pool"]),
    ("lausanne", "VD", ["residential-sfh", "residential-mfh", "hotel-spa"]),
    ("lucerne", "LU", ["residential-sfh", "residential-mfh", "hotel-spa"]),
    ("st-gallen", "SG", ["residential-sfh", "residential-mfh", "hotel-spa"]),
    ("lugano", "TI", ["residential-sfh", "residential-mfh", "hotel-spa", "indoor-pool"]),
    ("chur", "GR", ["residential-sfh", "residential-mfh", "hotel-spa"]),
]
KIND_LABEL = {"reference-house-case": "Reference house", "residential-sfh": "Single-family house",
              "residential-mfh": "Apartment building", "hotel-spa": "Hotel with spa", "indoor-pool": "Indoor pool"}


def sourced(value, unit, source):
    return {"value": value, "unit": unit, "source": source}


def synthetic(value, unit, note):
    return {"value": value, "unit": unit, "synthetic": True, "note": note}


def site_type_rows():
    rows = {}
    with SITE_TYPES.open(encoding="utf-8") as f:
        for r in csv.DictReader(f):
            rows[(r["site_type"], r["parameter"])] = r
    return rows


def num_list(text):
    return [float(x) for x in text.split(";")]


def read_weather(slug):
    path = ROOT / WEATHER.format(slug=slug)
    with path.open(encoding="utf-8") as f:
        lines = f.read().splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("time,"))
    out = []
    for line in lines[start + 1:]:
        if line.strip():
            fields = line.split(",")
            out.append((fields[0], float(fields[1])))
    if len(out) != 8760:
        raise ValueError(f"{path}: expected 8760 hours, got {len(out)}")
    return out


def heating_shape(weather, p):
    """Case formula per kW of design load: max(0, (base - T) / divisor) on heating days (mean T < threshold)."""
    base, divisor, threshold = p["BaseTemperatureC"]["value"], p["FormulaDivisorC"]["value"], p["HeatingDayThresholdC"]["value"]
    shape = []
    for d in range(0, len(weather), 24):
        day = weather[d:d + 24]
        heating = sum(t for _, t in day) / 24 < threshold
        shape += [max(0.0, (base - t) / divisor) if heating else 0.0 for _, t in day]
    return shape


def when2heat_column(column):
    """Map a When2Heat CH 2022 column to local 'MM-DDTHH' keys (first value wins on the DST duplicate hour)."""
    values = {}
    with WHEN2HEAT.open(encoding="utf-8") as f:
        reader = csv.reader(f, delimiter=";")
        header = next(reader)
        ts, col = header.index("cet_cest_timestamp"), header.index(column)
        for row in reader:
            key = row[ts][5:13]  # 'MM-DDTHH'
            values.setdefault(key, float(row[col].replace(",", ".")))
    return values


def profile_for(weather, mapping):
    """When2Heat 2022 values aligned to the 2025 time axis by month, day and hour; a missing hour
    (DST spring gap in 2022) takes the previous hour's value."""
    out, last = [], None
    for time, _ in weather:
        key = time[5:13]
        if key == "02-29T00":
            key = "02-28T00"
        last = mapping.get(key, last)
        if last is None:
            raise ValueError(f"No When2Heat value for {key}")
        out.append(last)
    return out


def monthly_hourly_hot_water(weather, annual_kwh, monthly_pct, hourly_pct):
    month_sum, hour_sum = sum(monthly_pct), sum(hourly_pct)
    out = []
    for time, _ in weather:
        y, m = int(time[:4]), int(time[5:7])
        days = (date(y + (m == 12), m % 12 + 1, 1) - date(y, m, 1)).days
        out.append(annual_kwh * monthly_pct[m - 1] / month_sum / days * hourly_pct[int(time[11:13])] / hour_sum)
    return out


def scaled(shape, annual_kwh):
    total = sum(shape)
    return [annual_kwh * x / total for x in shape] if total > 0 else [0.0] * len(shape)


def write_demand(site_id, weather, space, hot):
    path = OUT_DIR / f"{site_id}-demand.csv"
    with path.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["time", "space_heat_kwh", "hot_water_kwh", "temperature_c"])
        for (time, t), s, h in zip(weather, space, hot):
            w.writerow([time, f"{s:.4f}", f"{h:.4f}", f"{t:.1f}"])
    return path.relative_to(ROOT).as_posix()


def main():
    rng = random.Random(SEED)
    p = json.loads(REFERENCE.read_text(encoding="utf-8"))
    st = site_type_rows()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    OUT_CONFIG.parent.mkdir(parents=True, exist_ok=True)

    recover_kw = p["ServerElectricKw"]["value"] * p["HeatRecoveryShare"]["value"]
    price = p["ElectricityPriceChfPerKwh"]["value"]
    cop = p["HeatPumpEfficiency"]["value"]
    design_kw = p["DesignHeatingKw"]["value"]
    tank_per_server = p["TankCapacityKwh"]["value"] / p["ServerCount"]["value"]
    sfh_area = 4 * float(st[("residential-sfh", "floor_area_per_person_m2")]["value"])  # 4 persons (case household)
    design_kw_per_m2 = design_kw / sfh_area

    w2h_com_water = when2heat_column("CH_heat_profile_water_COM")
    sfh_hw_month = num_list(st[("residential-sfh", "hot_water_monthly_share_jan_dec")]["value"])
    sfh_hw_hour = num_list(st[("residential-sfh", "hot_water_hourly_share_h00_h23")]["value"])
    mfh_hw_hour = num_list(st[("residential-mfh", "hot_water_hourly_share_h00_h23")]["value"])

    sites, totals = [], []
    for slug, canton, kinds in CITIES:
        geo = json.loads((GEOCODING / f"{slug}.json").read_text(encoding="utf-8"))["results"][0]
        weather = read_weather(slug)
        shape = heating_shape(weather, p)
        counters = {}
        for kind in kinds:
            counters[kind] = counters.get(kind, 0) + 1
            site_id = f"{slug}-{kind.replace('residential-', '')}-{counters[kind]}"
            note_offset = ("City centre from Open-Meteo geocoding plus a seeded random offset (up to about "
                           "1.5 km); a placeholder position, not a real building.")
            lat = round(geo["latitude"] + rng.uniform(-0.012, 0.012), 5)
            lon = round(geo["longitude"] + rng.uniform(-0.018, 0.018), 5)
            site = {"id": site_id, "name": f"{KIND_LABEL[kind]} {geo['name']}", "kind": kind, "canton": canton,
                    "latitudeDeg": synthetic(lat, "°", note_offset),
                    "longitudeDeg": synthetic(lon, "°", note_offset), "countryCode": "CH"}
            weather_file = WEATHER.format(slug=slug)
            sizing_note = ("Servers = round(mean hourly heat demand / 0.8 kWh recoverable per server-hour): "
                           "sized for the year-round average, peaks from the backup heater (business model "
                           "lever A, docs/tickets/T-34). Scenario assumption.")
            if kind == "reference-house-case":
                site.update({"name": "Reference house Basel (case)",
                             "latitudeDeg": sourced(47.56, "°", CASE), "longitudeDeg": sourced(7.59, "°", CASE)})
                servers = sourced(p["ServerCount"]["value"], "servers", p["ServerCount"]["source"])
                tank = sourced(p["TankCapacityKwh"]["value"], "kWh", p["TankCapacityKwh"]["source"])
                demand = {"model": "case-linear", "weatherFile": "data/raw/open-meteo/basel-2025-hourly.csv",
                          "parameters": "config/reference-house.json"}
                space = [design_kw * x for x in shape]
                hot = [p["HotWaterKwhPerDay"]["value"] / 24] * len(weather)
            else:
                if kind == "residential-sfh":
                    space = [design_kw * x for x in shape]
                    annual_hw = sfh_area * float(st[("residential-sfh", "hot_water_kwh_per_m2_year")]["value"])
                    hot = monthly_hourly_hot_water(weather, annual_hw, sfh_hw_month, sfh_hw_hour)
                elif kind == "residential-mfh":
                    persons = rng.randint(16, 40)
                    area = persons * float(st[("residential-mfh", "floor_area_per_person_m2")]["value"])
                    space = [design_kw_per_m2 * area * x for x in shape]
                    annual_hw = area * float(st[("residential-mfh", "hot_water_kwh_per_m2_year")]["value"])
                    hot = monthly_hourly_hot_water(weather, annual_hw, [1.0] * 12, mfh_hw_hour)
                    site["persons"] = synthetic(persons, "persons", "Seeded random building size, 16-40 residents.")
                elif kind == "hotel-spa":
                    area = rng.randrange(1500, 4001, 100)
                    annual = area * float(st[("hotel-spa", "heat_kwh_per_m2_year_target_new")]["value"])
                    hw_share = 0.42  # BFE hotel guide: new buildings 42 % of heat for hot water
                    space = scaled(shape, annual * (1 - hw_share))
                    hot = scaled(profile_for(weather, w2h_com_water), annual * hw_share)
                    site["floorAreaM2"] = synthetic(area, "m2", "Seeded random hotel size, 1,500-4,000 m2.")
                else:  # indoor-pool
                    annual = float(st[("indoor-pool", "example_heat_mwh_per_year")]["value"]) * 1000
                    space = [annual / len(weather)] * len(weather)
                    hot = [0.0] * len(weather)
                mean_kwh = (sum(space) + sum(hot)) / len(weather)
                servers = synthetic(max(1, round(mean_kwh / recover_kw)), "servers", sizing_note)
                tank = synthetic(round(tank_per_server * servers["value"], 1), "kWh",
                                 "Same storage per server as the reference house (35 kWh / 4 servers). Scenario assumption.")
                demand = {"model": "file", "file": write_demand(site_id, weather, space, hot),
                          "weatherFile": weather_file, "method": kind}
            elec = (sourced(price, "CHF/kWh", p["ElectricityPriceChfPerKwh"]["source"]) if slug == "basel"
                    else synthetic(price, "CHF/kWh", "Basel IWB H4 2026 price used as a proxy; local tariffs not retrieved."))
            site.update({
                "serverCount": servers,
                "serverElectricKw": sourced(p["ServerElectricKw"]["value"], "kW/server", p["ServerElectricKw"]["source"]),
                "heatRecoveryShare": sourced(p["HeatRecoveryShare"]["value"], "fraction", p["HeatRecoveryShare"]["source"]),
                "tankCapacityKwh": tank,
                "minimumLoadShare": sourced(p["MinimumLoadShare"]["value"], "fraction/server", p["MinimumLoadShare"]["source"]),
                "electricityPriceChfPerKwh": elec,
                "heatValueChfPerKwh": {"value": round(price / cop, 6), "unit": "CHF/kWh",
                                       "source": f"{CASE}: electricity price / heat-pump efficiency {cop}"},
                "demand": demand,
            })
            if kind.startswith("residential") or kind == "reference-house-case":
                site["type1"] = {
                    "shareOfHeatLedServerHours": synthetic(0.3, "fraction", "Decision 0025 §6: central value of the 10/30/50 % sweep; no sourced Type 1 demand (decision 0020)."),
                    "deadlineHours": synthetic(24, "hours", "Decision 0020/0025 scenario: 24 h deadline."),
                    "serverHoursPerJob": synthetic(0.92, "server-hours/job", "Job = 1,000 LLM documents (synthetic batch size) x 0.92 Wh per document measured in T-33 (docs/jobs/llm-summary.md), at 1 kW per server."),
                }
            site["placement"] = {"mode": "country-allowlist", "allowedCountryCodes": ["CH"],
                                 "source": "Synthetic test scenario: customers require computing in Switzerland (decision 0020 value proposition); not a real contract."}
            sites.append(site)
            totals.append((site_id, kind, slug, servers["value"], sum(space), sum(hot),
                           max(s + h for s, h in zip(space, hot))))

    config = {
        "id": SCENARIO_ID,
        "description": ("Synthetic Swiss test network for the network scheduler and the live map (decision 0025 §8). "
                        "Weather, profiles and specific values are sourced; positions, building sizes, server counts, "
                        "tanks, non-Basel prices and Type 1 volumes are synthetic and marked per field. "
                        "Generated by scripts/build_network_dataset.py (seed %d). Not a market study." % SEED),
        "timezone": "Europe/Zurich", "year": 2025, "seed": SEED, "sites": sites,
    }
    OUT_CONFIG.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    with (OUT_DIR / "site-totals.csv").open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["site_id", "kind", "city", "servers", "space_heat_kwh", "hot_water_kwh", "peak_hour_kwh"])
        for t in totals:
            w.writerow([t[0], t[1], t[2], t[3], f"{t[4]:.0f}", f"{t[5]:.0f}", f"{t[6]:.2f}"])
    print(f"Wrote {len(sites)} sites to {OUT_CONFIG.relative_to(ROOT)} and demand files to {OUT_DIR.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
