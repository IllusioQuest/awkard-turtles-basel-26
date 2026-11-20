# Key figures and checklist (derived from the case)

> Quick reference for agents and the team, **derived** from [`challenge.md`](challenge.md).
> If anything here disagrees with `challenge.md`, `challenge.md` wins. Fix this file then.

## Reference house parameters (fixed for scored results)

| Parameter | Value | Note |
|---|---|---|
| Location | Basel, 47.56 N, 7.59 E | |
| Period | 2025-01-01 to 2025-12-31, hourly, Europe/Zurich | 8,760 hours |
| Heating demand per hour | `max(0, 10 kW × (20 − T) ÷ 25.6)` on heating days only | T = `temperature_2m` in °C |
| Heating day | local calendar day (00:00–23:00) with mean of the 24 hourly T below 12 °C | |
| Constant 25.6 | fixed | 20 − (−5.6 °C coldest hour of 2025) |
| Expected space heating | ≈ 27,000 kWh/year on 206 heating days | sanity check |
| Hot water | 12 kWh/day, all year | household of four, ≈ 50 l/person/day |
| Heat storage | 1 tank, ≈ 35 kWh usable, no losses | 1,000 l, 30 K spread |
| Electricity price | ElCom 2026, IWB Basel, H4, total CHF/kWh incl. fees, one flat price | value still to look up |
| Value of used heat | electricity price ÷ 2.5 | replaces a heat pump with efficiency 2.5 |

## Server assumptions (DigitalHeat)

| Parameter | Value |
|---|---|
| Electrical power per rack server | 1 kW |
| Heat produced | ≈ 1 kW |
| Recoverable into water system | 80 % → 0.8 kW |
| Recoverable heat per server-day (continuous) | 19.2 kWh (= 0.8 kW × 24 h) |
| Heat pump needed | no (case simplification) |

## The central tension

- Coldest hour: 10 kW ÷ 0.8 kW ≈ **12.5 servers** per house.
- Summer hot water only: 12 kWh ÷ 19.2 kWh ≈ **0.6 servers** per house.
- Servers per house and what they do April–September is **our decision**.

## Must-haves checklist (all on the reference house)

1. **Workloads**
   - [ ] ≥ 2 job types (e.g. embeddings, reranking, image recognition, transcription)
   - [ ] runtime and energy per job (own measurement or open benchmark), with source
   - [ ] interruptible yes/no and deadline per job type
   - [ ] ≥ 1 job type actually runs on our machine, with measured runtime
   - [ ] energy from a software meter or runtime × device power, plus how we scale to the 1 kW server
   - [ ] jobs arriving per day and where that number comes from (or stating "unlimited jobs")
2. **Scheduling**
   - [ ] distribute / pause / reschedule jobs by heat demand (winter heating, summer hot water)
   - [ ] simple heat storage
   - [ ] defined behaviour for a job whose heat is not needed (pause, move, dump heat, ...)
   - [ ] the **same scheduler code** that produces the yearly balance also controls ≥ 1 real running
         job: pause/resume, or stop at checkpoint and restart, on the heat signal
3. **Yearly balance per house**
   - [ ] servers per house
   - [ ] share of total heat demand (space heating + hot water) covered by server heat (rest = backup heater)
   - [ ] heat dumped
   - [ ] utilisation = server running hours ÷ (servers × 8,760)
   - [ ] missed deadlines in % of jobs
   - [ ] electricity cost and computing income
   - [ ] break-even computing price, CHF per server-hour =
         (electricity cost − value of heat actually used) ÷ server-hours sold
   - [ ] every assumption visible
   - [ ] clear statement of when the model does **not** pay off

## Pitch (5 min + 3 min Q&A)

1. Open with key figures: servers per house, share of demand covered, heat dumped, utilisation,
   missed deadlines, break-even computing price.
2. Live demo: one January day, one July day, one interrupted job on a real running process that the
   scheduler pauses and resumes on the heat signal.

Not valid on its own: spreadsheet, slides or simulation without a real job under scheduler control.

## Jury weights

Transparency & reproducibility 25 % · Feasibility 25 % · Scheduling quality & impact 20 % ·
Creativity & innovation 20 % · Design & usability 10 %.

Scheduling quality ranking order: (1) share of heat demand covered at the least heat dumped,
(2) utilisation and missed deadlines.

## Scope

- Stretch goal (only after all must-haves work, not scored separately): "Prepper Challenge", solar +
  computing + thermal storage for self-sufficiency.
- Out of scope: feeding heat into the district heating grid (may be mentioned as outlook).
