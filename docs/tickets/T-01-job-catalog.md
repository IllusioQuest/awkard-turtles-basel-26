# T-01: Job model and job catalog (must-have 1)

- **Tier:** 1 Draft  ·  **Area:** Core / jobs
- **Depends on:** T-00
- **Suggested owner:** Codex or anyone  ·  **Owner:**  ·  **Status:** open

## Goal

Describe job types as data, so the scheduler and the year simulation can use any job without code changes.

## Tasks

- [ ] Define a job-type schema in `config/jobs/*.json`: id, service, work unit (audio minute, page, batch), runtime per unit, energy per unit (kWh), power while running (kW), interruptible (bool), supported control levels (freeze / throttle / checkpoint), checkpoint cost, deadline (h), arrivals per day, eligible sites / data rules, data size per unit, price per unit and per server-hour. Every value with unit and source or `TODO(source)`.
- [ ] Create at least two job types from the chosen services (T-00), e.g. `transcription-batch` and `document-ocr`.
- [ ] Arrivals per day: take them from a named source (e.g. Alibaba trace statistics, a customer-size assumption) or state explicitly "unlimited jobs" (the case allows this if stated).
- [ ] Add a loader in `DigitalHeat.Core` (no logic duplication), plus unit tests for loading/validation.
- [ ] Decision record (next free number, currently 0008) if the schema changes a shared interface.

## Done when

- ≥ 2 job types load from JSON with sources; tests pass; `docs/status.md` row updated.

## Read first

- `docs/case/challenge.md (must-have 1)`
- `data/raw/market/ai-services.csv`
- `docs/research/ai-services.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Runtime/energy of the laptop job come from T-02; leave TODO(source) until then.
