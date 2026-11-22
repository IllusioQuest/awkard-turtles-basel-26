# Intro film: draft voice-over (proposal)

A proposal only, cut to the current `timeline.json`. When the team writes `docs/pitch/voiceover.md`,
that file wins: adjust the beat `start`/`end` and `marks` in `timeline.json` to its timestamps and
re-render. Speaking pace about 2.3 words per second; every number is in `numbers.json`.

| Time | Beat | Line | Number source |
|---|---|---|---|
| 0:00–0:08 | street | Basel. A January evening, minus three degrees. | `evening_temperature_c` |
| 0:08–0:21 | cold | This is Bob. His house needs heat. And this envelope says what it costs. | – |
| 0:21–0:28 | dc | A few streets away, a data centre throws its heat into the night. | – |
| 0:28–0:48 | dive | What if that computing ran where the heat is needed? Four servers in Bob's basement do paid work. Their heat goes into the water: to the radiators, and the tank stores the rest. | `servers_per_house`, `heat_recovery_share` |
| 0:48–0:58 | lapse | Weeks pass. On the twenty-second of March the tank is full, and the servers pause by themselves. The house decides when the computers work. | `first_full_tank`, `tank_kwh` |
| 0:58–1:08 | figures | Over the year 2025, four servers cover fifty-eight percent of the house's heat, at sixty-five percent utilisation, without dumping a single kilowatt-hour. | `coverage`, `utilisation`, `dumped_kwh` |
| 1:08–1:20 | rise | But in summer one house needs little heat. So where does the work go? | – |
| 1:20–1:46 | map | Across Basel, houses take work when they need heat. Near Dreispitz, an indoor pool needs heat all year. In summer the work moves to the pool. The heat stays where it is made. | `region_houses`, `pool_*` |
| 1:46–1:52 | end | Let us show you the scheduler doing this live. | – |

Wording rules from `docs/pitch/claim-sheet.md` (branch `docs/T-67-pitch-film-treatment`): say
"simulated" for the year figures; do not state a missed-deadline rate for the unlimited-jobs run; the
break-even is the case formula (electricity minus heat value), not a full-cost price.
