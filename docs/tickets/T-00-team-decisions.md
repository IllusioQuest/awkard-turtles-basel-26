# T-00: Team decisions that unblock the tickets

- **Tier:** 1 Draft  ·  **Area:** Team
- **Depends on:** –
- **Suggested owner:** whole team (15 min)  ·  **Owner:**  ·  **Status:** open

## Goal

Settle the open choices that several tickets depend on, so nobody builds on a guess.

## Tasks

- [ ] Confirm the services for the draft: confidential transcription + document AI (OCR/indexing) as in `docs/research/service-brainstorm.md` ("Current main plan"), or change it.
- [ ] Confirm the vision draft `docs/vision.md` (actors, ownership models, tiers) or list changes.
- [ ] Hardware for the draft: keep the case's abstract 1 kW server for scored figures (recommended); CPU vs GPU profiles come in T-15.
- [ ] Decide what the draft submission at 15:00 must contain (ask organisers if unclear) and record it in AGENTS.md §2/§12 (feeds T-07).
- [ ] Decide the UI stack for dashboards/configurator (React/Lovable reading exported data vs. ASP.NET/Blazor) — needed before T-17..T-19.
- [ ] Assign owners: write names into `docs/tickets/README.md`.

## Done when

- Each bullet has an answer written into the relevant file (vision, AGENTS.md, ticket index), and owners are set for all tier-1 tickets.

## Read first

- `docs/vision.md`
- `docs/research/service-brainstorm.md`
- `docs/status.md`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

## Notes

Pitch storyboard is NOT decided here: the team lead wants to rework it (see T-21).
