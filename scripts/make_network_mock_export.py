#!/usr/bin/env python3
"""MOCK export in the map data format of decision 0025 §7, for building the map before the network
engine's exporter exists. File and field reference: docs/model/network-map-data.md.

NOT A RESULT. The numbers are a deliberately crude stand-in so the map can be laid out:
no tank, no scheduler, Type 1 volume spread naively. The real export comes from the
network engine (DigitalHeat.NetworkRun); delete mock/ once it exists. Every run.json says
"mock": true and the map shows a banner.

Usage (repository root, offline, standard library):
  python scripts/make_network_mock_export.py
Writes data/derived/network/switzerland-test/mock-export/{B1,B2}/{run,sites,site-hours,flows,summary}.json
"""
import csv
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "config" / "network" / "switzerland-test.json"
OUT = ROOT / "data" / "derived" / "network" / "switzerland-test" / "mock-export"
HOURS = 8760


def demand_of(site):
    d = site["demand"]
    if d["model"] == "file":
        rows = list(csv.DictReader((ROOT / d["file"]).open(encoding="utf-8")))
        return [r["time"] for r in rows], [float(r["space_heat_kwh"]) + float(r["hot_water_kwh"]) for r in rows]
    # case-linear: reuse the Basel single-family file's space heat (same formula and weather) + 0.5 kWh/h hot water
    rows = list(csv.DictReader((ROOT / "data/derived/network/switzerland-test/basel-sfh-1-demand.csv").open(encoding="utf-8")))
    return [r["time"] for r in rows], [float(r["space_heat_kwh"]) + 0.5 for r in rows]


def km(a, b):
    la1, lo1, la2, lo2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    return 6371 * math.acos(min(1, math.sin(la1) * math.sin(la2) + math.cos(la1) * math.cos(la2) * math.cos(lo1 - lo2)))


def main():
    cfg = json.loads(CONFIG.read_text(encoding="utf-8"))
    sites = cfg["sites"]
    times, series = None, {}
    for s in sites:
        times, series[s["id"]] = demand_of(s)
    pos = {s["id"]: (s["latitudeDeg"]["value"], s["longitudeDeg"]["value"]) for s in sites}
    cap = {s["id"]: s["serverCount"]["value"] * 0.8 for s in sites}
    sinks = [s["id"] for s in sites if s["kind"] in ("indoor-pool", "hotel-spa")]

    for strategy in ("B1", "B2"):
        hours = {}
        flows = []
        moved = missed_total = due_total = 0.0
        for s in sites:
            sid, d = s["id"], series[s["id"]]
            load = [min(1.0, x / cap[sid]) for x in d]
            used = [min(x, cap[sid]) for x in d]
            hours[sid] = {"demandKwh": d, "usedKwh": used, "dumpedKwh": [0.0] * HOURS,
                          "backupKwh": [x - u for x, u in zip(d, used)], "tankKwh": [0.0] * HOURS,
                          "loadShare": load, "type1ServerHours": [0.0] * HOURS,
                          "type2ServerHours": [l * s["serverCount"]["value"] for l in load],
                          "missedServerHours": [0.0] * HOURS}
        for s in sites:
            if "type1" not in s:
                continue
            sid, h = s["id"], hours[s["id"]]
            daily = 0.3 * sum(h["type2ServerHours"]) / 365
            for day in range(365):
                rng = range(day * 24, day * 24 + 24)
                free = sum(h["type2ServerHours"][i] for i in rng)
                local = min(daily, free)
                for i in rng:
                    share = h["type2ServerHours"][i] / free if free else 0
                    h["type1ServerHours"][i] += local * share
                    h["type2ServerHours"][i] -= local * share
                rest = daily - local
                due_total += daily
                if rest <= 1e-9:
                    continue
                if strategy == "B2":
                    for sink in sorted(sinks, key=lambda x: km(pos[sid], pos[x])):
                        g = hours[sink]
                        avail = sum(g["type2ServerHours"][i] for i in rng)
                        take = min(rest, avail)
                        if take <= 0:
                            continue
                        for i in rng:
                            part = take * (g["type2ServerHours"][i] / avail)
                            g["type2ServerHours"][i] -= part
                            g["type1ServerHours"][i] += part
                            if part > 1e-6:
                                flows.append([i, sid, sink, "type1", round(part, 3)])
                        moved += take
                        rest -= take
                        if rest <= 1e-9:
                            break
                h["missedServerHours"][day * 24 + 23] += rest
                missed_total += rest

        out = OUT / strategy
        out.mkdir(parents=True, exist_ok=True)
        r3 = lambda xs: [int(v) if v == int(v) else v for v in (round(x, 3) for x in xs)]  # noqa: E731
        sha = hashlib.sha256(CONFIG.read_bytes()).hexdigest()
        (out / "run.json").write_text(json.dumps({
            "mock": True, "note": "MOCK data for map layout only; not a scheduler result.",
            "scenario": cfg["id"], "strategy": strategy,
            "strategyLabel": {"B1": "B1 local greedy (mock)", "B2": "B2 network greedy (mock)"}[strategy],
            "gitCommit": None, "forecast": {"label": "none (mock)", "sigma24": None, "rho": None, "seed": None}, "fillRate": 1, "type1Share": 0.3,
            "horizonHours": None, "inputs": [{"path": "config/network/switzerland-test.json", "sha256": sha}],
        }, indent=2), encoding="utf-8")
        (out / "sites.json").write_text(json.dumps({"scenario": cfg["id"], "timezone": cfg["timezone"],
                                                    "sites": sites}, ensure_ascii=False), encoding="utf-8")
        (out / "site-hours.json").write_text(json.dumps({
            "timeAxis": {"start": times[0], "hours": HOURS, "stepHours": 1, "timezone": cfg["timezone"]},
            "sites": {sid: {k: r3(v) for k, v in h.items()} for sid, h in hours.items()}}), encoding="utf-8")
        (out / "flows.json").write_text(json.dumps({
            "columns": ["hour", "fromSiteId", "toSiteId", "type", "serverHours"], "rows": flows}), encoding="utf-8")
        demand = sum(sum(h["demandKwh"]) for h in hours.values())
        used = sum(sum(h["usedKwh"]) for h in hours.values())
        sh = sum(sum(h["type1ServerHours"]) + sum(h["type2ServerHours"]) for h in hours.values())
        servers = sum(s["serverCount"]["value"] for s in sites)
        (out / "summary.json").write_text(json.dumps({"strategy": strategy, "mock": True, "network": {
            "coverage": used / demand, "dumpedHeatKwh": 0.0, "utilisation": sh / (servers * HOURS),
            "serverHours": sh, "missedDeadlinePercent": 100 * missed_total / due_total if due_total else None,
            "movedServerHours": moved},
            "sites": [{"siteId": sid, "coverage": (sum(h["usedKwh"]) / sum(h["demandKwh"])) if sum(h["demandKwh"]) else None,
                       "dumpedHeatKwh": sum(h["dumpedKwh"]), "serverHours": sum(h["type1ServerHours"]) + sum(h["type2ServerHours"]),
                       "type1ServerHours": sum(h["type1ServerHours"]), "missedServerHours": sum(h["missedServerHours"])}
                      for sid, h in hours.items()]}, indent=2), encoding="utf-8")
        print(strategy, f"flows={len(flows)} moved={moved:.0f} missed%={100 * missed_total / due_total:.1f}")


if __name__ == "__main__":
    main()
