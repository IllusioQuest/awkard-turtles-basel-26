# Pitch storyboard (draft, agreed direction 2026-11-21)

> Ticket: [T-21](../tickets/T-21-pitch.md). Agreed with the team lead on 2026-11-21 as the direction;
> details, numbers and slides still to do. 5 minutes pitch incl. live demo + 3 minutes Q&A.

**Core idea:** data centres destroy heat; we sell it twice, as an AI service and as heating.
Lessons applied (Junction 2026 research): the demo is the centre, clarity beats complexity, prototype
plus a concrete rollout plan, recorded fallback for the demo.

| Time | Scene | What the jury sees | Feeds from |
|---|---|---|---|
| 0:00–0:30 | **Key figures** (required by the case) | servers per house, share of demand covered, heat dumped, utilisation, missed deadlines, break-even price | T-06 |
| 0:30–1:00 | **The tension** | year heatmap: 12.5 servers in winter vs. 0.6 in summer | year explorer / T-13 |
| 1:00–2:15 | **Inside the example house** (heart of the demo) | animated cutaway: servers in the basement, tank, radiators, hot water over 15 January; the tank fills and **the real job on the laptop freezes live**, then resumes ("the house decides when AI computes"). Then 15 July: tank full, only hot water. Question: "where does the work go?" | T-04, T-25 |
| 2:15–3:00 | **Zoom out to the map** | camera leaves the house to the Basel neighbourhood; in July jobs flow as arrows to a hotel spa and an indoor pool; heat stays local, only work moves. Numbers: greedy vs. our optimiser (less dump, more utilisation) | T-10, T-12, T-17 |
| 3:00–4:00 | **Who earns: one server-hour, followed** | a receipt per server-hour: the law firm pays X CHF for an hour of transcription (less than AWS, data stays in Basel) → 0.33 CHF electricity → 0.11 CHF heat to Anna (homeowner) → DigitalHeat margin. Three faces with one number each (Anna saves Y CHF/yr, the law firm Z, DigitalHeat W per server-year). A slider moves into the red zone: "here it stops paying off" | T-14, T-16 |
| 4:00–4:30 | **Pilot plan** | one sentence: which houses, which customers, what we measure | T-21 |
| 4:30–5:00 | **"Oh, and by the way…"** | casually: another city typed in (Zurich, Helsinki), map and numbers switch instantly; "every homeowner can try it" + QR code to the configurator; "and it is the basis of a paper on the routing problem" | T-10, T-19, T-22 |
| Q&A | **Evidence** | assumptions table, method docs, verification script, source CSVs | T-06, T-13 |

The "by the way" ending only works if another location really runs live (T-10, weather by coordinates).
