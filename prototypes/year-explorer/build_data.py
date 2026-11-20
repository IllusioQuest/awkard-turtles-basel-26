#!/usr/bin/env python3
"""Build explorer-data.json for the year explorer prototype from data/raw/.

Usage: python3 prototypes/year-explorer/build_data.py

Reads the committed raw data (Open-Meteo Basel 2025, ElCom IWB H4 2026) and writes a small JSON
file next to index.html. No values are invented here; everything comes from data/raw/.
"""

from __future__ import annotations

import csv
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
WEATHER = REPO / "data" / "raw" / "open-meteo" / "basel-2025-hourly.csv"
ELCOM = REPO / "data" / "raw" / "elcom" / "iwb-basel-h4-2026.json"
OUT = HERE / "explorer-data.json"
RP_PER_CHF = 100.0


def read_temperatures() -> tuple[str, list[float]]:
    lines = WEATHER.read_text().splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("time,"))
    rows = list(csv.reader(lines[start:]))
    header, body = rows[0], [r for r in rows[1:] if r]
    t_col = next(i for i, h in enumerate(header) if h.startswith("temperature_2m"))
    return body[0][0], [float(r[t_col]) for r in body]


def read_price_chf_per_kwh() -> float:
    snapshot = json.loads(ELCOM.read_text())
    basel = [o for o in snapshot["response"]["data"]["observations"] if o["municipalityLabel"] == "Basel"]
    return basel[0]["total"] / RP_PER_CHF


def main() -> None:
    first_hour, temps = read_temperatures()
    data = {
        "first_hour_local": first_hour,
        "temperature_2m_c": temps,
        "electricity_price_chf_per_kwh": read_price_chf_per_kwh(),
        "sources": {
            "temperature_2m_c": "data/raw/open-meteo/basel-2025-hourly.csv (Open-Meteo archive, Basel 47.56 N 7.59 E, 2025)",
            "electricity_price_chf_per_kwh": "data/raw/elcom/iwb-basel-h4-2026.json (ElCom 2026, IWB Basel, H4, total)",
        },
    }
    OUT.write_text(json.dumps(data, separators=(",", ":")) + "\n")
    print(f"wrote {OUT.relative_to(REPO)}: {len(temps)} hours, price {data['electricity_price_chf_per_kwh']} CHF/kWh")


if __name__ == "__main__":
    main()
