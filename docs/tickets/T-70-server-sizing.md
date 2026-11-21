# T-70: Optimise servers per household

- **Owner:** Ennio / Claude
- **Status:** done
- **Depends on:** Core year model, hardware profiles (T-14/T-15)
- **Decision:** [0062](../decisions/0062-marginal-value-server-sizing.md)

## Goal

A traceable, pitch-ready rule for how many servers the reference house should get, using the shared
heat model and sourced hardware costs and prices. Scope cut to the reference house (user, 2026-11-21).

## Done when

- Core sweep + optimum with tests (case values 7,631 / 22,661 h, 13 servers = 100%).
- `YearRun --sizing` report, reproducible.
- Result page `docs/results/server-sizing.md` with method, worked example, assumptions and limits.

## Next (not in scope)

- Many houses at once (per-house rule + shadow price; shared jobs as an LP).
- Demand-aware sizing: sold hours from real job demand instead of the unlimited-work upper bound.
- Tank-size variants once a tank cost and size are sourced.
