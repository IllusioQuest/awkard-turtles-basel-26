"""Prepare the film's data from repository sources (plain Python 3, no Blender needed).

Writes
  video/blender/numbers.json         every number the film shows, with value, unit and source
  video/blender/data/film-data.json  compact series that drive animation (house day, year, map)

Inputs (all in the repository):
  data/derived/reference-house-v1/sites/reference-house/hourly.csv   C# year run, 4 servers (T-13)
  data/derived/reference-house/explorer-default.json                 4-server year summary (T-06)
  data/raw/open-meteo/basel-2025-hourly.csv                          case weather
  config/fleet/houses/dreispitz-5-pool.json, basel-region-100.csv    fleet scenario inputs
  FleetRun outputs of config/fleet/scenarios/{dreispitz-5-pool,basel-region-100}.json
    (run here into video/blender/.cache/ when missing; needs the .NET 8 SDK)

Usage: python3 video/blender/prepare_data.py [--skip-fleet]
"""
from __future__ import annotations

import csv
import json
import os
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CACHE = HERE / ".cache"
HOURS_PER_DAY = 24

HOURLY = "data/derived/reference-house-v1/sites/reference-house/hourly.csv"
SUMMARY = "data/derived/reference-house/explorer-default.json"
WEATHER = "data/raw/open-meteo/basel-2025-hourly.csv"
POOL_SCEN = "config/fleet/scenarios/dreispitz-5-pool.json"
REGION_SCEN = "config/fleet/scenarios/basel-region-100.json"
POOL_HOUSES = "config/fleet/houses/dreispitz-5-pool.json"
REGION_HOUSES = "config/fleet/houses/basel-region-100.csv"
REF_CONFIG = "config/reference-house.json"
L4_PROFILE = "config/hardware/gpu-inference-l4.json"

# The evening of beat 1: a cold January evening in Basel. Chosen from the case weather file
# (13 Jan 2025 19:00 is one of the coldest January evening hours, -3.0 degC). Scenario choice.
EVENING = "2025-01-13T19:00"


def rel(p: str) -> Path:
    return ROOT / p


def read_weather() -> dict[str, float]:
    lines = rel(WEATHER).read_text(encoding="utf-8").splitlines()
    start = next(i for i, l in enumerate(lines) if l.startswith("time,"))
    rows = csv.DictReader(lines[start:])
    return {r["time"]: float(r["temperature_2m (°C)"]) for r in rows}


def fleet_run(scenario: str, name: str, skip: bool) -> Path:
    out = CACHE / name
    if (out / "houses-timeseries.csv").exists() or skip:
        return out
    dll = ROOT / "src/DigitalHeat.FleetRun/bin/Release/net8.0/DigitalHeat.FleetRun.dll"
    if not dll.exists():
        subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", str(ROOT / "src/DigitalHeat.FleetRun")], check=True)
    subprocess.run(["dotnet", str(dll), "run", str(rel(scenario)), "--out", str(out)], check=True, cwd=ROOT)
    # The large per-job files are not needed by the film.
    for big in ("jobs.csv", "jobs-input.csv"):
        (out / big).unlink(missing_ok=True)
    return out


def daily_series(run_dir: Path, servers: dict[str, int]) -> dict[str, dict[str, list[float]]]:
    """Per house: daily mean load (electric kWh / (servers x 24 h)), heat used and backup kWh, tank end."""
    out: dict[str, dict[str, list[float]]] = defaultdict(lambda: defaultdict(list))
    with open(run_dir / "houses-timeseries.csv", newline="") as f:
        for r in csv.DictReader(f):
            h = r["house_id"]
            n = servers[h]
            out[h]["load"].append(round(float(r["electric_kwh"]) / (n * HOURS_PER_DAY), 4))
            out[h]["used_kwh"].append(round(float(r["heat_used_kwh"]), 2))
            out[h]["demand_kwh"].append(round(float(r["heat_demand_kwh"]), 2))
            out[h]["tank_kwh"].append(round(float(r["tank_end_kwh"]), 2))
    return out


def summary_rows(run_dir: Path) -> dict[str, dict[str, str]]:
    with open(run_dir / "houses-summary.csv", newline="") as f:
        return {r["house_id"]: r for r in csv.DictReader(f)}


def _when(local_time: str) -> str:
    import datetime as dt
    d = dt.datetime.fromisoformat(local_time)
    return f"{d.day} {d.strftime('%B')}, {d.strftime('%H:%M')}"


def num(value, unit, display, source, note=""):
    d = {"value": value, "unit": unit, "display": display, "source": source}
    if note:
        d["note"] = note
    return d


def main() -> int:
    skip = "--skip-fleet" in sys.argv
    weather = read_weather()
    hourly = list(csv.DictReader(open(rel(HOURLY), newline="")))
    summ = json.loads(rel(SUMMARY).read_text())["Summary"]
    ref_cfg = json.loads(rel(REF_CONFIG).read_text())

    # --- Reference house: the evening and the run-up to the first full tank -------------------
    idx = {r["local_time"]: i for i, r in enumerate(hourly)}
    ev = hourly[idx[EVENING]]
    first_full = next(r for r in hourly if float(r["tank_stored_kwh"]) >= 35 - 1e-6)
    first_pause = next(r for r in hourly if float(r["actual_load_per_server"]) < 1 - 1e-9)
    assert first_full["local_time"] == first_pause["local_time"]
    i0, i1 = idx[EVENING], idx[first_full["local_time"]]
    # Daily samples between the evening and the first full tank, for the time-lapse of beat 4.
    run_up = []
    for i in range(i0, i1 + 1, 6):
        r = hourly[i]
        run_up.append({
            "t": r["local_time"], "temp_c": weather[r["local_time"]],
            "load": float(r["actual_load_per_server"]), "tank_kwh": float(r["tank_stored_kwh"]),
            "demand_kwh": float(r["heat_demand_kwh"]), "produced_kwh": float(r["produced_heat_kwh"]),
        })
    # the hours after the first full tank, to show the pause
    after = []
    for i in range(i1 - 3, i1 + 9):
        r = hourly[i]
        after.append({"t": r["local_time"], "load": float(r["actual_load_per_server"]), "tank_kwh": float(r["tank_stored_kwh"])})

    # --- Fleet runs ---------------------------------------------------------------------------
    pool_houses = json.loads(rel(POOL_HOUSES).read_text())["houses"]
    pool_servers = {h["house_id"]: int(h["server_count"]) for h in pool_houses}
    pool_dir = fleet_run(POOL_SCEN, "dreispitz-5-pool", skip)
    pool_daily = daily_series(pool_dir, pool_servers)
    pool_sum = summary_rows(pool_dir)

    region_rows = list(csv.DictReader(l for l in open(rel(REGION_HOUSES), newline="") if not l.startswith("#")))
    region_scen = json.loads(rel(REGION_SCEN).read_text())
    n_region = int(region_scen["servers"]["n"])
    region_servers = {r["house_id"]: n_region for r in region_rows}
    region_dir = fleet_run(REGION_SCEN, "basel-region-100", skip)
    region_daily = daily_series(region_dir, region_servers)

    def monthly(series: list[float]) -> list[float]:
        import datetime as dt
        acc = defaultdict(list)
        for d, v in enumerate(series):
            acc[(dt.date(2025, 1, 1) + dt.timedelta(days=d)).month].append(v)
        return [round(sum(acc[m]) / len(acc[m]), 3) for m in range(1, 13)]

    houses_ids = [h for h in pool_servers if not h.startswith("pool")]
    house_month = [round(sum(monthly(pool_daily[h]["load"])[m] for h in houses_ids) / len(houses_ids), 3) for m in range(12)]
    pool_month = monthly(pool_daily["pool-dreispitz"]["load"])

    film = {
        "_note": "Generated by video/blender/prepare_data.py from repository data; do not edit by hand.",
        "evening": {"t": EVENING, "temp_c": weather[EVENING], "demand_kwh": float(ev["heat_demand_kwh"]),
                    "produced_kwh": float(ev["produced_heat_kwh"]), "backup_kwh": float(ev["backup_heat_kwh"]),
                    "load": float(ev["actual_load_per_server"])},
        "run_up": run_up,
        "first_full": first_full["local_time"],
        "after_full": after,
        "pool": {
            "sites": [{"id": h["house_id"], "lat": h["lat_deg"], "lon": h["lon_deg"], "servers": int(h["server_count"])} for h in pool_houses],
            "daily": {h: dict(v) for h, v in pool_daily.items()},
            "month_load_houses": house_month, "month_load_pool": pool_month,
        },
        "region": {
            "sites": [{"id": r["house_id"], "lat": float(r["lat_deg"]), "lon": float(r["lon_deg"]), "servers": n_region} for r in region_rows],
            "daily_load": {h: v["load"] for h, v in region_daily.items()},
        },
    }
    (HERE / "data").mkdir(exist_ok=True)
    (HERE / "data/film-data.json").write_text(json.dumps(film, separators=(",", ":")))

    # --- numbers.json -------------------------------------------------------------------------
    hr = f"{HOURLY} (C# year run, 4 servers, row {{}})"
    pool = pool_sum["pool-dreispitz"]
    pool_cfg = next(h for h in pool_houses if h["house_id"] == "pool-dreispitz")
    pool_hot = float(pool_cfg["hot_water_kwh_per_day"])
    l4 = json.loads(rel(L4_PROFILE).read_text())
    numbers = {
        "_about": "Every number shown in the film, with its source. Generated by video/blender/prepare_data.py; "
                  "values are read from the files named in 'source'. Fleet values are scenario variants (T-67), not scored figures.",
        "evening_temperature_c": num(weather[EVENING], "degC", f"{weather[EVENING]:.1f} °C", f"{WEATHER} row {EVENING}"),
        "evening_heat_demand_kw": num(float(ev["heat_demand_kwh"]), "kWh in that hour", f"{float(ev['heat_demand_kwh']):.1f} kW",
                                      hr.format(EVENING) + " heat_demand_kwh; case formula 10 kW x (20 - T) / 25.6 + hot water"),
        "servers_per_house": num(summ["ServerCount"], "servers", f"{summ['ServerCount']}", f"{SUMMARY} Summary.ServerCount",
                                 "draft baseline, not an optimum (docs/results/reference-house.md)"),
        "server_electric_kw": num(1, "kW", "1 kW", "docs/case/challenge.md via config/reference-house.json"),
        "heat_recovery_share": num(0.8, "fraction", "80 %", "docs/case/challenge.md via config/reference-house.json"),
        "tank_kwh": num(35, "kWh", "35 kWh", "docs/case/challenge.md (1,000 l, 30 K spread) via config/reference-house.json"),
        "evening_server_heat_kw": num(float(ev["produced_heat_kwh"]), "kWh in that hour", f"{float(ev['produced_heat_kwh']):.1f} kW", hr.format(EVENING) + " produced_heat_kwh"),
        "first_full_tank": num(first_full["local_time"], "local time", _when(first_full["local_time"]), hr.format(first_full["local_time"]) + " tank_stored_kwh = 35, actual_load_per_server < 1 (first pause of the year)"),
        "heating_days": num(summ["HeatingDays"], "days", f"{summ['HeatingDays']}", f"{SUMMARY} Summary.HeatingDays"),
        "heat_demand_kwh": num(summ["TotalHeatDemandKwh"], "kWh/year", f"{summ['TotalHeatDemandKwh']:,.0f} kWh".replace(",", "'"), f"{SUMMARY} Summary.TotalHeatDemandKwh"),
        "coverage": num(summ["Coverage"], "fraction", f"{100*summ['Coverage']:.1f} %", f"{SUMMARY} Summary.Coverage", "share of space heating + hot water covered by server heat"),
        "dumped_kwh": num(summ["DumpedHeatKwh"], "kWh/year", f"{summ['DumpedHeatKwh']:.0f} kWh", f"{SUMMARY} Summary.DumpedHeatKwh"),
        "utilisation": num(summ["Utilisation"], "fraction", f"{100*summ['Utilisation']:.1f} %", f"{SUMMARY} Summary.Utilisation"),
        "break_even_chf_per_server_hour": num(summ["BreakEvenChfPerServerHour"], "CHF/server-hour", f"CHF {summ['BreakEvenChfPerServerHour']:.2f}",
                                              f"{SUMMARY} Summary.BreakEvenChfPerServerHour", "case formula: electricity minus used-heat value; excludes hardware and operations"),
        "missed_deadlines": num(None, "", "not defined (unlimited jobs)", f"{SUMMARY} Summary.MissedDeadlines; docs/pitch/claim-sheet.md (branch docs/T-67-pitch-film-treatment)",
                                "not shown as a number in the film"),
        "coldest_hour_servers": num(12.5, "servers", "12.5", "docs/case/key-figures.md (10 kW / 0.8 kW)"),
        "summer_servers": num(0.6, "servers", "0.6", "docs/case/key-figures.md (12 kWh / 19.2 kWh)"),
        "pool_annual_heat_kwh": num(float(pool["heat_demand_kwh"]), "kWh/year", f"{float(pool['heat_demand_kwh']):,.0f} kWh".replace(",", "'"), f"{POOL_HOUSES} pool-dreispitz ({pool_hot:.1f} kWh/day; Hallenbad Allschwil, data/raw/sites/site-types.csv); FleetRun houses-summary heat_demand_kwh"),
        "pool_servers": num(int(pool["servers"]), "servers", pool["servers"], f"{POOL_HOUSES} server_count (synthetic scenario value)"),
        "pool_houses": num(len(houses_ids), "houses", str(len(houses_ids)), f"{POOL_HOUSES}"),
        "pool_month_load_pool": num(pool_month, "mean server load per month", "", f"FleetRun {POOL_SCEN}: houses-timeseries electric_kwh / (servers x 24)"),
        "pool_month_load_houses": num(house_month, "mean server load per month", "", f"FleetRun {POOL_SCEN}: houses-timeseries electric_kwh / (servers x 24), mean of the 5 houses"),
        "pool_jan_load": num(pool_month[0], "fraction", f"{100*pool_month[0]:.0f} %", f"FleetRun {POOL_SCEN}, pool, January"),
        "pool_jul_load": num(pool_month[6], "fraction", f"{100*pool_month[6]:.0f} %", f"FleetRun {POOL_SCEN}, pool, July"),
        "houses_jan_load": num(house_month[0], "fraction", f"{100*house_month[0]:.0f} %", f"FleetRun {POOL_SCEN}, 5 houses, January"),
        "houses_jul_load": num(house_month[6], "fraction", f"{100*house_month[6]:.0f} %", f"FleetRun {POOL_SCEN}, 5 houses, July"),
        "pool_dumped_kwh": num(sum(float(r["heat_dumped_kwh"]) for r in pool_sum.values()), "kWh/year", "0 kWh", f"FleetRun {POOL_SCEN} houses-summary heat_dumped_kwh, all sites"),
        "region_houses": num(len(region_rows), "houses", str(len(region_rows)), f"{REGION_HOUSES} (synthetic positions, Basel-Landschaft grid cells)"),
        "region_servers_per_house": num(n_region, "servers", str(n_region), f"{REGION_SCEN} servers.n"),
        "hardware_profile": num(l4.get("id", "gpu-inference-l4"), "", "L4 GPU inference server", L4_PROFILE, "fleet scenarios use this profile, abstracted to the case 1 kW server"),
    }
    (HERE / "numbers.json").write_text(json.dumps(numbers, indent=2, ensure_ascii=False) + "\n")
    print("wrote numbers.json and data/film-data.json")
    print("house months", house_month)
    print("pool months", pool_month)
    return 0


if __name__ == "__main__":
    sys.exit(main())
