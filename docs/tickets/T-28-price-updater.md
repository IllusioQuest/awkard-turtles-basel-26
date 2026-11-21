# T-28: Self-updating computing price (snapshotted)

- **Tier:** 2 Pitch  ·  **Area:** Data
- **Depends on:** T-26 (price method), T-06 (config)
- **Suggested owner:** Claude  ·  **Owner:** Team lead / Claude (subagent)  ·  **Status:** PR #11 open (branch `feat/T-28-price-updater`)

## Goal

The computing price (currently CHF 0.66 per server-hour, `docs/research/computing-price.md`) should
update itself from public price sources, without breaking reproducibility.

## Proposed approach

- [x] Extend `scripts/fetch_data.py` with a price source that needs no login, e.g. the public Azure
      Retail Prices API (Switzerland North spot/on-demand prices for the VM sizes matched to 1 kW in
      `computing-price.md`); other providers only where a public API exists.
- [x] Write a **dated snapshot** to `data/raw/market/` (with `SOURCE.md`), never overwrite the old one.
- [x] Derive the server-hour price with the method from `computing-price.md` (documented formula),
      and record which snapshot each run used (`run.json`, T-13).
- [x] Scored figures use a **pinned** snapshot (reproducible); the dashboard can show "latest".
- [x] Test: derivation from a fixed snapshot gives the documented value.

## Implementation (2026-11-21)

- `python3 scripts/fetch_data.py --prices` -> `scripts/fetch_prices.py`: fetches the Azure Retail Prices
  API (Switzerland North, Linux on-demand + Spot, D32as v5 / D32s v5, follows `NextPageLink`), writes
  `data/raw/market/price-snapshots/azure-switzerlandnorth-YYYY-MM-DD.json` (never overwritten, see its
  `SOURCE.md`) and `data/derived/computing-price.json`.
- Method: `docs/research/computing-price.md` §2 mapping, §3 A1/A2 and C1; every factor in
  `config/computing-price-method.json` (value, unit, source); server power from `config/reference-house.json`.
- The derived file records the snapshot (path, SHA-256, retrieval time); `--check` verifies it reproduces
  exactly. `run.json` (T-13) does not exist yet: when T-13/T-06 adopt a price, copy `snapshot.path` and
  `snapshot.sha256` from the derived file into the run record.
- **Pinning:** scored figures keep using `ComputePriceChfPerServerHour` in `config/reference-house.json`
  (owner T-06). This script never changes it; adopting a new value is a team decision.
- Result 2026-11-21: C1 = **CHF 0.687** per server-hour (adopted 0.66, +0.027), A2 spot = 1.80, A1 on-demand = 9.73.
  The adopted 0.66 comes from anchor A3 (Hetzner, no public API), so it is not tracked automatically;
  C1 is its documented cross-check.
- Tests: `python3 -m unittest discover -s scripts/tests` (fixture snapshot, no network).

## Done when

- `python3 scripts/fetch_data.py --prices` refreshes the snapshot; a run states the snapshot date; scored results stay reproducible.

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
