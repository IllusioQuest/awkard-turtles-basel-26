#!/usr/bin/env python3
"""Snapshot Azure Switzerland North VM prices and derive CHF per 1 kW server-hour (ticket T-28).

Usage:
    python3 scripts/fetch_prices.py                    # fetch a new dated snapshot, derive from it
    python3 scripts/fetch_prices.py --derive-only      # derive from the latest existing snapshot
    python3 scripts/fetch_prices.py --snapshot PATH    # derive from a pinned snapshot (no network)
    python3 scripts/fetch_data.py --prices             # same as the first line

Method (docs/research/computing-price.md §2, §3 A1, A2, C1), parameters in
config/computing-price-method.json, server power from config/reference-house.json:

    instance_power_w            = vcpu x WattsPerVcpuAtFullLoad + ram_gb x WattsPerGbMemory
    instances_per_server        = ServerElectricKw x WattsPerKw / instance_power_w
    price_chf_per_server_hour   = instances_per_server x price_usd_per_hour x UsdToChf
    A1 / A2                     = lowest on-demand / spot value over the configured sizes
    home_host_ratio             = min over pairs (community price / datacentre price)
    C1 (market-tracking value)  = A2 x home_host_ratio

Snapshots are never overwritten. The derived file records the snapshot (path, SHA-256,
retrieval time), so any figure can be reproduced from it. The adopted scored value lives in
config/reference-house.json (owner T-06) and is not changed here.
Standard library only.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

REPO_ROOT = Path(__file__).resolve().parent.parent
METHOD_FILE = REPO_ROOT / "config" / "computing-price-method.json"
HOUSE_FILE = REPO_ROOT / "config" / "reference-house.json"
COMPUTE_CSV = REPO_ROOT / "data" / "raw" / "market" / "compute-prices.csv"
SNAPSHOT_DIR = REPO_ROOT / "data" / "raw" / "market" / "price-snapshots"
DERIVED_FILE = REPO_ROOT / "data" / "derived" / "computing-price.json"
SNAPSHOT_PREFIX = "azure-"
OUTPUT_DIGITS = 4          # decimals of derived values in the output (published prices are kept as is)
RAW_PRICE_SUFFIX = "_usd_per_hour"
MAX_PAGES = 50             # safety stop for NextPageLink pagination
HTTP_TIMEOUT_S = 60

SPOT_MARK = " Spot"
LOW_PRIORITY_MARK = "Low Priority"
LINUX_PRODUCT_PREFIX = "Virtual Machines "
WINDOWS_PRODUCT_SUFFIX = " Windows"


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return str(path)


def load_method(path: Path = METHOD_FILE) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_server_kw(path: Path = HOUSE_FILE) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))["ServerElectricKw"]


def load_compute_rows(path: Path = COMPUTE_CSV) -> dict[str, dict]:
    with path.open(newline="") as f:
        return {row["id"]: row for row in csv.DictReader(f)}


# --- Fetch ---------------------------------------------------------------------------

def build_filter(method: dict) -> str:
    skus = " or ".join(f"armSkuName eq '{i['armSkuName']}'" for i in method["Instances"]["value"])
    return (f"serviceName eq 'Virtual Machines' and armRegionName eq '{method['AzureRegion']['value']}' "
            f"and priceType eq 'Consumption' and ({skus})")


def _get_json(url: str) -> dict:
    with urllib.request.urlopen(url, timeout=HTTP_TIMEOUT_S) as resp:
        return json.loads(resp.read())


def fetch_items(method: dict, get_json: Callable[[str], dict] = _get_json) -> tuple[str, list[dict], int]:
    """Return (first request URL, all items over all pages, page count). Follows NextPageLink."""
    query = {"$filter": build_filter(method), "currencyCode": method["AzureCurrency"]["value"]}
    first_url = method["AzureRetailPricesUrl"]["value"] + "?" + urllib.parse.urlencode(query)
    url, items, pages = first_url, [], 0
    while url:
        pages += 1
        if pages > MAX_PAGES:
            raise RuntimeError(f"more than {MAX_PAGES} pages; check the filter")
        page = get_json(url)
        items.extend(page.get("Items", []))
        url = page.get("NextPageLink")
    return first_url, items, pages


def write_snapshot(method: dict, first_url: str, items: list[dict], pages: int,
                   now: datetime | None = None, directory: Path = SNAPSHOT_DIR) -> Path:
    """Write a new dated snapshot; never overwrite an existing file."""
    now = now or datetime.now(timezone.utc)
    region = method["AzureRegion"]["value"]
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / f"{SNAPSHOT_PREFIX}{region}-{now:%Y-%m-%d}.json"
    if target.exists():
        target = directory / f"{SNAPSHOT_PREFIX}{region}-{now:%Y-%m-%d}-{now:%H%M%S}Z.json"
    if target.exists():
        raise FileExistsError(target)
    snapshot = {
        "source": method["AzureRetailPricesUrl"]["value"],
        "request_url": first_url,
        "filter": build_filter(method),
        "currency": method["AzureCurrency"]["value"],
        "retrieved_at_utc": now.isoformat(timespec="seconds"),
        "pages": pages,
        "items": sorted(items, key=lambda i: (i.get("armSkuName", ""), i.get("productName", ""),
                                              i.get("meterName", ""), i.get("skuId", ""))),
    }
    target.write_text(json.dumps(snapshot, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return target


def latest_snapshot(directory: Path = SNAPSHOT_DIR) -> Path:
    files = sorted(directory.glob(f"{SNAPSHOT_PREFIX}*.json"))
    if not files:
        raise FileNotFoundError(f"no snapshot in {rel(directory)}; run without --derive-only")
    return max(files, key=lambda p: json.loads(p.read_text(encoding="utf-8"))["retrieved_at_utc"])


# --- Derive (pure) -------------------------------------------------------------------

def linux_price_usd(items: list[dict], arm_sku: str, spot: bool) -> dict:
    """The single Linux VM consumption item for a size: on-demand or Spot (Low Priority excluded)."""
    matches = [
        i for i in items
        if i.get("armSkuName") == arm_sku
        and i.get("type") == "Consumption"
        and i.get("productName", "").startswith(LINUX_PRODUCT_PREFIX)
        and not i.get("productName", "").endswith(WINDOWS_PRODUCT_SUFFIX)
        and LOW_PRIORITY_MARK not in i.get("meterName", "")
        and i.get("meterName", "").endswith(SPOT_MARK) == spot
    ]
    if len(matches) != 1:
        kind = "spot" if spot else "on-demand"
        raise ValueError(f"expected 1 Linux {kind} item for {arm_sku}, found {len(matches)}")
    return matches[0]


def home_host_ratio(method: dict, compute_rows: dict[str, dict]) -> dict:
    ratios = []
    for pair in method["HomeHostRatioPairs"]["value"]:
        community = compute_rows[pair["community_id"]]
        datacentre = compute_rows[pair["datacentre_id"]]
        if community["currency"] != datacentre["currency"]:
            raise ValueError(f"currency mismatch in ratio pair {pair}")
        ratios.append({**pair, "community_value": float(community["value"]),
                       "datacentre_value": float(datacentre["value"]),
                       "currency": community["currency"],
                       "ratio": float(community["value"]) / float(datacentre["value"])})
    return min(ratios, key=lambda r: r["ratio"])


def derive(snapshot: dict, method: dict, server_kw: dict, compute_rows: dict[str, dict]) -> dict:
    """Derive CHF per server-hour from one snapshot. Pure: same inputs give the same output."""
    w_vcpu = method["WattsPerVcpuAtFullLoad"]["value"]
    w_gb = method["WattsPerGbMemory"]["value"]
    server_w = server_kw["value"] * method["WattsPerKw"]["value"]
    fx = method["UsdToChf"]["value"]
    if snapshot["currency"] != method["AzureCurrency"]["value"]:
        raise ValueError("snapshot currency differs from the method's currency")

    sizes = []
    for inst in method["Instances"]["value"]:
        power_w = inst["vcpu"] * w_vcpu + inst["ram_gb"] * w_gb
        per_server = server_w / power_w
        od = linux_price_usd(snapshot["items"], inst["armSkuName"], spot=False)
        sp = linux_price_usd(snapshot["items"], inst["armSkuName"], spot=True)
        sizes.append({
            "armSkuName": inst["armSkuName"],
            "instance_power_w": power_w,
            "instances_per_server": per_server,
            "on_demand_usd_per_hour": od["retailPrice"],
            "on_demand_meter": f"{od['productName']} / {od['meterName']} (effective {od['effectiveStartDate']})",
            "spot_usd_per_hour": sp["retailPrice"],
            "spot_meter": f"{sp['productName']} / {sp['meterName']} (effective {sp['effectiveStartDate']})",
            "on_demand_chf_per_server_hour": per_server * od["retailPrice"] * fx,
            "spot_chf_per_server_hour": per_server * sp["retailPrice"] * fx,
        })

    a1 = min(s["on_demand_chf_per_server_hour"] for s in sizes)
    a2 = min(s["spot_chf_per_server_hour"] for s in sizes)
    ratio = home_host_ratio(method, compute_rows)
    c1 = a2 * ratio["ratio"]
    adopted = method["AdoptedComputePriceChfPerServerHour"]["value"]

    def r(x):
        return round(x, OUTPUT_DIGITS) if isinstance(x, float) else x

    return {
        "value_chf_per_server_hour": r(c1),
        "value_method": "C1 = A2 (cheapest Azure Switzerland North Linux spot, per 1 kW server-hour) x lowest "
                        "community/datacentre price ratio; docs/research/computing-price.md §3 C1",
        "adopted_value_chf_per_server_hour": adopted,
        "adopted_value_source": method["AdoptedComputePriceChfPerServerHour"]["source"],
        "difference_to_adopted_chf_per_server_hour": r(c1 - adopted),
        "results": {
            "a1_on_demand_min_chf_per_server_hour": r(a1),
            "a2_spot_min_chf_per_server_hour": r(a2),
            "home_host_ratio": r(ratio["ratio"]),
            "c1_market_tracking_chf_per_server_hour": r(c1),
        },
        "per_size": [{k: v if k.endswith(RAW_PRICE_SUFFIX) else r(v) for k, v in s.items()} for s in sizes],
        "home_host_ratio_pair": {**ratio, "ratio": r(ratio["ratio"])},
        "inputs": {
            "ServerElectricKw": {**server_kw, "file": rel(HOUSE_FILE)},
            **{k: method[k] for k in ("AzureRegion", "AzureCurrency", "Instances", "WattsPerVcpuAtFullLoad",
                                      "WattsPerGbMemory", "WattsPerKw", "UsdToChf", "HomeHostRatioPairs")},
            "compute_prices_csv": rel(COMPUTE_CSV),
        },
    }


def derive_file(snapshot_path: Path) -> dict:
    """Derive from a snapshot file and add its provenance."""
    raw = snapshot_path.read_bytes()
    snapshot = json.loads(raw)
    result = derive(snapshot, load_method(), load_server_kw(), load_compute_rows())
    return {
        "generated_by": "python3 scripts/fetch_prices.py --snapshot " + rel(snapshot_path),
        "method_file": rel(METHOD_FILE),
        "snapshot": {"path": rel(snapshot_path), "sha256": hashlib.sha256(raw).hexdigest(),
                     "retrieved_at_utc": snapshot["retrieved_at_utc"], "source": snapshot["source"]},
        **result,
        "note": "Derived, not adopted. Scored figures use config/reference-house.json "
                "(ComputePriceChfPerServerHour, owner T-06); adopting a new value is a team decision "
                "that cites this file and its snapshot. The adopted 0.66 (anchor A3, Hetzner) has no "
                "public API; this file tracks the C1 cross-check and the A2 sensitivity bound.",
    }


def write_derived(result: dict, target: Path = DERIVED_FILE) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def check_derived(target: Path = DERIVED_FILE) -> bool:
    """The committed derived file must equal a fresh derivation from its recorded snapshot."""
    if not target.exists():
        print(f"[check] FAIL {rel(target)} missing")
        return False
    stored = json.loads(target.read_text(encoding="utf-8"))
    fresh = derive_file(REPO_ROOT / stored["snapshot"]["path"])
    ok = fresh == stored
    print(f"[check] {'OK  ' if ok else 'FAIL'} computing price reproducible from "
          f"{stored['snapshot']['path']}: {stored['value_chf_per_server_hour']} CHF/server-hour "
          f"(adopted {stored['adopted_value_chf_per_server_hour']})")
    return ok


def refresh(fetch: bool = True, snapshot: Path | None = None) -> dict:
    if snapshot is None and fetch:
        method = load_method()
        first_url, items, pages = fetch_items(method)
        snapshot = write_snapshot(method, first_url, items, pages)
        print(f"[fetch] Azure Retail Prices -> {rel(snapshot)} ({len(items)} items, {pages} page(s))")
    snapshot = snapshot or latest_snapshot()
    result = derive_file(snapshot)
    write_derived(result)
    print(f"[price] {result['value_chf_per_server_hour']} CHF/server-hour (C1) from {rel(snapshot)}; "
          f"A2 spot {result['results']['a2_spot_min_chf_per_server_hour']}, "
          f"adopted {result['adopted_value_chf_per_server_hour']} "
          f"(difference {result['difference_to_adopted_chf_per_server_hour']:+}) -> {rel(DERIVED_FILE)}")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--derive-only", action="store_true", help="derive from the latest snapshot, no download")
    group.add_argument("--snapshot", type=Path, help="derive from this (pinned) snapshot, no download")
    args = parser.parse_args()
    refresh(fetch=not args.derive_only, snapshot=args.snapshot)
    return 0


if __name__ == "__main__":
    sys.exit(main())
