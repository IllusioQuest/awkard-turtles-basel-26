# Year explorer: method and how to check it

> **Status: exploration prototype.** Built on 2026-11-20 to get a first feel for the numbers. It
> is **not** the project's model and not the scheduler that will control the real job (that will be
> the C# core). Its numbers are reproduced exactly by an independent Python script
> ([`verify.py`](verify.py)), but the team has not reviewed the approach yet.
>
> Page: `index.html` (published privately as "Basel Heat Year"). Data: `explorer-data.json`,
> built from `data/raw/` by `build_data.py`.

## 1. Inputs

| Input | Value | Source |
|---|---|---|
| Hourly outdoor temperature `T` | 8,760 values, Basel 2025 | `data/raw/open-meteo/basel-2025-hourly.csv` |
| Electricity price | 0.3325 CHF/kWh | `data/raw/elcom/iwb-basel-h4-2026.json` (ElCom 2026, IWB, H4, total) |
| Server electric power | 1 kW | case |
| Recovered heat share | 0.8 → 0.8 kW of heat per running server | case |
| Space-heating formula | `max(0, 10 kW × (20 − T) ÷ 25.6)` on heating days | case |
| Heating day | local day whose 24-hour mean of `T` is below 12 °C | case |
| Hot water | 12 kWh/day | case |
| Tank | 35 kWh usable, no losses (slider) | case |
| Value of heat | electricity price ÷ 2.5 = 0.133 CHF/kWh | case |
| Computing price | 0.30 CHF per server-hour (slider) | **example input, not sourced** |
| Hardware cost | 0 CHF per server and year (slider) | **not included by default** |

## 2. Calculation, step by step (one hour at a time)

All quantities are per hour, so kW and kWh are numerically the same.

**Step 1: demand.**
`space = max(0, 10 × (20 − T) ÷ 25.6)` if the day is a heating day, else 0.
`hot water = 12 ÷ 24 = 0.5 kWh` (the case gives only a daily total; spreading it evenly is our
simplification). `demand = space + hot water`.

**Step 2: how many servers run (the scheduling rule).**
The rule is "heat-led with a minimum load". Servers produce what the house needs this hour, plus
what fits into the tank. They never exceed the installed number and never drop below the minimum load.

```
wanted  = (demand + (tank capacity − tank level)) ÷ 0.8
running = max( min(servers, wanted), minimum load % × servers )
heat    = running × 0.8
```

`running` can be a fraction (e.g. 2.4). That means 2.4 server-hours in this hour: either some
servers run part of the hour or at part load. All server-hours are assumed to be sold (unlimited
jobs).

**Step 3: where the heat goes.**
- If `heat ≥ demand`: the house gets `demand`; the surplus fills the tank up to capacity; anything
  left is **dumped**.
- If `heat < demand`: the tank gives what it has; whatever is still missing comes from the
  **backup heater** (outside the model, as the case says).

The tank starts empty on 1 January.

**Step 4: yearly figures** (sums over 8,760 hours).

| Figure | Formula |
|---|---|
| Heat demand covered | (demand − backup) ÷ demand. Heat taken from the tank counts, because it came from the servers |
| Heat dumped | sum of dumped heat |
| Server-hours sold | sum of `running` |
| Utilisation | server-hours ÷ (servers × 8,760), as defined in the case |
| Electricity cost | server-hours × 1 kW × electricity price |
| Value of heat used | (demand − backup) × electricity price ÷ 2.5 |
| Computing income | server-hours × computing price |
| Break-even computing price (case formula) | (electricity cost − value of heat used) ÷ server-hours |
| System surplus | computing income + value of heat used − electricity cost − hardware cost |

**Why the break-even stays at 0.226 CHF/h as long as nothing is dumped:** every server-hour costs
1 kWh × 0.3325 = 0.3325 CHF of electricity and delivers 0.8 kWh of useful heat worth
0.8 × 0.133 = 0.1064 CHF, so 0.3325 − 0.1064 = **0.2261 CHF** must come from computing. Once heat
is dumped, part of the electricity buys no useful heat, and the break-even rises.

## 3. Worked example: 15 January 2025, 18:00

Output of `python3 prototypes/year-explorer/verify.py` (4 servers, minimum load 0 %, tank 35 kWh):

| Step | Value |
|---|---|
| T, daily mean | −0.6 °C, −1.40 °C, so it is a heating day |
| Space heating | 10 × (20 − (−0.6)) ÷ 25.6 = 8.0469 kWh |
| Hot water | 0.5 kWh, so demand = 8.5469 kWh |
| Servers wanted | (8.5469 + 35 − 0) ÷ 0.8 = 54.43, capped at 4 |
| Heat produced | 4 × 0.8 = 3.2 kWh |
| Tank | empty, gives 0 |
| Backup heater | 8.5469 − 3.2 = 5.3469 kWh |

## 4. Results with the page's default settings

4 servers, minimum load 0 %, tank 35 kWh, 0.3325 CHF/kWh, computing 0.30 CHF/h, hardware 0:

| Figure | Value |
|---|---|
| Heating days | 206 (case: 206) |
| Space heating | 26,851 kWh (case: about 27,000) |
| Hot water | 4,380 kWh |
| Total demand | 31,231 kWh |
| Server heat to the house | 18,129 kWh, so **58.0 % covered** |
| Backup heater | 13,102 kWh |
| Dumped | 0 kWh |
| Server-hours | 22,661 h, so **64.7 % utilisation** |
| Electricity | CHF 7,535 |
| Value of heat used | CHF 2,411 |
| Computing income (example price) | CHF 6,798 |
| Break-even (case formula) | **0.226 CHF per server-hour** |
| System surplus | CHF 1,675 (no hardware cost, example computing price) |
| Energy check | heat to house + dumped + tank at year end − produced = 0.000000 |

Reproduce: `python3 prototypes/year-explorer/verify.py`. Other settings:
`--servers 8 --min-load 50 --tank 35 --compute 0.30 --hw 0 --hour 2025-07-15T12:00`.

## 5. How to read the page

- **Key figures (top):** the case's yearly key figures for the current slider settings. The sentence
  below them says whether the chosen computing price is above the break-even.
- **The year, hour by hour:** 365 columns (days, January on the left), 24 rows (hours, midnight at
  the top). Darker means more. "Server load" = share of installed servers running (0–100 %).
  "Heat demand", "Backup heat" and "Dumped heat" are in kWh per hour, scaled to the year's maximum.
  The light block from May to September is summer: no space heating, only hot water. Click a day
  to see it below.
- **Day chart:** one bar per hour. Orange = server heat that reached the house (directly or via the
  tank), grey = backup heater, lilac = dumped. Orange + grey = the house's demand. The blue line is
  the tank level at the end of each hour (right axis).
- **Sweet spot:** the same calculation for 1 to 16 servers. Orange = share of demand covered, blue =
  utilisation, lilac = share of server heat dumped. The rings mark your choice.
- **System surplus:** the yearly surplus for 1 to 16 servers. Green = positive, red = negative.
  Careful: with hardware cost at 0 and unlimited jobs, more servers always look better. Set a
  hardware cost to see the real sweet spot.

## 6. Limits of this prototype

- No forecast: the rule only sees the current hour.
- Hot water spread evenly over the day; no Legionella constraint; no supply temperature.
- Unlimited jobs: every server-hour is sold, so missed deadlines are not modelled.
- Fractional servers (part load) are allowed.
- Computing price is an example; hardware, installation and operations costs are 0 by default.
  Sourced values now exist in `data/raw/market/` (WP1, WP2) but are not wired in.
- The tank starts empty; there are no tank losses (as in the case).
