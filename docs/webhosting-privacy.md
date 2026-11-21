# Hosting privacy and component isolation (T-27)

Status: four-node prototype verified on feat/T-27-hosting-isolation; PR #12 ready for review. Current commands/results: [runtime guide](webhosting-isolation-runtime.md). Do not use real personal data.
User approved component isolation first on 2026-11-21; confidentiality from node operators is
outside this increment. See decision 0012.

## Research and implications
Sources accessed 2026-11-21:
- [Oasis privacy overview](https://oasis.net/blog/oasis-101-web3-privacy-what-is-it):
  privacy must be designed explicitly. Oasis describes confidential execution with TEEs;
  splitting a normal Docker application does not reproduce that protection.
- [Pharos / Medium](https://medium.com/pharos-production/privacy-and-security-in-web3-d993ff441a69):
  discusses distributed encrypted storage and user control. Direct access returned 403;
  the search index supplied the article text. Its encryption claim is not a property of
  arbitrary distributed hosting. Our current app has neither encrypted distributed storage
  nor user-controlled keys.
- [Startupik overview](https://startupik.com/web3-privacy-explained/):
  distinguishes application, identity, transaction and network privacy. This is conceptual
  background, not technical evidence that any specific framework gives us those guarantees.
- [Docker Engine security](https://docs.docker.com/engine/security/):
  namespaces and capabilities provide isolation, but daemon control is privileged.
  Application containers must not receive Docker sockets or capabilities to change policy.
- [IPFS privacy](https://docs.ipfs.tech/concepts/privacy-and-encryption/):
  consult before any future storage design; do not assume sharding or content addressing
  encrypts content. No IPFS dependency is added by this ticket.

Engineering conclusion: replication gives availability; separating components reduces what
each application receives; encryption and key custody are separate requirements.
Do not automatically parse an arbitrary app into secure fragments. Developers must define
frontend/API/storage boundaries and their explicit data contracts.

## Baseline lab and implemented topology
The original fallback lab: two Linux gateways route to identical combined HTML/API samples
on engine A/B. Engine C exists but is outside routing. Local watchdogs and two outer-engine
monitors recover gateway failures. Shared heat scheduling only controls a disposable probe.

Implemented opt-in topology (firewall revision and results below):
~~~mermaid
flowchart TD
    browser["Windows browser: localhost:18080"] --> gateways["Linux gateways A/B"]
    monitors["Two trusted Linux recovery monitors"] --> gateways
    gateways -->|"page route"| frontend["Frontend pool: Linux engines A/B"]
    gateways -->|"/api route"| api["API pool: Linux engines C/D"]
    frontend -. "no app-initiated API connection intended" .-> api
~~~
Each pool has two replicas so loss of one node need not remove the component.
Every runtime component is Linux. Windows hosts Docker Desktop, scripts, browser and IDE.
The browser calls the API through the same gateway origin; frontend code contains no server
data-store access or credentials. API returns only synthetic component/node identity.
Frontend and API are separate projects/images, not a runtime switch in a single combined image.

## Threat boundaries
- Frontend nodes serve public assets. API nodes execute API code and see API inputs.
- A compromised application should have no Docker socket, no capabilities, no shared volumes,
  a read-only root filesystem and only explicitly allowed network access.
- Gateway operators can see HTTP requests, response bodies and connection metadata.
- Node administrators can inspect containers and memory; outer Docker administrators control
  all simulated nodes. Privileged dind and monitoring socket access make this a trusted lab.
- This does not provide end-to-end encryption, anonymity, protection from malicious node
  operators, real data-residency attestation, or independent physical failure domains.
- New input/network/access logging must avoid credentials, tokens and payload bodies.
- Future placement must reuse Jona's T-24 locality eligibility; heat demand must not override it.
- Storage, authentication and private workloads require separate agreed designs. A database
  cannot simply be placed on every node without increasing data exposure.

## Verification so far
Both independent .NET 10 Linux images build successfully with the existing pinned base images.
One disposable sample ran non-root with read-only root, dropped capabilities and no-new-privileges
on an internal inner bridge. Its process listened successfully, but HTTP through the published
port failed (premature response); engine-local published-port access was refused too.
The network namespace had no default route and numeric internet access returned Network
unreachable. A hostname lookup failed; that is not proof of a working host-access firewall.

Therefore internal-bridge configuration is NOT accepted as a working topology.
Historical next step (subsequently approved and verified below): ordinary inner bridges plus per-engine firewall rules that admit inbound
HTTP and established replies but block new app-initiated connections to host, peer nodes and
internet. No unrestricted-network fallback has been deployed. Full four-node routing and failure
tests remain pending. Existing lab gateways were not stopped by the experiment.

## Approved revision and first successful network check
User approved normal inner bridges with per-node firewall rules. Internal bridge proposal is
superseded after the ingress failure. The frontend now returns HTTP 200 through its published
port; app-namespace probes to the engine bridge host and internet timed out, with matching
DROP counters in INPUT/DOCKER-USER chains. Full pool/failure checks subsequently passed; see the runtime guide for measured outcomes.
Application restart policy is no: after engine failure, Start-IsolatedNetwork.ps1 reapplies
rules before starting the app. The other pool replica serves traffic during manual recovery.
No automatic node re-provisioning is claimed.


