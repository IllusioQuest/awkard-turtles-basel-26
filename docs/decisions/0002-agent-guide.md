# 0002: Agent working guide (AGENTS.md)

- **Status:** accepted
- **Date:** 2026-11-20
- **Decided by:** team (requirements from the team, drafted by agent)

## Context

Three people work in parallel with Claude Code, OpenAI Codex and Gemini CLI for ~35 hours. Agents
need shared rules for priorities, code quality, reviews, Git and deadlines.

## Decision

`AGENTS.md` is the single guide for all tools (`CLAUDE.md` and `GEMINI.md` only import it). Main
points: the case is the case owner's view, not our plan; must-haves first; no hard-coded values;
core logic tested, presentation not; important decisions get a decision record plus a
fresh-context review ([`../agents/review.md`](../agents/review.md)); feature branches + PRs, agents
may merge small PRs with their user's consent; deadline reminders via `scripts/deadline-reminder.sh`
(automatic in Claude Code through hooks).

## Alternatives considered

- Separate guides per tool: would drift apart.
- No reviews: faster, but the team explicitly wants a second look at important decisions.

## Consequences

The guide must be kept current: vision, stack and layout are still open (§12 of `AGENTS.md`).

## Review

Fresh-context subagent, verdict **go with changes**:
1. Open stack/layout/parameter format may stall agents before the draft. **Partly applied:**
   vision-independent must-have work may start now. The stack is a team decision, so it is not
   fixed in the guide; it was flagged to the team as urgent.
2. Parallel work needs an interface contract, and decision numbers can collide. **Applied:**
   renumbering rule. Interface contract: recommended to the team as the next decision record.
3. Review overhead too high for 35 h. **Applied:** until the draft, review only hard-to-reverse
   decisions; the user's agreement is enough to start.
Also noted: the final pitch time is TBD, so the script gives no reminder for it yet.
