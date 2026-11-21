# T-55 — Preserve heat and speed while validating gaming GPU eligibility

- Owner: Nikita / Codex
- Status: validation complete; draft PR #44 in review; actual CS GPU dispatch not enabled
- Branch: feat/T-55-gaming-gpu-eligibility (stacked on T-53 and T-49)
- Decision: 0043; user approved direction, 2026-11-21.

Reuse existing shared GPU resource accounting and heat ranking. Add focused regression
checks; validate Intel OpenGL on unused E without changing active D or Minecraft. Keep
probing outside admission and compare the existing scheduler diagnostic. No CPU limit
increase was approved or applied. No distributed GPU aggregation or automatic active-game
migration. Automatic CS heat-based dispatch remains blocked by missing measured heat and
fresh physical-GPU occupancy. Document actual results, including incompatibilities.

Done: reviewed decision, reproducible probe, regression tests/timing evidence and code PR.


Results: 49 tests pass, E desktop accelerated Intel GL verified. Startup/probe p95 exceeded the fixed threshold; steady follow-up passed. Both reports retained. See docs/jobs/gaming-gpu-eligibility.md. Actual Wine CS GPU performance is unverified; heat/occupancy gates remain.
