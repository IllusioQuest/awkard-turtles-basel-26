# Pitch storyboard (draft, agreed direction 2026-11-21)

> Ticket: [T-21](../tickets/T-21-pitch.md). Agreed with the team lead on 2026-11-21 as the direction;
> details, numbers and slides still to do. 5 minutes pitch incl. live demo + 3 minutes Q&A.

**Core idea:** data centres destroy heat; we sell it twice, as an AI service and as heating — and we make it work in the place where everybody else failed: the ordinary home.
Lessons applied (Junction 2026 research): the demo is the centre, clarity beats complexity, prototype
plus a concrete rollout plan, recorded fallback for the demo.

| Time | Scene | What the jury sees | Feeds from |
|---|---|---|---|
| 0:00–0:30 | **Key figures** (required by the case) | servers per house, share of demand covered, heat dumped, utilisation, missed deadlines, break-even price | T-06 |
| 0:30–1:00 | **The tension** | year heatmap: 12.5 servers in winter vs. 0.6 in summer | year explorer / T-13 |
| 1:00–2:15 | **Inside the example house** (heart of the demo) | animated cutaway: servers in the basement, tank, radiators, hot water over 15 January; the tank fills and **the real job on the laptop freezes live**, then resumes ("the house decides when AI computes"). Then 15 July: tank full, only hot water. Question: "where does the work go?" | T-04, T-25 |
| 2:15–3:00 | **Zoom out to the map** | camera leaves the house to the Basel neighbourhood; in July jobs flow as arrows to a hotel spa and an indoor pool; heat stays local, only work moves. Numbers: greedy vs. our optimiser (less dump, more utilisation) | T-10, T-12, T-17 |
| 3:00–4:00 | **Who earns: one server-hour, followed** | a receipt per server-hour: a research group pays X CHF for an hour of simulation, or an AI customer per token for document work (cheaper than the cloud, computed in Switzerland; decision 0020) → 0.33 CHF electricity → 0.11 CHF heat to Anna (homeowner) → DigitalHeat margin. Three faces with one number each (Anna saves Y CHF/yr, the research group Z, DigitalHeat W per server-year). A slider moves into the red zone: "here it stops paying off" | T-14, T-16 |
| (inside 3:00–4:00) | **Why others failed, why we don't** | one line each: Nerdalize bankrupt (2018), Cloud&Heat gave up homes, Qarnot left apartment radiators; their mistakes: sized for winter peak, always-on + dumping, new hardware, no demand. Our answer: size for year-round heat, refurbished GPUs, sovereign premium AI, route instead of dump | T-34, `research/competitors.md` |
| 4:00–4:30 | **Pilot plan** | one sentence: which houses, which customers, what we measure | T-21 |
| 4:30–5:00 | **"Oh, and by the way…"** | casually: another city typed in (Zurich, Helsinki), map and numbers switch instantly; "and it is the basis of a paper on the routing problem" | T-10, T-22 |
| Q&A | **Evidence** | assumptions table, method docs, verification script, source CSVs | T-06, T-13 |

The "by the way" ending only works if another location really runs live (T-10, weather by coordinates).

## Visual reference for the pitch video (2026-11-21)

From a winning pitch at another hackathon (traffic planning; we take only the visual language):
- **Dark map with a glowing overlay on the network** (green → yellow → red/purple), compact toggles in a
  corner (for us: `HEAT` / `JOBS` / `FLOW`).
- **Split-screen with a draggable divider** comparing two strategies on the same map (for us: greedy /
  always-on vs. our LP router, T-12), labels top left/right, tabs "Formalization" and "Comparison".
- **Ending: zoom out continuously until the whole 3D globe is visible**, which carries our
  "Oh, and by the way: any location" ending (T-10, weather by coordinates).
Feeds T-17 (admin map) and T-12 (map export, decision 0025).
