#!/usr/bin/env python3
"""Independent check of the year explorer's numbers (prototypes/year-explorer/index.html).

Re-implements the explorer's calculation in plain Python from data/raw/ (not from the page's
JSON), prints the yearly balance, one worked example hour and a short day trace, so a human can
check every step by hand. Method: prototypes/year-explorer/METHOD.md.

Usage:
    python3 prototypes/year-explorer/verify.py                    # default settings of the page
    python3 prototypes/year-explorer/verify.py --servers 8 --min-load 50 --tank 35 \
        --price 0.3325 --compute 0.30 --hw 0 --hour 2025-01-15T18:00

This is a verification tool for a prototype. It is not the project's model; the scored model
will live in the C# core.
"""

from __future__ import annotations

import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
WEATHER = REPO / "data" / "raw" / "open-meteo" / "basel-2025-hourly.csv"
ELCOM = REPO / "data" / "raw" / "elcom" / "iwb-basel-h4-2026.json"

# Case parameters (docs/case/challenge.md), same names as in the page.
SERVER_ELECTRIC_KW = 1.0       # one rack server uses 1 kW
HEAT_RECOVERY_SHARE = 0.8      # 80 % of the heat is recovered into water
DESIGN_HEATING_KW = 10.0       # heating formula 10 kW x (20 - T) / 25.6
BASE_TEMP_C = 20.0
FORMULA_DIVISOR_K = 25.6
HEATING_DAY_MEAN_BELOW_C = 12.0
HOT_WATER_KWH_PER_DAY = 12.0
HEAT_PUMP_COP = 2.5            # value of heat = electricity price / 2.5
HOURS_PER_DAY = 24
RP_PER_CHF = 100.0


def load_hours() -> list[tuple[str, float]]:
    lines = WEATHER.read_text().splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("time,"))
    rows = [r for r in csv.reader(lines[start:])][1:]
    return [(r[0], float(r[1])) for r in rows if r]


def elcom_price() -> float:
    snap = json.loads(ELCOM.read_text())
    basel = [o for o in snap["response"]["data"]["observations"] if o["municipalityLabel"] == "Basel"]
    return basel[0]["total"] / RP_PER_CHF


def demand_per_hour(hours: list[tuple[str, float]]) -> list[dict]:
    by_day: dict[str, list[float]] = defaultdict(list)
    for t, temp in hours:
        by_day[t[:10]].append(temp)
    mean = {d: sum(v) / len(v) for d, v in by_day.items()}
    out = []
    for t, temp in hours:
        heating_day = mean[t[:10]] < HEATING_DAY_MEAN_BELOW_C
        space = max(0.0, DESIGN_HEATING_KW * (BASE_TEMP_C - temp) / FORMULA_DIVISOR_K) if heating_day else 0.0
        out.append({"time": t, "temp_c": temp, "day_mean_c": mean[t[:10]], "heating_day": heating_day,
                    "space_kwh": space, "hot_water_kwh": HOT_WATER_KWH_PER_DAY / HOURS_PER_DAY})
    return out


def simulate(demand: list[dict], servers: int, min_load_pct: float, tank_kwh: float) -> list[dict]:
    """Greedy heat-led rule with a minimum load (same rule as the page)."""
    heat_per_server_kw = SERVER_ELECTRIC_KW * HEAT_RECOVERY_SHARE
    floor = min_load_pct / 100.0 * servers
    tank = 0.0
    trace = []
    for h in demand:
        d = h["space_kwh"] + h["hot_water_kwh"]
        tank_start = tank
        wanted_servers = (d + (tank_kwh - tank)) / heat_per_server_kw   # cover demand and refill tank
        run = max(min(servers, wanted_servers), floor)                  # never above n, never below floor
        q = run * heat_per_server_kw
        backup = dump = to_tank = from_tank = 0.0
        if q >= d:
            to_tank = min(q - d, tank_kwh - tank)
            tank += to_tank
            dump = q - d - to_tank
        else:
            from_tank = min(tank, d - q)
            tank -= from_tank
            backup = d - q - from_tank
        trace.append({**h, "demand_kwh": d, "wanted_servers": wanted_servers, "run": run, "heat_kwh": q,
                      "tank_start": tank_start, "to_tank": to_tank, "from_tank": from_tank, "tank_end": tank,
                      "backup_kwh": backup, "dump_kwh": dump, "to_house_kwh": d - backup})
    return trace


def summarise(trace: list[dict], servers: int, price: float, compute: float, hw: float) -> dict:
    s = lambda k: sum(h[k] for h in trace)
    run, demand, backup = s("run"), s("demand_kwh"), s("backup_kwh")
    used = demand - backup
    elec = run * SERVER_ELECTRIC_KW * price
    heat_value = used * price / HEAT_PUMP_COP
    income = run * compute
    return {
        "heating_days": len({h["time"][:10] for h in trace if h["heating_day"]}),
        "space_heating_kwh": s("space_kwh"), "hot_water_kwh": s("hot_water_kwh"), "demand_kwh": demand,
        "server_heat_produced_kwh": s("heat_kwh"), "heat_to_house_kwh": used, "backup_kwh": backup,
        "dump_kwh": s("dump_kwh"), "tank_end_of_year_kwh": trace[-1]["tank_end"],
        "server_hours": run, "coverage": used / demand, "utilisation": run / (servers * len(trace)),
        "electricity_chf": elec, "heat_value_chf": heat_value, "computing_income_chf": income,
        "hardware_chf": hw * servers, "break_even_chf_per_server_hour": (elec - heat_value) / run,
        "system_surplus_chf": income + heat_value - elec - hw * servers,
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--servers", type=int, default=4)
    ap.add_argument("--min-load", type=float, default=0.0, help="percent")
    ap.add_argument("--tank", type=float, default=35.0, help="kWh")
    ap.add_argument("--price", type=float, default=None, help="CHF/kWh, default ElCom snapshot")
    ap.add_argument("--compute", type=float, default=0.30, help="CHF per server-hour (example input)")
    ap.add_argument("--hw", type=float, default=0.0, help="CHF per server and year")
    ap.add_argument("--hour", default="2025-01-15T18:00", help="hour for the worked example")
    a = ap.parse_args()
    price = a.price if a.price is not None else elcom_price()

    trace = simulate(demand_per_hour(load_hours()), a.servers, a.min_load, a.tank)
    r = summarise(trace, a.servers, price, a.compute, a.hw)

    print(f"Settings: {a.servers} servers, min load {a.min_load:g} %, tank {a.tank:g} kWh, "
          f"electricity {price:.4f} CHF/kWh, computing {a.compute:.2f} CHF/h, hardware {a.hw:g} CHF/server/yr\n")
    print("Yearly balance")
    for k, v in r.items():
        print(f"  {k:32s} {v:14,.4f}" if isinstance(v, float) else f"  {k:32s} {v:14,d}")
    check = r["heat_to_house_kwh"] + r["dump_kwh"] + r["tank_end_of_year_kwh"] - r["server_heat_produced_kwh"]
    print(f"  {'energy check (should be 0)':32s} {check:14.6f}   heat to house + dumped + tank at end - produced\n")

    h = next(x for x in trace if x["time"] == a.hour)
    print(f"Worked example hour {h['time']}")
    print(f"  T = {h['temp_c']} °C, day mean {h['day_mean_c']:.2f} °C -> heating day: {h['heating_day']}")
    print(f"  space heating = max(0, 10 x (20 - {h['temp_c']}) / 25.6) = {h['space_kwh']:.4f} kWh")
    print(f"  hot water     = 12 / 24 = {h['hot_water_kwh']:.4f} kWh   -> demand {h['demand_kwh']:.4f} kWh")
    print(f"  tank at start {h['tank_start']:.4f} kWh; servers wanted = (demand + room in tank) / 0.8 = {h['wanted_servers']:.4f}")
    print(f"  servers running = max(min({a.servers}, wanted), floor) = {h['run']:.4f} -> heat {h['heat_kwh']:.4f} kWh")
    print(f"  to tank {h['to_tank']:.4f}, from tank {h['from_tank']:.4f}, backup {h['backup_kwh']:.4f}, "
          f"dumped {h['dump_kwh']:.4f}, tank at end {h['tank_end']:.4f} kWh\n")

    day = a.hour[:10]
    print(f"Day trace {day} (kWh per hour)")
    print("  hour  T°C   demand  servers  heat   to_house  backup  dump   tank_end")
    for x in (x for x in trace if x["time"].startswith(day)):
        print(f"  {x['time'][11:16]} {x['temp_c']:5.1f} {x['demand_kwh']:7.3f} {x['run']:7.3f} {x['heat_kwh']:6.3f} "
              f"{x['to_house_kwh']:8.3f} {x['backup_kwh']:7.3f} {x['dump_kwh']:6.3f} {x['tank_end']:8.3f}")


if __name__ == "__main__":
    main()
