# Windows ingress adapter proposal

Status: explanation requested by the user; not approved, implemented or verified.

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

Does this test boundary match the user's intended prototype, or must ingress-adapter failure
also be covered? Wait for the answer before creating the adapter. If approved, first run a small
gateway-only experiment, measuring recovery and failed requests at one unchanged Windows URL.
Do not claim the design works until that experiment passes.
