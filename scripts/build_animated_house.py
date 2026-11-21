#!/usr/bin/env python3
"""Embed two T-13 C# export days in a single offline T-25 browser file."""

import argparse
import csv
import json
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BUNDLE = ROOT / "data/derived/reference-house-v1"
TEMPLATE = ROOT / "prototypes/animated-house/index.template.html"
DEFAULT_OUTPUT = ROOT / "prototypes/animated-house/index.html"
# T-25 and docs/pitch/storyboard.md specify these two demo days.
DAYS = ("2025-01-15", "2025-07-15")
REQUIRED_COLUMNS = (
    "heat_demand_kwh", "space_heat_kwh", "hot_water_kwh",
    "target_load_per_server", "actual_load_per_server", "produced_heat_kwh",
    "direct_used_kwh", "tank_charge_kwh", "tank_discharge_kwh",
    "tank_stored_kwh", "dumped_heat_kwh", "backup_heat_kwh",
)


def load_payload(bundle: Path) -> dict:
    summary = json.loads((bundle / "summary.json").read_text(encoding="utf-8"))
    parameters = json.loads((bundle / "parameters.json").read_text(encoding="utf-8"))
    run = json.loads((bundle / "run.json").read_text(encoding="utf-8"))
    if any(item.get("schema_version") != 1 for item in (summary, parameters, run)):
        raise ValueError("T-25 needs result bundle schema_version 1")
    sites = summary.get("sites", [])
    if len(sites) != 1 or sites[0].get("site_id") != "reference-house":
        raise ValueError("T-25 needs the reference-house site")
    relative_csv = sites[0]["hourly_path"]
    csv_path = (bundle / relative_csv).resolve()
    if not csv_path.is_relative_to(bundle.resolve()):
        raise ValueError("Hourly path leaves the result bundle")
    selected = {day: [] for day in DAYS}
    with csv_path.open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        if not set(REQUIRED_COLUMNS + ("local_time",)).issubset(reader.fieldnames or []):
            raise ValueError("Hourly export lacks required T-25 columns")
        for row in reader:
            day = row["local_time"][:10]
            if day not in selected:
                continue
            hour = {"local_time": row["local_time"]}
            for name in REQUIRED_COLUMNS:
                value = float(row[name])
                if not math.isfinite(value) or value < 0:
                    raise ValueError(f"Invalid {name} at {row['local_time']}")
                hour[name] = value
            selected[day].append(hour)
    for day, hours in selected.items():
        if len(hours) != 24 or [hour["local_time"][11:] for hour in hours] != [
            f"{index:02d}:00" for index in range(24)
        ]:
            raise ValueError(f"Expected the 24 nominal local hours for {day}")
    capacity = parameters["reference_parameters"]["tank_capacity_kwh"]
    server_count = parameters["reference_parameters"]["server_count"]
    if capacity["unit"] != "kWh" or server_count["unit"] != "servers":
        raise ValueError("Unexpected reference parameter units")
    try:
        source_csv = csv_path.relative_to(ROOT).as_posix()
    except ValueError:
        source_csv = f"{bundle.name}/{relative_csv}"
    return {
        "schema_version": 1,
        "site_id": sites[0]["site_id"],
        "timezone": run["timezone"],
        "source_commit": run.get("git_commit"),
        "source_csv": source_csv,
        "tank_capacity_kwh": capacity["value"],
        "server_count": server_count["value"],
        "days": selected,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, default=DEFAULT_BUNDLE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    payload = load_payload(args.bundle.resolve())
    template = TEMPLATE.read_text(encoding="utf-8")
    marker = "/*__EMBEDDED_C_SHARP_EXPORT__*/"
    if template.count(marker) != 1:
        raise ValueError("HTML template must contain exactly one export marker")
    embedded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(template.replace(marker, embedded), encoding="utf-8")
    print(f"Wrote {args.output} with {len(DAYS)} days / 48 exported hours")


if __name__ == "__main__":
    main()
