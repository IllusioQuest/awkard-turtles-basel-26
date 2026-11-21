#!/usr/bin/env python3
"""Run the shared C# year model for sourced reference-house server-count variants."""

import argparse
import csv
import json
import math
import shutil
import subprocess
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INPUTS = (
    "data/raw/open-meteo/basel-2025-hourly.csv",
    "data/raw/elcom/iwb-basel-h4-2026.json",
    "config/job-scenarios/unlimited.json",
)


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n",
                    encoding="utf-8")


def validate_server_count(parameter):
    value = parameter.get("value")
    if (not isinstance(value, int) or isinstance(value, bool) or value <= 0
            or parameter.get("unit") != "servers" or not parameter.get("source")):
        raise ValueError("Each variant needs a positive integer server count, unit and source")


def cost_row(rows, row_id, expected_unit):
    row = rows.get(row_id)
    if row is None or row["normalised_unit"] != expected_unit or not row["source_url"]:
        raise ValueError(f"Missing or incompatible WP2 cost row: {row_id}")
    return row, float(row["normalised_value"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help=".NET 8 executable")
    options = parser.parse_args()
    base_parameters = read_json(ROOT / "config/reference-house.json")
    report_config = read_json(ROOT / "config/reference-house-results.json")
    variants = [{"id": "explorer-default", "serverCount": base_parameters["ServerCount"]}]
    variants.extend(report_config["variants"])
    if len({v["id"] for v in variants}) != len(variants):
        raise ValueError("Variant IDs must be unique")
    if len({v["serverCount"]["value"] for v in variants}) != len(variants):
        raise ValueError("Variant server counts must be unique")
    for variant in variants:
        validate_server_count(variant["serverCount"])

    with (ROOT / "data/raw/market/costs.csv").open(newline="", encoding="utf-8") as handle:
        costs = {row["id"]: row for row in csv.DictReader(handle)}
    hardware = report_config["hardwareIllustration"]
    price_row, price_chf = cost_row(costs, hardware["purchaseCostRowId"], "CHF per server")
    life_row, life_years = cost_row(costs, hardware["accountingLifeRowId"], "years")
    if not math.isfinite(price_chf) or price_chf < 0 or not math.isfinite(life_years) or life_years <= 0:
        raise ValueError("Invalid WP2 hardware illustration values")

    output_dir = ROOT / "data/derived/reference-house"
    for variant in variants:
        with tempfile.TemporaryDirectory(prefix="digitalheat-reference-") as directory:
            temporary_root = Path(directory)
            for relative in INPUTS:
                target = temporary_root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ROOT / relative, target)
            parameters = json.loads(json.dumps(base_parameters))
            parameters["ServerCount"] = variant["serverCount"]
            write_json(temporary_root / "config/reference-house.json", parameters)
            command = [options.dotnet, "run", "--project", str(ROOT / "src/DigitalHeat.YearRun"),
                       "--", str(temporary_root)]
            run = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, check=True)
            print(f"{variant['id']}: {run.stdout.strip().splitlines()[-1]}")
            result = read_json(temporary_root / "results/reference-house/summary.json")

        summary = result["Summary"]
        if summary["Jobs"] is not None:
            raise ValueError("Reference comparison must remain an explicitly unlimited-job run")
        server_hours = summary["ServerHours"]
        annual_depreciation_chf = variant["serverCount"]["value"] * price_chf / life_years
        case_threshold = summary["BreakEvenChfPerServerHour"]
        illustration = {
            "Status": "illustrative partial cost, not a full-cost result",
            "Purpose": hardware["purpose"],
            "PurchaseCost": {"RowId": hardware["purchaseCostRowId"], "ValueChfPerServer": price_chf,
                             "SourceUrl": price_row["source_url"], "Accessed": price_row["accessed"]},
            "AccountingLife": {"RowId": hardware["accountingLifeRowId"], "Years": life_years,
                               "SourceUrl": life_row["source_url"], "Accessed": life_row["accessed"]},
            "AnnualDepreciationChf": annual_depreciation_chf,
            "BreakEvenWithThisDepreciationOnlyChfPerServerHour":
                case_threshold + annual_depreciation_chf / server_hours if server_hours else None,
            "SystemSurplusAfterThisDepreciationOnlyChf":
                summary["ComputingIncomeChf"] + summary["HeatValueChf"]
                - summary["ElectricityCostChf"] - annual_depreciation_chf,
        }
        result["Variant"] = {"Id": variant["id"], "Status": "illustrative, not team-selected",
                             "ServerCount": variant["serverCount"]}
        result["Inputs"]["VariantConfig"] = "config/reference-house-results.json"
        result["Inputs"]["HardwareCostResearch"] = "data/raw/market/costs.csv"
        result["HardwareIllustration"] = illustration
        result["FullCost"] = {
            "Status": "unknown: no comparable hardware system, complete installation or operations quote",
            "Formula": "(electricity - used-heat value + annual hardware depreciation + annual operations + annualized installation) / sold server-hours",
            "BreakEvenChfPerServerHour": None,
        }
        write_json(output_dir / f"{variant['id']}.json", result)


if __name__ == "__main__":
    main()
