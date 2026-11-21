# T-60: One network decision, safe real admission

- **Tier:** 2 Pitch
- **Owner:** Jona / Codex
- **Status:** ready for review
- **Depends on:** T-12, T-43, T-54

## Goal

Make the network scheduler the authoritative source of cross-site work and heat-load decisions. A real workload start must be able to use its selected node without the orchestration admission layer silently selecting another. Preserve resource, locality, deadline, journal and Docker safety checks.

## This branch

- Add an explicit selected-site reservation and start path in Orchestration, bound to the planning inventory and heat signal; retain the old autonomous path for callers without a network decision until they are migrated.
- Cross-check the fixed one-house unlimited and finite annual runs against the one-site Network B2 work strategy, including hourly loads and case totals. Do not change scored results without explicit evidence.
- Route the unlimited YearRun through B2 after an exact full-year regression, with its network configuration included in run provenance.
- Document which live paths consume Network decisions and which still require migration. The two-laptop live demo remains a separate branch.

## Done when

- Selected-site admission never falls back to a different site, including after retry and resource changes.
- Relevant unit tests and the one-house cross-check pass.
- Decision record and migration boundary are reviewed and documented.
