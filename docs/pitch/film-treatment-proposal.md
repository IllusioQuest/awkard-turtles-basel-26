# Pitch film treatment — proposal for T-21

Status: proposal for the team lead; do not replace `storyboard.md` until agreed. The complete pitch is five minutes, including a short live demonstration. This document describes the film and the surrounding spoken story, not evidence that a film can provide by itself.

## One sentence

One Basel house needs heat at changing times; useful computing can follow that need, and paid work can move through a Basel network when another site can use its heat.

The fixed case reference house is a *model*, not the private house-plan archive. The camera may give that model an architectural form, but the geometry is illustrative. The 2025 yearly figures come from the C# result, not from animation keyframes.

## Two possible voices, same images

**Character-led:** “Meet Lea. On a cold January evening, her home needs more heat than four servers can supply. Across Basel, a planning firm needs a batch of documents checked by morning. The computers work; their recovered heat enters Lea's heating circuit. In July she only needs hot water. The scheduler pauses work here and offers the next eligible job to another Basel site.” Lea and the firm are fictional examples. Do not imply a real customer or a measured house.

**Evidence-led:** “This is the case's reference house in Basel. We simulated every hour of 2025. Its heating requirement changes from winter to summer; the scheduler places interruptible work where recovered heat can be used. Here is the yearly balance, followed by the same scheduling code controlling a real process.”

Recommendation: use the character-led opening for recognition, then switch to the evidence-led voice as the first annual result appears. Research on scientific storytelling recommends one central theme and an evidence-based resolution; a narrative alone can backfire with professional evaluators who want substance ([MIT Communication Lab](https://mitcommlab.mit.edu/cee/commkit/scientific-storytelling/); [MIT Sloan study](https://dspace.mit.edu/entities/publication/ec4f828f-3b16-4ddd-b5f3-47d7573a55de)).

## Five-minute run of show (working timings)

| Time | Medium and action | Spoken point / evidence | Gate |
|---|---|---|---|
| 0:00–0:16 | **Film A:** Basel reference house appears. A compact six-figure annual slate is legible before the camera enters. | Server count; total heat covered; heat dumped; utilisation; missed deadlines; *case-formula* break-even. Say “simulated reference house, Basel 2025.” | All six values must come from one labelled, reproducible C# scenario. Do not display an undefined deadline rate as zero. Four servers are the draft baseline, not a proven optimum. |
| 0:16–0:37 | Camera passes through the wall to a quiet basement plant room. Show server → water-side exchanger → tank → radiator/hot water, with the backup heater visibly filling a winter gap. | Paid work produces useful heat; recovered heat and backup heat are different colours. | The water route is conceptual until hydraulic compatibility is verified. Avoid an air-cooled rack merely standing next to a tank. |
| 0:37–0:54 | Seasons accelerate in the same house. July: space heat vanishes, hot water remains, tank fills, compute demand is held. | “The house cannot use every kilowatt-hour at every time.” | Do not show a job stopped solely by the film if the live process cannot demonstrate the same rule. |
| 0:54–1:19 | Camera exits through the roof into the **actual Basel map view**. A 2025 timeline sweeps across winter, shoulder season and summer. Thin soft flow lines show eligible job transfers between named Basel nodes; heat never travels along those lines. | “The work moves. The heat stays.” Show one transfer decision with origin, destination and reason, then let the yearly pattern speak visually. | Flow paths and counts must derive from the network export or be labelled “illustrative”; no invented hotel, pool or national network. City geometry and camera match are storyboarded against the existing map, not rebuilt as a second dashboard. |
| 1:19–1:30 | Film fades into the same live node/map framing. | “Now watch the scheduler do it.” | A deliberate transition from recorded sequence to live software; keep the change of medium honest. |
| 1:30–2:15 | **Live demo, no video:** run a measured real job; change heat signal; observe pause/checkpoint and resume or placement on an eligible node. | The same scheduler used by the year run controls the process. Show a small live process/console trace, not a decorative fake animation. | Offline fallback is a labelled recording. Rehearse exact clicks and reset state. |
| 2:15–2:55 | Two workload cards and one timeline, spoken over a still from the house/map. | For **each** type: runtime/job, energy/job and source, deadline, interruptibility, daily arrivals or “unlimited”; for the real type: laptop measurement and scaling to the 1 kW case server. Explain distribution, pausing, rescheduling, tank and the job whose heat is not needed. | No unsupported measured values; splitability, if claimed, needs its own evidence. |
| 2:55–3:45 | One simple money-flow view follows a sold work unit to compute buyer, household and DigitalHeat. | Electricity, computing income, used-heat value, case break-even **and** full-cost/host-operator outcome; state when it fails. Name the first paying customer and what result they buy. | Price and sold hours need sources. Never call system surplus operator profit. Hardware, integration and operations gaps remain visible. |
| 3:45–4:25 | Return to the house and Basel map, now with one pilot site highlighted. | Why this site and number of servers; backup heat and acoustic/hydraulic/electrical gates. Compare scheduler to a simple baseline with sourced figures if available. | The private house-plan analysis is a separate variant; it must not replace the scored reference house. No claimed pilot approval. |
| 4:25–4:48 | **Film B:** Basel map shrinks into a globe. The music resolves quietly; the camera does not hurry. | “We also wrote down the model, assumptions and limits as a paper. Basel is the test; the method can be applied elsewhere.” | Paper status must be described accurately (draft/preprint if applicable); globe does not imply a validated world-scale simulation. |
| 4:48–5:00 | Back to one house and one line: useful computing, useful heat, both parties benefit under stated conditions. | Clear closing request: a pilot site and a paying workload partner, if that is the team's chosen ask. | Rehearsal under five minutes including the live reset/transition. |

## Visual direction and production order

1. Write the final spoken script against the *approved result snapshot*. Make a source sheet for every displayed number, job event and map flow.
2. Build a low-fidelity animatic with still frames and time-coded narration. Check whether the six-figure opening is readable at presentation distance and whether the film-to-live transition survives a failure.
3. Approve architecture, material palette, lighting and camera in three style frames: winter interior, summer exterior, Basel map. Use a single colour grammar: useful server heat, backup heat, stored heat and moving jobs remain distinct.
4. Produce house animation and Basel year time-lapse from one shot list. Flow lines are narrow, soft and directional, but should brighten only for an actual transfer event; density reflects count, never arbitrary decoration.
5. Composite a camera-matched handoff into the existing map. A true continuous 3D-to-map camera requires compatible geometry/projection; a matched transition can look seamless while preserving the current MapLibre data view.
6. Export both film segments, captions and a silent fallback. Run one timed rehearsal with the live demonstration and one with the labelled fallback. Check every factual claim against the case and source sheet.

## Must-have ledger for the director

The film's first slate covers the six mandated annual key figures. The following live and spoken segments cover two job types, per-job runtime/energy/deadline/interruption, arrival rate and source, one measured job and 1 kW scaling, heat-led distribute/pause/reschedule, tank, unwanted heat, the shared scheduler code, electricity/income, and the break-even formula. The detailed assumptions and calculation path remain available for Q&A. Sources: [`challenge.md`](../case/challenge.md), [`key-figures.md`](../case/key-figures.md).

## Open editorial decisions

- Select character-led or evidence-led opening, or the proposed blend.
- Choose the actual paying customer and the one product outcome to name; the current business model is provisional.
- Lock one scored result bundle with a valid missed-deadline figure before rendering the six-figure slate.
- Confirm which real process and network transfer the live demonstration can reliably show; cut any film beat that overclaims it.
- Confirm whether the paper is a draft, preprint or submitted work at pitch time.
