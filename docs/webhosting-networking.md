# Windows ingress adapter proposal

Status: the user rejected the single-adapter failure boundary AND clarified that all runtime
services must run in Linux containers. Neither the static adapter nor the Windows-native ingress
proposal was implemented. Designs below are retained as history, not implementation instructions.

## Confirmed Windows / Linux boundary

| Windows development/test host | Linux containers under Docker Desktop |
|---|---|
| Visual Studio, Git, PowerShell, .NET SDK for builds/tests, browser, Docker Desktop UI/CLI | Ingress adapters if needed, gateways, controllers, etcd members, node agents, per-node Docker engines, hosted apps |

Docker Desktop supplies the Linux VM/kernel for these containers. C#/.NET services target Linux
container images; they do not need native Windows services, IIS or HTTP.sys. The SDK currently
builds/tests code on Windows, but no distributed runtime has been launched and no native ingress
was implemented. The Windows laptop remains the shared physical test host.

## Linux ingress candidate to investigate

Docker Desktop supports opt-in host networking for Linux containers, at TCP/UDP layer 4.
Investigate two Linux ingress containers with active/standby ownership of the same listening port
and test recovery at the unchanged Windows-browser URL. This may avoid both a separate adapter
and Windows-native services. It is a candidate, not a verified solution or approved change to
Docker Desktop settings. Do not assume Linux floating-IP/VRRP behavior traverses Desktop's
layer-4 integration. Ask before enabling host networking or implementing a revised design.

Source checked 2026-11-20:
[Docker host networking](https://docs.docker.com/engine/network/drivers/host/).

## Purpose

Windows browsers can reach Docker Desktop published ports, but a port published on one gateway
does not automatically move when that gateway fails. The proposed adapter is a small Linux
container that acts as a virtual router between a published localhost port and a floating IP.
It uses static packet forwarding, not application-aware proxying or backend health decisions.

```text
Windows browser: fixed localhost URL
    -> Docker Desktop published port
    -> network adapter: static packet forwarding
    -> floating IP owned by one gateway
    -> gateway chooses a healthy app replica
```

Keepalived would monitor the gateways and move ownership of the floating IP after a gateway
failure. The adapter's forwarding destination stays unchanged. The browser keeps its URL;
connections active during failure can still break and subsequent requests may recover after
detection and takeover. This behavior needs actual testing before making availability claims.

## Failure boundary

Test failure of either gateway and each simulated server. Treat the adapter, Docker Desktop,
outer Docker networking and laptop as shared test infrastructure that stays up. If the adapter
fails, this local demo would become unreachable. It is therefore NOT a demonstration that every
part of the entire network is redundant. A production deployment needs resilient ingress routing
as well, or a suitable network where the floating IP is reachable without this extra adapter.

This candidate avoids installing a separate Ubuntu WSL development environment or adding a
single application proxy in front of the redundant gateways. It needs elevated networking
capabilities inside its container and an isolated demo network. It must not modify unrelated
Docker networks or workloads.

## Approval requested

The user answered that adapter failure must also be covered. Do not implement this single-adapter
design. The revised proposal must have redundant ingress and verify failure of the instance that
first owns the stable address, not only a secondary worker. The shared laptop/OS failure boundary
must be explicit; actual host-failure tolerance requires independent hosts.

## Withdrawn Windows-native candidate (history)

Run two Windows-native C# / YARP ingress processes using Kestrel and the same explicit localhost
address/port. Only one binds successfully; the other stays alive and retries. If the active process
crashes, the standby attempts to bind that same endpoint. Both can route to either Docker gateway.
No additional front proxy or singleton supervisor is introduced. This replaces the proposed static
Docker adapter, and does not need Keepalived for the Windows-facing endpoint.

This is a new design choice awaiting approval, not an implemented solution. Windows exclusive-port
reuse timing must be measured. Test crashes of either ingress process and both startup orders,
then restore redundancy and repeat. Killing a process can interrupt requests; an indefinitely
hung listener still holding its socket needs additional recovery handling outside this candidate.
See [ADR 0004](decisions/0004-redundant-windows-ingress.md) for the review and test criteria.
