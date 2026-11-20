# 0001: Data storage layout

- **Status:** accepted
- **Date:** 2026-11-20
- **Decided by:** team (proposal by agent, confirmed in chat)

## Context

The case requires transparent, reproducible numbers. Inputs come from public sources (Open-Meteo,
ElCom), from the organisers (official CSV, PDF) and from large datasets (Alibaba trace, MLPerf).
Web sources can change or become unreachable during the hackathon.

## Decision

- `data/raw/`: source files unchanged and committed when small, each with a `SOURCE.md` (URL,
  parameters, date, caveats).
- `data/derived/`: produced only by scripts; whether a file is committed is decided per file.
- `scripts/fetch_data.py` re-downloads the small public sources and validates them against the
  case's expected values (206 heating days, ≈ 27,000 kWh).
- Large datasets are never committed; only extracted numbers with source go into the repo.
- The official organiser CSV, once available, wins over our own computation.
- The assumptions list lives in [`../assumptions.md`](../assumptions.md) as an interim solution,
  to be replaced by a central machine-readable configuration once the architecture exists.

## Alternatives considered

- Download everything on demand, commit nothing: not reproducible offline or if a source changes.
- Git LFS for large files: setup overhead for three people for no scored benefit.

## Consequences

Anyone can rerun the numbers from a fresh clone. Raw data must not be edited by hand.

## Review

Not reviewed by a subagent: decided before the review rule existed; confirmed by the team in chat.
