#!/usr/bin/env python3
"""Fetch the small public source datasets into data/raw/ and validate them.

Usage:
    python3 scripts/fetch_data.py           # download all sources, then validate
    python3 scripts/fetch_data.py --check   # validate the files already in data/raw/

Standard library only, so it runs on any machine with Python 3.9+.
Sources, parameters and expected values are defined in SOURCES / EXPECTED below
and documented in data/README.md. Large datasets (Alibaba trace, MLPerf) are not
fetched here; see data/README.md.
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
import urllib.parse
import urllib.request
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
RAW = REPO_ROOT / "data" / "raw"

# --- Source definitions (see docs/case/challenge.md, "Reference house") -------------

OPEN_METEO = {
    "url": "https://archive-api.open-meteo.com/v1/archive",
    "params": {
        "latitude": "47.56",
        "longitude": "7.59",
        "start_date": "2025-01-01",
        "end_date": "2025-12-31",
        "hourly": "temperature_2m,shortwave_radiation,direct_radiation,diffuse_radiation",
        "timezone": "Europe/Zurich",
        "format": "csv",
    },
    "target": RAW / "open-meteo" / "basel-2025-hourly.csv",
}

ELCOM = {
    "url": "https://www.strompreis.elcom.admin.ch/api/graphql",
    # Operator 624 = IWB Industrielle Werke Basel (found via the operators(query:"IWB") search).
    "query": """{ observations(locale: "de", filters: {period: ["2026"], operator: ["624"], category: ["H4"]}) {
      ... on OperatorObservation {
        period category operator operatorLabel municipality municipalityLabel
        total: value(priceComponent: total)
        energy: value(priceComponent: energy)
        gridusage: value(priceComponent: gridusage)
        charge: value(priceComponent: charge)
        aidfee: value(priceComponent: aidfee)
      } } }""",
    "target": RAW / "elcom" / "iwb-basel-h4-2026.json",
}

# --- Reference-house rules and expected values (from docs/case/challenge.md) -------

HEATING = {
    "design_power_kw": 10.0,     # house heating power at the design point
    "indoor_base_c": 20.0,       # base temperature of the demand formula
    "divisor_k": 25.6,           # fixed constant: 20 - (-5.6 °C coldest hour of 2025)
    "heating_day_mean_below_c": 12.0,
}
HOT_WATER_KWH_PER_DAY = 12.0

EXPECTED = {
    "hours": 8760,
    "heating_days": 206,
    "space_heating_kwh_approx": 27_000,
    "space_heating_tolerance": 0.02,  # "about 27,000" -> accept +-2 %
    "coldest_hour_c": -5.6,
}


def _download(url: str, data: bytes | None = None, headers: dict | None = None) -> bytes:
    req = urllib.request.Request(url, data=data, headers=headers or {})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read()


def fetch_open_meteo() -> None:
    url = OPEN_METEO["url"] + "?" + urllib.parse.urlencode(OPEN_METEO["params"])
    body = _download(url)
    OPEN_METEO["target"].parent.mkdir(parents=True, exist_ok=True)
    OPEN_METEO["target"].write_bytes(body)
    print(f"[fetch] Open-Meteo -> {OPEN_METEO['target'].relative_to(REPO_ROOT)} ({len(body)} bytes)")


def fetch_elcom() -> None:
    payload = json.dumps({"query": ELCOM["query"]}).encode()
    body = _download(ELCOM["url"], data=payload, headers={"content-type": "application/json"})
    result = json.loads(body)
    if "errors" in result:
        raise RuntimeError(f"ElCom API error: {result['errors']}")
    snapshot = {
        "source": ELCOM["url"],
        "query": " ".join(ELCOM["query"].split()),
        "retrieved_at_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "unit": "Rp./kWh (CHF cents per kWh); 'total' price component as published by ElCom "
                "(VAT treatment: verify on strompreis.elcom.admin.ch)",
        "response": result,
    }
    ELCOM["target"].parent.mkdir(parents=True, exist_ok=True)
    ELCOM["target"].write_text(json.dumps(snapshot, indent=2, ensure_ascii=False) + "\n")
    print(f"[fetch] ElCom -> {ELCOM['target'].relative_to(REPO_ROOT)}")


def read_open_meteo(path: Path) -> list[tuple[str, float]]:
    """Return (local time ISO string, temperature_2m in °C) per hour."""
    lines = path.read_text().splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("time,"))
    reader = csv.reader(lines[start:])
    header = next(reader)
    t_col = next(i for i, h in enumerate(header) if h.startswith("temperature_2m"))
    return [(row[0], float(row[t_col])) for row in reader if row]


def check_open_meteo() -> bool:
    rows = read_open_meteo(OPEN_METEO["target"])
    days: dict[str, list[float]] = defaultdict(list)
    for t, temp in rows:
        days[t[:10]].append(temp)
    heating_days = [d for d, v in days.items() if sum(v) / len(v) < HEATING["heating_day_mean_below_c"]]
    space_kwh = sum(
        max(0.0, HEATING["design_power_kw"] * (HEATING["indoor_base_c"] - temp) / HEATING["divisor_k"])
        for d in heating_days
        for temp in days[d]
    )
    coldest = min(temp for _, temp in rows)
    hot_water_kwh = HOT_WATER_KWH_PER_DAY * len(days)

    exp = EXPECTED
    checks = [
        ("hours", len(rows), len(rows) == exp["hours"]),
        ("24 values per local day", "all" if all(len(v) == 24 for v in days.values()) else "NO",
         all(len(v) == 24 for v in days.values())),
        ("coldest hour (°C)", coldest, abs(coldest - exp["coldest_hour_c"]) < 1e-9),
        ("heating days", len(heating_days), len(heating_days) == exp["heating_days"]),
        ("space heating (kWh/yr)", round(space_kwh),
         abs(space_kwh / exp["space_heating_kwh_approx"] - 1) <= exp["space_heating_tolerance"]),
    ]
    ok = True
    for name, value, passed in checks:
        ok &= passed
        print(f"[check] {'OK  ' if passed else 'FAIL'} {name}: {value}")
    print(f"[info]  hot water (kWh/yr): {hot_water_kwh:.0f}; total demand (kWh/yr): {space_kwh + hot_water_kwh:.0f}")
    return ok


def check_elcom() -> bool:
    snapshot = json.loads(ELCOM["target"].read_text())
    obs = snapshot["response"]["data"]["observations"]
    basel = [o for o in obs if o["municipalityLabel"] == "Basel"]
    passed = len(basel) == 1 and basel[0]["total"] > 0
    if passed:
        total = basel[0]["total"]
        print(f"[check] OK   ElCom IWB Basel H4 2026 total: {total} Rp./kWh "
              f"(heat value = price / 2.5 = {total / 2.5:.2f} Rp./kWh)")
    else:
        print("[check] FAIL ElCom snapshot has no unique Basel observation")
    return passed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="only validate existing files, do not download")
    args = parser.parse_args()

    if not args.check:
        fetch_open_meteo()
        fetch_elcom()
    ok = check_open_meteo() & check_elcom()
    official = sorted((RAW / "official").glob("*.csv"))
    if official:
        print(f"[info]  official profile present: {[p.name for p in official]} "
              "(automatic comparison not implemented yet, see data/raw/official/README.md)")
    print("[done]  all checks passed" if ok else "[done]  SOME CHECKS FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
