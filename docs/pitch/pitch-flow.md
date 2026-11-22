# Pitch flow (draft 2026-11-22)

5 minutes including the live demo, then 3 minutes Q&A (`docs/case/challenge.md`). Order chosen by
the team: intro film → dashboard of the reference house → map → zoom out to a globe →
interactive demo via QR. Later all of it runs as one browser app; this file describes the flow in
words. Replaces the scene order of [`storyboard.md`](storyboard.md) (older draft).

**Case rules this flow must meet:** open with the reference house's key figures (servers per
house, share of heat covered, heat dumped, utilisation, missed deadlines, break-even price); show a
January day, a July day and a real running job that the scheduler pauses and resumes; say when
the model does not pay off (feasibility, 25 %). Every number on screen comes from a Fleet run
(`docs/model/fleet-simulation.md` §16) and can be traced in [`SOURCES.md`](../../SOURCES.md).

## Flow

| Time | Scene | On screen | What we say (one message per scene) |
|---|---|---|---|
| 0:00–0:48 | **1. Intro film, cut after the `dive` beat** | the rendered film (`feat/T-78-intro-film`), 0:00–0:48: Basel in January, Bob, the data centre, the dive into the basement with four servers, tank and radiators | (voice-over of the film) |
| 0:48–1:30 | **2. Key figures: the reference house** | dashboard, household page of run `reference`, whole year 2025 | The case house with 4 servers for a simulated year: the numbers the case asks for (table below). One sentence on the organisers' real house: it uses three times more heat in summer, and its missed deadlines drop from 6,905 to 11 (run `real-house`). |
| 1:30–2:30 | **3. Map: January day vs July day** | admin map, run `dreispitz-5-pool` (5 houses and the indoor pool at FHNW Dreispitz), slider on 15 January, then on 15 July | January: every house wants heat, the work spreads over all of them. July: the houses only need hot water, three quarters of the work moves to the pool. The heat stays where it is needed; the work moves. Almost nothing is late (0.02 %). |
| 2:30–2:55 | **4. Where it stops paying off** | money chart (income vs electricity) and the net after hardware | Income covers electricity and the homeowner's heat, but not yet the GPU hardware at today's volumes. It pays off at … (number from the break-even sweep). |
| 2:55–3:15 | **5. Zoom out to the globe** | the map itself zooms out to a turning globe | Any place with weather data runs the same model. |
| 3:15–4:40 | **6. Interactive demo via QR** | QR code to `/speak`; split screen: runner console left, `/live` right | Scan and ask the house a question. Your request runs for real on this laptop, on the July day you just saw. At 13:00 nobody uses hot water and the tank is full: the scheduler freezes the job. The 14:30 draw brings useful heat back: it continues. At the end the console shows how many kWh it really used. |
| 4:40–5:00 | **7. Close** | one sentence on screen | Pilot ask: which houses, which customers, what we measure. |

**Case requirement "January day, July day, interrupted job":** the two days are shown on the map
(scene 3, the same Fleet model and scheduler policy), the interrupted job is the real container in
scene 6. Scene 6 replays exactly the July day of scene 3, which links the two.

### Why the film is cut at 0:48 (decided by the team 2026-11-22)

The film's next two beats use numbers of the retired year model: `lapse` (0:48–0:58) says the tank
is first full on 22 March (Fleet reference run: 14 May), and `figures` (0:58–1:08) says 58 % of the
heat is covered at 65 % utilisation (Fleet: 22.8 % and 25.4 %). Shown next to the dashboard, the
jury would see two different answers for the same house. Cutting at 0:48 keeps the film free of
outdated numbers; the dashboard then gives all key figures. Alternative: keep the film to 1:08 and
re-render those two beats with Fleet numbers (`video/blender/prepare_data.py` would read
`data/derived/fleet/reference/` instead of the year-model files). The film's own map part
(1:20–1:46) is replaced by the live map in scene 3.

### Numbers for scene 2 (run `reference`, `main` `dc325db`)

| Key figure (case order) | Value |
|---|---:|
| Servers per house | 4 |
| Share of heat demand covered | 22.8 % (7,133 of 31,231 kWh) |
| Heat dumped | 0 kWh |
| Utilisation | 25.4 % |
| Missed deadlines | 8.9 % (6,905 of 77,840 jobs) |
| Break-even computing price (case formula) | CHF 0.226 per server-hour |

For scene 4: income CHF 13,360, electricity CHF 2,965, margin CHF 11,344, L4 hardware CHF 25,661
per year → net −CHF 14,317.

### Numbers for scene 3 (run `dreispitz-5-pool`, re-run on `main` 2026-11-22)

| | January | July |
|---|---:|---:|
| Work on the 5 houses | 6,849 kWh | 2,325 kWh |
| Work on the pool | 3,452 kWh (34 %) | 7,396 kWh (76 %) |
| Missed deadlines over the year | 125 of 778,536 jobs (0.02 %) | |

Volumes are the sourced Basel volumes (× 1, not × 0.1 as the single house) and customers accept
every Type 2 price (elasticity 0), as stated in the scenario's `_note`.

## Transitions

- **1 → 2:** cut on the last frame of the shortened film to the dashboard (both dark).
- **2 → 3:** "back to the network" link from the household page; switch the run to `dreispitz-5-pool`.
- **3 → 5 (via 4):** must be seamless. Simplest and safest: no video, but the map itself zooms out
  to a globe (MapLibre GL 6 has a globe projection), so the last map frame and the first globe frame
  are the same picture. A rendered video only works if its first frame matches the map exactly.
- **5 → 6:** the globe stops, the QR code fades in.

## Things to decide or prepare

- **Scene 4 sentence:** break-even volume or price from the sweep (in progress).
- **QR access:** the QR must carry the ngrok URL with the token (`/speak?t=…`); a phone hotspot
  as fallback. If nobody scans, the presenter speaks or types the request.
- **Fallback:** a screen recording of scenes 2–6, in case the laptop or the network fails.

## Q&A preparation

[`ARCHITECTURE.md`](../../ARCHITECTURE.md) (what runs where), [`SOURCES.md`](../../SOURCES.md)
(where each number comes from), `docs/model/fleet-simulation.md` §15–16 (assumptions,
simplifications, results), `docs/demo/voice-request.md` (why the freeze is real: cgroup probe).
