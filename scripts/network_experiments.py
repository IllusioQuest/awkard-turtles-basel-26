#!/usr/bin/env python3
"""Network scheduler experiments (T-12, decisions 0024/0025/0029).

Runs DigitalHeat.NetworkRun on config/network/switzerland-test.json and writes one CSV + JSONL per
experiment to data/derived/network/experiments/. Every run is a labelled SCENARIO: the Swiss sites
are synthetic test data and the Type 1 volume is a share of heat-led server-hours (decision 0025 §6).

Usage (repository root, .NET 8 SDK):
    python3 scripts/network_experiments.py            # greedy strategies only (seconds)
    python3 scripts/network_experiments.py --lp       # also full-year LP runs (≈ 45-60 min each)
    python3 scripts/network_experiments.py --lp --jobs 3

Experiments:
    A  Type 1 volume sweep (share 0.1/0.3/0.5/0.7): missed deadlines B1 vs B2 (vs LP)
    B  filler availability phi (1.0/0.6/0.35/0.2) at share 0.3: B1, B2
    C  forecast error sigma24 (0/0.05/0.10/0.20, rho 0.8, fixed seed) for the LP at share 0.3
    D  network mix: all sites vs residential only (no hotels/pools) at share 0.3, incl. reserve controls
"""

from __future__ import annotations

import argparse
import concurrent.futures
import csv
import json
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENARIO = "config/network/switzerland-test.json"
OUT = ROOT / "data" / "derived" / "network" / "experiments"
BINARY = ROOT / "src" / "DigitalHeat.NetworkRun" / "bin" / "Release" / "net8.0" / "DigitalHeat.NetworkRun"
SEED = 20251121
RHO = 0.8
GREEDY = "B1,B1-reserve,B2,B2-reserve"
RESIDENTIAL_ONLY = "hotel-spa,indoor-pool"


def runs(with_lp: bool) -> list[tuple[str, list[str]]]:
    """(experiment id, NetworkRun arguments) for every run."""
    r: list[tuple[str, list[str]]] = []
    for share in (0.1, 0.3, 0.5, 0.7):
        r.append(("A_type1_share", ["--strategy", GREEDY, "--type1-share", str(share)]))
        if with_lp:
            r.append(("A_type1_share", ["--strategy", "LP", "--type1-share", str(share)]))
    for phi in (1.0, 0.6, 0.35, 0.2):
        r.append(("B_phi", ["--strategy", GREEDY, "--type1-share", "0.3", "--phi", str(phi)]))
    if with_lp:
        for sigma in (0.05, 0.10, 0.20):  # sigma 0 = perfect foresight = experiment A at share 0.3
            r.append(("C_forecast_error", ["--strategy", "LP", "--type1-share", "0.3",
                                           "--sigma24", str(sigma), "--rho", str(RHO), "--seed", str(SEED)]))
    r.append(("D_network_mix", ["--strategy", GREEDY, "--type1-share", "0.3"]))
    r.append(("D_network_mix", ["--strategy", GREEDY, "--type1-share", "0.3", "--exclude-kinds", RESIDENTIAL_ONLY]))
    if with_lp:
        r.append(("D_network_mix", ["--strategy", "LP", "--type1-share", "0.3", "--exclude-kinds", RESIDENTIAL_ONLY]))
    return r


def run_one(exp: str, args: list[str]) -> list[dict]:
    with tempfile.TemporaryDirectory() as tmp:
        summary = Path(tmp) / "summary.jsonl"
        cmd = [str(BINARY), ".", "--scenario", SCENARIO, *args, "--summary-json", str(summary)]
        subprocess.run(cmd, cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
        rows = [json.loads(line) for line in summary.read_text().splitlines() if line.strip()]
    for row in rows:
        row["experiment"] = exp
    print(f"[done] {exp} {' '.join(args)}", flush=True)
    return rows


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--lp", action="store_true", help="include full-year LP runs (slow)")
    ap.add_argument("--jobs", type=int, default=1, help="parallel runs (LP runs are single-threaded)")
    a = ap.parse_args()

    subprocess.run(["dotnet", "build", "-c", "Release", "src/DigitalHeat.NetworkRun"], cwd=ROOT, check=True,
                   stdout=subprocess.DEVNULL)
    commit = subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, capture_output=True,
                            text=True).stdout.strip()
    todo = runs(a.lp)
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, a.jobs)) as pool:
        results = [row for rows in pool.map(lambda t: run_one(*t), todo) for row in rows]

    OUT.mkdir(parents=True, exist_ok=True)
    fields = ["experiment", "strategy", "forecast", "type1Share", "phi", "excludeKinds", "sites", "coverage",
              "dumpedHeatKwh", "serverHours", "utilisation", "type1MissedDeadlinePercent", "type1Arrived",
              "type1Missed", "type1MovedServerHours", "runtimeSeconds", "command"]
    for exp in sorted({r["experiment"] for r in results}):
        rows = [r for r in results if r["experiment"] == exp]
        suffix = "-lp" if a.lp else ""
        with open(OUT / f"{exp}{suffix}.csv", "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
            w.writeheader()
            for r in rows:
                w.writerow({**r, "excludeKinds": ",".join(r.get("excludeKinds") or [])})
        with open(OUT / f"{exp}{suffix}.jsonl", "w") as f:
            for r in rows:
                f.write(json.dumps({**r, "gitCommit": commit, "scenarioLabel": "synthetic test network, scenario"}) + "\n")
    print(f"wrote {len(results)} rows to {OUT.relative_to(ROOT)} (commit {commit})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
