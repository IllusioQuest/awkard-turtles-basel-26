#!/usr/bin/env python3
"""Check the explorer's reimplemented heat rule, hourly invariants and C# reference case."""

from __future__ import annotations

import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
DATA = json.loads((HERE / "explorer-data.json").read_text(encoding="utf-8"))
REFERENCE = json.loads((HERE.parent.parent / "data" / "derived" / "reference-house"
                        / "explorer-default.json").read_text(encoding="utf-8"))["Summary"]

HOURS_PER_DAY = 24
SERVER_COUNT = 4
TANK_CAPACITY_KWH = 35.0
SERVER_HEAT_KW = 0.8
DESIGN_HEATING_KW = 10.0
BASE_TEMPERATURE_C = 20.0
FORMULA_DIVISOR_C = 25.6
HEATING_DAY_THRESHOLD_C = 12.0
HOT_WATER_KWH_PER_DAY = 12.0
HEAT_PUMP_COP = 2.5


def close(actual: float, expected: float, tolerance: float, label: str) -> None:
    if not math.isclose(actual, expected, rel_tol=0, abs_tol=tolerance):
        raise AssertionError(f"{label}: got {actual}, expected {expected} ± {tolerance}")


def main() -> None:
    temps = DATA["temperature_2m_c"]
    assert len(temps) == 8760, f"expected 8760 hourly temperatures, got {len(temps)}"
    daily_means = [sum(temps[d:d + HOURS_PER_DAY]) / HOURS_PER_DAY
                   for d in range(0, len(temps), HOURS_PER_DAY)]
    heating = [mean < HEATING_DAY_THRESHOLD_C for mean in daily_means]
    space_heat = [
        max(0.0, DESIGN_HEATING_KW * (BASE_TEMPERATURE_C - temp) / FORMULA_DIVISOR_C)
        if heating[i // HOURS_PER_DAY] else 0.0
        for i, temp in enumerate(temps)
    ]
    hot_water = HOT_WATER_KWH_PER_DAY / HOURS_PER_DAY

    tank = 0.0
    server_hours = total_demand = backup_total = dumped_total = used_total = 0.0
    for i, space in enumerate(space_heat):
        demand = space + hot_water
        want = (demand + TANK_CAPACITY_KWH - tank) / SERVER_HEAT_KW
        running = max(0.0, min(SERVER_COUNT, want))
        produced = running * SERVER_HEAT_KW
        before = tank
        backup = dumped = 0.0
        if produced >= demand:
            tank += min(produced - demand, TANK_CAPACITY_KWH - tank)
            dumped = produced - demand - (tank - before)
        else:
            draw = min(tank, demand - produced)
            tank -= draw
            backup = demand - produced - draw
        # Energy conservation: server heat + stored heat + backup = demand + dumped + storage after.
        close(produced + before + backup, demand + dumped + tank, 1e-9, f"hour {i} energy balance")
        assert -1e-9 <= tank <= TANK_CAPACITY_KWH + 1e-9, f"hour {i} tank out of bounds: {tank}"
        server_hours += running
        total_demand += demand
        backup_total += backup
        dumped_total += dumped
        used_total += demand - backup

    price = DATA["electricity_price_chf_per_kwh"]
    break_even = (server_hours * price - used_total * price / HEAT_PUMP_COP) / server_hours
    close(sum(heating), 206, 0, "heating days")
    close(sum(space_heat), REFERENCE["SpaceHeatDemandKwh"], 1.0, "space heat kWh")
    close(server_hours, REFERENCE["ServerHours"], 1.0, "server-hours")
    close(used_total / total_demand, REFERENCE["Coverage"], 0.001, "coverage")
    close(dumped_total, REFERENCE["DumpedHeatKwh"], 1.0, "dumped heat kWh")
    close(break_even, REFERENCE["BreakEvenChfPerServerHour"], 0.001, "case break-even")
    print("PASS: hourly energy/tank invariants and C# reference case agree")
    print(f"heating days={sum(heating)}, space heat={sum(space_heat):.2f} kWh, "
          f"server-hours={server_hours:.2f}, coverage={used_total / total_demand:.4%}, "
          f"dumped={dumped_total:.4f} kWh, case break-even={break_even:.4f} CHF/h")


if __name__ == "__main__":
    main()
