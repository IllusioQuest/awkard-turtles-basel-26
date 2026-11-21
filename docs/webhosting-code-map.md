# Webhosting code map

Code lives on `feature-webhosting`; these docs live on `main` by user request.
Open `webhosting/Webhosting.sln` in Visual Studio. Paths below are relative to the repository.

| File / directory | What is there now |
|---|---|
| `webhosting/src/Webhosting.Contracts/Deployments.cs` | Immutable deployment, component, heartbeat, status and execution-command DTOs. These describe the planned API; endpoints are not implemented yet. |
| `webhosting/src/Webhosting.Contracts/DeploymentValidation.cs` | Input checks for identities, replicas, resources, ports and route prefixes. |
| `webhosting/src/Webhosting.Core/Placement.cs` | Pure deterministic placement with CPU/memory budgets, separate-node replicas and explicit shortfalls. It does not start Docker containers. |
| `webhosting/src/Webhosting.Gateway/Program.cs` | Linux active/standby listener loop, YARP registration, gateway identity and health endpoints. Exclusive port ownership transfers after a crash. |
| `webhosting/src/Webhosting.Gateway/LocalGatewayWatchdog.cs` | Runs the gateway as a child process, probes its identity after activation and terminates an unresponsive worker so the peer can take over. Cannot run while its whole container is paused. |
| `webhosting/src/Webhosting.Gateway/appsettings.json` | Configurable listener/retry values, static backend addresses, round-robin routing and health checks. |
| `webhosting/samples/Webhosting.Sample/Program.cs` | Stateless HTML page, `/health`, `/api/server`; shows serving gateway, app instance and Linux runtime. Frontend and API still share one process. |
| `webhosting/deploy/Dockerfile` | Pinned Linux SDK/runtime images; publishes the gateway or sample as a non-root application. |
| `webhosting/deploy/ingress.compose.yaml` | Two Linux host-network gateways and optional standalone samples for the ingress-only experiment. |
| `webhosting/deploy/engines.compose.yaml` | Three privileged Linux server containers, separate inner Docker daemons/data volumes, loopback-only published app ports. No exposed Docker TCP API or outer socket mount. |
| `webhosting/scripts/Start-Network.ps1` | Test-host setup: loads the sample image into each engine and starts the known replicas. This is bootstrap, not the future controller. |
| `webhosting/scripts/Test-Ingress.ps1` | Repeatable gateway crashes and backend crashes; `-NestedEngines` kills entire simulated app servers. Restores this lab's containers afterward. |
| `webhosting/tests/Webhosting.Tests/` | Seventeen contract/placement test cases, independent of Docker. |
| `webhosting/src/Webhosting.Controller/`, `Webhosting.Node/` | Starter projects only; controller leadership, heartbeats and execution APIs remain pending. |
| `webhosting/src/Webhosting.Infrastructure/` | Placeholder for approved Docker Unix-socket and etcd HTTP adapters. |

## Current request path

```text
Windows browser http://127.0.0.1:18080
  -> Docker Desktop host networking (shared test infrastructure)
  -> active Linux gateway A or B (same port; standby retries binding)
  -> healthy sample in nested Docker Engine A or B
```

Engine C also runs a sample, reachable directly on its configured port, but is not routed or
automatically recruited yet. Gateways currently run beside the engines, not inside those nodes.
All deployed services are Linux containers. PowerShell scripts are Windows test drivers only.

## Next integration work

Resolve whole-container pause recovery and gateway role placement with the user before extending
the network design. Then implement the approved etcd/controller/node adapters and connect pure
placement to real execution, readiness, route updates and replacement after failures.
Use the shared `src/DigitalHeat.Core` `IExecutor`/`IScheduler` boundary for later heat integration;
do not build a separate heat scheduler. No shared heat interface has been changed by this milestone.

See [runtime guide](webhosting-runtime.md) for commands and verified limits.

Whole-container recovery: ContainerPauseMonitor.cs contains the approved external monitor loop; deploy/monitors.compose.yaml opts into two trusted Linux managers; scripts/Test-ContainerPause.ps1 verifies pause recovery and an unrelated-container negative control.

