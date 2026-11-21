# 0006: Linux host-network ingress experiment

- **Status:** proposed; user approved the Linux-only experiment and host-network setting; team confirmation pending
- **Date:** 2026-11-20
- **Decided by:** webhosting contributor and user

## Context

Windows is the development host; every runtime service must be a Linux container.
The browser URL must survive either gateway crashing. A single adapter was rejected.

## Decision

Use two Linux C#/YARP containers sharing Docker Desktop's opt-in host network.
One binds the configured loopback port; the other retries exclusive binding until it becomes free.
No additional adapter or native Windows listener is installed. This is a crash-recovery experiment,
not a complete resilience solution: a paused listener retains its socket and prevents takeover.
The user subsequently approved a local watchdog; it terminates an unresponsive active worker,
but freezes with the entire container. This worker-freeze scenario now passes the runtime test.
Three privileged nested engines separately emulate app servers; gateways currently sit outside them.
Keep this milestone distinct from the planned co-located controller/etcd/agent topology.

## Alternatives considered

- Single forwarding adapter: rejected because its failure loses ingress.
- Native Windows listener: withdrawn because runtime must be Linux-only.
- Floating IP/Keepalived: still needs a suitable network; Desktop host networking is layer 4.

## Consequences

Same-URL crash recovery is measurable, with interrupted requests recorded rather than hidden.
Outer Desktop networking, kernel and laptop remain shared test infrastructure.
Gateway/container pause recovery and role placement require a further user-approved design.
No controller resilience, automatic rescheduling, production HA or scored heat result is claimed.
See [runtime guide](../webhosting-runtime.md) for measurements and reproducible commands.

## Review

Fresh-context verdict: go with changes. Applied all three findings: wait for the standby bind-retry event from its current start before killing the active gateway; label failures as post-kill-command probes (not interrupted traffic); report first-success and sustained-confirmation times separately. Full pause/container-role resilience remains a separate user decision.
