# Fresh-context review

How an agent gets an important decision reviewed by a second agent that does **not** share its
conversation. When to do this: `AGENTS.md`, section "Decisions and reviews".

Why fresh context: the reviewer should see the decision the way a teammate or juror would, without
the reasoning that led up to it. A small packet also keeps the review cheap.

## 1. Build the review packet

Write it to the decision record (`docs/decisions/NNNN-*.md`, status `proposed`), or for UI choices,
a short description plus a screenshot path. The packet the reviewer gets is only:

- the path of the decision record (or the description),
- at most ~5 paths of files it needs to judge the decision (code, docs),
- one sentence on what kind of feedback is wanted.

Do **not** paste the conversation, your own reasoning chain or long file contents.

## 2. Start the reviewer

| Tool | How |
|---|---|
| Claude Code | Use the `decision-reviewer` subagent (`.claude/agents/decision-reviewer.md`) with the packet as prompt. |
| Gemini CLI | Use a subagent if your version has one; otherwise run a separate non-interactive call: `gemini -p "<packet + reviewer instructions below>"`. |
| OpenAI Codex | Use a subagent if available; otherwise a separate non-interactive call: `codex exec "<packet + reviewer instructions below>"`. |
| Other | Any way of getting a second agent with only the packet and the instructions below. |

One review per decision. Do not loop reviews; if the verdict is "rethink", report to the user.

## 3. Use the result

Apply what convinces you, note what you rejected and why, and fill in the "Review" section of the
decision record. Report to the user in 2–4 lines: decision, verdict, what changed.

## Reviewer instructions

You review one decision for a 3-person team at a ~35-hour hackathon (Basel GreenTech 2026, case
"DigitalHeat": servers in buildings run interruptible computing jobs so that their waste heat covers
heat demand). You did not take part in the discussion. Read only what you need: the packet, and if
relevant `docs/case/key-figures.md` (case rules, must-haves, jury weights) and `AGENTS.md`
(project principles). Do not edit any files.

Judge the decision on:
1. **Case fit:** does it keep the must-haves and the fixed reference house intact?
2. **Jury impact:** transparency & reproducibility 25 %, feasibility 25 %, scheduling quality 20 %,
   creativity 20 %, design & usability 10 %.
3. **Time:** can three people build it in the time left, and does it keep the live demo safe?
4. **Generality and modularity:** no hard-coded values; parameters swappable (location, house,
   hardware, prices); clear interfaces; same scheduler for simulation and real job.
5. **Hidden assumptions or errors:** units, formulas, numbers without source.
6. **Simpler alternative:** is there one that gets 80 % of the value for much less work?

Answer in at most ~200 words, in this form:

```
Verdict: go | go with changes | rethink
Top issues (max 3, most important first): issue -> concrete fix
Simpler alternative (if any): ...
```

Do not repeat the decision back, do not praise, and skip points that are fine.
