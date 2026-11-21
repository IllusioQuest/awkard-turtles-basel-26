# T-45: Federated scheduling protocol

- **Tier:** 3 Later
- **Owner:** Jona / Codex
- **Status:** in progress
- **Depends on:** T-12; T-43 only for live Docker integration

## Goal

Remove the permanent single-coordinator assumption without changing the scored reference-house scheduler. Each node should keep local heat control, while eligible nodes can agree on a unique owner for a movable job. Document and test the failure boundary before a multi-device claim.

## Scope

- Record the architecture and safety invariants in a proposed ADR.
- Add a small deterministic protocol model and tests for job claims, expiry, and partitions, using existing job locality and heat-capacity rules.
- Add a clearly labelled research proposal to the paper, with an evaluation design rather than unmeasured results.
- Coordinate the runtime boundary with T-43 and paper text with T-22; do not change their owned files without handoff.

## First prototype

`FederatedClaimGate` is a pure, deterministic admission boundary in `DigitalHeat.Network`.
It checks quorum evidence supplied by a trusted service, claim time/generation, old-attempt
termination and the existing `JobPlacement.IsEligible` rule. It is not a quorum service or Docker
integration. `dotnet test src/DigitalHeat.Network.Tests/DigitalHeat.Network.Tests.csproj` exercises
partition, stale policy, locality and uncertain old-process cases. The local heat test uses the
existing `GreedyHeatScheduler` and `SchedulerController` path.
`FederatedOfferSelection` independently filters expired, contradictory and contractually forbidden
site offers and ranks remaining sites by the shared `HeatCapacity.RemainingKwh` rule. Offers never
authorize a start. The offer selection and claim gate together are a protocol model; they do not
send messages or form a quorum. Network tests cover both pieces.

## Done when

- A job cannot start twice under the modelled failure cases; invalid locality cannot be bypassed.
- A disconnected node retains safe local heat control; claims and unknown outcomes fail closed.
- Tests, reproducible command, limitations and open production questions are recorded.

## Boundary review update

- [x] Reject missing policy revisions, require predecessor evidence for generations above one, and validate malformed prior attempts.
- [x] Skip offers without a heat signal; deduplicate identical retransmissions; retain fail-closed behavior for conflicting reports.
- [x] Require a trusted stop-time bound for reassignment even when work is labelled interruptible; apply the clock margin without timestamp overflow.
- [x] Expand tests for these boundaries and a finite clock-offset sweep.
- [x] Replace broad prose with a conditional safety argument, explicit assumptions, experiment controls and primary literature.
