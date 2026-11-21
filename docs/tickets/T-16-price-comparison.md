# T-16: Price comparison with known providers

- **Tier:** 2 Pitch  ·  **Area:** Market / presentation
- **Depends on:** T-02, T-14
- **Suggested owner:** anyone  ·  **Owner:** Jona / Claude  ·  **Status:** in progress (Claude; approach proposed, waiting for approval)

## Goal

Show our cost per unit (e.g. per audio hour, per 1,000 pages) next to AWS, Google, Azure, ElevenLabs etc.

## Tasks

- [ ] Our cost per unit = (electricity − heat value + hardware share) ÷ units per server-hour (from T-02 throughput).
- [ ] Market prices from `data/raw/market/ai-services.csv` (normalised CHF).
- [ ] Chart data + a short note on what is comparable (quality, data location) in `docs/research/price-comparison.md`.

## Done when

- A table/chart the pitch can use, every number sourced.

## Read first

- `docs/research/ai-services.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
