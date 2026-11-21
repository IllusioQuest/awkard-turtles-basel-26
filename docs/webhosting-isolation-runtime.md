# Run the component-isolated hosting prototype

Open webhosting/Webhosting.sln in Visual Studio. Frontend and API are separate projects under
samples/. Gateways retain the existing watchdog and paused-container monitor code.
All runtime services are Linux containers; Windows runs test scripts and Docker Desktop.

## Run
From the repository root, PowerShell 7, Docker Desktop Linux engines with host networking enabled:
    ./webhosting/scripts/Start-IsolatedNetwork.ps1 -Build
Open http://127.0.0.1:18080. The script stops only the old lab's gateways and monitors.
The four new node engines use separate data volumes and distinct backend ports 18481..18484;
existing old sample engines are left intact. These ports are lab configuration, not model inputs.
Inner engines expose only their Unix Docker sockets; application containers never receive them.

## Request path
~~~mermaid
flowchart TD
  B["Browser · same URL :18080"] --> G["Gateway A / B · active / standby"]
  M["Recovery monitor A / B"] --> G
  G -->|"/"| F1["Linux node A · frontend"]
  G -->|"/"| F2["Linux node B · frontend"]
  G -->|"/api/server"| A1["Linux node C · API"]
  G -->|"/api/server"| A2["Linux node D · API"]
~~~
Each node has an independent nested Docker Engine and one non-root component container.
Frontend images contain only the frontend project; API images contain only the API project.
The browser, not the frontend server, calls the API through the gateway. There is no database,
private payload or shared component volume. JSON API properties use snake_case.
Routes are static and round-robin among healthy destinations. No heat-aware placement exists yet.

## Network boundary
Each engine has one app on its default inner bridge. app-firewall.sh installs dedicated
DOCKER-USER forwarding and INPUT host-access chains before application startup.
New inbound TCP to the configured app port and established replies are allowed.
New outbound app connections are dropped, including peer nodes, engine services and internet.
IPv6 is disabled on this lab's inner bridge; the verification refuses an IPv6-enabled bridge.
The privileged engine and outer Docker administrator can override these rules; they are trusted.
Loopback-published backend ports remain accessible to the Windows host for diagnostics.
Applications have read-only root filesystems, temporary /tmp, no added capabilities,
no-new-privileges, and no Docker socket or persistent/shared volume.

## Verify
    ./webhosting/scripts/Test-IsolatedNetwork.ps1
    ./webhosting/scripts/Test-IsolatedNetwork.ps1 -FailureTests
Checks all component endpoints and expected 404s, container hardening, distinct engine identities,
peer positive controls and app-namespace denial of peer/host/internet traffic with drop counters.
Failure tests kill each node in turn, require five consecutive successful page/API probes,
restore it, then pause the active gateway and verify monitor-driven recovery.
Results are in webhosting/TestResults/isolation-results.json (local, ignored).
Recovery seconds measure until five successful probe pairs, not first success or an SLA.
Failed probes count test attempts only; in-flight requests can fail and a browser may wait longer.

## Restore nodes or switch back
A restarted engine does not automatically start its component. This prevents serving before
the firewall is reinstalled. Restore one node explicitly:
    ./webhosting/scripts/Start-IsolatedNetwork.ps1 -Nodes frontend-a -SkipGatewaySwitch
The other replica in that pool serves traffic while the node is restored. Controller-driven
replacement is still future work; this is a manually bootstrapped resilience prototype.

To return to the original lab without deleting any volumes:
    docker compose -f webhosting/deploy/isolation.compose.yaml stop
    ./webhosting/scripts/Start-Network.ps1
    docker compose -f webhosting/deploy/monitors.compose.yaml up -d

## Privacy limits
Read webhosting-privacy.md for source review and the failed internal-network experiment.
Component isolation limits a compromised app's reach; it does not provide confidentiality
against node operators. Gateways see HTTP data. The laptop, Docker Desktop, engine administrators
and outer-socket monitoring are trusted shared dependencies. This is synthetic-only local HTTP.
T-24 owns future locality policy; shared heat-scheduler integration remains a separate T-23 probe.

## Measured verification, 2026-11-21
Both images built; 17 existing hosting tests passed; browser screenshot inspection showed a
frontend-a page with separately changing API identities. All four nodes passed component-route,
hardening, distinct-engine, peer positive-control and blocked-egress checks.

| Failure injected | Seconds until five successful page/API probes | Failed probes |
|---|---:|---:|
| frontend-a killed | 2.392 | 2 |
| frontend-b killed | 2.033 | 2 |
| api-c killed | 5.281 | 1 |
| api-d killed | 2.101 | 1 |
| active gateway-b paused | 24.645 | 7 |

Method: Test-IsolatedNetwork.ps1 -FailureTests, 250 ms between probe pairs, 3 s per HTTP request,
75 s recovery timeout. Measurements are a local run, not a production availability guarantee.
The timer starts after Docker returns from fault injection and ends on the fifth consecutive
successful page/API pair. Restore time is excluded. Results contain only synthetic node identity.
Both gateways and all four application nodes were restored after testing.
