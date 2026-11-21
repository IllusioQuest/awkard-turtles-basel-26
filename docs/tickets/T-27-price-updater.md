# T-27: Self-updating computing price (snapshotted)

- **Tier:** 2 Pitch  ·  **Area:** Data
- **Depends on:** T-26 (price method), T-06 (config)
- **Suggested owner:** Claude  ·  **Owner:**  ·  **Status:** proposed (waiting for team lead's go)

## Goal

The computing price (currently CHF 0.66 per server-hour, `docs/research/computing-price.md`) should
update itself from public price sources, without breaking reproducibility.

## Proposed approach

- [ ] Extend `scripts/fetch_data.py` with a price source that needs no login, e.g. the public Azure
      Retail Prices API (Switzerland North spot/on-demand prices for the VM sizes matched to 1 kW in
      `computing-price.md`); other providers only where a public API exists.
- [ ] Write a **dated snapshot** to `data/raw/market/` (with `SOURCE.md`), never overwrite the old one.
- [ ] Derive the server-hour price with the method from `computing-price.md` (documented formula),
      and record which snapshot each run used (`run.json`, T-13).
- [ ] Scored figures use a **pinned** snapshot (reproducible); the dashboard can show "latest".
- [ ] Test: derivation from a fixed snapshot gives the documented value.

## Done when

- `python3 scripts/fetch_data.py --prices` refreshes the snapshot; a run states the snapshot date; scored results stay reproducible.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
