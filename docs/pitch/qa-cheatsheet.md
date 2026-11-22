# Q&A cheat sheet (3 minutes after the pitch)

Short answers first, exact numbers after the arrow. Sources: `docs/model/fleet-simulation.md` §16
(results and assumptions), [`SOURCES.md`](../../SOURCES.md) (every external source), the paper
`docs/paper/latex/main.pdf`. Questions 1–5 are the ones a simulated juror asked.

## The questions we expect

**1. Where do the jobs per day come from? What price do you earn?**
Public data, scaled down to one house → per day: 42.8 medical dictations (428 for Basel-Stadt: 1,269
physicians × 66.7 dictated minutes a month, Bax et al. 2011), 156.6 LLM jobs of 1,000 requests
(OpenRouter 194 trillion tokens a week × 1 % Swiss share × 1 % market share, **both shares are our
assumption**), 13.5 research jobs (sciCORE Basel ≈ 80,000 server-hours a year). Everything × 0.1,
the team's scale for one house. About 77,800 jobs a year. Prices: dictation CHF 0.36 per audio hour
(Infomaniak / kvant), LLM Public AI Apertus 8B CHF 0.084 / 0.168 per million tokens, research priced
by load between CHF 0.226 and 1.20 per server-hour against CSCS at CHF 0.82. Average realised:
CHF 1.81 per server-hour with one server (CHF 13,333 ÷ 7,366 h), CHF 1.50 with four (CHF 13,360 ÷
8,917 h). Break-even is CHF 0.226, so every sold hour covers its electricity; the hardware decides.

**2. The six key figures, one versus four servers?** (run `reference-1-server` / `reference`)
1 server · 18.9 % heat covered · 0 kWh dumped · 84.1 % utilisation · 5.9 % missed deadlines ·
break-even CHF 0.226 per server-hour. Income CHF 13,333, electricity CHF 2,449, after hardware
+CHF 5,252 a year. Four servers: 22.8 % · 0 · 25.4 % · 8.9 % · CHF 0.226; income CHF 13,360,
electricity CHF 2,965, after hardware −CHF 14,317 (−CHF 15,266 for DigitalHeat alone, without
Bob's heat value).

**Why is the break-even identical for one and four servers?** With nothing dumped, all heat is used,
so it reduces to electricity price × (1 − 0.8 ÷ 2.5) = 0.3325 × 0.68 = CHF 0.2261 per server-hour,
whatever the server count.

**3. What froze my job? Is it the same code as the yearly result? What drives the clock?**
The house's useful heat in that hour: hot-water demand plus free tank room. At 13:00 it is zero:
the EU standard tapping profile has no draw from 12:45 to 14:30, and the tank is full. The rule
(Core `HeatFirstScheduler` / `HeatCapacity`, decision 0068) is the same code the year simulation
uses. The freeze is `docker pause` (kernel cgroup freezer); an independent probe shows the CPU
counter standing still for 9.2 s. The clock is a replay of 15 July at 10 s per hour; the house is
simulated, the job is real. The demo uses the EU draw profile instead of the case's flat 12 kWh/day,
because a flat profile never has a zero hour.

**4. Who keeps the 5,000 francs? What does Bob save?**
Both together. DigitalHeat keeps about CHF 4,470 a year (payback 3.4 years); Bob gets free heat worth
CHF 784 a year at the case's heat-pump comparison (electricity price ÷ 2.5). With four servers Bob
gets CHF 949 of heat, but DigitalHeat loses CHF 15,266 (the pitch's "fourteen thousand" is the
combined loss, CHF 14,317).

**5. When does one server stop paying? How do you scale the laptop measurement to 1 kW?**
Below about half of our assumed demand (break-even at volume scale ≈ 0.05 instead of 0.1). Twice the
electricity price still leaves about CHF 3,600 (electricity +CHF 2,449, heat value +CHF 784).
Scaling: each job type's energy per unit comes from GPU benchmarks normalised to the 1 kW server
(Whisper: 469.9 audio hours per server-hour; LLM: 25 million tokens per server-hour). Our own laptop
measurement: Whisper large-v3 used 0.477 Wh per audio minute (CPU package), i.e. about 2,100 audio
minutes per kWh on a CPU (`docs/jobs/transcription.md`).

## Further likely questions

**Why only 23 % coverage?** Demand-limited in winter (not enough jobs), heat-limited in summer (only
hot water). With unlimited jobs: 58.05 %, the case's physical limit, which our model reproduces to
0.02 %.

**Why are jobs late?** Four servers: 6,905 late jobs = 5,769 never finished plus 1,136 finished
late; 5,763 of the unfinished arrived May–September, because the scheduler never runs work without
useful heat. One server: 4,622 late (3,400 never finished, 1,222 late). The real house, with about
three times the summer hot water, has 956 late jobs with one server (1.2 %) and 11 with four. In the
Dreispitz neighbourhood with the pool: 126 of 778,536 (0.016 %).

**Is your house model realistic?** We fitted it to the organisers' real house (monthly gas, 2025,
86 % boiler efficiency): 10.13 kW design heat versus the case's 10 kW, R² 0.996. Summer use is
38.8 kWh/day versus 12. Caveat: October–December missing; boiler efficiency assumed.

**Why GPU servers?** Transcription and LLM inference are the paid work; on a GPU they earn far more
per kWh. Hardware: L4 cards (CHF 2,235 each, Bechtle) on a Dell R570 host (CHF 8,248, Digitec),
11.6 cards per kW, six-year accounting life (CoreWeave 10-K), CHF 59 a month operations.

**Why would anyone buy compute from a house?** Swiss, sovereign, interruptible capacity cheaper than
the cloud for deadline-tolerant work: dictation transcription, document LLM batches, research
simulations. Interruptible work is the only work a heat-led server can promise.

**Medical dictations in a private home?** The scheduler enforces data locality: these jobs may only
run in Switzerland (placement policy). A real offer needs a contract and encryption; that is part of
the pilot.

**What is simulated, what is real?** Real: the containers, their freeze and resume, the transcript,
the answer, CPU time and energy on the laptop, the measured job runtimes. Simulated: the houses, the
tank, the year, the network, the prices.

**Can I reproduce it?** Yes. One command per result, about one second for the reference house:
`QUICKSTART.md`. Every assumption and source: `docs/model/fleet-simulation.md` §16 and `SOURCES.md`.
The paper's appendix lists one command per table.

**What would you do with a pilot?** One server in ten houses for one winter: measure whole-node
power, useful heat at the tank, delivery temperature, hot-water draws and sold hours. That replaces
our three biggest assumptions (80 % recovery, flat hot water, demand scale).

**Ten pilot houses at a tenth of the demand each would need all of Basel's dictations?** The
scale is a modelling device for one house in a market. A pilot needs a contracted customer, not a
market share, and in a network the demand spreads: the Dreispitz scenario runs Basel's full
sourced volumes on five one-server houses and a pool with 0.016 % late jobs.

**The pool loses money?** Yes, with 20 servers: about −CHF 24,900 a year after hardware. Its 20 servers
are a scenario value; the houses with one server each keep about +CHF 1,800 each. Sizing the pool is
the next step, not done.

**The demo's hot water is not the case's flat 12 kWh/day.** Correct, and we say so: the live demo
uses the EU standard tapping profile L (Regulation 814/2013), scaled to the same 12 kWh/day. A flat
profile never has an hour without hot-water demand. All scored figures use the case's flat profile.
