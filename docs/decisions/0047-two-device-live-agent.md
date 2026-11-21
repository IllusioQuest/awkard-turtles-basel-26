# 0047: One scheduler and a node agent across two demo laptops

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona requested an eight-node demonstration on two devices; implementation by Codex

## Context

Decision 0042 planned one coordinator for eight sites, with four Docker worker containers on each
laptop. The existing `LiveCoordinator` reads task files and controls Docker only on its own machine.
Running a second independent LiveGrid would produce two unrelated scheduler decisions and would not
demonstrate network placement.

## Decision

Laptop A alone runs `INetworkScheduler`, advances model hours, owns job/deadline bookkeeping and
serves the UI. Laptop B runs a small authenticated HTTP node agent for its four local container
executors and physical task files. `LiveWorkStore` gives the coordinator local and remote file
operations without duplicating task state; `RemoteExecutor : IExecutor` applies the same
`SchedulerController.Apply` decision on B. A waiting cross-device task is atomically claimed into
an outbox on the source, written to the destination's queue, acknowledged, then removed from the
outbox. Running tasks never move. If B is offline, its capacity is zero and no new tasks are sent
there; its local files remain on B until reconnection.

The agent requires a shared token supplied at runtime through `DIGITALHEAT_AGENT_TOKEN`. Only
synthetic Ising work is allowed in this demo. The two browser views both fetch A's single API,
using `?nodes=1-4` and `?nodes=5-8` to display their respective four houses.

## Alternatives considered

- Two independent schedulers: their decisions and deadlines would diverge.
- One shared SMB queue: Docker Desktop bind mounts and atomic rename behavior across Windows shares
  are uncertain, and files would have no clear node owner.
- Mirror task files on A: the animation could show a move before the remote worker actually owns it.

## Consequences

The transport adds LAN and Windows firewall dependencies. A remains a single coordinator; this is
an eight-node live demonstration, not the fully federated protocol of decision 0035. The current
agent uses synchronous HTTP operations and is intended for a trusted local network. An interrupted
transfer leaves an inert outbox copy; the coordinator checks the destination before restoring it,
so a lost HTTP response cannot release both copies to workers. A new demo start
resets all queues, so this path does not promise production-grade crash recovery. We must test on
the actual two laptops and recalibrate task runtime on B before claiming the same modeled
server-hour capacity on both CPUs.

## Review

Fresh-context review: **go with changes**. The reviewer found that ambiguous PUT/TAKE responses
could duplicate or strand work, and peer failure could interrupt local decisions. The implementation
now holds uncertain transfers in an inert outbox, rechecks source and destination ownership before
acknowledging or restoring, and excludes offline peer capacity while the local executor calls still
run. The physical two-laptop failure rehearsal remains open.
