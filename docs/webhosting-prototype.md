# Distributed webhosting prototype

Status: solution, DTOs/validation, placement, Linux gateway crash recovery and nested-engine samples
implemented on `feature-webhosting`. Controller/node orchestration, pause recovery and full role
resilience remain pending. See [runtime guide](webhosting-runtime.md) and [code map](webhosting-code-map.md).
C#, separate engines and a stable browser URL confirmed by the user.
Ask the user before selecting libraries, project structure or DTO contracts. Once those choices
are agreed, implementation, tests, documentation, commits and pushing are authorized.
Updated: 2026-11-20.
Branch: `feature-webhosting`, created from `origin/main` at `90cea5d`.

## Documentation workflow

User instruction (2026-11-20): publish this task's Markdown documentation directly to `main`
under `docs/`, while code stays on `feature-webhosting`. Use an isolated checkout to commit only
the intended documentation, then integrate `main` back into the feature branch. Never publish
unfinished code to `main` as part of a documentation update. Ask about new design choices;
routine implementation, tests, commits and pushes are authorized after those choices are agreed.

## Objective

Build a small, Flux-inspired hosting platform owned by this project. Docker represents a
network of servers. The platform places application components and replicas on these nodes,
routes requests to healthy replicas, and restores capacity after a node fails or is drained.
This is a local infrastructure demonstrator, not a deployment onto FluxCloud.

The user requested an explicit plan and questions before implementation, prefers Kotlin or C#,
and wants documentation under `docs/` for collaboration with the other builders.

Confirmed follow-up requirements:
- Include a small sample application.
- Controller and gateway failures must be survivable in the first version, not deferred.
- Use C# and separate Docker engines inside simulated node containers (confirmed 2026-11-20).
- Gateway failover must preserve the same URL in an ordinary browser.
- Commit and push completed work on `feature-webhosting`. Ask about design decisions first,
  including libraries, structure and DTOs; do not require another routine commit/push approval.
- The user approved the library/structure/contract proposal and selected Windows / Visual Studio
  with Docker Desktop as the working environment. A separate Ubuntu WSL lab is not selected.
- Clarification: Windows is only the development/test host. Every runtime service, including
  any ingress adapter, gateway, controller and node, must run in Linux containers. No native
  Windows ingress or HTTP.sys runtime is approved.

## Proposed scope and open choices

The user's selected options are marked below. Detailed interfaces and dependencies still need
an ADR and review; team-wide acceptance remains separate.

| Choice | Recommendation | Alternative / question |
|---|---|---|
| Language | C# confirmed; propose .NET 10, ASP.NET Core and YARP | Exact dependencies to be recorded in the ADR |
| Node representation | Separate Docker engines inside simulated node containers, confirmed | Outer laptop/engine remains a shared failure domain |
| App deployment | Explicit manifest for stateless components: image, port, health endpoint, replicas and resources | Small sample app confirmed; frontend/API split remains the proposed example |
| Coordination | Multiple controllers with leader election and replicated desired state; multiple gateways | Required resilience confirmed; exact implementation awaits ADR and review |
| Local demo scale | Three simulated nodes, two app replicas per required component, two controllers, two gateways and three coordination-store members spread across nodes | Proposed configurable demo settings, not measured hardware or case assumptions |

## Language and engine rationale

C# is recommended for implementation convenience: ASP.NET Core APIs, background services for
monitoring/reconciliation, and YARP for the gateway can share one toolchain. This is not a claim
of better reliability or performance than Kotlin. Kotlin/Ktor is equally viable, especially
if it matches the team's experience. Either language can use a separate proxy implementation.
The language of hosted applications is independent of the platform's language.

In a Docker-based deployment, each real server normally runs its own Docker Engine, managing
that server's containers. Containers on one server share that engine; an engine per app is not
needed. Node agents talk to their local engines, while the platform coordinates the agents.
On a laptop, nested engines emulate this boundary; they still share the outer Docker host and
cannot protect against its failure. Using VMs instead would model separate operating systems.

## Proposed failure-tolerant coordination and ingress

- Use an existing consensus store (candidate: a three-member etcd cluster) for desired state
  and a controller leader lease; do not implement a new consensus algorithm for the prototype.
  Three members can retain a majority after one member fails. This is a candidate dependency,
  not yet a stack decision.
- A standby controller takes over after lease expiry. Fence stale controller actions using
  leadership generations validated by the execution path; a paused old leader must not resume
  and issue conflicting commands. Reconciliation must remain idempotent.
- Gateways retain their last valid routes and independently check replica health while a
  controller is unavailable. Coordination loss must not immediately remove existing routes.
- Run redundant services in different simulated failure domains and test loss of a whole node,
  not only a service process. State replication and controller failover must be tested together.
- **Stable browser URL is required:** two gateways alone do not give a browser a stable
  surviving endpoint. Validate a failover-capable network entry (candidate: a virtual IP in
  a suitable Linux network) before building the rest of the prototype. A multi-endpoint test
  client does not satisfy the user's requirement. A single front proxy would reintroduce a
  failure point. Docker Desktop port publishing must not be assumed to move automatically
  between nodes. Host/browser reachability and gateway failure must be proved together.
- Proposed failure budget: survive one simulated node failure with remaining capacity and
  coordination majority. With lost quorum, stop placement changes safely while healthy existing
  workloads continue serving where possible. This budget needs user confirmation.

An arbitrary monolithic application cannot automatically be divided into independent services.
The initial manifest would describe components already separated by the app developer. Each
required component must have replicas on distinct eligible nodes; splitting alone gives no
failure tolerance. Stateful applications need a separate persistence and consistency design.

## Implementation sequence after clarification

1. **Record and review the decision.** Write a proposed ADR for the selected stack, boundaries
   and deployment contract. Obtain the fresh-context review required by `AGENTS.md` section 8,
   record changes, and report its verdict before implementation. Team acceptance remains separate.
2. **Create a vertical slice.** Add an isolated `webhosting/` area with controller, node agent,
   redundant gateways/controllers and a small sample application. Docker Compose starts the
   configured local nodes and the selected replicated coordination store.
   The sample response identifies its serving node and replica.
3. **Deploy and reconcile.** Accept and validate a deployment manifest. Maintain desired and
   observed placement, receive node heartbeats, check readiness and place replicas on distinct
   eligible nodes. Make reconciliation idempotent so retries do not create duplicate replicas.
   Reject impossible placement visibly rather than claiming the replica requirement is met.
4. **Route and recover.** Route only to ready replicas. Exclude failed nodes, maintain service
   through surviving replicas, and replace missing replicas where capacity permits. A planned
   drain starts replacement capacity, waits for readiness, removes old routes, then waits for
   active requests before stopping or pausing the old workload. If spare capacity is absent,
   retain service and report that the requested drain cannot safely complete.
5. **Connect heat signals.** Keep placement policy independent of Docker and HTTP. Coordinate
   its input/output contract with the team's shared simulation scheduler; do not create a
   separate demo-only heat policy. Until integrated, label manually supplied node eligibility
   as a test input, not a physical simulation or scored result. Availability constraints can
   prevent a heat-driven shutdown; expose that conflict rather than silently dropping service.
6. **Demonstrate and verify.** Provide a minimal status page showing nodes, replica placement,
   readiness and events. Run a repeatable request probe during node failure, recovery and drain.
   Report failed requests and observed recovery time; do not assume perfect availability.
7. **Document and hand off.** Update this document with actual commands, implementation status,
   API and manifest examples, test results, limits and integration notes. Keep planned and
   completed features distinct. Open a focused PR after implementation and relevant checks.

## Proposed validation

- Unit tests: deterministic placement, node eligibility, replica separation, insufficient
  capacity, idempotent reconciliation and readiness-before-drain behavior.
- Integration scenario: deploy, verify distinct serving replicas, abruptly stop a logical node
  and its workloads, observe surviving service, restore capacity, then rejoin the node.
- Planned pause scenario: drain before pause, observe traffic on remaining nodes, then resume.
- Controller failure: kill the active controller, verify takeover and persistent deployment
  state, then resume the old controller and verify stale actions cannot mutate placement.
- Gateway failure: verify service at the same browser URL without manual rerouting or a
  client-specific list of gateway endpoints.
- Coordination failure: lose one store member and continue reconciliation; lose quorum and
  verify safe suspension of mutations without unnecessarily stopping existing workloads.
- Component scenario if selected: independently replicate frontend and API and exercise their
  connection through stable service routing.
- Run after dependencies/images are cached with external networking unavailable, as required
  for the laptop demonstration. Check the small status page visually, not with layout tests.

## Boundaries and integration with the team

- All simulated nodes share a laptop and outer Docker engine. This tests simulated node and
  service failures, not laptop or outer-engine failure. Controller/gateway redundancy is now
  required; a resilient browser entry point is required and its implementation needs validation.
- Existing requests may fail when their serving node crashes; surviving replicas provide
  subsequent service. Do not automatically retry non-idempotent requests.
- Relocation means starting a new stateless replica, not transferring a running process's memory.
- Trusted local demo workloads and synthetic data are the proposed trust model. Distributing
  code does not conceal plaintext from a node executing it. Docker control belongs to trusted
  orchestration components; customer workloads must not receive the host Docker socket.
- No blockchain, token, payments, arbitrary code partitioner or database replication is implied
  by this first proposal. These would need explicit scope choices.
- Coordinate the deployment/placement contract with the teammate building scheduling before
  fixing it. Keep this prototype's language choice local until the team chooses a shared stack.
- [The case](case/challenge.md), [checklist](case/key-figures.md) and [draft vision](vision.md)
  still require two characterized workload types, a measured real job paused/resumed by the
  same scheduler as the yearly simulation, and the reference-house annual balance. Hosting
  failover alone does not satisfy those requirements. This document does not mark them complete.
- Traffic, power, energy and income are not measured yet. Any later numerical model must use
  measurements or sourced inputs, with units and provenance; missing inputs are `TODO(source)`.

## Earlier implementation history (superseded by runtime guide)

- Read the project instructions, case, research plan and current team vision/questions/tasks.
- Fetched the latest `origin/main` and created the requested local `feature-webhosting` branch.
- Confirmed .NET SDK `10.0.401`, Java `25.0.4.1` and Docker CLI `29.8.2` are installed.
- Docker CLI could not connect to the Docker Desktop Linux engine; container tests have not run.
- Added this plan and a buildable six-project Visual Studio solution at
  `webhosting/Webhosting.sln`; three starter HTTP services still use template responses.
- No distributed-hosting implementation, workload execution or failure recovery exists yet.
- Verified `dotnet restore Webhosting.sln` and `dotnet build Webhosting.sln --configuration
  Release --no-restore` from `webhosting/`: all six projects build with zero warnings/errors.
  No behavioral tests exist yet; this check verifies the scaffold only.
- Updated the plan after user feedback to require controller/gateway failure recovery, a
  small sample app, C#, independent node engines and failover at the same browser URL.
- Code commit `753ea19` adds DTOs, validation and deterministic capacity-aware placement.
  Release build with `-warnaserror` succeeds; all 17 xUnit cases pass. These are pure contract
  and placement tests, not evidence of live gateway/controller recovery.
- Docker Desktop is now running. Pulled the nested-engine image `docker:29-dind`, resolved as
  `docker@sha256:4bbc7223f81db790097c6dea42fb93e05b8fead4f0431fbe6ccaba6e8473201c`.
- The user rejected a single adapter: ingress must also survive failure. The Windows-native
  candidate in [ADR 0004](decisions/0004-redundant-windows-ingress.md) was then withdrawn after
  the Linux-only runtime clarification. No adapter has been created. Linux ingress is unverified.

## Placement method (implemented, infrastructure only)

Inputs are the complete desired workload set, each workload's replica/CPU/memory requirements,
node health/capacity/eligibility, and existing assignments. Keep healthy existing assignments
first. Fill gaps on eligible nodes with sufficient remaining CPU and memory, with one replica
of a workload per node. Prefer fewer assigned replicas, breaking ties by ordinal node ID.
For each assignment subtract its CPU/memory limits from the node's available budget. Report
any unplaced replicas as shortfalls; do not silently colocate replicas or overcommit.

Worked example from synthetic test fixtures: three nodes each have 2 CPU cores and 512 MiB;
API and frontend each request two replicas, each using 1 core and 128 MiB. Losing one node
leaves 2 x 2 = 4 cores, sufficient for four replicas (4 x 1 = 4 cores), with one API and one
frontend per survivor (2 x 128 = 256 MiB per node). This is a test fixture, not measured server
capacity or a heat/energy result. Rejecting new work does not evict running replicas; a future
drain coordinator must make replacement capacity ready before stopping existing work.

## Technical references

Official documentation checked on 2026-11-20:

- [YARP reverse proxy](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/getting-started)
- [.NET background workers](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Ktor server projects](https://ktor.io/docs/server-create-a-new-project.html)
- [Docker Compose application model](https://docs.docker.com/compose/intro/compose-application-model/)
- [Docker pause](https://docs.docker.com/reference/cli/docker/container/pause/)
- [Docker Engine architecture](https://docs.docker.com/engine/)
- [etcd quorum and failure tolerance](https://etcd.io/docs/v3.6/faq/)
