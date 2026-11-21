"""Swiss 10 km weather grid (T-47): grid definition, hourly weather 2025, archived forecasts (lead 0-3 days).

Steps (each writes into data/raw/swiss-grid/, see its SOURCE.md):

    python3 scripts/fetch_swiss_grid.py grid       # LV95 10 km grid, keep points inside CH (geo.admin.ch)
    python3 scripts/fetch_swiss_grid.py weather    # Open-Meteo archive 2025, hourly temperature_2m + shortwave
    python3 scripts/fetch_swiss_grid.py forecast   # Open-Meteo Previous Runs API 2025, lead 1-3 days
    python3 scripts/fetch_swiss_grid.py check      # validate the files (rows, hours, gaps)

Standard library only. Weather/forecast steps resume: finished batches are cached in
data/external/swiss-grid-cache/ and skipped on rerun (useful when the Open-Meteo rate limit hits).
"""
import argparse
import csv
import gzip
import io
import json
import math
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "data", "raw", "swiss-grid")
CACHE_DIR = os.path.join(ROOT, "data", "external", "swiss-grid-cache")
GRID_CSV = os.path.join(OUT_DIR, "grid-10km.csv")
WEATHER_CSV = os.path.join(OUT_DIR, "weather-2025-hourly.csv.gz")
FORECAST_CSV = os.path.join(OUT_DIR, "forecast-2025-hourly-lead0-3.csv.gz")

# Grid: cell centres every GRID_STEP_M metres in Swiss LV95 (EPSG:2056), bounding box of Switzerland.
GRID_STEP_M = 10_000
GRID_E_RANGE_M = (2_485_000, 2_835_000)  # CH spans E 2'485'000-2'834'000
GRID_N_RANGE_M = (1_075_000, 1_295_000)  # CH spans N 1'075'000-1'296'000
GEOADMIN_IDENTIFY = "https://api3.geo.admin.ch/rest/services/api/MapServer/identify"
LAYER_COUNTRY = "ch.swisstopo.swissboundaries3d-land-flaeche.fill"
LAYER_MUNICIPALITY = "ch.swisstopo.swissboundaries3d-gemeinde-flaeche.fill"

START_DATE, END_DATE = "2025-01-01", "2025-12-31"
TIMEZONE = "Europe/Zurich"
ARCHIVE_API = "https://archive-api.open-meteo.com/v1/archive"
PREVIOUS_RUNS_API = "https://previous-runs-api.open-meteo.com/v1/forecast"
WEATHER_VARS = ["temperature_2m", "shortwave_radiation"]
# temperature_2m here = the forecast model at lead 0 (its own "actual"), the consistent reference for errors
FORECAST_VARS = ["temperature_2m", "temperature_2m_previous_day1", "temperature_2m_previous_day2", "temperature_2m_previous_day3"]
BATCH_SIZE = 20          # locations per Open-Meteo request (cost ~5.2 per location-year, measured 2026-11-21)
PAUSE_S = 15.0           # between requests: keeps cost under the 600-per-minute limit

# Extra points: the 26 cantonal capitals (BFS municipality number, canton). A 10 km grid misses small
# cantons (Basel-Stadt has no grid point). Basel uses the case's reference coordinates instead.
GEOADMIN_SEARCH = "https://api3.geo.admin.ch/rest/services/api/SearchServer"
CAPITALS = [("Zürich", "ZH"), ("Bern", "BE"), ("Luzern", "LU"), ("Altdorf", "UR"), ("Schwyz", "SZ"),
            ("Sarnen", "OW"), ("Stans", "NW"), ("Glarus", "GL"), ("Zug", "ZG"), ("Fribourg", "FR"),
            ("Solothurn", "SO"), ("Basel", "BS"), ("Liestal", "BL"), ("Schaffhausen", "SH"),
            ("Herisau", "AR"), ("Appenzell", "AI"), ("St. Gallen", "SG"), ("Chur", "GR"), ("Aarau", "AG"),
            ("Frauenfeld", "TG"), ("Bellinzona", "TI"), ("Lausanne", "VD"), ("Sion", "VS"),
            ("Neuchâtel", "NE"), ("Genève", "GE"), ("Delémont", "JU")]
REFERENCE_BASEL_LAT_LON = ("47.56", "7.59")  # docs/case/challenge.md, reference house
HOURS_2025 = 8760


def lv95_to_wgs84(e_m, n_m):
    """Swiss LV95 -> WGS84 (deg). swisstopo approximate formulas, accuracy ~1 m.
    Source: swisstopo, "Approximate formulas for the transformation between Swiss projection
    coordinates and WGS84" (https://www.swisstopo.admin.ch/en/transformation-calculation-services)."""
    y = (e_m - 2_600_000) / 1e6
    x = (n_m - 1_200_000) / 1e6
    lon = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * x * x - 0.0436 * y ** 3
    lat = 16.9023892 + 3.238272 * x - 0.270978 * y * y - 0.002528 * x * x - 0.0447 * y * y * x - 0.0140 * x ** 3
    return lat * 100 / 36, lon * 100 / 36


def http_get(url, retries=5):
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                return r.read()
        except urllib.error.HTTPError as err:
            body = err.read()[:300].decode("utf-8", "replace")
            if err.code == 429 or "limit" in body.lower():
                wait = 60 * (attempt + 1)
                print(f"  rate limited ({err.code}: {body}); waiting {wait}s", file=sys.stderr)
                time.sleep(wait)
                continue
            raise RuntimeError(f"HTTP {err.code} for {url[:200]}: {body}")
        except (urllib.error.URLError, TimeoutError) as err:
            print(f"  network error {err}; retry", file=sys.stderr)
            time.sleep(10 * (attempt + 1))
    raise RuntimeError(f"giving up on {url[:200]}")


def identify(point):
    e, n = point
    q = urllib.parse.urlencode({
        "geometryType": "esriGeometryPoint", "geometry": f"{e},{n}", "sr": "2056",
        "layers": f"all:{LAYER_COUNTRY},{LAYER_MUNICIPALITY}", "tolerance": "0",
        "returnGeometry": "false", "lang": "en",
    })
    res = json.loads(http_get(f"{GEOADMIN_IDENTIFY}?{q}"))["results"]
    country = next((r["attributes"].get("bez") for r in res
                    if r["layerBodId"] == LAYER_COUNTRY and r["attributes"].get("is_current_jahr", True)), None)
    muni = [r["attributes"] for r in res if r["layerBodId"] == LAYER_MUNICIPALITY
            and r["attributes"].get("is_current_jahr")]
    m = muni[0] if muni else {}
    return e, n, country, m.get("gde_nr", ""), m.get("gemname", ""), m.get("kanton", "")


def capital_point(name_canton):
    name, kt = name_canton
    q = urllib.parse.urlencode({"searchText": f"{name} {kt}", "type": "locations", "origins": "gg25", "limit": "5"})
    res = json.loads(http_get(f"{GEOADMIN_SEARCH}?{q}"))["results"]
    hit = next(r["attrs"] for r in res if re.sub("<[^>]+>", "", r["attrs"]["label"]).split(" (")[0] == name)
    lat, lon = f"{hit['lat']:.4f}", f"{hit['lon']:.4f}"
    if kt == "BS":
        lat, lon = REFERENCE_BASEL_LAT_LON
    # LV95 from the search result (LV03 x/y + offsets 2'000'000 / 1'000'000)
    return {"e": round(hit["y"]) + 2_000_000, "n": round(hit["x"]) + 1_000_000, "lat": lat, "lon": lon,
            "nr": hit["featureId"], "name": name, "kt": kt}


def cmd_grid(_args):
    points = [(e, n) for e in range(GRID_E_RANGE_M[0], GRID_E_RANGE_M[1] + 1, GRID_STEP_M)
              for n in range(GRID_N_RANGE_M[0], GRID_N_RANGE_M[1] + 1, GRID_STEP_M)]
    print(f"identifying {len(points)} candidate points via geo.admin.ch ...")
    with ThreadPoolExecutor(8) as pool:
        rows = list(pool.map(identify, points))
    inside = [r for r in rows if r[2] == "Schweiz"]
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(GRID_CSV, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["point_id", "e_lv95_m", "n_lv95_m", "lat_deg", "lon_deg",
                    "bfs_municipality_nr", "municipality", "canton"])
        for i, (e, n, _c, nr, name, kt) in enumerate(sorted(inside, key=lambda r: (-r[1], r[0]))):
            lat, lon = lv95_to_wgs84(e, n)
            w.writerow([f"g{i:04d}", e, n, f"{lat:.4f}", f"{lon:.4f}", nr, name, kt])
        for c in map(capital_point, CAPITALS):
            w.writerow([f"cap-{c['kt'].lower()}", c["e"], c["n"], c["lat"], c["lon"], c["nr"], c["name"], c["kt"]])
    print(f"{len(inside)} of {len(points)} points inside Switzerland + {len(CAPITALS)} capitals -> {GRID_CSV}")


def read_grid():
    with open(GRID_CSV, newline="") as f:
        return list(csv.DictReader(f))


def fetch_batches(api, variables, extra, out_path, tag):
    """Fetch hourly CSV for all grid points in batches; long format, one row per point and hour."""
    grid = read_grid()
    os.makedirs(CACHE_DIR, exist_ok=True)
    batches = [grid[i:i + BATCH_SIZE] for i in range(0, len(grid), BATCH_SIZE)]
    for bi, batch in enumerate(batches):
        cache = os.path.join(CACHE_DIR, f"{tag}-{bi:03d}.csv")
        if os.path.exists(cache):
            continue
        q = {"latitude": ",".join(p["lat_deg"] for p in batch),
             "longitude": ",".join(p["lon_deg"] for p in batch),
             "start_date": START_DATE, "end_date": END_DATE, "hourly": ",".join(variables),
             "timezone": TIMEZONE, "format": "csv", **extra}
        print(f"  {tag} batch {bi + 1}/{len(batches)}")
        body = http_get(f"{api}?{urllib.parse.urlencode(q)}")
        with open(cache + ".tmp", "wb") as f:
            f.write(body)
        os.replace(cache + ".tmp", cache)
        time.sleep(PAUSE_S)

    # Open-Meteo multi-location CSV: a block of location metadata (location_id,latitude,longitude,
    # elevation,...) then a block of hourly rows starting with location_id,time,...
    with gzip.open(out_path, "wt", newline="", compresslevel=9) as gz:
        w = csv.writer(gz)
        w.writerow(["point_id", "time_local"] + variables)
        meta_rows = []
        for bi, batch in enumerate(batches):
            with open(os.path.join(CACHE_DIR, f"{tag}-{bi:03d}.csv"), newline="") as f:
                text = f.read()
            blocks = [b for b in text.strip().split("\n\n") if b.strip()]
            meta = list(csv.DictReader(io.StringIO(blocks[0])))
            data = csv.reader(io.StringIO(blocks[1]))
            header = next(data)
            idx = [header.index(next(h for h in header if h.startswith(v + " ") or h == v)) for v in variables]
            for m, p in zip(meta, batch):
                meta_rows.append({"point_id": p["point_id"], "model_lat_deg": m["latitude"],
                                  "model_lon_deg": m["longitude"], "model_elevation_m": m["elevation"]})
            for row in data:
                p = batch[int(row[0])]
                w.writerow([p["point_id"], row[1]] + [row[i] for i in idx])
    return meta_rows


def cmd_weather(_args):
    meta = fetch_batches(ARCHIVE_API, WEATHER_VARS, {}, WEATHER_CSV, "archive")
    # Model grid cell and elevation per point go into the grid file (extra columns).
    grid = read_grid()
    by_id = {m["point_id"]: m for m in meta}
    cols = list(grid[0].keys())
    cols += [c for c in ("model_lat_deg", "model_lon_deg", "model_elevation_m") if c not in cols]
    with open(GRID_CSV, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=cols)
        w.writeheader()
        for g in grid:
            w.writerow({**g, **by_id[g["point_id"]]})
    print(f"-> {WEATHER_CSV}")


def cmd_forecast(_args):
    fetch_batches(PREVIOUS_RUNS_API, FORECAST_VARS, {"models": "best_match"}, FORECAST_CSV, "prevruns-lead0-3")
    print(f"-> {FORECAST_CSV}")


def cmd_check(_args):
    grid = read_grid()
    ids = {g["point_id"] for g in grid}
    ok = True
    for path in (WEATHER_CSV, FORECAST_CSV):
        if not os.path.exists(path):
            print(f"missing {path}")
            ok = False
            continue
        counts, empty, tmin, tmax = {}, 0, math.inf, -math.inf
        with gzip.open(path, "rt", newline="") as f:
            r = csv.reader(f)
            header = next(r)
            for row in r:
                counts[row[0]] = counts.get(row[0], 0) + 1
                empty += sum(1 for v in row[2:] if v == "")
                t = row[2]
                if t:
                    tmin, tmax = min(tmin, float(t)), max(tmax, float(t))
        bad = {k: c for k, c in counts.items() if c != HOURS_2025}
        print(f"{os.path.basename(path)}: {len(counts)} points (grid {len(ids)}), "
              f"rows/point != {HOURS_2025}: {len(bad)}, empty values: {empty}, "
              f"{header[2]} range {tmin} .. {tmax}")
        ok &= set(counts) == ids and not bad
    print("OK" if ok else "CHECK FAILED")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("step", choices=["grid", "weather", "forecast", "check"])
    args = ap.parse_args()
    return {"grid": cmd_grid, "weather": cmd_weather, "forecast": cmd_forecast, "check": cmd_check}[args.step](args) or 0


if __name__ == "__main__":
    sys.exit(main())
