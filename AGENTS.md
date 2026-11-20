# AGENTS.md

Instructions for AI coding agents (Claude Code, OpenAI Codex, Gemini CLI, ...) in this repository.
Read this file completely at the start of every session. It is short on purpose; follow the links
only when your task needs them.

## 1. Project and status

- **Event:** Basel GreenTech 2026 hackathon, case **DigitalHeat** ("Useful heat from computing").
  Team **Awkward Turtles**, 3 people, each working with their own agents in parallel.
- **The case text and the deep-dive notes are the case owner's view, not our plan.** We are free in
  what we build: in effect we design a business model for DigitalHeat and then simulate it. The
  team's own vision will be added here later. Until then, do not derive a product direction from
  the case owner's examples, and avoid choices that close off options.
- **Long-term direction:** a general simulation of *any* location with a network of computing and
  heat sites over time, solid enough that we might write a paper about it. This is why generality,
  reproducibility and decision records matter from the start.
- The case owner suggested a story in three layers: **financial** (money flows over time, sliders
  for when it stops paying off), **marketing** (which priced services we can beat), **physical**
  (simulation of heat and servers). See [`docs/deep-dive/notes-structured.md`](docs/deep-dive/notes-structured.md) §7.
  Treat this as one option, not a requirement.
- **Status:** data collection for the financial and market layers, see
  [`docs/research/README.md`](docs/research/README.md). Stack, architecture and repository layout
  are not decided (§12).
- **Available, not set up yet:** an **ElevenLabs** and a **Lovable** subscription (free for the
  team). Do not set them up or use them without the team; mention them where they fit (e.g.
  ElevenLabs as a transcription price point or for pitch audio, Lovable as a dashboard option in
  the stack decision).

## 2. Deadlines

All times are Europe/Zurich. `scripts/deadline-reminder.sh` reads the dated rows of this table, so
keep the format `| YYYY-MM-DD HH:MM | what | notes |` when you edit it.

| When | What | Notes |
|---|---|---|
| 2026-11-21 15:00 | **Draft submission** (first version) | Required contents not defined yet; ask the team. |
| TBD (≈ 22 Nov, morning) | End of hackathon, final pitch | Exact time to be added by the team. |
| TBD | Feature freeze | Team decides later. |

Reminder rules:
- At the start of a session, check the current time (`scripts/deadline-reminder.sh`, or `date`)
  and state the time left to the next deadline in one line.
- **Less than 6 h left:** end every reply with one line on the time left.
- **Less than 2 h left:** say so prominently at the top of every reply, and suggest securing the
  submission before any new work.
- In Claude Code, hooks in `.claude/settings.json` run the script automatically (session start, and
  on every prompt once a deadline is close). Other tools: run it yourself.

## 3. Where things are

| Path | What | Edit? |
|---|---|---|
| [`docs/case/challenge.md`](docs/case/challenge.md) | Full case text, verbatim. **Source of truth for scoring.** | never |
| [`docs/case/DigitalHeat.pdf`](docs/case/DigitalHeat.pdf) | Case presentation slides, original file | never |
| [`docs/case/DigitalHeat.md`](docs/case/DigitalHeat.md) | Text and figure descriptions of the slides, differences to `challenge.md` | keep as extracted |
| [`docs/case/key-figures.md`](docs/case/key-figures.md) | Reference-house parameters, must-have checklist, pitch, jury weights | keep in sync |
| [`docs/case/references.md`](docs/case/references.md) | External sources and where each lives in the repo | yes |
| [`docs/deep-dive/notes-verbatim.md`](docs/deep-dive/notes-verbatim.md) | Raw notes from the deep dive with the case owner | never |
| [`docs/deep-dive/notes-structured.md`](docs/deep-dive/notes-structured.md) | Our interpretation of those notes, open questions | yes |
| [`docs/research/`](docs/research/README.md) | Research plan (work packages) and research summaries | yes |
| [`docs/assumptions.md`](docs/assumptions.md) | Interim list of all assumptions (ID, value, unit, source) | add rows |
| [`docs/decisions/`](docs/decisions/README.md) | Decision records (ADRs) | new files |
| [`docs/agents/review.md`](docs/agents/review.md) | How to run a fresh-context review | yes |
| [`data/`](data/README.md) | Raw and derived data, how to fetch the originals | see rules there |
| `scripts/fetch_data.py` | Re-download and validate public data | yes |

If sources disagree: `challenge.md` > `notes-verbatim.md` > everything else. Read `key-figures.md`
before any task that touches the model; read `challenge.md` before anything about scoring, the
reference house, the must-haves or the pitch.

## 4. How we win: priorities

1. **Valid submission first.** The three must-haves in `key-figures.md` are a hard requirement. Get a
   thin end-to-end version working early (real job paused/resumed by the scheduler, yearly balance,
   a simple view), then improve it. Do not polish one part while another must-have is missing.
   Must-have groundwork that does not depend on our vision (e.g. measuring a real workload on a
   laptop) can start right away.
2. **Half the score is transparency and feasibility.** Every number traceable, every assumption
   visible, and an honest statement of when the model does not pay off. A correct, explained
   number beats an impressive, unexplained one.
3. **`main` is always demo-ready.** The live demo (January day, July day, interrupted real job) must
   run on a laptop, without internet, deterministically, and should have a fallback (e.g. a recorded
   run) in case something fails on stage.
4. **The WOW effect comes from the story and one or two strong visuals**, plus a real process being
   paused live. It does not come from the number of features. A homeowner must understand the result
   in one minute.
5. **Cut scope instead of leaving things half done.** If a task balloons or you are stuck, stop,
   report and propose a smaller version.
6. **Suggest ideas rarely.** Only when an idea clearly raises the score, the WOW effect or removes a
   real risk: say it in one to three sentences, then carry on with your task. Do not build it unasked.

## 5. Rules from the case (do not break)

1. **The reference house is fixed** for scored figures: exactly the parameters in `challenge.md`
   (see also `docs/assumptions.md`). Other houses, tanks, prices or locations are welcome **in
   addition**, labelled as variants, never instead.
2. **Every number has a source:** case text, a URL plus access date, or our own measurement with
   how to reproduce it. Never invent a number. If one is missing, leave a visible `TODO(source)` and
   tell the user.
3. **The same scheduler code** that produces the yearly balance controls the real running job. No
   separate demo-only control path.
4. **Units in names:** kW, kWh, °C, CHF, hours (e.g. `heat_demand_kwh`, `price_chf_per_kwh`).
5. **Time:** hourly steps, local time Europe/Zurich, 24 values per day as in the data
   (see `data/raw/open-meteo/SOURCE.md`).

## 6. Engineering principles

- **Modular and reusable.** Small units with clear interfaces. Core logic (heat physics, storage,
  scheduling, economics) is independent of UI, files and network, and uses pure functions where
  sensible, so it can be reused for other locations and later research.
- **No hard-coded values.** Every fixed number (house, tank, server, prices, efficiencies,
  thresholds, job types) is defined once, in a central parameter definition, with unit and source,
  and passed in. The reference house is one parameter set among many; location, house, hardware and
  prices must be swappable without touching logic. Even universal values get a named constant
  (`HOURS_PER_YEAR`).
- **Simulation and reality share interfaces.** The scheduler decides from a heat signal; simulated
  servers and real processes are executors behind the same interface.
- **Reproducible runs.** Deterministic (fixed seeds), one documented command per result, every
  output traceable to its inputs and parameters.
- **Simple and robust over clever.** Few, well-known dependencies; add one only with a reason.
  Readable code; formulas get a short docstring with their source.
- **High quality, but hackathon-sized.** Clean enough that a teammate can extend it tomorrow; no
  speculative abstractions for features nobody asked for.

## 7. Testing

- **Test the core logic:** heat balance, storage, scheduler decisions, economics and break-even,
  data validation. Use the known values from the case as checks (206 heating days, ≈ 27,000 kWh,
  12.5 servers in the coldest hour, 19.2 kWh per server-day).
- **No tests for presentation** (layout, plots, styling, copy): check it visually instead, e.g. with
  a screenshot.
- Run the relevant tests before opening a PR and say in the PR what you ran.

## 8. Decisions and reviews

An important decision gets a short decision record and a **review by a subagent with fresh,
limited context**. Decide yourself when this applies, run the review, and report the outcome.

**Triggers:** architecture or module boundaries · data model or interfaces between parts · stack or
a library that is hard to replace · scheduling strategy · economic model or key formulas · anything
that changes scored figures · scope changes · later: larger UI choices (dashboard structure, main
visual, demo flow).
**No review needed for:** bug fixes, small refactors, styling, docs, data re-fetches.

Process:
1. Write `docs/decisions/NNNN-title.md` from the template, status `proposed`.
2. Run the review as described in [`docs/agents/review.md`](docs/agents/review.md). In Claude Code
   use the `decision-reviewer` subagent. Give it the short packet only, never the conversation.
3. Apply what convinces you and record the result in the "Review" section.
4. Report to the user in 2–4 lines: decision, verdict, what changed. Your user's agreement is
   enough to start implementing; the team confirms later and sets the status to `accepted`.

One review per decision; no review loops. Keep it proportionate: until the draft submission, review
only decisions that are hard to reverse (interfaces, stack, scored formulas).
Decision numbers: take the next free number on your branch; if it collides with one merged to
`main` in the meantime, renumber yours before merging.

## 9. Git workflow

- One short-lived branch per task, created from the latest `main`: `feat/…`, `fix/…`, `docs/…`,
  `data/…`, `exp/…` (experiments).
- Small, focused commits with English imperative messages ("Add storage model").
- Open a PR to `main` with: what and why, how it was tested, assumptions or decisions touched.
- **Merging:** people merge by default. An agent may merge its own PR only if it is small, tests
  pass, it changes no architecture, interface or scored figure, needs no decision record, **and**
  its user agreed in the conversation. Use squash merge.
- Before opening a PR, bring the branch up to date with `main` and resolve conflicts carefully.
  Never force-push to `main`, never rewrite other people's branches.
- Stay inside your task's files; three people are working in parallel.

## 10. Communication

- **Reply in the user's language.** Everything in the repository (code, comments, docs, commits,
  PRs) is in English.
- Keep reports short: what changed, how it was checked, open questions, and deadline status when a
  deadline is close.
- **Ask instead of deciding** when the choice belongs to the team: vision, stack, hardware, servers
  per house, business model, scope, anything a juror might ask about. Give a recommendation with
  reasons.
- Be economical with time and tokens: read what you need, do not re-read, use subagents for reviews
  and broad searches.

## 11. Data

- Rules, layout and how to get the originals: [`data/README.md`](data/README.md).
- Raw data is never edited by hand; derived data comes from scripts only; large datasets are not
  committed.
- `python3 scripts/fetch_data.py --check` validates the data in the repository against the case.
- Never commit secrets, API keys or personal data.
- From the deep dive: data sovereignty matters (no data to other countries where not allowed;
  sensitive data stays local or anonymised). Respect this in any design.

## 12. To be decided by the team

Fill in when decided (each with a decision record). Until then, do not assume an answer.

- Our vision and product idea
- Tech stack (language, frameworks, dashboard)
- Repository layout for code, notebooks and results
- Central parameter/configuration format (replaces `docs/assumptions.md` later)
- Build, run and test commands
- Feature freeze time and the contents of the draft submission
