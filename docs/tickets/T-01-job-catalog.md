# T-01: Job model and job catalog (must-have 1)

- **Tier:** 1 Draft  ·  **Area:** Core / jobs
- **Depends on:** T-00
- **Suggested owner:** Codex or anyone  ·  **Owner:** Team lead / Claude  ·  **Status:** blocked: waiting for Nikita's decision on always-on representation (then Claude implements the schema)

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

## Decision needed from Nikita (2026-11-21, team lead's instruction)

The two job types for the draft are **transcription** (T-02, Ennio) and **webhosting** (T-23,
Nikita). `config/jobs/job.schema.json` and `JobDescriptorLoader` (decision 0009) only allow
interruptible jobs with freeze support, so the always-on webhosting service from
`docs/webhosting-heat-runtime.md` cannot be described honestly yet.

**Nikita decides the representation; Claude then implements only the schema/loader/test change**
(Nikita writes the webhosting descriptor in T-23). Options:

- **A. One catalog, extended:** new field `serviceMode: "batch" | "always-on"`; `interruptible: false`
  and an empty control-level list allowed only for `always-on`; optional `idlePowerKw` /
  `loadedPowerKw`. All job types for must-have 1 stay in one place. (Claude's recommendation.)
- **B. Two catalogs:** batch jobs stay in `config/jobs/`, always-on services get
  `config/services/*.json` with their own schema.

For the draft, webhosting goes **only into the catalog**, not into the year simulation (scored
figures stay stable); the constant base load in the simulation comes after the draft in its own
ticket, agreed with Jona (T-05/T-06).

Please write the decision here (or in PR #7) with a decision record number; the next free one is
on `main` in `docs/decisions/README.md`.
