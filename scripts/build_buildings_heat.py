"""Building sizes and heat demand per municipality and per 10 km grid cell (T-51).

    python3 scripts/build_buildings_heat.py --download   # fetch the two sources into data/external/ (~1 GB)
    python3 scripts/build_buildings_heat.py              # build data/derived/buildings/*.csv

Sources (see data/raw/buildings/SOURCE.md):
- GWR, federal register of buildings and dwellings (BFS), full CH export ch.zip: one row per building
  with footprint GAREA, floors GASTW, dwellings GANZWHG, energy reference area GEBF, period GBAUP,
  class GKLAS, heat generator and energy source.
- BFE heat demand map "Wärme-/Kältenachfrage Wohnen und DL": heat demand per hectare in MWh/a,
  residential (NEEDHOME) and services (NEEDSERVICE), modelled by BFE from the GWR.

Method:
1. Existing (GSTAT 1004) residential buildings (GKAT 1020/1030/1040). Floor area per building =
   GEBF if recorded, else GAREA x GASTW (gross floor area as proxy for the energy reference area,
   assumption A-T51-1 in data/raw/buildings/SOURCE.md).
2. Each BFE hectare's residential demand NEEDHOME is split among the residential buildings whose
   coordinates fall in that hectare, proportional to floor area (buildings without area get the
   hectare's mean area). Gives a modelled heat demand per building in kWh/a.
3. Aggregate per municipality x class (single-family 1110, two-family 1121, multi-family 1122, all)
   and per 10 km grid cell of data/raw/swiss-grid/grid-10km.csv (cell = square around the point).
Standard library only.
"""
import csv
import io
import os
import sqlite3
import statistics
import struct
import sys
import urllib.request
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXT = os.path.join(ROOT, "data", "external")
GWR_ZIP = os.path.join(EXT, "gwr", "ch.zip")
HEAT_GPKG = os.path.join(EXT, "bfe-heat-demand", "hd.gpkg")
GWR_URL = "https://public.madd.bfs.admin.ch/ch.zip"
HEAT_URL = ("https://data.geo.admin.ch/ch.bfe.fernwaerme-nachfrage_wohn_dienstleistungsgebaeude/"
            "fernwaerme-nachfrage_wohn_dienstleistungsgebaeude/fernwaerme-nachfrage_wohn_dienstleistungsgebaeude_2056.gpkg")
GRID_CSV = os.path.join(ROOT, "data", "raw", "swiss-grid", "grid-10km.csv")
OUT_DIR = os.path.join(ROOT, "data", "derived", "buildings")

GSTAT_EXISTING = "1004"
GKAT_RESIDENTIAL = {"1020", "1030", "1040"}
CLASSES = {"1110": "single_family", "1121": "two_family", "1122": "multi_family"}
HECTARE_M = 100
GRID_STEP_M = 10_000
MWH_TO_KWH = 1000
PERIODS = {  # GBAUP code -> bucket (GWR code list, kodes_codes_codici.csv)
    "8011": "before_1946", "8012": "before_1946", "8013": "1946_1980", "8014": "1946_1980",
    "8015": "1946_1980", "8016": "1981_2000", "8017": "1981_2000", "8018": "1981_2000",
    "8019": "1981_2000", "8020": "2001_later", "8021": "2001_later", "8022": "2001_later",
    "8023": "2001_later"}
HEAT_SOURCES = {  # GENH1 code -> bucket
    "7501": "heat_pump", "7510": "heat_pump", "7511": "heat_pump", "7512": "heat_pump", "7513": "heat_pump",
    "7520": "gas", "7530": "oil", "7540": "wood", "7541": "wood", "7542": "wood", "7543": "wood",
    "7560": "electric", "7580": "district", "7581": "district", "7582": "district"}
SOURCE_BUCKETS = ["heat_pump", "gas", "oil", "wood", "electric", "district", "other_unknown"]
PERIOD_BUCKETS = ["before_1946", "1946_1980", "1981_2000", "2001_later", "unknown"]


def download():
    for url, path in ((GWR_URL, GWR_ZIP), (HEAT_URL, HEAT_GPKG)):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        print(f"[download] {url}")
        urllib.request.urlretrieve(url, path)


def num(s):
    try:
        return float(s)
    except ValueError:
        return None


def read_hectares():
    """BFE hectares -> {(e//100, n//100): (home_mwh, service_mwh)}. Centre from the GPKG envelope."""
    con = sqlite3.connect(HEAT_GPKG)
    out = {}
    for geom, home, service in con.execute("SELECT geom, NEEDHOME, NEEDSERVICE FROM HOMEANDSERVICES"):
        flags = geom[3]
        endian = "<" if flags & 1 else ">"
        assert (flags >> 1) & 7 == 1, "expected xy envelope in GPKG header"
        minx, maxx, miny, maxy = struct.unpack(endian + "4d", geom[8:40])
        key = (int((minx + maxx) / 2 // HECTARE_M), int((miny + maxy) / 2 // HECTARE_M))
        h, s = out.get(key, (0.0, 0.0))
        out[key] = (h + (home or 0.0), s + (service or 0.0))
    return out


def read_buildings():
    z = zipfile.ZipFile(GWR_ZIP)
    r = csv.DictReader(io.TextIOWrapper(z.open("gebaeude_batiment_edificio.csv"), encoding="utf-8"), delimiter="\t")
    out = []
    for b in r:
        if b["GSTAT"] != GSTAT_EXISTING or b["GKAT"] not in GKAT_RESIDENTIAL:
            continue
        e, n = num(b["GKODE"]), num(b["GKODN"])
        if e is None or n is None:
            continue
        ebf, footprint, floors = num(b["GEBF"]), num(b["GAREA"]), num(b["GASTW"])
        area = ebf if ebf and ebf > 0 else (footprint * floors if footprint and floors else None)
        out.append({
            "muni": b["GGDENR"], "muni_name": b["GGDENAME"], "canton": b["GDEKT"], "e": e, "n": n,
            "cls": CLASSES.get(b["GKLAS"], "other_residential"), "area": area,
            "footprint": footprint, "floors": floors, "dwellings": num(b["GANZWHG"]) or 0,
            "period": PERIODS.get(b["GBAUP"], "unknown"),
            "source": HEAT_SOURCES.get(b["GENH1"], "other_unknown"),
        })
    return out


def allocate_heat(buildings, hectares):
    by_ha = {}
    for b in buildings:
        by_ha.setdefault((int(b["e"] // HECTARE_M), int(b["n"] // HECTARE_M)), []).append(b)
    unmatched_mwh = 0.0
    for key, (home_mwh, _s) in hectares.items():
        group = by_ha.get(key)
        if not group:
            unmatched_mwh += home_mwh
            continue
        known = [b["area"] for b in group if b["area"]]
        fill = statistics.fmean(known) if known else 1.0
        total = sum(b["area"] or fill for b in group)
        for b in group:
            b["heat_kwh"] = home_mwh * MWH_TO_KWH * (b["area"] or fill) / total
    for b in buildings:
        b.setdefault("heat_kwh", 0.0)
    return unmatched_mwh


def q(values, p):
    if not values:
        return ""
    values = sorted(values)
    return round(values[min(len(values) - 1, int(p * len(values)))], 1)


def summarise(group):
    areas = [b["area"] for b in group if b["area"]]
    heat = [b["heat_kwh"] for b in group if b["heat_kwh"] > 0]
    row = {
        "n_buildings": len(group), "n_dwellings": int(sum(b["dwellings"] for b in group)),
        "floor_area_median_m2": q(areas, 0.5), "floor_area_mean_m2": round(statistics.fmean(areas), 1) if areas else "",
        "footprint_median_m2": q([b["footprint"] for b in group if b["footprint"]], 0.5),
        "floors_median": q([b["floors"] for b in group if b["floors"]], 0.5),
        "heat_demand_total_mwh_per_yr": round(sum(heat) / MWH_TO_KWH, 1),
        "heat_demand_p25_kwh_per_yr": q(heat, 0.25), "heat_demand_median_kwh_per_yr": q(heat, 0.5),
        "heat_demand_p75_kwh_per_yr": q(heat, 0.75),
        "heat_demand_per_area_kwh_per_m2_yr": round(sum(b["heat_kwh"] for b in group if b["area"]) / sum(areas), 1) if areas else "",
    }
    for p in PERIOD_BUCKETS:
        row[f"share_built_{p}"] = round(sum(b["period"] == p for b in group) / len(group), 3)
    for s in SOURCE_BUCKETS:
        row[f"share_heating_{s}"] = round(sum(b["source"] == s for b in group) / len(group), 3)
    return row


def write(path, rows):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)
    print(f"[out] {len(rows)} rows -> {os.path.relpath(path, ROOT)}")


def main():
    if "--download" in sys.argv:
        download()
    hectares = read_hectares()
    buildings = read_buildings()
    unmatched = allocate_heat(buildings, hectares)
    home_total = sum(h for h, _ in hectares.values())
    print(f"[info] {len(buildings)} residential buildings, {len(hectares)} hectares, "
          f"residential heat {home_total / 1e6:.2f} TWh/a, of which {unmatched / home_total:.1%} in hectares "
          "without a GWR residential building (not allocated)")

    by_muni = {}
    for b in buildings:
        by_muni.setdefault(b["muni"], []).append(b)
    rows = []
    for nr in sorted(by_muni, key=int):
        group = by_muni[nr]
        for cls in ["all_residential", *CLASSES.values(), "other_residential"]:
            sub = group if cls == "all_residential" else [b for b in group if b["cls"] == cls]
            if sub:
                rows.append({"bfs_municipality_nr": nr, "municipality": group[0]["muni_name"],
                             "canton": group[0]["canton"], "building_class": cls, **summarise(sub)})
    write(os.path.join(OUT_DIR, "municipality-buildings-heat.csv"), rows)

    # 10 km grid cells: square of GRID_STEP_M around each regular grid point (capital points skipped).
    with open(GRID_CSV, newline="") as f:
        grid = [g for g in csv.DictReader(f) if g["point_id"].startswith("g")]
    half = GRID_STEP_M // 2
    cell_of = {(int(g["e_lv95_m"]), int(g["n_lv95_m"])): g["point_id"] for g in grid}

    def snap(v):
        """Centre of the 10 km cell holding coordinate v (grid centres lie at multiples of 10 km + 5 km)."""
        return int(v // GRID_STEP_M * GRID_STEP_M + half)

    def nearest_cell(e, n):
        """Own cell, else the nearest neighbouring cell inside CH (border cells whose centre is abroad)."""
        ce, cn = snap(e), snap(n)
        if (ce, cn) in cell_of:
            return cell_of[(ce, cn)]
        near = [(ce + de, cn + dn) for de in (-GRID_STEP_M, 0, GRID_STEP_M) for dn in (-GRID_STEP_M, 0, GRID_STEP_M)
                if (ce + de, cn + dn) in cell_of]
        return cell_of[min(near, key=lambda c: (c[0] - e) ** 2 + (c[1] - n) ** 2)] if near else None

    cells = {g["point_id"]: {"home": 0.0, "service": 0.0, "b": []} for g in grid}
    for (he, hn), (home, service) in hectares.items():
        pid = nearest_cell(he * HECTARE_M + 50, hn * HECTARE_M + 50)
        if pid:
            cells[pid]["home"] += home
            cells[pid]["service"] += service
    for b in buildings:
        pid = nearest_cell(b["e"], b["n"])
        if pid:
            cells[pid]["b"].append(b)
    grid_rows = []
    for g in grid:
        c = cells[g["point_id"]]
        sfh = [b["heat_kwh"] for b in c["b"] if b["cls"] == "single_family" and b["heat_kwh"] > 0]
        grid_rows.append({"point_id": g["point_id"], "e_lv95_m": g["e_lv95_m"], "n_lv95_m": g["n_lv95_m"],
                          "heat_demand_residential_mwh_per_yr": round(c["home"], 1),
                          "heat_demand_services_mwh_per_yr": round(c["service"], 1),
                          "n_residential_buildings": len(c["b"]),
                          "n_single_family": sum(b["cls"] == "single_family" for b in c["b"]),
                          "single_family_heat_median_kwh_per_yr": q(sfh, 0.5)})
    write(os.path.join(OUT_DIR, "grid-10km-heat-demand.csv"), grid_rows)
    allocated = sum(r["heat_demand_residential_mwh_per_yr"] for r in grid_rows)
    print(f"[check] grid cells hold {allocated / 1e6:.2f} of {home_total / 1e6:.2f} TWh/a residential heat")


if __name__ == "__main__":
    main()
