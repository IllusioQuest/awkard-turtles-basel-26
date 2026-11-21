# T-69: Live one-house request and dashboard

- **Owner:** Codex · **Status:** in progress · **Branch:** `feat/T-69-live-household-demo`
- **Depends on:** T-68, T-58, T-03 and decision 0061.

Build an offline live demonstration using the existing winter and summer reference-house inputs. The existing Fleet heat-first scheduler controls one dedicated real, interruptible local workload; expose simulated house state and independently observed process state to the household dashboard. Prefer transcription when its assets exist, with the existing Monte Carlo image as the local fallback. Never label simulated heat or simulated job completion as measured process output.

## Initial findings

- Docker is installed and the `digitalheat/research-sim:live-demo` image is available locally. A one-task Monte Carlo workload ran in a dedicated temporary container; the transcription image and model were not found locally.
- `HeatFirstFleetScheduler` is the scheduler used by the Fleet annual and day runs. Its actions can be wrapped, but Fleet's virtual chunk-completion timer is independent of actual Docker process progress. Never treat one as proof of the other.
- The existing `DigitalHeat.LiveRun` uses independent annual tank snapshots and logs only to the console; the Fleet API and household page read static, cached results. A live path needs bounded telemetry and must distinguish simulated house data from actual container observations.
- Review (decision 0061): pace with a monotonic clock; serialize and confirm process actions; account for simulated completion and real early exit; clean up only a container owned by the demo; test pause, resume and failure paths. A small console proof is the first milestone.

## Proposed demonstration

The operator submits one public-data Monte Carlo request in the console, choosing the January or July reference-house scenario. The one-site Fleet engine advances its model clock at a configurable accelerated rate. An adapter forwards the actual scheduler's accepted load changes to the demo's dedicated Docker container and publishes a bounded, atomic local snapshot. The household page polls a read-only endpoint and plots the modelled tank and heat demand while showing the real container's running/paused/exited state and observed task output separately. The existing yearly and day runs stay unchanged.

The request is time-bounded: it computes real Monte Carlo tasks until the Fleet job's simulated end, then receives SIGTERM, finishes the current task and exits. A clean actual exit with output is checked independently; simulated task quantities and real completed tasks are not equated. If the process exits early or Docker actions fail, the run reports failure. If Whisper's local model and image are prepared later, the same executor boundary can operate the existing transcription workload, but that adapter is not implemented yet.

## Run locally

Requires Docker and the locally available `digitalheat/research-sim:live-demo` image. This image is not downloaded automatically. The configured image, CPU limit, time scale and workload parameters are in `config/live-household-demo.json`. Results and the current live snapshot are written under git-ignored `data/external/live-household/`.

```powershell
dotnet run -c Release --project src/DigitalHeat.FleetApi
```

In another terminal:

```powershell
cd web
npm run dev
```

Open `http://127.0.0.1:5173/live`, then in a third terminal from the repository root:

```powershell
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- summer
# Or run the same workload against the winter day:
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- winter
# Add the illustrative simulated queue alongside the one real request:
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- winter --busy
dotnet run -c Release --project src/DigitalHeat.LiveFleetRun -- summer --busy
```

The console shows scheduler actions and observed completed tasks. Ctrl+C cancels the run. The runner creates and removes only its own uniquely named container. The winter live replay uses the first winter request; the offline winter simulation still contains both jobs. Busy mode adds the configured number of illustrative simulated requests at the same arrival and deadline; only the explicitly marked real request is a Docker process. The queue counts include both real and simulated jobs as represented in the model. In the heat chart, compare modelled house demand with modelled server heat: energy above demand can charge the tank; a deficit is supplied by backup heat.

Worked example: on 15 January at 08:00, the case formula applied to the Basel weather gives 9.09375 kWh of house demand in that hour (including hot water). Even four fully loaded reference servers produce at most 4 × 1 kW × 0.8 × 1 h = 3.2 kWh recoverable heat, so the tank cannot charge in that hour. The heat-demand line is modelled demand in kW; the server-heat line is modelled production in kW. Neither is a physical measurement.

## Verification

Both live seasons ran successfully with the local Monte Carlo image. Summer recorded actual start, pause, resume and graceful exit; winter started and exited without a heat pause. Busy winter and summer ran with one actual request and the additional simulated queue. Actual task results were written in the dedicated run directory. The .NET and web builds passed. Simulated heat is not measured electrical or recovered heat, and the configured full-load requirement remains an unmeasured workload assumption.

Cancellation and unexpected container-exit paths are implemented but have not yet been exercised in a live failure test.
