# T-63 local house-siting prototype

This is a **variant** analysis for a private building archive. It does not modify the scored Basel reference house, the C# heat model or its scheduler. The archive, reviewed evidence and generated report stay in git-ignored `data/external/house-siting/`; do not commit or upload them. Only generic prototype code, synthetic tests and these instructions belong in Git.

## Run the deterministic analysis

Python 3.9+ and the standard library are sufficient. From the repository root:

```powershell
$houseZip = 'C:\path\to\private-house.zip'
New-Item -ItemType Directory -Force data/external/house-siting | Out-Null
python -B scripts/house_siting.py analyze --zip $houseZip --evidence data/external/house-siting/reviewed-plan-evidence.json --out data/external/house-siting/house-siting-variant.md
python -B -m unittest scripts.tests.test_house_siting -v
```

The program reads only the expected PDFs and CSVs from the nested ZIP, rejects unsafe paths, encrypted entries and oversized archives, and does not execute bundled content. The gas summary sums the monthly KWH consumption rows and excludes fixed-charge/KW rows. Electricity totals sum 15-minute kWh entries; the peak is `max(interval_kwh) × 4` kW. It does not calculate delivered heat from gas input, spare electrical capacity from past import, or an hourly heat profile from monthly bills.

The private evidence JSON can be written after visual inspection of scanned plan pages. Each room records its PDF/page, observations and a `reviewed` flag. The deterministic siting rule gives an adjacent, non-occupied, non-attic room **survey priority only**. It always leaves installation at zero until thermal, hydronic, electrical, space, acoustic, ventilation, fire and access gates have evidence. This intentionally does not claim an economic optimum or a physical maximum. The case hardware illustration is read from `config/reference-house.json` with its source fields; it is not a variant demand value.

## Local AI evidence extraction

The AI adapter sends rendered page images to a locally running Ollama vision model at `127.0.0.1:11434`. The endpoint is fixed in code and proxy environment variables are disabled. No CSV account records are sent. The model only proposes room observations; every generated room has `reviewed: false`. Review each cited page image before setting that flag to true. Preserve unknown fields as `null` where the plan does not show them. The local model's output cannot supply installation approval or server count.

The adapter follows the [Ollama chat API](https://docs.ollama.com/api/chat) and its [vision image format](https://docs.ollama.com/capabilities/vision) (base64 images, `stream: false`, JSON output). On 2026-11-21 the real local path was run with Ollama 0.40.14 and `qwen2.5vl:3b` on an Intel Vulkan integrated GPU, bound to `127.0.0.1` with `OLLAMA_NO_CLOUD=true`. All plan pages produced proposals in the ignored private output. The first attempt exposed a timeout and lost in-memory results; the extractor now checkpoints each completed page and resumes only when ZIP hash and model match. It renders at 1200 pixels and allows up to 600 seconds per page. The local model **misread some labels and inferred adjacency not shown by the plan**. Its output is unsuitable for unattended siting decisions; the reviewed evidence and engineering gates remain authoritative. To run it on a machine with a locally installed vision model and Poppler `pdfinfo` / `pdftoppm` on `PATH`:

```powershell
python -B scripts/house_siting.py extract-ai --zip $houseZip --model '<local-vision-model>' --out data/external/house-siting/proposed-plan-evidence.json
```

Verify that the Ollama daemon and selected model execute locally before using private plans. Do not select a cloud model. Check the proposed JSON against the rendered scans, then pass the reviewed JSON to `analyze`. Review the actual generated result too; the model may misread old plan labels or dimensions.

For an **offline mock, which is not AI**, run:

```powershell
python -B scripts/house_siting.py extract-mock --zip $houseZip --out data/external/house-siting/mock-evidence.json
python -B scripts/house_siting.py analyze --zip $houseZip --evidence data/external/house-siting/mock-evidence.json --out data/external/house-siting/mock-report.md
```

The mock has no plan facts, remains unreviewed and is labelled `mock_not_ai` in the report. The AI HTTP request/response path is covered by an offline protocol test with a fake loopback server response; that test does not establish model accuracy.

## Evidence still needed for a server count

1. Confirm which gas meter serves which heated zones, the number of dwellings and whether hot water is included. Obtain a complete recent year and hourly useful-heat demand, not only monthly gas input.
2. Measure required heating supply/return temperatures and document a safe water-side exchanger and control route. Check whether the case's no-heat-pump simplification applies here.
3. Have an electrician document service rating, phase balance, spare circuit capacity, breaker/cable protection and power quality. The single-meter import maximum is not a service rating.
4. Survey rack footprint, door/service clearances, structure, airflow, noise/vibration, fire separation and maintenance access. The scanned plans may differ from today's building.
5. Specify a sourced workload/price and run a labelled house variant through the existing C# scheduler and economics model before selecting an economic count.

The three decisions are the hourly thermal-economic optimum, physical/electrical maximum, and permitted installation. A definite positive count requires evidence for all three and their intersection.
