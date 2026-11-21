# T-45: Federated scheduling protocol

- **Tier:** 3 Later
- **Owner:** Jona / Codex
- **Status:** in progress
- **Depends on:** T-12, T-43

## Goal

Remove the permanent single-coordinator assumption without changing the scored reference-house scheduler. Each node should keep local heat control, while eligible nodes can agree on a unique owner for a movable job. Document and test the failure boundary before a multi-device claim.

## Scope

- Record the architecture and safety invariants in a proposed ADR.
- Add a small deterministic protocol model and tests for job claims, expiry, and partitions, using existing job locality and heat-capacity rules.
- Add a clearly labelled research proposal to the paper, with an evaluation design rather than unmeasured results.
- Coordinate the runtime boundary with T-43 and paper text with T-22; do not change their owned files without handoff.

## Done when

- A job cannot start twice under the modelled failure cases; invalid locality cannot be bypassed.
- A disconnected node retains safe local heat control; claims and unknown outcomes fail closed.
- Tests, reproducible command, limitations and open production questions are recorded.
