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

The spoken words are in [`script.md`](script.md) (version 3, after two juror tests); this table is the
scene plan.

| Time | Scene | On screen |
|---|---|---|
| 0:00–0:48 | **1. Intro film**, cut after the `dive` beat | the film; for its first 7 s a strip with the key figures of the reference house with one server |
| 0:48–1:20 | **2. Key figures: one server** | household page of run `reference-1-server`, whole year |
| 1:20–1:40 | **3. The real house** | household page of run `real-house-1-server` |
| 1:40–2:25 | **4. Map: January day vs July day** | run `dreispitz-5-pool-1-server`, slider 15 January, then 15 July |
| 2:25–2:50 | **5. Does one server pay?** | money chart, then a strip with the money split |
| 2:50–2:57 | **6. Globe bridge** | the map zooms out to a globe, the QR code fades in |
| 2:57–4:25 | **7. Interactive demo via QR** | QR, then split screen: runner console left, `/live` right |
| 4:25–4:45 | **8. Close** | one line: *Let the house decide.* |

**Case requirement "January day, July day, interrupted job":** the two days are shown on the map
(scene 4, five houses exactly like the reference house, the same Fleet model and scheduler
policy), the interrupted job is the real container in scene 7. Scene 7 replays exactly the July day of scene 4, which links the two.

### Why the film is cut at 0:48 (decided by the team 2026-11-22)

The film's next two beats use numbers of the retired year model: `lapse` (0:48–0:58) says the tank
is first full on 22 March (Fleet reference run: 14 May), and `figures` (0:58–1:08) says 58 % of the
heat is covered at 65 % utilisation (Fleet: 22.8 % and 25.4 %). Shown next to the dashboard, the
jury would see two different answers for the same house. Cutting at 0:48 keeps the film free of
outdated numbers; the dashboard then gives all key figures. Alternative: keep the film to 1:08 and
re-render those two beats with Fleet numbers (`video/blender/prepare_data.py` would read
`data/derived/fleet/reference/` instead of the year-model files). The film's own map part
(1:20–1:46) is replaced by the live map in scene 4.

### Numbers on screen (runs on `main`, 2026-11-22)

| Run | Shown in | Key figures |
|---|---|---|
| `reference-1-server` | strip in the film, scene 2 | 1 server · 18.9 % covered · 0 kWh dumped · 84.1 % utilisation · 5.9 % late (4,622 of 77,840) · break-even CHF 0.226; income CHF 13,333 (CHF 1.81 per server-hour), after hardware +CHF 5,252 (DigitalHeat +CHF 4,470, payback 3.4 years; Bob CHF 784 of heat) |
| `reference` (comparison) | one sentence in scene 2 | 4 servers · 22.8 % · 0 · 25.4 % · 8.9 % late · CHF 0.226; after hardware −CHF 14,317 |
| `real-house-1-server` | scene 3 | 10.13 kW fitted vs case 10 kW, 38.8 kWh/day summer use vs 12; 956 late jobs (1.2 %), +CHF 6,332 after hardware |
| `dreispitz-5-pool-1-server` | scene 4 | pool share of the work 68 % in January, 76 % in July; 126 of 778,536 jobs late (0.016 %) |
| break-even sweep | scene 5 | one server stops paying below half of the assumed demand (volume scale 0.05 instead of 0.1) |

Sources and the full tables: `docs/model/fleet-simulation.md` §16.

## Transitions

- **1 → 2:** cut on the last frame of the shortened film to the dashboard (both dark).
- **2 → 3:** "back to the network" link from the household page; switch the run to `dreispitz-5-pool`.
- **3 → 5 (via 4):** must be seamless. Simplest and safest: no video, but the map itself zooms out
  to a globe (MapLibre GL 6 has a globe projection), so the last map frame and the first globe frame
  are the same picture. A rendered video only works if its first frame matches the map exactly.
- **5 → 6:** the globe stops, the QR code fades in.

## Things to decide or prepare

- **Servers per house:** decided: scene 2 shows the case's 4 servers as the comparison, scene 5 shows
  the key figures for 1 server, the configuration that pays.
- **QR access:** the QR must carry the ngrok URL with the token (`/speak?t=…`); a phone hotspot
  as fallback. If nobody scans, the presenter speaks or types the request.
- **Fallback:** a screen recording of scenes 2–6, in case the laptop or the network fails.

## Q&A preparation

[`ARCHITECTURE.md`](../../ARCHITECTURE.md) (what runs where), [`SOURCES.md`](../../SOURCES.md)
(where each number comes from), `docs/model/fleet-simulation.md` §15–16 (assumptions,
simplifications, results), `docs/demo/voice-request.md` (why the freeze is real: cgroup probe).
