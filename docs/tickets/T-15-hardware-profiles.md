# T-15: Hardware profiles: CPU vs GPU

- **Tier:** 2 Pitch  ·  **Area:** Data / model
- **Depends on:** T-00
- **Suggested owner:** anyone  ·  **Owner:** Team lead / Claude (subagent)  ·  **Status:** done (PR #14 merged)

## Goal

Make hardware a parameter: 2–3 profiles the economics and routing can use.

## Tasks

- [x] Profiles e.g. CPU server, GPU inference server (L4/T4 class), optionally mixed: electric power, recoverable heat share, purchase price, lifetime, throughput per job type, market price per hour — each with source (WP1, WP2) or TODO(source).
- [x] `config/hardware/*.json` + loader; the reference house keeps the case's 1 kW abstraction.
- [x] Short comparison in `docs/research/hardware.md` and a recommendation for the pitch (team decides).

## Done when

- Profiles load; economics (T-14) can switch profiles.

## Read first

- `data/raw/market/compute-prices.csv`
- `data/raw/market/costs.csv`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Result (first version)

- Profiles `cpu-server`, `gpu-inference-l4`, `gpu-consumer-rtx4090` in `config/hardware/`, schema
  `hardware-profile.schema.json`, loader `DigitalHeat.Core.HardwareProfile` (+ `HardwareProfileTests`).
- New raw rows: `data/raw/market/hardware.csv`. Comparison: `docs/research/hardware.md`, reproduced by
  `python3 scripts/hardware_comparison.py`.
- Open `TODO(source)`s are listed in `docs/research/hardware.md` §5.
