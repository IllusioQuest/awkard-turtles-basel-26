#!/usr/bin/env python3
"""Write compact figure data for the LaTeX paper from the network experiment outputs (T-12).

Usage: python3 scripts/paper_figure_data.py
Reads  data/derived/network/experiments/*.csv (the "-lp" files when present, they include the
       greedy rows too) and writes docs/paper/latex/figures/data/*.dat (space-separated, no quoting)
       for pgfplots. Values are copied, not recomputed. Standard library only.
"""

from __future__ import annotations

import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EXP = ROOT / "data" / "derived" / "network" / "experiments"
OUT = ROOT / "docs" / "paper" / "latex" / "figures" / "data"


def rows(experiment: str) -> list[dict]:
    lp = EXP / f"{experiment}-lp.csv"
    path = lp if lp.exists() else EXP / f"{experiment}.csv"
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def pick(rs: list[dict], strategy: str, **match: str) -> dict | None:
    for r in rs:
        if r["strategy"] == strategy and all(r[k] == v for k, v in match.items()):
            return r
    return None


def num(r: dict | None, key: str, scale: float = 1.0) -> str:
    if r is None or r[key] in ("", None):
        return "nan"
    return f"{float(r[key]) * scale:.4f}"


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    a = rows("A_type1_share")
    lines = ["share B1missed B2missed LPmissed B2moved B2Rmoved LPmoved"]
    for share in ("0.1", "0.3", "0.5", "0.7"):
        b1, b2, b2r, lp = (pick(a, s, type1Share=share) for s in ("B1", "B2", "B2-reserve", "LP"))
        lines.append(" ".join([str(float(share) * 100),
                               num(b1, "type1MissedDeadlinePercent"), num(b2, "type1MissedDeadlinePercent"),
                               num(lp, "type1MissedDeadlinePercent"),
                               num(b2, "type1MovedServerHours", 1e-3), num(b2r, "type1MovedServerHours", 1e-3),
                               num(lp, "type1MovedServerHours", 1e-3)]))
    (OUT / "a_type1_share.dat").write_text("\n".join(lines) + "\n", encoding="utf-8")

    d = rows("D_network_mix")
    lines = ["network B1missed B2missed LPmissed"]
    for label, excl in (("all", ""), ("residential", "hotel-spa,indoor-pool")):
        b1, b2, lp = (pick(d, s, excludeKinds=excl) for s in ("B1", "B2", "LP"))
        lines.append(" ".join([label, num(b1, "type1MissedDeadlinePercent"),
                               num(b2, "type1MissedDeadlinePercent"), num(lp, "type1MissedDeadlinePercent")]))
    (OUT / "d_network_mix.dat").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
