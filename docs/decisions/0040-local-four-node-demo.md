# 0040: Local four-node shared work queue demo

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex, direction approved by team lead

## Context

T-51 needs four separate laptop containers to behave as heat-network nodes while processing one
real workload. The existing Ising task grid consists of independent seeded simulation tasks. A
per-container copy of the old runner would duplicate the whole queue and make pause behavior hard
to demonstrate.

## Decision

For this local demo only, nodes share one mounted work directory and claim one task at a time with
an atomic claim-directory create. Results use exclusive create-if-absent publication. Active work
renews its lease; if a lease expires, another node may retry the task. The first accepted result
cannot be overwritten by a later publisher. Each container keeps its own completion log. A small C#
LiveGrid surface sends each node's illustrative heat signal through the existing
`GreedyHeatScheduler` → `SchedulerController.Apply` → `DockerJobExecutor` path.

## Alternatives considered

- Copy the full task list into each container: creates duplicate work and false aggregate progress.
- Add a broker or distributed database: unnecessary for four containers on one laptop and makes
  the offline demo harder to start.
- Change the production federated queue: out of scope; this is a local demo mechanism, not a
  cross-laptop ownership protocol.

## Consequences

The demo shows real queue-level distribution and pause, with one accepted result per task. Work
execution is at least once if a lease expires: two nodes may spend CPU on the same task, but only
one result is accepted. The queue relies on atomic directory creation and hard links on the shared
local Docker volume; it is not a two-laptop network protocol. Tasks are indivisible work units; a
pause lets peers claim subsequent tasks, while the paused container resumes its in-flight task
unless its lease expires and a peer takes it over. The temperature slider is a clearly labelled
illustrative mapping to reference-house heat demand; laptop watts and delivered heat are not
measured.

## Review

Fresh-context review (2026-11-21): **go with changes**. Clarified at-least-once compute on lease
expiry, rather than implying exactly-once execution; kept the shared-volume boundary and illustrative
temperature mapping explicit. Active tasks now renew the claim lease while their container runs.
