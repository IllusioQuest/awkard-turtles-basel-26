# Heat-guided hosting with six Linux nodes (T-29)

Status: implementation and Docker verification in progress; no measured heat claim.
User approved six-node automatic stateless relocation/pause and a common batch/always-on catalog.
Decision 0016 and review are on main. T-01 owns the shared catalog schema implementation.

## Architecture and shared scheduler
Each component has three independent node engines: frontend-a/b/e and api-c/d/f.
Two replicas per component are desired serving capacity; one is a prepared spare.
The currently active gateway (exclusive port :18080) runs serial lifecycle reconciliation.
Standby performs no mutations before owning the port. Existing local watchdogs and two external
Linux pause monitors handle gateway failure. All application containers retain network isolation.
Trusted gateway containers now have the outer Docker Unix socket and root privileges solely for
this lab control adapter; this grants broad engine authority despite code-level label checks.
Frontend/API containers never receive a Docker socket.

HostingPlan.Scores calls the existing DigitalHeat.Core GreedyHeatScheduler with HeatSignal.
No formula is copied and no annual model, IScheduler, IExecutor or placement-policy contract changes.
Candidate selection reuses JobPlacement.IsEligible before any dispatch, even for one backend.
Frontend and API cannot route into one another's pools. Address identity and /health node/component
identity must match the configured inventory.

~~~mermaid
flowchart TD
  P["Trusted plan + sourced/synthetic hourly heat"] --> S["Shared C# GreedyHeatScheduler"]
  S --> G["Active gateway: eligible preferred pair"]
  G --> R["Start/resume replacement"]
  R --> H["Verify component readiness"]
  H --> D["Stop old admissions; drain existing requests"]
  D --> C["Recheck two OTHER healthy eligible replicas"]
  C --> X["Pause old replica"]
  X --> G
~~~

## Input and behavior
deploy/hosting-plan.example.json is snake_case and labels all heat values as a synthetic test fixture.
Start-ScheduledHosting.ps1 copies it to TestResults/hosting-state/plan.json and sets a finite validity.
One simulated site per node; CH country labels are lab configuration, not trusted real-world
location attestation. Values are nominal hourly kWh. Site maximum recovered heat is the shared
HeatSignal server_count times recoverable_kwh_per_server_at_full_load; it is not a new hardware rating.

Example: with demand 0.4 kWh, no tank, one simulated server and 0.8 kWh per full-load server-hour,
the shared scheduler returns 0.5. Demand 0.8 returns 1.0; this ranks that site higher.
This is a scheduler input/output example, not an assertion that HTTP traffic produces those kWh.

Incomplete, nonfinite or expired heat data falls back to round-robin over healthy eligible
replicas and disables scale-down; paused eligible replicas are resumed. Missing/invalid policy
data fails closed (HTTP 503), never overriding locality to maintain service.
Zero demand still serves requests: hosting is always-on, not an interruptible batch job.
Valid targets rank preferred replicas; equal targets use stable node-ID ordering for the retained
pair and round-robin for requests. This initial policy is for controlled demo traffic;
it does not estimate spare HTTP capacity or guarantee throughput.

## Lifecycle safety
The active gateway holds per-replica readiness, draining and request counts behind one lock.
Requests acquire an admission lease BEFORE forwarding and release it after forwarding completes.
Draining prevents new admissions; timeout leaves the old replica running. Readiness and full plan
content are rechecked after drain. A pause requires two other ready eligible replicas.
The plan is mounted read-only into both gateways. Operator updates use atomic file replacement.
No HTTP mutation endpoint exists. Inspect state at /_platform/hosting (synthetic lab metadata).

On leadership change restore eligible candidates before scale-down; reconcile actual readiness
continuously. Docker mutations are checked against actual nested-container state.
A failed outer node is replaced by a prepared spare; creating a destroyed engine is not automated.
Stopped inner apps receive the firewall again before restart. Stateless relocation means deploying
or resuming the same component elsewhere, not live RAM migration or distributed database failover.

## Reproduce
PowerShell 7 from repository root:
    ./webhosting/scripts/Start-ScheduledHosting.ps1 -Build
    ./webhosting/scripts/Test-ScheduledHosting.ps1 -FailureTests
Open http://127.0.0.1:18080.
The Dockerfile now builds from repository root to include src/DigitalHeat.Core; its dedicated
Dockerfile.dockerignore limits context to the required source trees.
Updated Start-IsolatedNetwork.ps1 -Build and ingress.compose.yaml preserve fallback builds.

## Current validation and limits
43 tests pass: existing hosting tests plus shared-scheduler mapping, locality, stale/partial heat,
single-destination denial, admission/drain race protection, replacement failure and restart recovery.
Real Docker test outcomes will be recorded after completion.
No achieved heat/power measurements, LP optimiser, billing or scored-model claims are introduced.
An HTTP success count is not energy. Node administrators and gateway operators can inspect data.
Exclusive host-port ownership is not cross-host consensus/fencing; late remote Docker operations
may survive a killed gateway. Interrupted-transition recovery must be tested, and transient errors
remain possible. The physical laptop and Docker Desktop remain shared dependencies.
