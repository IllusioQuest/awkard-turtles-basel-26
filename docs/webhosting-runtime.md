> RETIRED (T-40): the user withdrew webhosting. This page is historical evidence; its code and setup commands are no longer supported. See [gaming profiles](gaming-profiles.md).

# Linux webhosting runtime experiment

Status: a runnable networking milestone on `feature-webhosting`, not completed distributed
orchestration. See [code map](webhosting-code-map.md) and [ADR 0006](decisions/0006-linux-host-network-ingress.md).

## Environment and startup

Measured on Windows with Docker Desktop 4.94.0, Linux engine 29.8.2 and .NET SDK 10.0.401.
Docker Desktop **Settings > Resources > Network > Enable host networking** was enabled with
the user's permission. Every deployed service is Linux-based; no native Windows proxy was added.
YARP is pinned to 2.3.0; Docker image digests are in the Dockerfile and engine Compose file.
The pinned inner engine reports version 29.10.0, independently of Desktop's outer engine.
Nested engines are privileged test infrastructure for trusted sample workloads, not a tenant
security boundary. Docker's control API listens only on each engine's local Unix socket.

From the repository's `webhosting` directory, using PowerShell:

```powershell
dotnet build Webhosting.sln --configuration Release -warnaserror
dotnet test Webhosting.sln --configuration Release
docker compose -f deploy/ingress.compose.yaml build gateway-a sample-a
./scripts/Start-Network.ps1
./scripts/Test-Ingress.ps1 -NestedEngines
```

Open **http://127.0.0.1:18080**. The page displays the gateway and application instance that served
each request. Three inner engines have distinct `docker info` IDs, recorded in ignored
`webhosting/TestResults/engines.json`. Samples run inside each engine. Only A and B are routed;
C is initialized but not yet used by a controller. Bootstrap replaces only this lab's named sample.

Images and packages must be cached before an offline demo; a disconnected run has not been verified.
`Start-Network.ps1` assumes the images have been built. It stops the standalone sample containers,
loads the image into the inner engines and starts the gateways. It is not an automatic scheduler.
The official dind entrypoint is retained for crash cleanup and cgroup initialization. The image
archive is copied through `/root` because dind mounts a private `/tmp`.

To stop the lab without deleting stored engine data:

```powershell
docker compose -f deploy/ingress.compose.yaml stop
docker compose -f deploy/engines.compose.yaml stop
```

For the smaller standalone ingress experiment, stop the engines first, then run
`./scripts/Test-Ingress.ps1` without `-NestedEngines`. Do not run both test modes concurrently.
Both scripts restore/start only their named lab services; tests intentionally kill those services.

## Method and measurement meaning

The probe requests `/api/server` at one unchanged URL using `HttpClient`. It checks the gateway
header and sample identity. Crash scenarios kill one container with SIGKILL. The worker-freeze
scenario uses SIGSTOP while its container's watchdog stays runnable. The stopwatch starts only
after the injection command returns. It does not measure requests interrupted during that command,
full outage duration, or an availability percentage. JSON field names retain `AfterKillCommand`
for compatibility; in the freeze row this means after the SIGSTOP command.

Gateway checks kill the standby, verify the active response, restart the standby, wait for its
current-start `GATEWAY_STANDBY` log, then kill the active gateway. The response must come from the
other instance. Outer gateway restart policies are disabled: restarting the failed gateway cannot
explain takeover. Backend checks kill Engine A then B separately and require responses from the
survivor. Each killed engine is restarted and its sample must return before the next scenario.

Defaults are synthetic test choices: 20-second acceptance timeout, 100 ms between probes,
1,500 ms HTTP timeout, two gateway rounds, 20 consecutive successes for backend confirmation.
They are not a user-agreed performance SLA. Durations use a monotonic stopwatch; failed probes
are counted between injection completion and confirmation. First-success and confirmation times
are reported separately, because a first success can precede further transient failures.
YARP's passive failure detection window is explicitly five seconds. Its implementation clamps
reactivation to at least that window; leaving the default one-minute window caused the original
20-second rejoin test to fail despite active health checks becoming healthy. The shorter lab
window is configured in appsettings, not a production tuning recommendation.
For example, a healthy 20-probe streak includes at least 19 x 0.1 = 1.9 seconds of intentional waits.
Runtime settings live in gateway appsettings and Compose; matching backend ports currently need
coordinated edits in both files. They are lab wiring, not measured hardware capacity.

Results are written to ignored `TestResults/network.json` (nested mode) or `ingress.json`.
Completed run on 2026-11-20, default probe settings, at the unchanged URL above:

| Fault | Survivor | First success after command (s) | Confirmation after command (s) | Failed post-command probes |
|---|---|---:|---:|---:|
| Kill gateway B | Gateway A | 1.099 | 1.099 | 0 |
| Kill gateway A | Gateway B | 1.009 | 1.009 | 0 |
| Freeze gateway B worker | Gateway A | 6.990 | 6.990 | 4 |
| Kill entire app-server Engine A | App on Engine B | 0.145 | 3.548 | 6 |
| Kill entire app-server Engine B | App on Engine A | 0.123 | 3.739 | 7 |

Gateway confirmation requires one successful response; backend confirmation requires 20 in a row.
These are one local run's observations, not worst-case bounds or zero-downtime claims. In particular,
zero failed post-command probes says nothing about requests during Docker's kill command.

## Verified behavior and limits

- Release build: zero warnings/errors. All 17 placement/contract tests pass.
- Visual check: the page renders and identifies the runtime as Ubuntu Linux.
- The three nested Docker engines report distinct identities; each runs its own sample container.
- In this Desktop environment, .NET dual-mode loopback sockets caused proxy connection failures.
  `DOTNET_SYSTEM_NET_DISABLEIPV6=1` is scoped to the gateway containers and fixes the tested IPv4
  loopback path; no Windows/global IPv6 setting was changed.
- A paused or hung active gateway keeps its listening socket. This design does **not** recover
  from an entirely frozen container. The user approved a local watchdog: it probes the active
  worker's identity every second, with a one-second timeout, and kills it after three consecutive
  failures. The supervisor then exits instead of racing the standby to restart. These are configurable
  lab values in `Gateway:Watchdog`. A standby does not probe the shared URL as its own health check.
  A separate process-freeze test verifies watchdog intervention. Entire-container pause recovery
  still requires an independently runnable monitor with authority to release the frozen listener.
- Gateways are outside the emulated app servers. No co-located controller/store/gateway node-loss
  test, controller failover, etcd quorum behavior, automated replacement or safe drain exists yet.
- No separate adapter exists, so there is no adapter container to fail. Docker Desktop's own
  networking, outer engine, kernel and physical laptop remain shared failure domains. Protecting
  against their loss requires independent hosts; this experiment cannot demonstrate that.
- Static stateless replicas are implemented. Automatic splitting of arbitrary applications,
  database replication, in-flight request preservation and production security are not implemented.
- This milestone does not fulfill the heat simulation, two-workload measurements or shared-scheduler
  real-job requirements; integration must use the team's existing C# core interfaces.

Source for the host-network capability (checked 2026-11-20):
[Docker host networking](https://docs.docker.com/engine/network/drivers/host/).
Passive health semantics: [YARP health checks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/dests-health-checks)
and [YARP 2.3.0 policy source](https://github.com/dotnet/yarp/blob/v2.3.0/src/ReverseProxy/Health/TransportFailureRateHealthPolicy.cs).
Behavior and measurements here come from the local scripts, not a vendor availability guarantee.

## Whole-container pause milestone (2026-11-21)

The user approved two trusted Linux monitors with access to the outer Docker socket (ADR 0007).
Implementation: `webhosting/src/Webhosting.Gateway/ContainerPauseMonitor.cs`, activated only by
`webhosting/deploy/monitors.compose.yaml`. This supersedes the earlier whole-container pause
limitation above. Both monitors remain outside the gateway containers. No native Windows service
is added. Each checks gateway identity, requires repeated failures, rechecks paused state and
Compose labels, then stops the immutable container ID. Docker stop released the paused listener
without a separate unpause operation in the initial feasibility experiment.

From `webhosting`, after starting the existing network:

```powershell
docker compose -f deploy/ingress.compose.yaml build gateway-a
./scripts/Test-ContainerPause.ps1
```

The script enables the monitors, pauses the active gateway with either monitor stopped and then
with both running, checks the new gateway at the unchanged URL, and restores the lab. A paused
Engine C is the negative control: it must remain paused throughout these faults. Result JSON is
in ignored `TestResults/container-pause.json`. To stop the new managers explicitly:
`docker compose -f deploy/monitors.compose.yaml stop`.

These measurements start after the pause command, count failed HTTP probes and end at the first
successful response from the other gateway. Default test timeout is 45 seconds; this is not an SLA.
Monitor defaults: three failed probes, one-second interval, five-second HTTP/Engine timeout.
The stop operation requests a two-second grace period. Sequential checks and Docker delays can
extend recovery considerably. Measured run (seconds rounded by the test script):

| Missing monitor | Paused gateway | Survivor | Seconds after pause command | Failed probes |
|---|---|---|---:|---:|
| monitor-a | gateway-b | gateway-a | 24.328 | 10 |
| monitor-b | gateway-a | gateway-b | 30.823 | 14 |
| none | gateway-b | gateway-a | 43.397 | 19 |

All three scenarios passed; the unrelated paused container was untouched. Concurrent managers produced bounded request timeouts and retried safely in this run. Recovery was slow, with failed requests; this is not seamless availability. Adversarial stale-identity/recovery-race tests remain pending. The 17 existing core/contract tests pass; the Linux image builds. The socket grants broad host authority despite application allowlists. Controller/etcd orchestration, automatic placement repair, role co-location and host-loss resilience remain unfinished.

