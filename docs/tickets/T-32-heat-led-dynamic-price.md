# T-32: Heat-led dynamic price for Type 2 compute

- **Tier:** 3 Pitch stretch  ·  **Area:** Economics / market
- **Depends on:** T-31, T-26, T-28; decision 0018
- **Suggested owner:** anyone  ·  **Owner:**  ·  **Status:** open

## Goal

A price per server-hour for Type 2 that follows free capacity and the heat signal, so customers
get cheap compute exactly when the house needs heat. Shown as a variant next to the pinned
CHF 0.66; scored figures are unchanged.

## Tasks

- [ ] Define the hourly price: free capacity = heat-led target − Type 1 load; floor = marginal cost (0.226 CHF/server-h when the heat is used, 0.3325 when it would be dumped, `docs/research/computing-price.md` §2); ceiling = cheapest Swiss spot equivalent (1.80, same file, A2).
- [ ] Whether Type 2 may ever run with dumped heat (only above the dump floor; costs scheduling score) is a team decision.
- [ ] Demand response (how much Type 2 is bought at a given price): `TODO(source)`. Until sourced, show only the price curve and the income at "all offered hours sold", never an invented elasticity.
- [ ] One chart: price over the year (January vs. July) with floor, ceiling and 0.66.
- [ ] Short method note with a worked example (AGENTS.md §10).

## Done when

- A documented, reproducible price curve with sourced bounds and a visible statement of what is not sourced.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
