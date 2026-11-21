> RETIRED by user request in T-40. Webhosting is withdrawn; do not resume this feature. Preserve this ticket as historical context.

# T-36: Connect hosting request routing to the shared heat scheduler

- **Tier:** 2 Pitch · **Area:** Hosting / scheduler integration
- **Depends on:** T-09, T-24, T-27
- **Owner:** Nikita / Codex · **Status:** retired by user request (T-40)

## Goal
Route requests within separate frontend/API pools using the existing heat scheduler's decisions,
without duplicating its formula, violating locality rules or pausing the always-on service.

## Original routing-only proposal (superseded by the approved extension below)
- Reuse IScheduler/GreedyHeatScheduler and HeatSignal from DigitalHeat.Core.
- Filter component, readiness and JobPlacement.IsEligible before heat preference, including
  the single-backend case. No eligible destination means unavailable, never a policy bypass.
- Prefer higher scheduler target within each component pool; equal targets rotate.
- Keep replicas running; zero heat targets still permit eligible HTTP traffic.
- Explicit snake_case lab snapshots provide per-site state and provenance, not fake sensors.
- Stale/unavailable heat input falls back only among policy-eligible healthy replicas.
- Validate changing heat preference, blocked locality, one candidate, no allowed candidate,
  zero heat, invalid/stale heat data and gateway/node failure.
- Keep the scored YearSimulation unchanged. No LP, queue allocation, physical power guarantee,
  automatic migration or new economic mode. T-10/T-12 own the network simulation/optimiser.

## Completion
Reviewed decision, user agreement, tests plus Docker integration demonstration, code PR, and
commands/limits/results published to main/docs. Do not mark complete before merge.

## Related work
T-23 remains the hosting catalog/runtime handoff, blocked on the user's catalog choice requested
again on 2026-11-21. T-01 owns schema implementation; T-24 owns locality eligibility.

Review complete: go with changes recorded in decision 0015. Implementation must test forbidden
sole destinations, empty eligibility and reload, plus whole-set fallback on partial heat input.
Gate actual heat preference to controlled demo use; no serving-capacity claim.
T-27 code is still absent from origin/main at this check; PR #12 must be merged or treated as an
explicit stacked dependency. Do not silently merge it. The approved scope extension below resolves the earlier design questions.

## Approved scope extension (2026-11-21)
User chose automatic pause/move rather than routing-only, then approved six nodes (three per
component), two serving replicas per component and a common batch/always-on catalog.
Decision 0016 extends 0015. Implementation must verify readiness-before-switch, request drain,
safe aborts, locality denial and gateway failure during a transition. T-01 notified via its ticket.


## Review handoff
Implementation and verification complete in PR #19. All eight final Docker scenarios pass; 45 hosting and 51 core/catalog tests pass. Code remains unmerged; mark done after merge. See docs/webhosting-scheduler.md for commands, evidence and limitations.
