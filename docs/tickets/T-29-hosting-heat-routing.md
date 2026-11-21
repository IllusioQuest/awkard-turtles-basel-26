# T-29: Connect hosting request routing to the shared heat scheduler

- **Tier:** 2 Pitch · **Area:** Hosting / scheduler integration
- **Depends on:** T-09, T-24, T-27
- **Owner:** Nikita / Codex · **Status:** in progress (design approval pending)

## Goal
Route requests within separate frontend/API pools using the existing heat scheduler's decisions,
without duplicating its formula, violating locality rules or pausing the always-on service.

## Scope proposed to the user
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
