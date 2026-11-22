# Quickstart

Copy-paste commands to run every part of the project. Run them from the repository root unless a
block says otherwise. Where Linux/macOS and Windows differ, both are given (Windows = PowerShell).
What the parts are: [`ARCHITECTURE.md`](ARCHITECTURE.md).

## 0. Install once

| Tool | Needed for | Linux (Debian/Ubuntu) | macOS | Windows |
|---|---|---|---|---|
| .NET 8 SDK | everything in C# | `sudo apt install dotnet-sdk-8.0` | installer from [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) | `winget install Microsoft.DotNet.SDK.8` |
| Node.js 22 LTS | web dashboard | `sudo apt install nodejs npm` (or nvm) | `brew install node@22` | `winget install OpenJS.NodeJS.LTS` |
| Python 3.10+ | data scripts (standard library only) | preinstalled / `sudo apt install python3` | `brew install python` | `winget install Python.Python.3.12` |
| Docker | real jobs, voice demo, labs | Docker Engine | Docker Desktop | Docker Desktop (Linux containers) |
| ngrok (optional) | phone access to `/speak` | [ngrok.com/download](https://ngrok.com/download) | [ngrok.com/download](https://ngrok.com/download) | `winget install ngrok.ngrok` |

On Windows use `python` instead of `python3` in the commands below.

## 1. Build and test

```bash
dotnet build src/DigitalHeat.sln
dotnet test src/DigitalHeat.sln
```

Expected: all tests pass except `DeadlineTests.LegacyJournalRetainsExactPreDeadlineContainerHash`
(Orchestration), which also fails on `main` and is unrelated to the results.

Check the committed data against the case (206 heating days, ElCom price, …):

```bash
python3 scripts/fetch_data.py --check
```

## 2. Run a Fleet simulation

All inputs are committed; a single house takes about 1 s, 100 houses a few seconds.

```bash
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/reference.json
dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/reference
```

The console prints jobs, heat, money and break-even; files land in `data/derived/fleet/reference/`.
Other scenarios (replace `reference` in both lines):

| Scenario | What |
|---|---|
| `reference` | case reference house, 4 servers (main result) |
| `real-house` | the organisers' real house, calibrated to its gas data |
| `dreispitz-5-pool` | 5 houses and an indoor pool at FHNW Dreispitz |
| `alps-border-50` | 50 houses in Interlaken, Brig-Goms and the Mittelland |
| `basel-region-100` | 100 houses in Basel-Landschaft |
| `ch-100` | 100 houses across Switzerland (larger output; close other programs first) |

Low on memory? Limit the .NET heap first: `export DOTNET_GCHeapHardLimit=0x60000000` (Linux/macOS)
or `$env:DOTNET_GCHeapHardLimit="0x60000000"` (Windows).

## 3. Dashboard (map, houses, money)

Terminal 1, the API (reads the runs from step 2):

```bash
dotnet run -c Release --project src/DigitalHeat.FleetApi
```

Terminal 2, the web app:

```bash
cd web
npm ci
npm run dev
```

Open <http://127.0.0.1:5173/admin>, pick a run, click a house on the map. A house page is at
`/admin/<run>/houses/<house-id>`. Without the API the pages show mock data (yellow banner).

## 4. Live voice demo (pitch)

Tested on Linux (Fedora). macOS works without energy measurement. On Windows run it inside
**WSL 2** (Ubuntu, Docker Desktop with WSL integration); native Windows is not supported.
Full runbook: [`docs/demo/voice-request.md`](docs/demo/voice-request.md).

Once: models (about 8 GB) and images.

```bash
mkdir -p data/external/voice-demo/models
curl -L -o data/external/voice-demo/models/ggml-large-v3.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin
curl -L -o data/external/voice-demo/models/Apertus-8B-Instruct-2509-Q4_K_M.gguf https://huggingface.co/unsloth/Apertus-8B-Instruct-2509-GGUF/resolve/main/Apertus-8B-Instruct-2509-Q4_K_M.gguf
docker build -t digitalheat/transcription-whisper jobs/transcription
docker build -t digitalheat/llm-summary jobs/llm
docker build -t digitalheat/voice-assistant jobs/voice-assistant
```

Every time, four terminals (close large programs first; the LLM needs about 6 GB RAM):

```bash
VOICE_DEMO_TOKEN=pick-a-secret dotnet run -c Release --project src/DigitalHeat.FleetApi   # 1: API
cd web && npm run dev                                                                    # 2: dashboard
dotnet run -c Release --project src/DigitalHeat.VoiceDemo                                # 3: runner (left half of the screen)
ngrok http 5173                                                                          # 4: phone tunnel (optional)
```

- Laptop browser: <http://127.0.0.1:5173/live> (right half of the screen).
- Phone: `https://<your-id>.ngrok-free.app/speak?t=pick-a-secret`, tap **Speak**, talk, tap **Stop & send**.
- No phone or no internet: type the question into terminal 3 and press Enter, or open
  <http://127.0.0.1:5173/speak> on the laptop.
- Stage timing, language, answer length: `config/voice-demo.json`.

## 4b. Pitch mode (the whole pitch in one browser tab)

Needs the API, the dashboard and the voice runner from §4 running, and the Fleet runs of the pitch:

```bash
for r in reference-1-server reference real-house-1-server dreispitz-5-pool-1-server; do
  dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/$r.json
done
```

Copy the cut intro film to `web/public/pitch/intro.mp4` (not in git), start `ngrok http 5173`,
then open <http://127.0.0.1:5173/pitch?t=pick-a-secret> (the same token as `VOICE_DEMO_TOKEN`).
Press **H**: every line should be green. **F** fullscreen, **→** start. Keys: → / Space / PageDown
next, ← back, 1–8 scene, G globe, B or . black, T type a request, V fallback recording, Q QR
address, R reload the page on screen. Scenes, runs and texts: `web/public/pitch/pitch.json`.

## 5. Real house calibration (organisers' data)

Needs the organisers' data in `data/external/housedata/` (not in git; see
[`data/raw/housedata/SOURCE.md`](data/raw/housedata/SOURCE.md)). Then:

```bash
python3 -I scripts/calibrate_real_house.py
dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/real-house.json
```

## 6. Measure a job on this machine

Example: the research simulation (no model download needed).

```bash
docker build -t digitalheat/research-sim jobs/research-sim
python3 scripts/measure_job.py --help
```

Measurement recipes per job (energy meters, repetitions): `docs/jobs/research-sim.md`,
`docs/jobs/transcription.md`, `docs/jobs/llm-summary.md`. The CPU energy counter needs read access
on Linux: `sudo chmod o+r /sys/class/powercap/intel-rapl:0/energy_uj`.

## 7. Analysis scripts

```bash
python3 scripts/price_comparison.py        # our cost per unit vs market prices
python3 scripts/profitability_inputs.py    # profitability input tables
python3 scripts/hardware_comparison.py     # hardware profiles side by side
```

Which script writes which file: [`data/derived/README.md`](data/derived/README.md).

## 8. Network research and paper

```bash
python3 scripts/network_experiments.py     # greedy strategies, about 1 min
cd docs/paper/latex && tectonic main.tex    # or: latexmk -pdf main.tex
```

## 9. Experiments (Windows PowerShell with Docker Desktop)

These labs were built on Windows; their scripts are PowerShell.

| Lab | Start | Docs |
|---|---|---|
| Linux mesh (3 peers) | `.\mesh\Start-MeshLab.ps1` | `docs/model/unified-heat-first-mesh.md` |
| Landscape renderer | `.\rendering\Start-Renderer.ps1`, open <http://localhost:18900/ui> | `docs/jobs/landscape-renderer.md`, `docs/jobs/five-worker-renderer.md` |
| Minecraft server | `.\gaming\scripts\Build-Minecraft.ps1` | `docs/minecraft-server.md` |

## Troubleshooting

| Problem | Fix |
|---|---|
| Port 5080 or 5173 in use | stop the old API / dev server (Ctrl+C), or start the API with `-- --port 5081` (the web proxy expects 5080) |
| Dashboard shows mock data | the API is not running or the run folder does not exist yet (step 2) |
| New run not listed | restart the API |
| Voice demo: `Docker volume dh-voice-demo points to …` | the repository moved: `docker volume rm dh-voice-demo`, start the runner again |
| Voice demo: `/live` says "Start a live request…" | the runner is not running yet |
| Voice demo freezes the laptop | not enough RAM for the LLM: close other programs |
