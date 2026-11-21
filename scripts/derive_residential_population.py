"""Extract pinned BFS 2020 counts and prepare a composition-only residential population.

Standard library only. Source counts refer to buildings, not dwellings, people or servers.
The derived node array intentionally is not a HouseFile: it contains no invented thermal data.
"""
import argparse
import csv
from decimal import Decimal, ROUND_FLOOR
import hashlib
import io
import json
from pathlib import Path
import posixpath
from urllib.request import urlopen
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PROFILE = "config/fleet/populations/bfs-residential-2020.json"
XML_NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"


def worksheet_cells(archive):
    """Read just the pinned source's numeric/label cells, without changing the workbook."""
    strings = ["".join(node.itertext()) for node in ET.fromstring(archive.read("xl/sharedStrings.xml"))]
    relationships = {r.attrib["Id"]: r.attrib["Target"]
                     for r in ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))}
    workbook = ET.fromstring(archive.read("xl/workbook.xml"))
    for sheet in workbook.find("s:sheets", XML_NS):
        target = relationships[sheet.attrib[f"{{{REL_NS}}}id"]]
        path = target.lstrip("/") if target.startswith("/") else posixpath.normpath(posixpath.join("xl", target))
        cells = {}
        for cell in ET.fromstring(archive.read(path)).findall(".//s:sheetData/s:row/s:c", XML_NS):
            value = cell.find("s:v", XML_NS)
            if value is not None:
                cells[cell.attrib["r"]] = strings[int(value.text)] if cell.attrib.get("t") == "s" else value.text
        yield sheet.attrib["name"], cells


def extract_table(workbook_path, source):
    records = []
    owners = source["owner_columns"]
    with ZipFile(workbook_path) as archive:
        for sheet, cells in worksheet_cells(archive):
            region = sheet if sheet == "CH" else sheet.split("_", 1)[1]
            total = int(cells["B6"])
            category_total = sum(int(cells[f"B{row}"]) for name, row in source["category_rows"].items() if name != "total")
            if category_total != total:
                raise ValueError(f"{sheet}: category counts do not reconcile to Total")
            for category, row in source["category_rows"].items():
                # Excel's stored binary-float text can be 1.1000000000000001 for a published 1.1%.
                # Use the source's stated display precision, keeping raw text alongside for traceability.
                stored = {owner: cells[f"{column}{row}"] for owner, column in owners.items()}
                percents = {owner: Decimal(value).quantize(Decimal(source["percentage_precision"]))
                            for owner, value in stored.items()}
                percent_sum = sum(percents.values())
                rounding_bound = Decimal(source["percentage_precision"]) / 2 * len(owners)
                if abs(percent_sum - 100) > rounding_bound:
                    raise ValueError(f"{sheet}:{row}: owner percentages exceed source rounding tolerance")
                count = int(cells[f"B{row}"])
                records.append({"region": region, "building_category": category, "building_count": count,
                    "category_share": str(Decimal(count) / total), "published_owner_percent_sum": str(percent_sum),
                    **{f"{owner}_percent": str(value) for owner, value in percents.items()},
                    **{f"{owner}_stored_value": value for owner, value in stored.items()},
                    **{f"{owner}_weight": str(value / percent_sum) for owner, value in percents.items()},
                    "source_sheet": sheet, "source_count_cell": f"B{row}", "source_owner_cells": f"C{row}:G{row}",
                    "source_url": source["asset_url"], "accessed_date": source["accessed_date"],
                    "reference_date": source["reference_date"], "coverage": source["coverage"]})
    return records


def allocate(count, weights):
    """Hamilton quotas; stable source order resolves equal remainders. No repeated random draws."""
    total = sum(weights)
    if count < 0 or not weights or any(weight < 0 for weight in weights) or total <= 0:
        raise ValueError("Population size and weights must be nonnegative, with a positive total weight")
    quotas = [Decimal(count) * weight / total for weight in weights]
    result = [int(quota.to_integral_value(rounding=ROUND_FLOOR)) for quota in quotas]
    order = sorted(range(len(weights)), key=lambda i: (-(quotas[i] - result[i]), i))
    for index in order[:count - sum(result)]:
        result[index] += 1
    return result


def population(records, profile, source):
    rows = [r for r in records if r["region"] == profile["region"] and r["building_category"] != "total"]
    if len(rows) != len(source["category_rows"]) - 1:
        raise ValueError("Region is outside source coverage or has incomplete building categories")
    owners = list(source["owner_columns"])
    category_counts = allocate(profile["node_count"], [Decimal(r["building_count"]) for r in rows])
    nodes, summary = [], []
    for row, category_count in zip(rows, category_counts):
        owner_counts = allocate(category_count, [Decimal(row[f"{owner}_percent"]) for owner in owners])
        for owner, owner_count in zip(owners, owner_counts):
            summary.append({"region": profile["region"], "building_category": row["building_category"],
                "owner_type": owner, "synthetic_count": owner_count, "requested_population": profile["node_count"],
                "source_category_count": row["building_count"], "published_conditional_owner_percent": row[f"{owner}_percent"],
                "normalization_percent_sum": row["published_owner_percent_sum"], "source_url": source["asset_url"],
                "reference_date": source["reference_date"], "accessed_date": source["accessed_date"], "coverage": source["coverage"]})
            for _ in range(owner_count):
                nodes.append({"node_id": f"synthetic-residential-{len(nodes) + 1:05}",
                    "building_category": row["building_category"], "owner_type": owner})
    return nodes, summary


def csv_bytes(rows):
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=list(rows[0]), lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", default=DEFAULT_PROFILE)
    parser.add_argument("--download", action="store_true", help="Download the pinned source only when absent")
    parser.add_argument("--check", action="store_true", help="Reproduce outputs in memory; do not rewrite them")
    args = parser.parse_args()
    profile = json.loads((ROOT / args.profile).read_text(encoding="utf-8"))
    source_path = ROOT / profile["source_manifest"]
    source = json.loads(source_path.read_text(encoding="utf-8"))
    workbook = source_path.parent / source["local_file"]
    if args.download and not workbook.exists():
        with urlopen(source["asset_url"], timeout=30) as response:
            content = response.read()
        if hashlib.sha256(content).hexdigest() != source["sha256"]:
            raise ValueError("Downloaded source differs from pinned hash; review the source before updating it")
        workbook.write_bytes(content)
    if hashlib.sha256(workbook.read_bytes()).hexdigest() != source["sha256"]:
        raise ValueError("Source workbook differs from pinned hash")
    records = extract_table(workbook, source)
    nodes, summary = population(records, profile, source)
    node_json = "[\n" + ",\n".join("  " + json.dumps(node) for node in nodes) + "\n]\n"
    outputs = {profile["calibration_csv"]: csv_bytes(records), profile["nodes_output"]: node_json.encode(),
               profile["summary_output"]: csv_bytes(summary)}
    for relative, content in outputs.items():
        path = ROOT / relative
        if args.check:
            if not path.exists() or path.read_bytes() != content:
                raise ValueError(f"Derived file is missing or out of date: {relative}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    print(f"{'Checked' if args.check else 'Generated'} {len(records)} source rows and {len(nodes)} synthetic residential buildings; composition only")


if __name__ == "__main__":
    main()
