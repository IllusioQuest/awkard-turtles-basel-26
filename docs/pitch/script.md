# Pitch script (5 minutes)

Spoken words are exact; read them, then make them yours. Three roles: **A** (story, opens and
closes), **B** (numbers, real house, map), **O** (operator at the laptop, leads the live demo). The
team assigns names. Every number, with its source, is in [`pitch-flow.md`](pitch-flow.md); answers
for the Q&A are in [`qa-cheatsheet.md`](qa-cheatsheet.md).

The whole pitch runs in the pitch app (`/pitch`, decision 0069); O only presses → (keys in
QUICKSTART §4b). Pace: about 140 words per minute. *(beat)* = one second of silence. Version 3, after two juror
tests (see the end of this file). Target length 4:45, leaving 15 seconds for the live demo.

| Time | Screen and action | Spoken |
|---|---|---|
| **0:00–0:48** | **Intro film** 0:00–0:48. For its first 7 seconds a strip at the bottom: *Bob's house, 2025, one server: 18.9 % of heat covered · 0 kWh dumped · 84 % utilisation · 5.9 % late jobs · break-even CHF 0.23 per server-hour.* | *(the film's voice-over)* |
| 0:48–1:20 | Household page of run `reference-1-server`, whole year. O points at each figure as it is spoken. | **A:** In the film Bob has four servers. That was our first plan, and it was wrong. *(beat)* **B:** Here is Bob's house, the case's reference house, with one server, for a whole year. Nineteen percent of its heat covered. Zero kilowatt-hours dumped. Eighty-four percent utilisation. Six percent of jobs late. Break-even: twenty-three rappen per server-hour, and we earn one franc eighty. With four servers it would cover twenty-three percent, and lose fourteen thousand francs a year after hardware. |
| 1:20–1:40 | O presses →: household page of run `real-house-1-server`. | **B:** Is the case house realistic? The organisers gave us the gas bills of a real house. Its heating fits the case almost exactly: ten point one kilowatts against ten. But in summer it uses about three times as much hot water, and its late jobs fall to one percent. Summer is where this business is decided. |
| 1:40–1:55 | O presses →: the Dreispitz map (`dreispitz-5-pool-1-server`) on 15 January. | **B:** So we built a neighbourhood: five houses like Bob's, one server each, around the FHNW campus in Dreispitz, and one indoor pool. Fifteenth of January. |
| 1:55–2:05 | Map, January: all houses glow, the pool too. | **B:** In winter every house runs its server for its own heat. The pool takes the overflow. |
| 2:05–2:25 | O presses →: the map jumps to 15 July; houses dim, the pool glows. | **B:** Fifteenth of July. *(beat)* The houses only need hot water, so the work moves: three quarters of it runs at the pool. Over the year, fewer than two jobs in ten thousand are late. The heat stays where it is needed. The work travels. |
| 2:25–2:50 | O presses →: the money view of the one-server house; → again shows the strip: *1 server: +CHF 5,252 a year after hardware · payback 3.4 years for DigitalHeat · Bob: CHF 784 of free heat.* | **A:** Does one server pay? After hardware it leaves five thousand francs a year, shared between DigitalHeat and Bob, who gets about eight hundred francs of free heat. DigitalHeat's payback: three and a half years. This assumes one house wins a tenth of the demand we sourced, Basel's dictations among it. Below half of that, it stops paying. |
| 2:50–2:57 | O presses →: the map fills the screen and zooms out to a turning globe. | **A:** The same model runs for any place with an hourly weather record. *(beat)* Here is one house. Live. |
| 2:57–3:10 | O presses →: the split screen (console left, `/live` right). O holds up their phone (`/speak` already open) and asks: *"How much heat does one server give off, and can it warm my shower water?"* | **O:** Let me ask Bob's house a question, from my phone. A speech model and a language model answer it, on this laptop, no cloud. |
| *cue: transcript appears* | Console: `admit`, `start`, the transcript. | **O:** *(read the transcript aloud)* Your question is now a real job. The house is simulated: a July day with a standard hot-water profile, ten seconds per hour, half past eleven. The job is not. |
| *cue: answer streams* | The answer streams in the console. | **O:** There is hot water to heat, so it runs. Watch the answer. |
| *cue: `freeze` line* | `freeze … free 0.000 < need 0.020 kWh`; the text stops. | **O:** One o'clock. Nobody draws hot water, the tank is full, the heat has nowhere to go. So the scheduler freezes your job. In the middle of a word. **B:** A data centre would keep running and blow the heat outside. |
| *cue: `thaw` line* | `thaw`; the answer continues; `exit 0 … kWh`. | **O:** Two o'clock. That hour has a draw again, so the job thaws exactly where it stopped. *(read the last sentence)* The heat rule that froze it is the same code that produced the yearly numbers. And it used one ten-thousandth of a kilowatt-hour, measured on this machine. |
| last 20 s | Closing slide: *Let the house decide.* | **A:** Data centres pay for cooling. Bob pays for heating. One server can do both, if the house decides when. *(beat)* We would like to put one server into ten real houses this winter and measure every kilowatt-hour. *(beat)* Let the house decide. Thank you. |

## After the pitch (optional): audience demo

Separate from the scored five minutes and from the live demo above (decision 0074,
[`docs/jobs/audience-apertus-demo.md`](../jobs/audience-apertus-demo.md)). Only if the Q&A leaves room
and the audience service is running (checks screen: "Audience demo" green).

| Screen and action | Spoken |
|---|---|
| O presses →: large QR to `swiss-heat.lovable.app` (scene 9). | **O:** Apertus is running on this laptop. Scan this and choose the ready-made question. Our software places each job where heat is useful. |
| After about 20 s, O presses →: the four-worker operator view (scene 10). | **O:** Four logical workers on this one laptop. The background colour is heat demand, not temperature; the heat figures are an illustrative scenario. Your answers arrive on your phone in a few minutes. |

Answers take 5–7 minutes (measured 314–442 s for four at once); never wait for them on the projector.

## Notes for the speakers

- The demo rows are driven by the console, not by the clock: speak each line when its cue appears.
  If the freeze comes while O is still talking, O finishes the sentence and then says "There. One
  o'clock." The replay runs at ten seconds per simulated hour, so freeze and thaw are about ten
  seconds apart; keep B's line to that one sentence.
- Rounded numbers on stage, exact ones in the Q&A.
- **O** opens `/speak` on their own phone before the pitch (scan the small code in the checks
  screen, key **H**). If the phone fails on stage, **O** presses **T** in the pitch app and types the
  question (same path as the phone).
- If the freeze comes before the answer starts streaming (a long spoken question), **O** says "It is
  still thinking, and now it is frozen" instead of "in the middle of a word".
- Never say "profitable" without "after hardware" and "at our assumed demand".

## Juror tests

- **Version 1: 6.75 / 10.** Key figures not at the start; four versus one server confusing; sources,
  money split and the replayed clock not said; scaling to 1 kW missing; 25 s too long.
- **Version 2: 7.55 / 10.** Remaining: build the opening around the one-server answer, put the
  demand assumption and "when it stops paying" into the pitch (feasibility), compare break-even with
  what we earn, drive the demo by cues, cut about 20 s.
- **Version 3 (this one):** all of the above applied; the map uses one server per house too, so the
  whole pitch shows one configuration, with four servers only as a comparison line.
