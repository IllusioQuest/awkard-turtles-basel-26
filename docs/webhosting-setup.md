> RETIRED (T-40): the user withdrew webhosting. This page is historical evidence; its code and setup commands are no longer supported. See [gaming profiles](gaming-profiles.md).

# Webhosting prototype workspace

Code is on `feature-webhosting`; documentation is published directly to `main` under `docs/`.
The current milestone includes placement/contracts, Linux gateways, a sample application and three
separate nested Docker engines. Controller/node orchestration and pause recovery remain pending.

## Open in Visual Studio on Windows

1. Use Visual Studio with .NET 10 support and the ASP.NET and web development workload.
2. Open `webhosting/Webhosting.sln`.
3. Build the solution. Docker is not needed for the build or pure unit tests.
4. Run the Linux infrastructure through the Docker commands in the [runtime guide](webhosting-runtime.md).
   Selecting Controller or Node as a startup project still runs a template placeholder.

The SDK is pinned in `webhosting/global.json` to 10.0.401 with patch roll-forward.
From the `webhosting` directory:

```powershell
dotnet restore Webhosting.sln
dotnet build Webhosting.sln --configuration Release --no-restore -warnaserror
dotnet test Webhosting.sln --configuration Release
```

See the [code map](webhosting-code-map.md) for each project's implemented and planned responsibilities.
See the [runtime guide](webhosting-runtime.md) for Docker startup, failure tests and measured limits.

Windows runs Visual Studio, build/test tools, browser and Docker Desktop. All deployed services are
Linux containers. Host networking was enabled with the user's approval. No native Windows ingress,
HTTP.sys service, additional Ubuntu WSL development environment or separate adapter is required.
The laptop and outer Docker Desktop engine remain shared test infrastructure.

The [design proposal](webhosting-design-proposal.md), [implementation plan](webhosting-prototype.md)
and [ADR 0003](decisions/0003-webhosting-prototype.md) describe the full target. The working crash
experiment in [ADR 0006](decisions/0006-linux-host-network-ingress.md) is only one milestone toward it.
Ask the user before new library, structure, DTO or ingress design choices.
