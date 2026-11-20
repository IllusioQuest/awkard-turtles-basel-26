# Webhosting prototype workspace

Status: buildable solution scaffold. Distributed hosting and failure recovery are not implemented.

## Open in Visual Studio on Windows

1. Use Visual Studio with .NET 10 support and the **ASP.NET and web development** workload.
2. Choose **Open a project or solution**, then select `webhosting/Webhosting.sln`.
3. Build the solution. No Docker engine is required to build this initial scaffold.
4. To run one starter service, select Controller, Node or Gateway as the startup project.
   Its `/` endpoint currently returns the ASP.NET template's `Hello World!`, not a hosting API.

The SDK is pinned in `global.json` to the installed `10.0.401`, allowing newer patches in that
feature band. Run the commands below from this directory so that the SDK selection applies.

```powershell
dotnet restore Webhosting.sln
dotnet build Webhosting.sln --configuration Release --no-restore
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

The three libraries are intentionally empty; no placeholder business logic or tests are included.
Project references are wired, but YARP, etcd integration and xUnit tests will be added with their
implementations. Samples, container deployment files and failure scripts are the next milestone.

## Agreed environment and next steps

- Develop in Windows / Visual Studio; use Docker Desktop for Linux node containers with separate
  inner Docker engines. Do not install a separate Ubuntu WSL development environment.
- First validate how an ordinary Windows browser can retain its URL while either gateway fails.
  Floating-IP reachability through Docker Desktop remains unproven. Do not interpret this
  scaffold as a working highly available network.
- Then implement the reviewed design, tests and sample application. Ask the user before new
  library, structure, DTO or ingress-design choices.

See [design proposal](../docs/webhosting-design-proposal.md),
[implementation plan](../docs/webhosting-prototype.md) and
[ADR 0003](../docs/decisions/0003-webhosting-prototype.md).
