# 0004: Investigate redundant Windows ingress

- **Status:** proposed
- **Date:** 2026-11-20
- **Decided by:** user requires adapter resilience; implementation choice awaits approval

## Context

The user rejected a single Docker packet-forwarding adapter even as test infrastructure.
Keep one browser URL, Windows development, separate Docker engines and redundant service roles.
The laptop/OS remain common infrastructure; physical-host failure needs independent hosts.

## Decision

Propose a small experiment using two Windows-native ASP.NET Core/YARP ingress processes with
Kestrel, competing for one explicit loopback address/port. One serves; the other independently
retries binding, disposing unsuccessful host instances. Active-process death should allow the
standby to acquire the same port, preserving the browser URL after a measured interruption.
Port reuse timing on Windows is unproven: test it before adopting the design. If proven, propose
an Ingress host project plus the existing Linux Docker gateway/node roles. No singleton supervisor,
extra proxy, IIS installation, new WSL distribution or HTTP.sys URL reservation is proposed.

## Alternatives considered

- Single static-forwarding adapter: rejected by the user.
- Adding a third proxy before two adapters: merely relocates the single point of failure.
- HTTP.sys shared queues: attachment alone does not preserve URL registration after owner death;
  more lifecycle handling than the simpler Kestrel experiment. Not the first choice after review.
- Independent Linux hosts/VMs and network-level failover: alternative if Windows experiment fails.

## Consequences

The proposed scope is recovery from one service or simulated-node crash at a time, with enough
remaining capacity and consensus majority. Kernel/host failure is not simulated service failure.
Test both startup orders, exclusive port ownership, active/standby termination, restored redundancy
and repeated takeover under persistent/concurrent traffic at the same browser URL. Record failed
requests and recovery duration. Do not promise uninterrupted in-flight requests or hang/partition
recovery. Proposed experiment time box: 30 minutes; agree a recovery target before acceptance.

## Review

Fresh-context review: **go with changes**. Prefer Kestrel bind-and-retry over HTTP.sys; applied.
Added startup-order, exclusive ownership, persistent-connection and repeated takeover checks.
Windows port reuse timing and HTTP.sys URL-owner lifecycle prevent claims based on documentation
alone. The user must approve Windows-native ingress and the crash-recovery boundary before coding.

## Sources

Checked 2026-11-20: [HTTP.sys options](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.httpsys.httpsysoptions?view=aspnetcore-10.0),
[request queue modes](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.httpsys.requestqueuemode?view=aspnetcore-10.0),
[Windows socket reuse](https://learn.microsoft.com/en-us/windows/win32/winsock/using-so-reuseaddr-and-so-exclusiveaddruse).
