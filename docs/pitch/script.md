# Pitch script (5 minutes)

Spoken words are exact; read them, then make them yours. Three roles: **A** (story, opens and
closes), **B** (numbers, real house, map), **O** (operator at the laptop, leads the live demo). The
team assigns names. Every number, with its source, is in [`pitch-flow.md`](pitch-flow.md); answers
for the Q&A are in [`qa-cheatsheet.md`](qa-cheatsheet.md).

Pace: about 140 words per minute. *(beat)* = one second of silence. Version 3, after two juror
tests (see the end of this file). Target length 4:45, leaving 15 seconds for the live demo.

| Time | Screen and action | Spoken |
|---|---|---|
| **0:00–0:48** | **Intro film** 0:00–0:48. For its first 7 seconds a strip at the bottom: *Bob's house, 2025, one server: 18.9 % of heat covered · 0 kWh dumped · 84 % utilisation · 5.9 % late jobs · break-even CHF 0.23 per server-hour.* | *(the film's voice-over)* |
| 0:48–1:20 | Household page of run `reference-1-server`, whole year. O points at each figure as it is spoken. | **A:** In the film Bob has four servers. That was our first plan, and it was wrong. *(beat)* **B:** Here is Bob's house, the case's reference house, with one server, for a whole year. Nineteen percent of its heat covered. Zero kilowatt-hours dumped. Eighty-four percent utilisation. Six percent of jobs late. Break-even: twenty-three rappen per server-hour, and we earn one franc eighty. With four servers it would cover twenty-three percent, and lose fourteen thousand francs a year after hardware. |
| 1:20–1:40 | Switch to run `real-house-1-server`, same page. | **B:** Is the case house realistic? The organisers gave us the gas bills of a real house. Its heating fits the case almost exactly: ten point one kilowatts against ten. But in summer it uses about three times as much hot water, and its late jobs fall to one percent. Summer is where this business is decided. |
| 1:40–1:55 | "← Network", run `dreispitz-5-pool-1-server`, slider on 15 January. | **B:** So we built a neighbourhood: five houses like Bob's, one server each, around the FHNW campus in Dreispitz, and one indoor pool. Fifteenth of January. |
| 1:55–2:05 | Map, January: all houses glow, the pool too. | **B:** In winter every house runs its server for its own heat. The pool takes the overflow. |
| 2:05–2:25 | O drags the slider to 15 July: houses dim, the pool glows. | **B:** Fifteenth of July. *(beat)* The houses only need hot water, so the work moves: three quarters of it runs at the pool. Over the year, fewer than two jobs in ten thousand are late. The heat stays where it is needed. The work travels. |
| 2:25–2:50 | Money chart, then the strip: *1 server: +CHF 5,252 a year after hardware · payback 3.4 years for DigitalHeat · Bob: CHF 784 of free heat.* | **A:** Does one server pay? After hardware it leaves five thousand francs a year, shared between DigitalHeat and Bob, who gets about eight hundred francs of free heat. DigitalHeat's payback: three and a half years. This assumes one house wins a tenth of the demand we sourced, Basel's dictations among it. Below half of that, it stops paying. |
| 2:50–2:57 | O presses `G`: the map zooms out to the globe; the QR code fades in. | **A:** The same model runs for any place with an hourly weather record. *(beat)* Here is one house. Live. |
| 2:57–3:10 | QR code large; then the split screen (runner console left, `/live` right). | **O:** Scan this code and ask Bob's house a question. Anything. A speech model and a language model answer it, on this laptop, no cloud. |
| *cue: transcript appears* | Console: `admit`, `start`, the transcript. | **O:** *(read the transcript aloud)* Your question is now a real job. The house is simulated: a July day with a standard hot-water profile, ten seconds per hour, half past eleven. The job is not. |
| *cue: answer streams* | The answer streams in the console. | **O:** There is hot water to heat, so it runs. Watch the answer. |
| *cue: `freeze` line* | `freeze … free 0.000 < need 0.020 kWh`; the text stops. | **O:** One o'clock. Nobody draws hot water, the tank is full, the heat has nowhere to go. So the scheduler freezes your job. In the middle of a word. **B:** A data centre would keep running and blow the heat outside. |
| *cue: `thaw` line* | `thaw`; the answer continues; `exit 0 … kWh`. | **O:** Two o'clock. That hour has a draw again, so the job thaws exactly where it stopped. *(read the last sentence)* The heat rule that froze it is the same code that produced the yearly numbers. And it used one ten-thousandth of a kilowatt-hour, measured on this machine. |
| last 20 s | Closing slide: *Let the house decide.* | **A:** Data centres pay for cooling. Bob pays for heating. One server can do both, if the house decides when. *(beat)* We would like to put one server into ten real houses this winter and measure every kilowatt-hour. *(beat)* Let the house decide. Thank you. |

## Notes for the speakers

- The demo rows are driven by the console, not by the clock: speak each line when its cue appears.
  If the freeze comes while O is still talking, O finishes the sentence and then says "There. One
  o'clock." The replay runs at ten seconds per simulated hour, so freeze and thaw are about ten
  seconds apart; keep B's line to that one sentence.
- Rounded numbers on stage, exact ones in the Q&A.
- If nobody scans within ten seconds, **O** says "Let me ask for you" and speaks the backup question
  into the phone: *"How much heat does one server give off, and can it warm my shower water?"* If the
  phone fails, **O** types it into the runner's terminal.
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
