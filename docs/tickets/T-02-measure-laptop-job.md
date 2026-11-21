# T-02: Measure one real job on a laptop (must-have 1)

- **Tier:** 1 Draft  ·  **Area:** Jobs / measurement
- **Depends on:** T-00
- **Suggested owner:** anyone with a laptop  ·  **Owner:** Ennio / Claude  ·  **Status:** done (PR #15)

## Goal

Get a measured runtime and energy per work unit for one real job, scaled to the 1 kW reference server.

## Tasks

- [ ] Pick the job from T-00 (e.g. whisper.cpp tiny/base on public-domain audio, or Tesseract OCR on public-domain scans). Must run offline after setup.
- [ ] Package it as a container (needed by T-03) or at least a reproducible command.
- [ ] Measure runtime per work unit (≥ 5 repetitions, report mean and spread) and note the machine (CPU, cores, OS).
- [ ] Energy: software meter if available, otherwise runtime × device power (state the power value and its source).
- [ ] State how you scale to the 1 kW reference server (e.g. throughput per kW) and write it in `docs/jobs/<job>.md` with the exact commands to reproduce.
- [ ] Copy the numbers into the job type in T-01.

## Done when

- `docs/jobs/<job>.md` with commands, raw measurements, energy method and scaling; values in the job catalog.

## Read first

- `docs/case/challenge.md (must-have 1)`
- `docs/research/ideas.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
