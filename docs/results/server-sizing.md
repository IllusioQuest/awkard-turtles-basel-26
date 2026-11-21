# Servers per household: how many pay off (T-70)

**Variant analysis, not a scored figure.** The scored reference house keeps its draft count and
the case's abstract 1 kW server ([reference-house.md](reference-house.md)). This page answers
"how many servers should a house get?" with one rule, computed by the shared C# year model.
Decision: [0062](../decisions/0062-marginal-value-server-sizing.md).

## Answer in one minute

- **Each extra server sells fewer hours.** In the reference house the 1st server runs
  7,631 h/year, servers 2–5 run about 4,600–5,200 h each, then it drops fast: 6th 3,796 h,
  8th 2,282 h, 10th 911 h, 13th 5 h. This comes from the heat demand alone (heat-first, no dumped heat).
- **A server is worth adding if its extra hours pay its fixed cost.** With the L4 GPU profile
  (CHF 6,415 per server-year) sold at AWS spot (CHF 2.23/h), the optimum is **6 servers**
  (CHF 23,771/year contribution vs. CHF 19,749 at today's 4). This is an **upper bound**: it assumes
  every heat-feasible hour is sold at that price.
- **At the adopted CHF 0.66/h, no profile has a positive optimum:** the best count is 0 for every
  hardware profile. Sizing cannot fix a price below full cost. The price has to rise first.

## Method

For every count n = 0, 1, 2, … the unchanged Core `YearSimulation` with the shared
`HeatFirstScheduler` (minimum load 0) runs the year on the house's hourly demand. It assumes
**unlimited pausable work**, so H(n) is the most server-hours the heat demand can absorb. It is an
**upper bound**: real sales are limited by job demand (see Limits). The sweep stops at the first n
that covers 100% of the demand, because more servers can then sell no extra hour without dumping heat.

Annual contribution, before installation, heating integration and the host/operator split:

```
C(n) = H(n) · price + used_heat_value(n) − electricity(n) − idle_electricity(n) − n · F
F    = purchase_price / lifetime + ops        (per server-year, config/hardware/<profile>.json)
```

`used_heat_value` and `electricity` are the year model's own figures (CHF 0.3325/kWh, heat at
0.3325 / 2.5 CHF/kWh). Idle draw is charged in **every unsold hour** where the profile has a sourced
value (no suspend or power-off, so conservative); unsourced idle draw counts as 0; idle heat is not
credited. With idle draw the break-even hours are `(F + idle_kw·elec·8760) / (margin + idle_kw·elec)`. The optimum n* is the argmax of C(n). This is the screening-curve rule of power
system capacity planning: a unit pays off if its hours on the load-duration curve exceed
`F / margin` (Stoft, *Power System Economics*, 2002, ch. 3). Here `margin = price − 0.3325 + 0.8 × 0.133
= price − 0.2261` CHF per server-hour. 0.2261 is the case break-even of reference-house.md, and
H(n) shows diminishing returns (`diminishing_returns: true`), so the rule and the argmax agree.

**Worked example (L4, spot).** F = 34,244.06 / 6 + 708 = CHF 6,415.34. Margin = 2.23 − 0.2261 =
CHF 2.0039/h. Break-even hours = 6,415.34 / 2.0039 = **3,201 h**. The 6th server adds 3,796 h
(≥ 3,201) and the 7th only 2,932 h (< 3,201), so n* = 6. C(6) = CHF 23,771.

## Assumptions behind the reference-house result (6 servers)

Everything reduces to one threshold, `h* = F / (p − e·(1 − r/COP))`. n* is the number of servers
whose added hours (table below) are at least h*. Money inputs move h*; physical inputs change the
added-hours curve. "If changed" holds all other inputs fixed. It is computed with the threshold
rule from the committed sweep, which equals the argmax because returns diminish.

| Assumption | Value | Source | If changed |
|---|---|---|---|
| House, heat formula, hot water | 10 kW, 20 °C / 25.6 °C, 12 kWh/day | case (`config/reference-house.json`) | fixed by the case |
| Weather | Basel 2025, hourly | `data/raw/open-meteo/SOURCE.md` | another year shifts the curve slightly |
| Tank | 35 kWh, lossless, starts empty | case | larger tank adds hours to servers 6+ (not run) |
| Server power, heat recovery | 1 kW, 80% | case | recovery also changes 0.68 = 1 − 0.8/2.5 |
| Heat value | electricity ÷ COP 2.5 | case | — |
| Electricity e | CHF 0.3325/kWh | ElCom IWB Basel H4 2026 | +20%: h* 3,275 → still 6 |
| Hardware | `gpu-inference-l4` (11.63 L4 cards + host per 1 kW) | `config/hardware/gpu-inference-l4.json` | other profiles: table further down |
| Purchase price | CHF 34,244.06 per server | Bechtle L4 CHF 2,235/card, Dell R570 host CHF 8,248.03 (`data/raw/market/`) | −10%: 7 servers; +10%: 6 |
| Lifetime | 6 years, straight line, no residual | CoreWeave accounting life (`costs.csv`) | 4 years: **4** servers; 8 years: **7** |
| Operations | CHF 708 per server-year | managed-server CHF 59/month (`costs.csv`) | 0: 7 servers |
| Idle draw | unsourced, counted as 0 | profile `TODO(source)` | 0.1 kW assumed: still 6 |
| Price p | CHF 2.23 per server-hour | AWS g6.xlarge spot eu-central-2 × 11.63 (`compute-prices.csv`) | 1.80: 5; 2.50: 7; 3.10: 8; **0.66 (adopted): 0** |
| Sold hours | all heat-feasible hours sold | modelling choice (unlimited pausable work) | real demand only lowers n* |
| Scheduler | heat-first, minimum load 0, no dumped heat | Core `HeatFirstScheduler` | — |
| Excluded | installation, heating integration, wear, host/operator split | not sourced | each raises F, so n* falls |

**Robustness.** With these inputs h* = 3,201 h sits in the 6-server band (2,932–3,796 h), so the 6
holds while h* stays within −8% to +19%. The softest inputs are the lifetime (an accounting life,
not measured hardware life) and the price (a spot price, assumed fully sold). Servers 2–5 all add
about 4,600–5,200 h, so when h* falls in that band, small input changes swing n* between 1 and 5.

## Results: reference house (Basel 2025, case house, 35 kWh tank)

| n | Sold server-hours | Hours added by the n-th server | Coverage | Utilisation |
|---:|---:|---:|---:|---:|
| 1 | 7,631 | 7,631 | 19.5% | 87.1% |
| 2 | 12,788 | 5,157 | 32.8% | 73.0% |
| 3 | 17,779 | 4,991 | 45.5% | 67.7% |
| 4 | 22,661 | 4,882 | 58.0% | 64.7% |
| 5 | 27,275 | 4,614 | 69.9% | 62.3% |
| 6 | 31,071 | 3,796 | 79.6% | 59.1% |
| 7 | 34,003 | 2,932 | 87.1% | 55.5% |
| 8 | 36,284 | 2,282 | 92.9% | 51.8% |
| 9 | 37,839 | 1,555 | 96.9% | 48.0% |
| 10 | 38,750 | 911 | 99.3% | 44.2% |
| 11 | 39,044 | 294 | ≈100% | 40.5% |
| 13 | 39,082 | 5 | 100% | 34.3% |

n = 1, 3, 4, 5 and 13 match the T-06 exports in `data/derived/reference-house/` (checked by a test).

**Price at which the n-th server breaks even** (`0.2261 + F / hours added`, CHF per server-hour;
only for profiles whose idle draw is 0 or unsourced):

| n | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `gpu-inference-l4` (F = 6,415) | 1.07 | 1.47 | 1.51 | 1.54 | 1.62 | 1.92 | 2.41 | 3.04 | 4.35 | 7.26 |
| `gpu-consumer-rtx4090` (F = 4,133) | 0.77 | 1.03 | 1.05 | 1.07 | 1.12 | 1.31 | 1.64 | 2.04 | 2.88 | 4.76 |

How to read it: at price p, install every server whose break-even is below p. Servers 2–5 have
almost the same break-even, so if the 2nd pays off, up to the 5th does too. From the 6th on, each
server needs a clearly higher price.

**Optimum per hardware profile and sourced price** (all prices assume every hour is sold):

| Profile | F, CHF/yr | Idle kW | Adopted 0.66 | Spot | Home-host | On-demand |
|---|---:|---|---|---|---|---|
| `gpu-inference-l4` | 6,415 | unsourced (0) | 0 | 6 (2.23) | 8 (4.30) | 10 (10.60) |
| `gpu-inference-l4-refurbished` | 6,643 | unsourced (0) | 0 | 6 (2.23) | 8 (4.30) | 10 (10.60) |
| `gpu-inference-t4-refurbished` | 3,909 | 0.464 | 0 | 8 (3.19) | 7 (2.04) | 10 (6.62) |
| `gpu-consumer-rtx4090` | 4,133 | unsourced (0) | 0 | – | 0 (0.57) | 6 (1.50) |
| `cpu-server` | 12,813 | 0.205 | 0 | 0 (1.80) | 0 (0.68) | 8 (7.10) |
| `cpu-server-refurbished` | 5,102 | 0.205 | 0 | 1 (1.20) | 0 (0.46) | 9 (4.72) |

Idle draw matters: without it the T4 profile would get 9 (spot) and 8 (home-host) servers.
"Unsourced (0)" makes those profiles look better than they are.

Contribution at the optimum, at 4 servers and per price is in
[`reference-house.json`](../../data/derived/server-sizing/reference-house.json) (`scenarios[].values`).
Refurbished profiles exclude the T-35 wear cost (`config/hardware/wear/`), so they look too good.

## Reproduce

`dotnet run --project src/DigitalHeat.YearRun -- . --sizing data/derived/server-sizing/reference-house.json`
(.NET 8). It writes the sweep and every profile × price scenario; repeated runs give identical SHA-256.
Code: `src/DigitalHeat.Core/ServerSizing.cs`; tests: `src/DigitalHeat.Core.Tests/ServerSizingTests.cs`.

## Limits (when this does not hold)

- **Sold hours are an upper bound.** Sizing assumes every heat-feasible hour is sold. The sourced
  Basel transcription demand alone fills only 38.79 server-hours a year on the same house
  ([reference-house.md](reference-house.md), finite-demand check), so **demand, not heat, is the
  binding limit today**. The 6 needs a buyer for about 31,000 server-hours a year.
- Sizing uses one full year. Fixed cost is pro-rated to the simulated window, so a winter-only
  window would recommend too many servers.
- The price is a planning input, assumed constant over the lifetime.
- Hardware cost excludes installation, heating integration and wear (refurbished). The host/operator
  split is not modelled; the host always prefers more servers, because their heat is free to them.
- One year of weather (2025). Tank size is fixed (case 35 kWh). A larger tank shifts heat and raises
  the hours of servers 6+, but is not a sourced variant yet.
- **Next step, not built:** many houses at once. Independent houses are the same rule per house, with
  one shared shadow price if the total is limited. Houses competing for shared jobs become a
  capacity-expansion LP (GLOP, representative days).
