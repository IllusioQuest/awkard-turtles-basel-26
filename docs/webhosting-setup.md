# Webhosting prototype workspace

Status: buildable solution with deployment contracts, validation and pure placement tests.
Live distributed hosting and failure recovery are not implemented. Code is on `feature-webhosting`;
documentation is published directly to `main` under `docs/` at the user's request.

## Open in Visual Studio on Windows

1. Use Visual Studio with .NET 10 support and the **ASP.NET and web development** workload.
2. Choose **Open a project or solution**, then select `webhosting/Webhosting.sln`.
3. Build the solution. No Docker engine is required to build this initial scaffold.
4. To run one starter service, select Controller, Node or Gateway as the startup project.
   Its `/` endpoint currently returns the ASP.NET template's `Hello World!`, not a hosting API.

The SDK is pinned in `global.json` to the installed `10.0.401`, allowing newer patches in that
feature band. Run the commands below from the webhosting directory so that the SDK selection applies.

```powershell
dotnet restore Webhosting.sln
dotnet build Webhosting.sln --configuration Release --no-restore
dotnet test Webhosting.sln --configuration Release
```

## Projects

| Project | Intended responsibility |
|---|---|
| Webhosting.Core | Pure placement rules and domain models |
| Webhosting.Contracts | Versioned HTTP DTOs, separate from domain models |
| Webhosting.Infrastructure | Docker and etcd adapters |
| Webhosting.Controller | Deployment API and reconciliation leadership |
| Webhosting.Node | Node-local execution and heartbeats |
| Webhosting.Gateway | YARP routing and health checks |

Core contains deterministic placement; Contracts contains the agreed DTOs and input validation.
Infrastructure is still empty. The xUnit project tests contracts, capacity limits, replica
separation, deterministic reconciliation and placement after simulated node loss. It does not
exercise real Docker nodes yet. Samples, YARP/etcd integration and failure scripts remain pending.

## Agreed environment and next steps

- Develop in Windows / Visual Studio; use Docker Desktop for Linux node containers with separate
  inner Docker engines. Do not install a separate Ubuntu WSL development environment.
- All infrastructure services, including ingress and gateways, must run in Linux containers.
  Native Windows ingress was only a proposal and was never implemented. Windows runs the IDE,
  build/test tools, browser and Docker Desktop, not the deployed application infrastructure.
- First validate how an ordinary Windows browser can retain its URL while either gateway fails.
  Floating-IP reachability through Docker Desktop remains unproven. Do not interpret this
  scaffold as a working highly available network.
- Then implement the reviewed design, tests and sample application. Ask the user before new
  library, structure, DTO or ingress-design choices.

See [design proposal](webhosting-design-proposal.md),
[implementation plan](webhosting-prototype.md) and
[ADR 0003](decisions/0003-webhosting-prototype.md).
