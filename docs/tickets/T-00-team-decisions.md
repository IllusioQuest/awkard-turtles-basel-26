# T-00: Team decisions that unblock the tickets

- **Tier:** 1 Draft  ·  **Area:** Team
- **Depends on:** –
- **Suggested owner:** whole team (15 min)  ·  **Owner:** Nikita / Codex (coordination; team decides) · **Status:** in progress

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

## Coordination packet (2026-11-21, Nikita / Codex)

A second agent reviewed the current documents. Recommendations are not team acceptance:
- Preserve the case abstract 1 kW server for scored figures; make T-09 capacity configurable.
- Proposed draft services remain transcription and document OCR. No customer demand, privacy
  guarantee or dialect accuracy is established by that choice.
- Preserve shared C# IScheduler/IExecutor; generic T-09 is authorized by Nikita without selecting
  hardware purchases, business ownership, UI stack or a routing optimiser. T-00 dependency is
  partially satisfied for that narrow scope only.
- Defer dashboard implementation until its stack decision; this does not block T-09.

Requests to other team agents: confirm your human's service/vision choices and claim your actual
owned tier-1 tickets in the board. Do not infer ownership from suggested names. The job-runtime
branch already contains T-03/T-04 groundwork: its author should update those tickets rather than
another agent duplicating it. Its ADR was renumbered to 0009 when PR #3 was merged.

Still needed from humans/organisers: exact draft deliverables, format and submission destination;
measurement owner; submission owner; confirmed owners for the remaining tier-1 tickets. Questions
have been sent to Nikita. T-00 remains in progress, not done, until these answers are recorded.
Other teammates' chats are not accessible in this local app; this repository packet is the shared
coordination channel, not evidence that they have responded.
