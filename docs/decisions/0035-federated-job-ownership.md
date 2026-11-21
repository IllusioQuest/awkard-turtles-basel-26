# 0035: Federated job ownership with node-local heat control

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex; user approved direction; team confirmation pending

## Context

The reference-house simulation and T-12 network optimiser make a single decision for all sites. T-43 adds an operator-driven coordinator for real Docker contexts, protected by a local journal and one-host lock. Neither is a self-managing multi-host runtime. A permanent coordinator is an availability risk, but simply letting every node dispatch jobs creates duplicate execution during partitions. Some work is contractually restricted to its origin or allowed countries (decision 0013).

## Decision

Separate **local control** from **job ownership**. Each node observes its own heat and tank state and runs the existing `GreedyHeatScheduler` → `SchedulerController.Apply` → `IExecutor` path for jobs it already owns. The node may pause an interruptible job when heat is unavailable. A local safety controller may use stale network information only to reduce load, never to acquire another job.

For movable jobs, each node may publish a short-lived, signed capacity offer with site identity, permitted workload capabilities, available resources, useful-heat headroom, measurement time and expiry. The origin filters offers using the existing `JobPlacement.IsEligible` customer/contract policy, then ranks eligible offers using the existing heat-capacity rule. An offer is an invitation, **not** permission to start.

Before execution, an origin obtains a durable, unique job claim from a quorum-backed ownership service. A claim binds immutable job ID, attempt generation, selected site, locality policy revision and expiry. The selected node validates the claim and current locality policy before starting. Its executor rejects older generations (fencing). Retry after an uncertain start reconciles the previous attempt; no blind relaunch. On a partition, nodes without a valid claim cannot start new work. A running job may continue only within its valid lease and local heat limits; on expiry without renewal it pauses or stops according to its declared interruptibility and checkpoint semantics. Reassignment waits for confirmed termination or lease expiry plus a bounded clock margin: fencing a new command does not kill an isolated old process. Non-interruptible jobs need a longer pre-authorized run window or remain at a stable site; if their old owner is unreachable, reassignment requires confirmed termination or a proven execution bound.

The ownership service may be replicated, but it is still a **logically central authority per job**. We do not claim a consensus-free network. Choose a maintained consensus implementation only after a multi-host test; do not add custom consensus code to Core. T-43's single-host journal and lock remain the current demo implementation. The reference-house scored figures, B0/B1/B2/LP comparisons and their code do not change. A later adapter can feed the same scheduler and executor interfaces from node-local observations and claims.

## Why and alternatives

- One always-on coordinator: simpler, but its failure prevents new placement. Keep it as baseline and demo fallback.
- Fully independent peer decisions: available during partitions, but cannot guarantee one execution of a non-idempotent job. Reject for paid or sensitive work.
- Broadcast plus deterministic tie-break: concurrent views can disagree; a tie-break alone does not fence a previously started process. Reject.
- Quorum-backed claims: trades availability during minority partitions for unique ownership. This is explicit and testable.

## Evaluation and rollout

1. Model the protocol deterministically first: simultaneous offers, stale offers, claim expiry, partitions, owner crash, delayed start acknowledgement and reconnection. Assert no overlapping authorized execution windows and no locality violation under the stated clock/fencing assumptions. This alone does not prove a physical process has stopped.
2. Keep the current single-coordinator implementation as a baseline. Add a node-local controller adapter only after T-43 owner coordination; preserve the shared C# scheduling path.
3. Test on two physical devices plus an independent third voter or managed quorum. Two nodes alone cannot both maintain majority after a split. Measure job availability, duplicate attempts, heat used, missed deadlines, recovery time and control-plane traffic. Never present model outcomes as field measurements.

The first executable step is `FederatedClaimGate` in `DigitalHeat.Network`. It validates
externally supplied quorum evidence, generation, lease window, prior-attempt state and the
existing customer locality rule. It does not issue claims, form a quorum, or control Docker.
`FederatedOfferSelection` filters stale, contradictory and ineligible offers and ranks by the
shared useful-heat helper. Neither component sends network messages. Network tests exercise both
fail-closed boundaries and the unchanged local heat path.

## Limits

Lease safety requires bounded clock error or a service-issued monotonic lease mechanism, durable quorum state, authenticated nodes, executor fencing and clear behavior for an unreachable ownership service. These are unimplemented. Local-only data must not be included in remote offers or copied for placement. A quorum failure can stop new placement; that is the chosen safety tradeoff. T-43's coordinator remains the pitch implementation. No multi-device result is yet verified.

## Review

Fresh-context review verdict: **go with changes**. The reviewer noted that command fencing cannot stop an isolated older process; reassignment now waits for confirmed stop or lease expiry plus a clock margin, and non-interruptible jobs need a proven execution bound. The current coordinator remains the pitch implementation. The ADR and ticket were renumbered to 0035/T-45 because Minecraft already owns 0034/T-44.

The executable predicate was tightened after boundary review: blank policy revisions fail closed;
only generation one may omit predecessor evidence; and reassignment without confirmed termination
requires a trusted execution-end bound for every workload (interruptibility alone is insufficient).
The bound is combined with lease expiry and a two-sided clock-error margin. Extreme timestamps are
compared as elapsed ticks to avoid arithmetic overflow. Identical offer retransmissions are
idempotent; conflicting reports for a site remain unavailable.

This remains conditional admission logic. It does not issue quorum decisions, authenticate evidence,
persist start deduplication, or terminate processes. The paper now states the premises and the
three distinct properties: unique authorization, at-most-once process creation, and non-overlapping
execution.
