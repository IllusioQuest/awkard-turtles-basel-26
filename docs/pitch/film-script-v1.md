# Pitch script v1 — two opening voices, one evidence spine

Status: working script for T-64; no recording or rendered film yet. Spoken language: English. The six annual figures and Basel flows have publication gates below. Bracketed text is a production cue, not spoken copy.

## Opening A — character-led (recommended)

**0:00–0:16 — annual slate, ordinary Basel street and reference-house exterior.**

“Meet Lea. Her house in Basel needs warmth every day, but not at the same hour or in the same amount. We tested what happens when useful computing follows that need for a full year.”

[Lea is fictional. The house is a representative architectural visualisation in a recognisable Basel urban setting, not a real identified case address. Overlay six figures from one validated reference-house result: server count, heat coverage, heat dumped, utilisation, missed deadlines, case break-even. Do not render an unavailable deadline rate. Label: `Reference house · simulation · Basel 2025`.]

**0:16–0:37 — camera approaches this house; only the basement wall becomes transparent; January.**

“Tonight, paid calculations run on the servers downstairs. A closed water loop takes their heat to the tank and the rooms. It covers part of the load. The existing heater covers the rest.”

**0:37–0:54 — same camera position; seasons pass into July.**

“By July, Lea needs hot water, but almost no space heating. Filling the tank is useful. Running a job just to throw its heat away is not.”

**0:54–1:19 — pull up through the roof; Basel map and year time-lapse.**

“So the scheduler asks a different question: which eligible Basel site can use this work *and* its heat now? The work may move. The heat stays in the building that produces it.”

[One thin soft line is a **simulated job transfer**: origin, destination, hour, and quantity from the real flow export. The map's yearly pulse reflects the actual 2025 scenario. If a Basel-only export is unavailable, show an explicitly labelled concept shot instead of numerical flows.]

**1:19–1:30 — film hands off to the live process.**

“The camera is only the explanation. This next part is running now.”

## Opening B — evidence-led (alternative)

**0:00–0:16.** “One reference house. One year of Basel weather. A measured computing job and a heat-led scheduler. Here are the six figures the case asks us to report.”

**0:16–0:54.** “In January, the servers' recovered heat passes through a water-side loop to the tank. The backup heater covers the gap. In July, space heating falls away, but hot water remains. The scheduler reduces work where heat cannot be used.”

**0:54–1:30.** “From the house, we now see the Basel network. Each line is a simulated job allocation, not heat or a container travelling across town. The following live process shows the same scheduling rule controlling a real job.”

## Shared live and spoken sequence

**1:30–2:15 — live demonstration, video off.**

Presenter names the real job, starts it, changes the heat signal and shows the scheduler's decision and the process pause/resume. The operator keeps an exact reset checklist outside the projected image. Say only what the current build actually demonstrates; if the process stays on one device, do not claim physical relocation. A recorded fallback must be introduced as a recording.

**2:15–2:55 — workloads and scheduling.**

“We model two kinds of work. For each, we record how long one job runs, how much energy it uses, whether it can be interrupted and when it is due. We state where the daily job count comes from. [Insert verified numbers and sources.] One type runs on this machine; its measured power and runtime are scaled transparently to the case's one-kilowatt server. The scheduler distributes work by heat need, pauses or reschedules it, and uses the tank to bridge hours. If nobody needs the heat and the job can wait, we do not run it just for the animation.”

**2:55–3:45 — customer, host, operator.**

“The compute customer buys [specific verified outcome], not a warm server. The house uses the heat. DigitalHeat pays for electricity, hardware, integration and operation. The case's break-even subtracts only the value of *used* heat from electricity cost and divides by sold server-hours. We show that number beside the full-cost boundary. [Insert sourced actor-level result.] Below that boundary, or without enough paid jobs, the model does not pay.”

**3:45–4:25 — feasibility and method.**

“This is a model of the fixed reference house, not an installation plan. A real pilot still needs a quiet location, hydraulic connection, electrical capacity and a paying workload. We compare our scheduler with [validated baseline] on useful heat, dumped heat, utilisation and missed deadlines. [Insert validated comparison or cut the sentence.]”

**4:25–4:48 — short film ending; Basel map to globe.**

“And we wrote down the method, assumptions and limits as [accurate paper status]. Basel is our tested setting. The framework can represent other places.”

[The globe is a visual horizon, not evidence of a worldwide run. Keep it almost incidental: no sweeping claim, no invented worldwide markers.]

**4:48–5:00 — closing still.**

“Useful computing. Useful heat. A model that tells us where both can work—and where they cannot. [Team's chosen pilot/customer ask.]”

## Source and claim lock before recording

| Claim on screen or in voice | Current source | Status before final cut |
|---|---|---|
| Fixed reference-house formula and six required figures | `docs/case/challenge.md` and `docs/case/key-figures.md` | Requirement verified; final single-scenario result still to select. |
| Four-server draft: 58.0% coverage, 0 kWh dumped, 64.7% utilisation, CHF 0.2261/h case break-even | `docs/results/reference-house.md`; C# export | Draft baseline, unlimited jobs. Its missed-deadline rate is **undefined**, so this bundle cannot fill the entire opening slate. |
| 2025 hourly house heat, tank and backup animation | C# reference-house hourly export | Bind frames to export; do not manually animate numerical quantities. |
| Basel-only year time-lapse and flow lines | `docs/model/network-map-data.md` currently specifies Swiss test network and a mock export | **Missing validated Basel-only network export.** Do not use Swiss mock as factual Basel flows. |
| Real job pause/resume through shared scheduler | T-04/T-03 live runner and current demo build | Rehearse exact build; film cannot substitute for live proof. |
| Two job types and daily arrivals | T-30/T-33, T-31 and sourced scenario docs | Verify actual merged state and chosen scenario before naming figures. |
| Customer value, actor surplus and full-cost boundary | `docs/business-model.md`, T-14 and T-42 | Business model provisional; use sourced result only and state unknown installation costs. |
| Paper status | Paper repository and submission record | Say “draft” unless a different status is verified. |

The creative structure follows a clear problem-to-resolution arc but leaves every numerical claim auditable. Narrative visualisation research describes the need to balance guided sequence with readable data ([Segel and Heer, 2010](https://vis.stanford.edu/papers/narrative.html)); this film guides attention, while the live demo and source sheet carry the proof.
