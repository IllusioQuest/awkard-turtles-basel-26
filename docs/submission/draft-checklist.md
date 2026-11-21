# Draft submission checklist (T-07)

> Checked 2026-11-21 ~02:30 Europe/Zurich on a **fresh clone of `main`** (commit 946be91 or later).
> Update this file whenever a gap closes. Owner: T-07 (team lead / Claude).

## Fresh-clone check

| Step | Command | Result |
|---|---|---|
| Data validation | `python3 scripts/fetch_data.py --check` | all checks passed |
| Build | `dotnet build src/DigitalHeat.sln` | 0 warnings, 0 errors |
| Tests | `dotnet test src/DigitalHeat.sln --no-build` | 27 / 27 passed |
| Year balance | `dotnet run --project src/DigitalHeat.YearRun -- .` | 58.0 % covered, 22,661 server-hours, break-even 0.226 CHF/h (matches the year explorer) |
| Live replay | `dotnet run --project src/DigitalHeat.LiveRun -- --date … --job … --container …` | needs Docker and a running job container; not runnable in the cloud check. Verified by Nikita with a webhosting replica (PR #7) and by Ennio with the transcription container (T-02 branch) |

## Must-haves → evidence

Legend: ✅ in `main` · 🟡 exists but not merged / partly · ❌ missing

### 1. Workloads

| Requirement (case) | Status | Evidence / gap | Ticket |
|---|---|---|---|
| ≥ 2 job types | 🟡 | Transcription (whisper.cpp) on `feat/T-02-transcription-job`; webhosting described in `docs/webhosting-heat-runtime.md` but no catalog entry yet (schema allows only interruptible jobs; Nikita decides the representation) | T-02, T-01, T-23 |
| Runtime and energy per job | 🟡 | Transcription measured on a laptop (T-02 branch, 2026-11-21); webhosting power `TODO(source)` | T-02, T-23 |
| At least one job runs on our machine, measured runtime | 🟡 | T-02 branch: measurement + container | T-02 |
| Energy method and scaling to 1 kW | 🟡 | T-02 branch: "scaling to 1 kW" documented | T-02 |
| Interruptible? and deadline per job type | 🟡 | Transcription: interruptible (freeze/throttle). Webhosting: not interruptible at service level; latency objective `TODO(source)` | T-01, T-23 |
| Jobs per day + source, or "unlimited" stated | ✅ | `config/job-scenarios/unlimited.json`; stated in `summary.json` (`JobDemandAssumption`) | T-05 |

### 2. Scheduling

| Requirement | Status | Evidence / gap | Ticket |
|---|---|---|---|
| Distribute / pause / reschedule by heat demand (winter heating, summer hot water) | ✅ | `GreedyHeatScheduler` in `DigitalHeat.Core` (decision 0005) | – |
| Simple heat storage | ✅ | lossless 35 kWh tank in the year simulation | – |
| What happens to a job whose heat is not needed | ✅ | excess-heat policy `wait` (decision 0010) | T-05 |
| Same scheduler code controls a real running job | ✅ | `DigitalHeat.LiveRun` → `SchedulerController.Apply` → `DockerJobExecutor` (decision 0009); verified runs see above | T-03, T-04 |

### 3. Balance per house and year

| Figure | Status | Value (default run) / gap | Ticket |
|---|---|---|---|
| Servers per house | ✅ | 4 (draft presentation baseline, T-06 branch) | T-06 |
| Share of total heat demand covered | ✅ | 58.0 % | – |
| Heat dumped | ✅ | 0 kWh | – |
| Utilisation | ✅ | 64.7 % | – |
| Missed deadlines in % of jobs | ❌ | `summary.json` says "not modelled yet" for the unlimited run. Needs a finite, sourced job scenario (T-05 supports it) or an explicit statement for the unlimited case | T-05, T-06 |
| Electricity cost and computing income | 🟡 | CHF 7,535 / CHF 6,798; income uses an **example** computing price, must be labelled or replaced by WP1 prices | T-06, T-14 |
| Break-even computing price (case formula) | ✅ | 0.226 CHF per server-hour | – |
| Every assumption visible | 🟡 | `config/reference-house.json` (unit + source per value), `docs/assumptions.md`; needs one page for the jury | T-06 |
| When the model does not pay off | ❌ | statement missing | T-06 |

### Prototype requirements

| Requirement | Status | Evidence / gap |
|---|---|---|
| Working demonstrator with a real job under scheduler control | ✅ | LiveRun + DockerJobExecutor |
| List of all assumptions | 🟡 | see above |
| Reproducible measurements and calculations | 🟡 | YearRun + tests ✅; T-02 measurement commands on its branch |
| Fallback recording of the live demo | ❌ | to do before the pitch (T-04 / T-21) |

## Open for the submission itself

- What exactly must be submitted at 15:00 (form, repo link, video, slides, text)? → T-00.
- README section "How to run the demo" (YearRun, LiveRun with the transcription container).
- Submit by 14:30 at the latest (buffer).
