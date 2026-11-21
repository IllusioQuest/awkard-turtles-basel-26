#!/usr/bin/env python3
"""Build a fleet house file with house clusters in chosen regions plus a spread over Switzerland (T-67).

    python3 scripts/make_climate_fleet.py config/fleet/regions/alps-border-50.json            # write house file
    python3 scripts/make_climate_fleet.py config/fleet/regions/alps-border-50.json --list     # print candidate cells

Per grid cell (data/raw/swiss-grid/weather-2025-hourly.csv.gz) the script computes the daily mean
temperature, then winter_c = mean over Dec/Jan/Feb 2025 days, summer_c = mean over Jun/Jul/Aug days,
swing_c = summer_c - winter_c, and heating_days_jja = summer days with daily mean < 12 °C (the case's
heating limit, config/reference-house.json). These are printed and stored per house; they do not
change the simulation, which reads the cell's hourly weather itself.

Spec (example: config/fleet/regions/alps-border-50.json; every value has an entry in "sources"):
  populated {min_single_family, max_model_elevation_m}: which cells count as places with houses.
  regions [{prefix, count?, min_spacing_km, populated?, select}]: one cluster each. select is one of
    {"near": {lat_deg, lon_deg, radius_km}}                    cells within radius, closest first;
    {"along": {points: [[lat, lon], ...], max_distance_km}}   cells near a polyline, in order along it;
    {"rank": "swing_desc" | "swing_asc", filter {cantons?, lat_min?, lat_max?, lon_min?, lon_max?, elevation_min_m?}}.
    Greedy in that order, keeping min_spacing_km to the cluster's houses already picked. Without
    count every cell that fits is taken. populated overrides the global filter for this cluster.
  between {prefix, total, min_spacing_km}: fills the fleet up to total houses, greedy by single-family
    count (descending), keeping min_spacing_km to every house already picked.
  house {<site fields shared by all houses>}, output {file, description}.
One house per picked cell, placed at the cell centre. Weather and forecasts come from that cell. Note:
a cell's weather is for its model elevation (grid-10km.csv model_elevation_m), which in the Alps can be
far above the village in the valley.
Electricity price: ElCom 2026 H4 of the cell's municipality (data/derived/elcom/price-per-municipality-2026.csv);
cells without a price are skipped. Deterministic: no random numbers.
"""
import csv
import gzip
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GRID = ROOT / "data/raw/swiss-grid/grid-10km.csv"
WEATHER = ROOT / "data/raw/swiss-grid/weather-2025-hourly.csv.gz"
HEAT = ROOT / "data/derived/buildings/grid-10km-heat-demand.csv"
PRICES = ROOT / "data/derived/elcom/price-per-municipality-2026.csv"
PRICE_CATEGORY = "H4"
WINTER_MONTHS, SUMMER_MONTHS = ("12", "01", "02"), ("06", "07", "08")
EARTH_MEAN_RADIUS_KM = 6371.0088  # IUGG mean radius, as in HouseGenerator.cs
FIELDS = ["house_id", "lat_deg", "lon_deg", "cell_id", "bfs_municipality_nr", "canton", "weather_ref",
          "design_heating_kw", "formula_divisor_c", "hot_water_kwh_per_day", "tank_kwh", "tank_initial_kwh",
          "electricity_price_chf_per_kwh", "server_count"]


def distance_km(a, b):
    """Great-circle distance (haversine, spherical Earth) between two {lat, lon}."""
    p1, p2 = math.radians(a["lat"]), math.radians(b["lat"])
    dp, dl = p2 - p1, math.radians(b["lon"] - a["lon"])
    h = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * EARTH_MEAN_RADIUS_KM * math.asin(math.sqrt(h))


def along_polyline(c, points):
    """(distance to the polyline in km, position along it in km), on a local flat projection (spans of tens of km)."""
    km_lat = EARTH_MEAN_RADIUS_KM * math.pi / 180
    km_lon = km_lat * math.cos(math.radians(points[0][0]))
    xy = [((lon - points[0][1]) * km_lon, (lat - points[0][0]) * km_lat) for lat, lon in points]
    px, py = (c["lon"] - points[0][1]) * km_lon, (c["lat"] - points[0][0]) * km_lat
    best, start = (math.inf, 0.0), 0.0
    for (ax, ay), (bx, by) in zip(xy, xy[1:]):
        seg = math.hypot(bx - ax, by - ay)
        t = max(0.0, min(1.0, ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / (seg * seg)))
        d = math.hypot(px - (ax + t * (bx - ax)), py - (ay + t * (by - ay)))
        if d < best[0]:
            best = (d, start + t * seg)
        start += seg
    return best


def load_cells(heating_limit_c):
    grid = {r["point_id"]: r for r in csv.DictReader(GRID.open(encoding="utf-8"))}
    sf = {r["point_id"]: float(r["n_single_family"]) for r in csv.DictReader(HEAT.open(encoding="utf-8"))}
    prices = {int(r["bfs_municipality_nr"]): float(r["price_chf_per_kwh"])
              for r in csv.DictReader(PRICES.open(encoding="utf-8")) if r["category"] == PRICE_CATEGORY}
    days = defaultdict(lambda: defaultdict(list))
    with gzip.open(WEATHER, "rt", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            days[r["point_id"]][r["time_local"][:10]].append(float(r["temperature_2m"]))
    cells = []
    for pid, d in days.items():
        g = grid[pid]
        means = {day: sum(v) / len(v) for day, v in d.items()}
        winter = [t for day, t in means.items() if day[5:7] in WINTER_MONTHS]
        summer = [t for day, t in means.items() if day[5:7] in SUMMER_MONTHS]
        bfs = int(g["bfs_municipality_nr"])
        c = {"id": pid, "lat": float(g["lat_deg"]), "lon": float(g["lon_deg"]), "canton": g["canton"],
             "municipality": g["municipality"], "bfs": bfs, "elevation_m": float(g["model_elevation_m"]),
             "single_family": sf.get(pid, 0.0), "price": prices.get(bfs),
             "winter_c": sum(winter) / len(winter), "summer_c": sum(summer) / len(summer),
             "heating_days_jja": sum(1 for t in summer if t < heating_limit_c)}
        c["swing_c"] = c["summer_c"] - c["winter_c"]
        cells.append(c)
    return cells


def is_populated(c, pop):
    return (c["price"] is not None and c["single_family"] >= pop["min_single_family"]
            and c["elevation_m"] <= pop.get("max_model_elevation_m", math.inf))


def matches(c, flt):
    return ((not flt.get("cantons") or c["canton"] in flt["cantons"])
            and flt.get("lat_min", -90) <= c["lat"] <= flt.get("lat_max", 90)
            and flt.get("lon_min", -180) <= c["lon"] <= flt.get("lon_max", 180)
            and c["elevation_m"] >= flt.get("elevation_min_m", -math.inf))


def candidates(cells, sel):
    """Cells of one cluster in pick order."""
    if "near" in sel:
        n = sel["near"]
        centre = {"lat": n["lat_deg"], "lon": n["lon_deg"]}
        return sorted((c for c in cells if distance_km(c, centre) <= n["radius_km"]),
                      key=lambda c: (distance_km(c, centre), c["id"]))
    if "along" in sel:
        a = sel["along"]
        near = [(along_polyline(c, a["points"]), c) for c in cells]
        return [c for (d, pos), c in sorted(near, key=lambda x: (x[0][1], x[1]["id"])) if d <= a["max_distance_km"]]
    if "rank" in sel:
        sign = 1 if sel["rank"] == "swing_asc" else -1
        return sorted((c for c in cells if matches(c, sel.get("filter", {}))), key=lambda c: (sign * c["swing_c"], c["id"]))
    raise SystemExit(f"unknown select {sel}")


def pick(cands, count, spacing_km, keep_away_from):
    chosen = []
    for c in cands:
        if count is not None and len(chosen) == count:
            break
        if all(distance_km(c, o) >= spacing_km for o in chosen + keep_away_from):
            chosen.append(c)
    if count is not None and len(chosen) < count:
        raise SystemExit(f"only {len(chosen)} of {count} cells fit (spacing {spacing_km} km)")
    return chosen


def describe(c):
    return (f'{c["id"]} {c["canton"]} {c["municipality"][:18]:18} model {c["elevation_m"]:4.0f} m  winter {c["winter_c"]:4.1f} °C  '
            f'summer {c["summer_c"]:4.1f} °C  JJA heating days {c["heating_days_jja"]:2d}')


def main(spec_path, list_only):
    spec = json.loads(Path(spec_path).read_text(encoding="utf-8"))
    cells = load_cells(spec["heating_limit_c"])
    picked = []
    for reg in spec["regions"]:
        pop = reg.get("populated", spec["populated"])
        cands = candidates([c for c in cells if is_populated(c, pop)], reg["select"])
        if list_only:
            print(f'\n{reg["prefix"]}: {len(cands)} candidates')
            for c in cands:
                print("  " + describe(c))
            continue
        for c in pick(cands, reg.get("count"), reg["min_spacing_km"], []):
            picked.append((reg["prefix"], c))
    if list_only:
        return
    b = spec["between"]
    taken = {c["id"] for _, c in picked}
    cands = sorted((c for c in cells if is_populated(c, spec["populated"]) and c["id"] not in taken),
                   key=lambda c: (-c["single_family"], c["id"]))
    for c in pick(cands, b["total"] - len(picked), b["min_spacing_km"], [c for _, c in picked]):
        picked.append((b["prefix"], c))

    houses, n = [], defaultdict(int)
    for prefix, c in picked:
        n[prefix] += 1
        h = {**spec["house"], "house_id": f"{prefix}{n[prefix]:02d}", "lat_deg": c["lat"], "lon_deg": c["lon"],
             "cell_id": c["id"], "weather_ref": c["id"], "bfs_municipality_nr": c["bfs"], "canton": c["canton"],
             "electricity_price_chf_per_kwh": c["price"]}
        houses.append({f: h[f] for f in FIELDS})
    for prefix in n:
        group = [c for p, c in picked if p == prefix]
        mean = lambda k: sum(c[k] for c in group) / len(group)
        print(f'{prefix}: {len(group)} houses, winter {mean("winter_c"):.1f} °C, summer {mean("summer_c"):.1f} °C, '
              f'JJA heating days {mean("heating_days_jja"):.1f}')
    out = spec["output"]
    doc = {"spec": {"description": out["description"] + f" Generated by scripts/make_climate_fleet.py from {spec_path}.",
                    "sources": spec["sources"],
                    "cells": {h["house_id"]: describe(c) for h, (_, c) in zip(houses, picked)}},
           "seed": None, "houses": houses}
    (ROOT / out["file"]).write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f'{len(houses)} houses -> {out["file"]}')


if __name__ == "__main__":
    if len(sys.argv) not in (2, 3) or (len(sys.argv) == 3 and sys.argv[2] != "--list"):
        raise SystemExit(__doc__)
    main(sys.argv[1], len(sys.argv) == 3)
