# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex, Cursor, ...) working in this repository.
Read this file completely before you start a task.

## Project in one paragraph

Team **Awkward Turtles** (3 people) builds a demonstrator for the **DigitalHeat** case at
**Basel GreenTech 2026**: interruptible computing jobs run on servers in people's basements, and a
scheduler runs them when and where their waste heat is needed for space heating and hot water,
over a full year. The result must be a working demonstrator with at least one real job under
scheduler control, plus transparent, reproducible yearly numbers for a fixed reference house.

**Status:** concept phase. Tech stack, architecture and repository layout are **not decided yet**.
Do not pick a stack, add frameworks or create a project skeleton unless a team member asks for it.

## Where the context lives

| File | What it is | Editable? |
|---|---|---|
| [`docs/case/challenge.md`](docs/case/challenge.md) | Full case description, verbatim. **Source of truth.** | No |
| [`docs/case/key-figures.md`](docs/case/key-figures.md) | Reference-house parameters, must-have checklist, pitch and jury weights, derived from the case | Yes, keep in sync with the case |
| [`docs/case/references.md`](docs/case/references.md) | All external links and data sources, with status | Yes |
| [`docs/deep-dive/notes-verbatim.md`](docs/deep-dive/notes-verbatim.md) | Raw notes from the deep dive with the case owner | No |
| [`docs/deep-dive/notes-structured.md`](docs/deep-dive/notes-structured.md) | Team interpretation of those notes, plus open questions | Yes |

Order of authority when sources disagree: `challenge.md` > `notes-verbatim.md` > everything else.
For anything that is scored, the case text always wins over the deep-dive notes.

Read at least `key-figures.md` before any task. Read `challenge.md` before anything that touches
scoring, the reference house, the must-haves or the pitch.

## Rules that follow from the case (do not break these)

1. **Reference house is fixed.** Scored figures use exactly the parameters in `challenge.md`
   (Basel 2025 weather, heating formula with 25.6, heating-day rule < 12 °C daily mean, 12 kWh/day
   hot water, 35 kWh lossless tank, ElCom 2026 IWB H4 flat price, heat valued at price ÷ 2.5,
   1 kW server with 80 % recovery). Other houses, tanks or prices are allowed **in addition**, never
   instead, and must be labelled as variants.
2. **Every number has a source.** Any constant in code, config, notebook or slide comes with its
   origin: case text, a URL plus access date, or our own measurement (with how to reproduce it).
   Never invent a number to fill a gap. If a value is missing, leave a clearly marked placeholder
   (e.g. `TODO(source)`) and tell the user.
3. **Assumptions are visible.** Each new assumption goes into the project's assumptions list (the
   location is not decided yet; until then, list it in your summary to the user).
4. **Reproducible.** Results must be reproducible from the repository by someone else. No manual
   steps that are not written down, no hidden local files.
5. **Say when it does not pay off.** Analyses must state the conditions (prices, servers per
   house, workload) under which the model stops being profitable. Do not hide unfavourable results.
6. **Real job under scheduler control.** The same scheduler code that produces the yearly balance
   must also pause/resume (or checkpoint/restart) at least one real running process. Do not build
   a separate "demo-only" control path that bypasses the scheduler.
7. **Units.** Use kW for power, kWh for energy, °C for temperature, CHF for money, hours as the time
   step. Name units in variable names or column headers (e.g. `heat_demand_kwh`).
8. **Time zone.** Hourly data is in `Europe/Zurich` local time, as the case defines heating days by
   local calendar day. Be explicit about DST handling.

## Data handling

- **External data is not in the repository yet.** [`docs/case/references.md`](docs/case/references.md)
  lists what exists and where. How and where data will be stored has **not been decided**: do not
  download datasets into the repo or commit data files without the team's go-ahead.
- Never commit secrets, API keys or personal data.
- From the deep dive: data sovereignty matters (no data to other countries where not allowed;
  sensitive customer data stays local or fully anonymised). Respect this in any design proposal.

## Working with the team

- The team has 3 members working in parallel. Keep changes focused and small, and do not rewrite
  files outside your task.
- If something in the case is ambiguous, or a decision belongs to the team (stack, hardware,
  servers per house, business model, scope), **ask instead of deciding**. Offer a recommendation
  with reasons.
- Write code, comments, docs and commit messages in **English**.
- Do not edit the files marked "No" in the table above. Add interpretations elsewhere and link back.
- When you learn something that future agents need (a decision, a convention, a new key file),
  propose an update to this file.

## To be decided by the team

These sections will be filled in once the team decides. Until then, do not assume an answer.

- Tech stack (language, frameworks, dashboard technology)
- Repository layout (code, data, notebooks, results)
- Data storage strategy (raw vs. derived data, what is committed, download scripts)
- Location and format of the assumptions list
- Build, run and test commands
- Branching and review workflow
