#!/usr/bin/env python3
"""Build explorer-data.json for the year explorer prototype from data/raw/.

Usage: python3 prototypes/year-explorer/build_data.py

Reads the committed raw data (Open-Meteo Basel 2025, ElCom IWB H4 2026) and writes a small JSON
file next to index.html. No values are invented here; everything comes from data/raw/.
"""

from __future__ import annotations

import csv
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
WEATHER = REPO / "data" / "raw" / "open-meteo" / "basel-2025-hourly.csv"
ELCOM = REPO / "data" / "raw" / "elcom" / "iwb-basel-h4-2026.json"
OUT = HERE / "explorer-data.json"
HTML = HERE / "index.html"
RP_PER_CHF = 100.0


def read_temperatures() -> tuple[str, list[float]]:
    lines = WEATHER.read_text(encoding="utf-8").splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("time,"))
    rows = list(csv.reader(lines[start:]))
    header, body = rows[0], [r for r in rows[1:] if r]
    t_col = next(i for i, h in enumerate(header) if h.startswith("temperature_2m"))
    return body[0][0], [float(r[t_col]) for r in body]


def read_price_chf_per_kwh() -> float:
    snapshot = json.loads(ELCOM.read_text(encoding="utf-8"))
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
    payload = json.dumps(data, separators=(",", ":"), ensure_ascii=False)
    OUT.write_text(payload + "\n", encoding="utf-8")
    html = HTML.read_text(encoding="utf-8")
    html, replaced = re.subn(
        r'(<script id="explorer-data" type="application/json">).*?(</script>)',
        lambda match: match.group(1) + payload + match.group(2), html, count=1, flags=re.DOTALL,
    )
    if replaced != 1:
        raise RuntimeError(f"Could not find the inline explorer-data script in {HTML}")
    HTML.write_text(html, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(REPO)} and inlined {len(temps)} hours into {HTML.relative_to(REPO)}")


if __name__ == "__main__":
    main()
