# Minute-level live batch deadlines (T-48)

User-approved experimental targets, 2026-11-21. These are completion budgets, including queue,
container creation and startup, not guaranteed service levels. Existing hourly simulations and
their 24-hour baseline are unchanged. Existing games/Minecraft receive no automatic expiry.

| Profile | Operator-declared finite input bound | Completion target | Runtime evidence |
|---|---|---|---|
| small-transcription | <=15 audio minutes, Whisper base | 300 s (5 min) | T-02: 4.62 s/audio min on measured AMD laptop, 8 threads; 15 x 4.62 = 69.3 s context |
| small-summary | One <=15-audio-minute transcript, comparable to measured token/output bounds | 300 s (5 min) | T-33: 14.98-minute transcript, 3310 input/319 output tokens, 133.3 s on measured AMD laptop |
| finite-research | <=320 finite simulation tasks | 600 s (10 min) | T-30: 320-task run, 274.4 s on measured Core Ultra 7 255H, 8 workers |

Policy and sources live once in `config/live-job-deadlines.json`. Larger/unbounded jobs require
an explicit absolute budget. Selecting a profile validates the declared job ID/size; it does not
inspect private input files, token lengths or actual container behavior. Never apply this profile
as proof that arbitrary input or a different CPU/GPU will meet the target. Infinite `PASSES=0`
does not become a finite successful research job merely because it has an expiry time.

## Contract and enforcement

`WorkloadRequest.deadline` is optional and contains `submitted_at_utc`, `due_at_utc`, `source`,
`stop_when_overdue`. Only batch jobs accept it. Times are absolute UTC and the interval is
exclusive at its end: `now >= due_at_utc` means expired. A retry/restart/pause never renews it.
The new null field is omitted individually from JSON, preserving existing untimed spec hashes.
An actual pre-change journal/hash fixture guards that compatibility; serializer defaults are unchanged.

Pure placement rejects expired new reservations. The live coordinator uses an injectable trusted
clock and rechecks after blocking work and immediately before dispatch. Expired waiting work never
launches. Already overdue owned batch work gets a durable `missed` outcome, with stop intent saved
before cleanup. It is stopped only if both deadline policy and owner lifecycle allow it. Resources
remain held until the existing runtime confirms termination/removal. A failed/uncertain stop does
not free capacity. Games cannot acquire the new deadline metadata and are not auto-stopped.

Deadline outcome is separate from lifecycle phase. A process stop never becomes successful job
completion. Successful completion observed before due is `on_time`; nonzero exit is `failed`;
first terminal observation after due is `unknown` unless lateness was already established.
Docker's reported finish time is informational, not trusted cross-host clock proof. First evidence
and established lateness survive restart. Clearing/replacing a journal is not a supported retry.

### Owner loop, not a new interactive console

The existing `DigitalHeat.WorkloadRun` backend CLI has two additive commands:

```powershell
# Configure an already-defined finite batch workload. Output must be a new private file.
dotnet run --project src/DigitalHeat.WorkloadRun -c Release -- set-deadline config.json submitted.json my-summary small-summary 15
# The deadline already counts from submission; start the explicit owner promptly.
dotnet run --project src/DigitalHeat.WorkloadRun -c Release -- watch-deadlines submitted.json gaming/TestResults/my-fleet-state
```

Allowed profile IDs are the table above; `small-summary` corresponds to the actual existing
`llm-summary-apertus-8b` descriptor. The container command/input bound is an operator declaration.
Use only a fresh fleet/config describing batch jobs the operator is authorized to run, correctly
sourced heat/resource inventory, an installed pinned image, and suitable volume/privacy bindings.
The deadline feature does not bypass existing admission or automatically create real heat capacity.

The finite manifest (maximum 100 jobs, sourced local policy) is ordered by earliest due time.
The owner holds the existing per-fleet lock, admits through the same coordinator and polls with a
configured 1 s delay **after work**, not a hard one-second enforcement guarantee. It reloads only
advancing inventory revisions: manifest deadlines/specs and runtime-node identity must remain
unchanged. Resource freshness is never silently extended. Operator-updated telemetry may enable
later admissions; stale inventory leaves work waiting until refresh or expiry.

Status is emitted as JSON on changes and saved in the private state directory as
`deadline-status.json`; exit 0 means all jobs confirmed on-time, exit 4 includes missed/failed/unknown
results. The manifest file preserves waiting-job submission times; it is not a public/distributed
queue or mutable admission API. `status` also exposes persisted outcomes. Read-only monitor API
gains no Docker write endpoints. Legacy `LiveRun` adapters are not retroactively deadline-enforced;
jobs use this universal owner path to obtain the new behavior.

**The owner must stay running.** Ctrl+C exits without cancelling all jobs and reports that expiry
enforcement stopped. After a crash, restart with the same immutable manifest/state; reconciliation
keeps uncertain claims. Docker RPCs have existing bounded timeouts; a blocked call can delay other
jobs' checks. UTC clock corrections, process scheduling, polling and graceful-stop duration mean
this is not an exact kill-at-deadline system. Docker stop may escalate after its existing grace
period; no lost work/success claim is inferred. `AllowStop=false` preserves the job and records lateness.

## Verified

Clean Release build; 42 orchestration tests, including exclusive due boundary, future submission,
expiry during preflight/create/start, running/paused expiry, forbidden stop, failed-stop recovery,
on-time outcome across restart, late-observed completion uncertainty, unchanged legacy hash and
profile-size bounds. A scoped actual Docker proof used an explicitly shortened 15 s test deadline,
not the service targets. It observed lateness at about 1.15 s after due, persisted stop intent,
confirmed removal/release, and left the random test fleet empty. This is an enforcement proof,
not proof that real inference/research jobs meet five/ten minutes on this laptop.

```powershell
dotnet test src/DigitalHeat.Orchestration.Tests -c Release
dotnet build src/DigitalHeat.WorkloadRun -c Release
scripts/Test-LiveDeadlines.ps1  # pre-existing local alpine:3.22 required; only random test fleet
```

Smoke evidence stays ignored under `gaming/TestResults/deadline-smoke-02679d80cef94003b7c74918bf2e4210/`.
Existing local measured workload docs: [transcription](transcription.md), [summary](llm-summary.md),
[research](research-sim.md). Additional public-source boundaries: [workload evidence guide](../research/workload-evidence.md).
