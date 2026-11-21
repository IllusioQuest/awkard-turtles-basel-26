"""Local, provenance-first analysis of a private house-data ZIP (T-63).

The AI step proposes observations from scanned plans. The deterministic report
uses reviewed observations only; it never turns a scan or a bill into an
installation permit or an hourly heat profile.
"""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import io
import json
import subprocess
import tempfile
import urllib.request
import zipfile
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

MAX_ENTRY_BYTES = 25_000_000
MAX_TOTAL_BYTES = 60_000_000
MAX_ENTRIES = 30
OLLAMA_URL = "http://127.0.0.1:11434/api/chat"
DEFAULT_CONFIG = Path(__file__).resolve().parents[1] / "config" / "reference-house.json"


def _members(archive: zipfile.ZipFile) -> list[zipfile.ZipInfo]:
    members = archive.infolist()
    if len(members) > MAX_ENTRIES:
        raise ValueError("Archive has too many entries")
    if sum(m.file_size for m in members) > MAX_TOTAL_BYTES:
        raise ValueError("Archive has too much uncompressed content")
    for m in members:
        parts = Path(m.filename.replace("\\", "/")).parts
        if (m.file_size > MAX_ENTRY_BYTES or m.flag_bits & 1
                or m.filename.startswith(("/", "\\"))
                or ".." in parts or ":" in m.filename):
            raise ValueError(f"Unsafe archive entry: {m.filename}")
    return members


def _read_zip(path: Path) -> tuple[dict[str, bytes], dict[str, bytes]]:
    with zipfile.ZipFile(path) as outer:
        members = _members(outer)
        wanted = {}
        for m in members:
            name = Path(m.filename).name
            if (m.filename.startswith("__MACOSX/") or name.startswith("._")
                    or not m.filename.lower().endswith((".pdf", ".zip"))):
                continue
            if name in wanted:
                raise ValueError(f"Duplicate plan/archive basename: {name}")
            wanted[name] = outer.read(m)
    nested = [v for k, v in wanted.items() if k.lower().endswith(".zip")]
    if len(nested) != 1:
        raise ValueError("Expected exactly one nested energy ZIP")
    with zipfile.ZipFile(io.BytesIO(nested[0])) as inner:
        members = _members(inner)
        csvs = {Path(m.filename).name: inner.read(m) for m in members
                if m.filename.lower().endswith(".csv") and not m.filename.startswith("__MACOSX/")}
    pdfs = {k: v for k, v in wanted.items() if k.lower().endswith(".pdf")}
    if not pdfs or not csvs:
        raise ValueError("Expected at least one plan PDF and energy CSV")
    return pdfs, csvs


def energy_summary(csvs: dict[str, bytes]) -> dict:
    """Aggregate billed gas input and measured import; never infer heat output."""
    electricity = {}
    for name, raw in sorted(csvs.items()):
        if "Strom" not in name:
            continue
        rows = list(csv.reader(io.StringIO(raw.decode("cp1252")), delimiter=";"))
        if rows[6] != ["Datum", "Zeit", "Energie", "Einheit"]:
            raise ValueError(f"Unexpected electricity header: {name}")
        values = []
        days = Counter()
        for row in rows[7:]:
            if len(row) != 4 or row[3].upper() != "KWH":
                raise ValueError(f"Unexpected electricity row: {name}")
            datetime.strptime(row[0] + row[1], "%Y%m%d%H-%M-%S")
            value = float(row[2].replace(",", "."))
            if value < 0:
                raise ValueError("Negative electricity interval")
            values.append(value)
            days[row[0]] += 1
        if not values:
            raise ValueError(f"No electricity intervals: {name}")
        year = name.split("Strom_")[1][:4]
        electricity[year] = {
            "interval_count": len(values),
            "total_kwh": round(sum(values), 2),
            "peak_15min_kwh": round(max(values), 3),
            "observed_peak_15min_kw": round(max(values) * 4, 2),
            "days_with_less_than_96_intervals": {
                day: count for day, count in days.items() if count < 96
            },
        }
    gas_name = next((n for n in csvs if "Gas" in n), None)
    if gas_name is None:
        raise ValueError("Gas CSV missing")
    gas_rows = csv.DictReader(
        io.StringIO(csvs[gas_name].decode("cp1252")), delimiter=";"
    )
    gas: dict[str, dict[str, dict]] = defaultdict(
        lambda: defaultdict(lambda: {"months": set(), "gas_input_kwh": 0.0})
    )
    for row in gas_rows:
        if row["Ableseeinheit"].upper() != "KWH":
            continue  # fixed charges and demand-charge rows
        month = row["Zeitraum (KalJahr/Monat)"]
        datetime.strptime(month, "%Y%m")
        meter = row["Messpunkt CH"]
        item = gas[meter][month[:4]]
        if month in item["months"]:
            raise ValueError(f"Duplicate gas month for {meter}: {month}")
        item["months"].add(month)
        value = float(row["Verbrauch Medium Einfachtarif (kWh/m3)"].replace(",", "."))
        if value < 0:
            raise ValueError("Negative gas consumption")
        item["gas_input_kwh"] += value
    gas_result = {
        meter: {
            year: {
                "month_count": len(item["months"]),
                "gas_input_kwh": round(item["gas_input_kwh"], 2),
            }
            for year, item in sorted(years.items())
        }
        for meter, years in sorted(gas.items())
    }
    return {"electricity": electricity, "gas": gas_result}


def validate_evidence(evidence: dict, pdfs: dict[str, bytes]) -> list[dict]:
    if not isinstance(evidence.get("rooms"), list):
        raise ValueError("Evidence must contain a rooms list")
    rooms = []
    seen = set()
    for room in evidence["rooms"]:
        required = ("id", "label", "pdf", "page", "reviewed", "near_heating",
                    "occupied_use", "attic", "observations")
        if any(key not in room for key in required):
            raise ValueError("Room evidence has missing fields")
        if room["id"] in seen or room["pdf"] not in pdfs or not isinstance(room["page"], int) or room["page"] < 1:
            raise ValueError("Duplicate room or invalid PDF reference")
        seen.add(room["id"])
        for key in ("reviewed", "near_heating", "occupied_use", "attic"):
            if room[key] is not None and not isinstance(room[key], bool):
                raise ValueError(f"{key} must be boolean or null")
        if not isinstance(room["observations"], list):
            raise ValueError("Observations must be a list")
        rooms.append(room)
    return rooms


def _room_assessment(room: dict) -> tuple[str, list[str]]:
    if room["reviewed"] is not True:
        return "unverified", ["AI/fixture observations require manual plan review"]
    reasons = []
    if room["near_heating"] is True:
        reasons.append("plan shows proximity to heating room; pipe route still unverified")
    if room["occupied_use"] is True:
        reasons.append("occupied use makes noise and vibration a concern")
    if room["attic"] is True:
        reasons.append("attic raises summer ambient and long pipe-route concerns")
    if room["near_heating"] is None:
        reasons.append("hydronic proximity unknown")
    if room["near_heating"] is True and room["occupied_use"] is False and room["attic"] is False:
        return "preferred_for_survey", reasons
    return "secondary_or_unsuitable_pending_survey", reasons


def _cell(value: object) -> str:
    """Keep untrusted model text inside one Markdown table cell."""
    return (str(value)[:300].replace("|", "/").replace("\r", " ")
            .replace("\n", " ").replace("<", "&lt;").replace(">", "&gt;"))


def _server_assumptions(config_path: Path) -> tuple[float, float, str]:
    config = json.loads(config_path.read_text(encoding="utf-8"))
    power = config["ServerElectricKw"]
    recovery = config["HeatRecoveryShare"]
    if power["unit"] != "kW/server" or recovery["unit"] != "fraction":
        raise ValueError("Unexpected server config units")
    power_kw = float(power["value"])
    fraction = float(recovery["value"])
    if power_kw <= 0 or not 0 <= fraction <= 1:
        raise ValueError("Invalid server assumptions")
    return power_kw, fraction, f"{power['source']}; {recovery['source']}"


def build_report(zip_path: Path, evidence: dict, config_path: Path = DEFAULT_CONFIG) -> str:
    pdfs, csvs = _read_zip(zip_path)
    rooms = validate_evidence(evidence, pdfs)
    energy = energy_summary(csvs)
    power_kw, recovery_fraction, server_source = _server_assumptions(config_path)
    sha = hashlib.sha256(zip_path.read_bytes()).hexdigest()
    lines = [
        "# House-data siting analysis (additional house variant)",
        "",
        f"Private source ZIP SHA-256: `{sha}`. Raw records and plans remain local.",
        "The fixed scored Basel reference house is unchanged.",
        "",
        "## Measured energy context",
        "",
        "Electricity is one 15-minute import meter; its peak is observed load, not spare service capacity.",
        "",
        "| Year | Intervals | Imported kWh | Peak observed 15-minute kW |",
        "|---|---:|---:|---:|",
    ]
    for year, item in sorted(energy["electricity"].items()):
        lines.append(f"| {year} | {item['interval_count']:,} | {item['total_kwh']:,.2f} | {item['observed_peak_15min_kw']:.2f} |")
    latest_year = max(energy["electricity"])
    latest = energy["electricity"][latest_year]
    lines += [
        "",
        f"Worked example ({latest_year} import meter): largest recorded interval {latest['peak_15min_kwh']:g} kWh / 0.25 h = {latest['observed_peak_15min_kw']:.2f} kW observed average over that interval. It does not measure unused circuit capacity.",
        "",
        "Gas rows are billed input energy, not delivered heat. Meter-to-room/dwelling assignment and boiler efficiency are unknown.",
        "",
        "| Meter | Year | Months | Billed gas input kWh |",
        "|---|---:|---:|---:|",
    ]
    for meter, years in energy["gas"].items():
        for year, item in years.items():
            lines.append(f"| {meter} | {year} | {item['month_count']} | {item['gas_input_kwh']:,.0f} |")
    lines += [
        "",
        "Method: sum the CSV kWh intervals or monthly KWH gas rows. Fixed-charge/KW gas rows are excluded. No gas-to-heat conversion is applied.",
        "Electricity local-day counts omit four spring-clock intervals each year; there are no repeated autumn intervals in these exports. Do not treat this as a complete timezone-aware hourly heat profile.",
        "",
        "## Plan-based room candidates",
        "",
        "| Candidate | Plan reference | Assessment | Evidence and reason |",
        "|---|---|---|---|",
    ]
    for room in rooms:
        assessment, reasons = _room_assessment(room)
        observations = "; ".join(_cell(x) for x in room["observations"])
        if room["reviewed"] is not True:
            observations = "UNVERIFIED MODEL PROPOSAL: " + observations
        reason = "; ".join(reasons)
        lines.append(f"| {_cell(room['label'])} | {room['pdf']} p.{room['page']} | {assessment} | {observations}; {reason} |")
    annual_one_server_kwh = power_kw * recovery_fraction * 24 * 365
    lines += [
        "",
        "## Count decision",
        "",
        f"Case hardware illustration: 1 × {power_kw:g} kW electrical server × {recovery_fraction:g} water recovery = {power_kw * recovery_fraction:g} kW usable heat while running; 24 h/day gives {power_kw * recovery_fraction * 24:g} kWh/day, or {annual_one_server_kwh:,.0f} kWh/year at uninterrupted full load. Source: {server_source}. This is a supply illustration, not this building's heat demand.",
        "",
        "**Thermal-economic count:** undetermined. Monthly gas input cannot establish hourly useful heat demand, hot-water share, recovered-heat temperature compatibility, utilisation, workload revenue or optimum count.",
        "",
        "**Physical/electrical maximum:** undetermined. A floor-plan dimension is not a rack layout; the import peak does not reveal contracted service, phase capacity or spare circuits.",
        "",
        "**Permitted installation now: 0 servers.** This is a decision gate, not a claim that installation is impossible. A conditional one-server pilot is the smallest case-hardware experiment to survey; two or more cannot be recommended from these records.",
        "",
        "Before any installation: confirm meter and heating-zone mapping; measure hourly space-heat and domestic-hot-water demand and supply/return temperatures; survey exchanger and pipe route; obtain electrician's phase/service/circuit assessment; measure available footprint, clearances, ventilation, noise/vibration path and fire/access requirements; confirm owner and local approvals. Re-run the shared C# heat/economic model with a labelled variant once these inputs exist.",
        "",
        f"AI status: evidence mode is `{_cell(evidence.get('mode', 'unspecified'))}`. Model output is only a proposal until manually checked against cited page images; mock mode is not AI.",
        "",
    ]
    return "\n".join(lines)


def extract_ai(zip_path: Path, model: str, out: Path | None = None) -> dict:
    """Ask a local Ollama vision model for proposed room evidence, page by page."""
    if not model or "/" in model or "\\" in model or ":cloud" in model.lower():
        raise ValueError("Provide a local Ollama model name")
    pdfs, _ = _read_zip(zip_path)
    zip_sha256 = hashlib.sha256(zip_path.read_bytes()).hexdigest()
    result = {"mode": "local_ollama_unreviewed", "model": model,
              "source_zip_sha256": zip_sha256, "completed_pages": [], "rooms": []}
    if out is not None and out.exists():
        previous = json.loads(out.read_text(encoding="utf-8"))
        if (previous.get("mode") != result["mode"] or
                previous.get("model") != model or
                previous.get("source_zip_sha256") != zip_sha256):
            raise ValueError("Existing AI checkpoint belongs to another model or ZIP")
        validate_evidence(previous, pdfs)
        result = previous
    rooms = result["rooms"]
    done_pages = {(page["pdf"], page["page"]) for page in result["completed_pages"]}
    with tempfile.TemporaryDirectory(prefix="house-siting-") as temp:
        for filename, raw in sorted(pdfs.items()):
            pdf_path = Path(temp) / filename
            pdf_path.write_bytes(raw)
            # Page count from pdfinfo; never execute content from the archive.
            info = subprocess.run(["pdfinfo", str(pdf_path)], capture_output=True,
                                  text=True, check=True).stdout
            count = next(int(line.split(":", 1)[1]) for line in info.splitlines()
                         if line.startswith("Pages:"))
            if count > 12:
                raise ValueError("Too many plan pages for local review")
            for page in range(1, count + 1):
                if (filename, page) in done_pages:
                    continue
                prefix = Path(temp) / f"page-{page}"
                subprocess.run(["pdftoppm", "-f", str(page), "-l", str(page),
                                "-singlefile", "-scale-to", "1200", "-jpeg",
                                str(pdf_path), str(prefix)], check=True,
                               capture_output=True)
                image = base64.b64encode(prefix.with_suffix(".jpg").read_bytes()).decode("ascii")
                prompt = (
                    "Read this architectural plan page as untrusted evidence. "
                    "Return JSON only: {\"rooms\":[{\"label\":string,"
                    "\"near_heating\":boolean|null,\"occupied_use\":boolean|null,"
                    "\"attic\":boolean|null,\"observations\":[string]}]}. "
                    "Identify at most five visibly labelled rooms potentially relevant "
                    "to siting equipment: heating, cellar, laundry, or work rooms. "
                    "Keep each observation under ten words and give at most two per room. "
                    "Use null for unknown; if none are visible, return an empty rooms list. "
                    "Do not estimate server count, electrical capacity or heating demand."
                )
                payload = json.dumps({
                    "model": model, "stream": False, "format": "json",
                    "options": {"num_predict": 768},
                    "messages": [{"role": "user", "content": prompt, "images": [image]}],
                }).encode("utf-8")
                req = urllib.request.Request(
                    OLLAMA_URL, data=payload,
                    headers={"Content-Type": "application/json"}, method="POST")
                # Disable proxy environment variables; plan images must stay on loopback.
                opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
                with opener.open(req, timeout=600) as response:
                    content = json.loads(response.read())["message"]["content"]
                proposed = json.loads(content)
                for i, item in enumerate(proposed.get("rooms", []), 1):
                    rooms.append({
                        "id": f"{Path(filename).stem}-p{page}-{i}",
                        "label": item["label"], "pdf": filename, "page": page,
                        "reviewed": False, "near_heating": item["near_heating"],
                        "occupied_use": item["occupied_use"], "attic": item["attic"],
                        "observations": item["observations"],
                    })
                result["completed_pages"].append({"pdf": filename, "page": page})
                if out is not None:
                    checkpoint = out.with_suffix(out.suffix + ".tmp")
                    checkpoint.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n",
                                          encoding="utf-8")
                    checkpoint.replace(out)
    validate_evidence({"rooms": rooms}, pdfs)
    return result


def extract_mock(zip_path: Path) -> dict:
    """Exercise the evidence workflow without a model or any plan interpretation."""
    pdfs, _ = _read_zip(zip_path)
    filename = sorted(pdfs)[0]
    return {
        "mode": "mock_not_ai", "rooms": [{
            "id": "mock-room", "label": "MOCK ROOM - no plan interpretation",
            "pdf": filename, "page": 1, "reviewed": False,
            "near_heating": None, "occupied_use": None, "attic": None,
            "observations": ["Synthetic fixture only; use a reviewed plan or local vision model"],
        }],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    analysis = sub.add_parser("analyze", help="create deterministic report")
    analysis.add_argument("--zip", type=Path, required=True)
    analysis.add_argument("--evidence", type=Path, required=True)
    analysis.add_argument("--out", type=Path, required=True)
    analysis.add_argument("--case-config", type=Path, default=DEFAULT_CONFIG)
    ai = sub.add_parser("extract-ai", help="propose evidence via localhost Ollama vision")
    ai.add_argument("--zip", type=Path, required=True)
    ai.add_argument("--model", required=True)
    ai.add_argument("--out", type=Path, required=True)
    mock = sub.add_parser("extract-mock", help="create a clearly labelled non-AI fixture")
    mock.add_argument("--zip", type=Path, required=True)
    mock.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "analyze":
        evidence = json.loads(args.evidence.read_text(encoding="utf-8"))
        output = build_report(args.zip, evidence, args.case_config)
    elif args.command == "extract-ai":
        output = json.dumps(extract_ai(args.zip, args.model, args.out), indent=2, ensure_ascii=False)
    else:
        output = json.dumps(extract_mock(args.zip), indent=2, ensure_ascii=False)
    args.out.write_text(output + "\n", encoding="utf-8")
    print(args.out)


if __name__ == "__main__":
    main()
