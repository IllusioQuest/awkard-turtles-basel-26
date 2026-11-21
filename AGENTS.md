# AGENTS.md

Instructions for AI coding agents (Claude Code, OpenAI Codex, ...) in this repository. Read this
file completely at the start of every session. **Then read only what your task needs:** your
ticket, the files it names, and the indexes (`docs/tickets/README.md`, `docs/decisions/README.md`).
Do not read all tickets, decisions or research files.

## 1. Project and status

- **Event:** Basel GreenTech 2026 hackathon, case **DigitalHeat** ("Useful heat from computing").
  Team **Awkward Turtles**, 3 people, each with their own agents in parallel.
- **The case text and deep-dive notes are the case owner's view, not our plan.** We design a
  business model for DigitalHeat and simulate it. Our direction: [`docs/vision.md`](docs/vision.md)
  (draft) and the business model in [`docs/business-model.md`](docs/business-model.md) (decision
  0020, provisional). It must pay off for the host *and* for DigitalHeat. Do not derive product
  direction from the case owner's examples.
- **Long-term:** a general simulation of any location with a network of computing and heat sites,
  solid enough for a paper. Hence generality, reproducibility and decision records.
- **Work is organised in tickets:** [`docs/tickets/README.md`](docs/tickets/README.md). Active
  branches and coordination: [`docs/status.md`](docs/status.md).
- **Available, not set up:** ElevenLabs and Lovable subscriptions. Do not use them without the team.

## 2. Deadlines

All times Europe/Zurich. `scripts/deadline-reminder.sh` reads the dated rows; keep the format
`| YYYY-MM-DD HH:MM | what | notes |`.

| When | What | Notes |
|---|---|---|
| 2026-11-21 15:00 | Draft submission (first version) | **Voluntary.** |
| TBD (≈ 22 Nov, morning) | End of hackathon, final pitch | Exact time to be added by the team. |
| TBD | Feature freeze | Team decides later. |

Do not state time left or push deadlines unless asked.

## 3. Where things are

| Path | What | Edit? |
|---|---|---|
| `docs/case/challenge.md` | Full case text, verbatim. **Source of truth for scoring.** | never |
| `docs/case/DigitalHeat.pdf`, `DigitalHeat.md` | Case slides and their text extraction | never / keep as extracted |
| `docs/case/key-figures.md` | Reference house, must-have checklist, pitch, jury weights | keep in sync |
| `docs/case/references.md` | External sources and literature | yes |
| `docs/deep-dive/notes-verbatim.md` / `notes-structured.md` | Raw deep-dive notes / our interpretation | never / yes |
| `docs/vision.md`, `docs/business-model.md` | Direction and business model | with the team |
| `docs/tickets/`, `docs/status.md` | Ticket board; active work and shared interfaces | yes |
| `docs/decisions/` | Decision records (ADRs) | new files |
| `docs/model/`, `docs/results/`, `docs/jobs/` | Model docs, result write-ups, measured jobs | yes |
| `docs/research/` | Research summaries, indexed in its README | yes |
| `docs/assumptions.md` | Assumptions (ID, value, unit, source) | add rows |
| `docs/team-notes.md` | Team to-dos and questions (verbatim, German) | yes |
| `docs/agents/review.md` | How to run a fresh-context review | yes |
| `src/` | C# core, runners, network scheduler, tests (`DigitalHeat.sln`) | yes |
| `web/` | Web dashboard (admin map, household view) | yes |
| `config/` | Parameters: reference house, hardware, jobs, network | yes |
| `data/` | Raw and derived data; rules in `data/README.md` | see rules |
| `scripts/` | Data fetch, measurement, export and experiment scripts | yes |

If sources disagree: `challenge.md` > `notes-verbatim.md` > everything else. Read `key-figures.md`
before touching the model and `challenge.md` before anything about scoring, the reference house,
the must-haves or the pitch.

## 4. How we win: priorities

1. **Valid submission first.** The three must-haves in `key-figures.md` are hard requirements. Do
   not polish one part while another must-have is missing.
2. **Half the score is transparency and feasibility.** Every number traceable, every assumption
   visible, an honest statement of when the model does not pay off.
3. **`main` is always demo-ready.** The live demo (January day, July day, interrupted real job)
   runs on a laptop, offline, deterministically, with a fallback (e.g. a recorded run).
4. **WOW comes from the story and one or two strong visuals** plus a real process paused live, not
   from feature count. A homeowner must understand the result in one minute.
5. **Cut scope instead of leaving things half done.** If a task balloons, stop, report, propose a
   smaller version.
6. **Suggest ideas rarely:** only when they clearly raise the score or remove a real risk, in one
   to three sentences. Do not build them unasked.

## 5. Rules from the case (do not break)

1. **The reference house is fixed** for scored figures (parameters in `challenge.md`). Other
   houses, tanks, prices or locations only **in addition**, labelled as variants.
2. **Every number has a source:** case text, URL plus access date, or our own reproducible
   measurement. Never invent one; if missing, leave a visible `TODO(source)` and tell the user.
3. **The same scheduler code** that produces the yearly balance controls the real running job.
4. **Units in names:** `heat_demand_kwh`, `price_chf_per_kwh`, ...
5. **Time:** hourly steps, Europe/Zurich, 24 values per day (see `data/raw/open-meteo/SOURCE.md`).

## 6. Engineering principles

- **Modular:** core logic (heat, storage, scheduling, economics) is independent of UI, files and
  network; pure functions where sensible.
- **No hard-coded values:** every fixed number is defined once in `config/` (or a named constant
  such as `HOURS_PER_YEAR`) with unit and source, and passed in. Location, house, hardware and
  prices are swappable without touching logic.
- **Simulation and reality share interfaces:** simulated servers and real processes are executors
  behind the same `IExecutor`.
- **Reproducible:** deterministic (fixed seeds), one documented command per result.
- **Simple and robust:** few well-known dependencies; formulas get a short docstring with source.
- **Hackathon-sized quality:** clean enough for a teammate to extend tomorrow; no speculative
  abstractions.

## 7. Testing

- Test the core logic (heat balance, storage, scheduler, economics, data validation), using the
  case's known values as checks (206 heating days, ≈ 27,000 kWh, 12.5 servers in the coldest hour,
  19.2 kWh per server-day).
- No tests for presentation; check it visually (e.g. a screenshot).
- Run the relevant tests before opening a PR and say in the PR what you ran.

## 8. Decisions and reviews

Hard-to-reverse decisions (interfaces, data model, stack, scheduling strategy, economic model or
scored formulas, scope, larger UI choices) get a decision record and one review by a fresh-context
subagent. Not for bug fixes, refactors, styling, docs or data re-fetches.

1. Write `docs/decisions/NNNN-title.md` from the template, status `proposed`, and add it to the
   index. Take the next free number on `main` and on open branches (see the index); renumber
   before merging if it collides.
2. Run the review as in [`docs/agents/review.md`](docs/agents/review.md) (Claude Code: the
   `decision-reviewer` subagent), with a short packet only, never the conversation.
3. Apply what convinces you, record it in the "Review" section, report to the user in 2–4 lines.
   Your user's agreement is enough to implement; the team later sets `accepted`.

## 9. Git workflow

- One short-lived branch per task from the latest `main`: `feat/…`, `fix/…`, `docs/…`, `data/…`,
  `exp/…`. Small commits, English imperative messages.
- PR to `main` with what and why, how it was tested, assumptions or decisions touched. Update the
  branch with `main` first. Never force-push to `main` or rewrite others' branches.
- **Merging:** people merge by default. An agent may squash-merge its own PR only if it is small,
  tests pass, it changes no architecture, interface or scored figure, needs no decision record,
  **and** its user agreed in the conversation.

### 9a. Integration between parallel work

- **Work from tickets:** claim one (owner + `in progress` in the index and the ticket file), name
  the branch after it (`feat/T-03-docker-executor`), reference it in the PR, set `done` when
  merged. New work without a ticket: add one and tell your human. If a ticket is wrong or too big,
  say so instead of changing its scope.
- **Check others' work** at the start, at milestones, before changing a shared interface and before
  a PR: `docs/status.md`, `git fetch && git branch -r`, `git log --oneline -15 origin/main`, open
  PRs. Keep your row in `docs/status.md` current.
- **Build on what exists:** the C# core and its interfaces (decisions 0005, 0009, 0024), `data/raw/`
  with its `SOURCE` files, `config/`. Never create a second heat or economic model; if an interface
  does not fit, propose a change via a decision record.
- **Same numbers everywhere:** if two artefacts compute the same figure (e.g. the year explorer
  prototype and the C# core), they must agree; add a cross-check when you touch either.
- When your work depends on or changes someone else's module, say so in the PR and tell your human.

## 10. Communication

- **Reply in the user's language.** Everything in the repository is in English.
- Short reports: what changed, how it was checked, open questions. Say what is a prototype and what
  is verified.
- **Ask instead of deciding** when the choice belongs to the team (vision, stack, hardware, business
  model, scope, anything a juror might ask about); give a recommendation with reasons.
- **Agree on the approach before building anything substantial** (model, calculation, visual, new
  module, published page): propose in a few lines, then wait. Small, precisely described tasks can
  go ahead.
- **Every result with numbers comes with its calculation path:** method, inputs with sources, one
  worked example, and how to read each chart.
- Prefer several short check-ins over one large surprise. Be economical with time and tokens.

## 11. Data

- Rules and layout: [`data/README.md`](data/README.md). Raw data is never edited by hand; derived
  data comes from scripts only; large datasets are not committed.
- `python3 scripts/fetch_data.py --check` validates the data against the case.
- Never commit secrets, API keys or personal data. Data sovereignty matters: sensitive data stays
  local or anonymised.

## 12. Team decisions

**Decided:**
- Core stack: C# / .NET 8 (`src/DigitalHeat.sln`, decision 0005); network optimiser with OR-Tools
  GLOP (decision 0024); Python for data and measurement scripts.
- Dashboard: `web/` (Vite, React, Recharts, MapLibre).
- Parameter format: JSON in `config/` with unit and source per value.
- Build, run and test commands: [`README.md`](README.md) (`dotnet build/test src/DigitalHeat.sln`).
- Business model (provisional): decision 0020.

**Still open (do not assume an answer):** confirming `docs/vision.md`; feature freeze time;
contents of the draft submission; whether `docs/assumptions.md` is replaced by `config/`.
