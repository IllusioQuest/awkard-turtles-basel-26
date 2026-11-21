"""ElCom 2026 electricity price per municipality and household category H1-H8 (T-47).

    python3 scripts/fetch_elcom_municipalities.py          # fetch raw JSON + write the flat CSV
    python3 scripts/fetch_elcom_municipalities.py --csv    # only rebuild the CSV from the raw JSON

Raw: data/raw/elcom/municipalities-2026/<category>.json (API response, unchanged).
Derived: data/derived/elcom/price-per-municipality-2026.csv, one row per municipality and category.
A municipality served by several operators gets the coverage-weighted mean (ElCom `coverageRatio`)
plus min and max. Prices are yearly flat tariffs ('total' component, Rp./kWh as published).
Standard library only.
"""
import csv
import json
import os
import sys
import urllib.request
from datetime import datetime, timezone

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW_DIR = os.path.join(ROOT, "data", "raw", "elcom", "municipalities-2026")
OUT_CSV = os.path.join(ROOT, "data", "derived", "elcom", "price-per-municipality-2026.csv")
URL = "https://www.strompreis.elcom.admin.ch/api/graphql"
PERIOD = "2026"
CATEGORIES = [f"H{i}" for i in range(1, 9)]
QUERY = """{ observations(locale: "de", filters: {period: ["%s"], category: ["%s"]}) {
  ... on OperatorObservation { period category operator operatorLabel municipality municipalityLabel
    canton cantonLabel coverageRatio total: value(priceComponent: total) } } }"""


def fetch():
    os.makedirs(RAW_DIR, exist_ok=True)
    for cat in CATEGORIES:
        q = QUERY % (PERIOD, cat)
        req = urllib.request.Request(URL, data=json.dumps({"query": q}).encode(),
                                     headers={"content-type": "application/json"})
        with urllib.request.urlopen(req, timeout=120) as r:
            result = json.loads(r.read())
        if "errors" in result:
            raise RuntimeError(f"ElCom API error for {cat}: {result['errors']}")
        snap = {"source": URL, "query": " ".join(q.split()),
                "retrieved_at_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
                "unit": "Rp./kWh, 'total' price component as published by ElCom", "response": result}
        with open(os.path.join(RAW_DIR, f"{cat}.json"), "w") as f:
            json.dump(snap, f, ensure_ascii=False, indent=0)
        print(f"[fetch] {cat}: {len(result['data']['observations'])} operator-municipality rows")


def build_csv():
    rows = []
    for cat in CATEGORIES:
        with open(os.path.join(RAW_DIR, f"{cat}.json")) as f:
            obs = json.load(f)["response"]["data"]["observations"]
        by_muni = {}
        for o in obs:
            by_muni.setdefault(o["municipality"], []).append(o)
        for nr, group in by_muni.items():
            cover = [o["coverageRatio"] or 0 for o in group]
            weights = cover if sum(cover) > 0 else [1] * len(group)  # no coverage data: plain mean
            mean = sum(o["total"] * wt for o, wt in zip(group, weights)) / sum(weights)
            prices = [o["total"] for o in group]
            g = group[0]
            rows.append({
                "bfs_municipality_nr": nr, "municipality": g["municipalityLabel"], "canton": g["cantonLabel"],
                "category": cat, "period": PERIOD,
                "price_chf_per_kwh": round(mean / 100, 5),
                "price_min_chf_per_kwh": round(min(prices) / 100, 5),
                "price_max_chf_per_kwh": round(max(prices) / 100, 5),
                "n_operators": len(group),
                "operators": "; ".join(sorted({o["operatorLabel"] for o in group})),
            })
    rows.sort(key=lambda r: (int(r["bfs_municipality_nr"]), r["category"]))
    os.makedirs(os.path.dirname(OUT_CSV), exist_ok=True)
    with open(OUT_CSV, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)
    print(f"[csv] {len(rows)} rows, {len({r['bfs_municipality_nr'] for r in rows})} municipalities -> {OUT_CSV}")
    basel = [r for r in rows if r["bfs_municipality_nr"] == "2701" and r["category"] == "H4"]
    ok = len(basel) == 1 and abs(basel[0]["price_chf_per_kwh"] - 0.3325) < 1e-9
    print(f"[check] {'OK  ' if ok else 'FAIL'} Basel H4 = {basel[0]['price_chf_per_kwh'] if basel else None} "
          "CHF/kWh (case: 0.3325)")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--csv" not in sys.argv:
        fetch()
    sys.exit(build_csv())
