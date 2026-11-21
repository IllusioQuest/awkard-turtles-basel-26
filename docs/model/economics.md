# Economics per actor (T-14)

**Status: model implemented and tested; all results are variants, not scored figures.** Decision
[0026](../decisions/0026-economics-per-actor.md) (reviewed: go with changes, applied). The scored
reference-house figures (case break-even CHF 0.226 per server-hour etc.) are unchanged and still come
from `YearSimulation` with `config/reference-house.json`.

- Code: `src/DigitalHeat.Core/Economics.cs` (pure functions), bundle writer
  `src/DigitalHeat.YearRun/EconomicsBundle.cs`, tests `src/DigitalHeat.Core.Tests/EconomicsTests.cs`.
- Parameters: [`config/economics/reference-house.json`](../../config/economics/reference-house.json)
  (every value `{value, unit, source}` or `TODO(source)`), hardware from
  [`config/hardware/*.json`](../../config/hardware/) (T-15; T-35 refurbished profiles plug in by file).
- Result: [`data/derived/economics-reference-house/`](../../data/derived/economics-reference-house/),
  reproduced by

      dotnet run --project src/DigitalHeat.YearRun -- . --economics config/economics/reference-house.json --output results/economics

Business frame (T-34, decision 0020): DigitalHeat runs a Swiss GPU cloud in homes. **Premium**
(Type 1) = compute-heavy batch AI on non-personal data, served first, priced at the Swiss on-demand
price. **Filler** (Type 2) = interruptible spot capacity, sold in a share φ (fill rate) of the
remaining capacity. Levers: **A** size for base load (fewer servers), **B** premium share,
**D** cheaper (refurbished) hardware, **G** route work away instead of dumping heat.

## 1. Method

### 1.1 Physics: the unchanged year simulation with a demand cap

Paid work per server and hour is limited to `a = p + φ·(1 − p)` (`p` = premium share of server
capacity, `φ` = fill rate). A `DemandLimitedExecutor` returns `min(target, a)` to the unchanged
`GreedyHeatScheduler` and `YearSimulation.Run`, so heat used, tank, backup and dump come from the same
hourly balance as the case figures. Unsold hours are not run (server off; GPU idle draw
`TODO(source)`). Per hour, premium is served first: premium load = `min(actual, p)`, filler = the rest.
Premium capacity the heat-led schedule leaves unused is reported as `unserved_premium_server_hours`
(those buyers must be served elsewhere in that hour).

Policies (lever G): **heat-led** = run only for heat (minimum load 0, as in the case scenario);
**always-on** = minimum load 1, i.e. run whenever work is sold and dump surplus heat.

### 1.2 Money: system surplus first, split second

Per site and year, with `S_P`, `S_F` premium and filler server-hours, `N` servers:

| Term | Formula | Inputs (source) |
|---|---|---|
| Premium income | `S_P × p_P` | `p_P` = 3.55 CHF/server-h for L4 (Infomaniak L4 on-demand 0.3053 CHF/h × 11.63 cards, `cp-ch-infomaniak-l4`) |
| Filler income | `S_F × p_F` | `p_F` = 1.42 (L4), 0.66 (CPU), 0.57 (RTX 4090) (`filler-demand.md` §6, `hardware.md` §2) |
| Heat value | `Q_used × c_heat` | heat pump: electricity ÷ 2.5 = 0.133 CHF/kWh (case); gas 0.1398 ÷ 0.9 = 0.155; oil 1.5394 ÷ (10.08 × 0.9) = 0.170 (BAZG Sept 2026, KTBL) |
| Electricity | `E_kWh × p_el` | 0.3325 CHF/kWh (ElCom 2026, IWB H4) |
| Depreciation | `N × purchase ÷ lifetime` | hardware profile (L4: 34,244 CHF, 6 years) |
| Wear / replacement | `N × purchase × w` | `w` = 0, `TODO(source)` until T-35 |
| Operations | `N × ops` | hardware profile (L4: 708 CHF/server-year) |
| Installation | `I_site ÷ lifetime` | 0, `TODO(source)`; the bundle reports the affordable budget instead |

`surplus = premium + filler + heat value − electricity − depreciation − wear − operations − installation`

Split (both sum to the surplus, tested):

- **Operator model** (main case, decision 0020): DigitalHeat owns hardware and installation, pays
  electricity and operations, keeps the income. Host gets the heat for a fee `h` per kWh
  (default 0, free heat, market practice in `competitors.md` lesson 2).
  `host = Q_used × (c_heat − h)`, `DigitalHeat = surplus − host`.
  With `h = 0` the host is positive by construction, so **win-win = DigitalHeat > 0**; a fee only moves
  money from host to DigitalHeat (at most `c_heat` per kWh before the host loses).
- **Host-owned model** (variant, no market precedent): host pays hardware, wear, installation and
  electricity and keeps `(1 − c)` of the income; DigitalHeat keeps commission `c` = 0.15 (vast.ai,
  low) and pays operations. `DigitalHeat = c × income − operations`, `host = surplus − DigitalHeat`.

Break-evens, side by side:

- **Case break-even (scored formula):** `(electricity − Q_used × p_el ÷ 2.5) ÷ S`, electricity only.
- **Full-cost break-even:** `(electricity + depreciation + wear + operations + installation − heat value) ÷ S`,
  the average income per sold server-hour needed for surplus = 0.
- **Required premium share:** smallest `p` (step 0.01) with surplus > 0 / operator win-win /
  host-owned win-win.
- **Break-even hardware price factor (lever D):** purchase price at which surplus = 0, as a share of
  the profile's price: `(income + heat − electricity − ops − installation) ÷ (N (1/L + w)) ÷ purchase`.
  Negative = even free hardware does not pay.

### 1.3 Variants from `creative-ideas.md` (labelled, off in the base case)

- `variant-co2-credit`: `Q_used × CO₂ factor ÷ boiler efficiency × CHF 100/t` (KliK, low; BAFU oil
  73.7 t/TJ, gas 56.5 t/TJ (2014 value)); zero for the heat-pump alternative.
- `variant-own-pv`: 750 PV-surplus hours per server-year (`TODO(source)`, [A]) bought at the forgone
  feed-in CHF 0.06/kWh instead of 0.3325; expected sold share `a` of those hours.

Both shift the required premium share by at most 0.01; they are not levers that decide the case.

## 2. Worked example (check by hand)

L4 profile, **1 server** (lever A), heat-led, premium share **p = 0.25** (lever B), fill rate 0.35,
heat pump replaced, electricity 0.3325 CHF/kWh. Row in `levers.csv`.

1. Cap `a = 0.25 + 0.35 × 0.75 = 0.5125`. The house needs at least this much heat in every hour (hot water alone needs 0.625 server on
   average), so the cap binds all year and
   `S = 0.5125 × 8,760 = 4,489.5` server-hours; premium `0.25 × 8,760 = 2,190`, filler `2,299.5`.
2. Income `2,190 × 3.55 + 2,299.5 × 1.42 = 7,774.50 + 3,265.29 = 11,039.79` CHF.
3. Heat used `4,489.5 × 0.8 = 3,591.6` kWh (nothing dumped) × 0.133 = **477.68** CHF (11.5 % coverage).
4. Electricity `4,489.5 × 0.3325 = 1,492.76`; depreciation `34,244.06 ÷ 6 = 5,707.34`; operations 708.
5. Surplus `11,039.79 + 477.68 − 1,492.76 − 5,707.34 − 708 = ` **3,609.37 CHF/year**.
6. Operator model: host **+477.68** (free heat), DigitalHeat **+3,131.69** → win-win.
   Host-owned: DigitalHeat `0.15 × 11,039.79 − 708 = 947.97`, host 2,661.40 → win-win.
7. Case break-even `(1,492.76 − 477.68) ÷ 4,489.5 = 0.2261`; full-cost break-even
   `(1,492.76 + 5,707.34 + 708 − 477.68) ÷ 4,489.5 = 1.655` CHF/server-h (income here: 2.46).

Cross-check (test `UncappedRunReproducesTheScoredCaseFigures`): with φ = 1 (unlimited jobs) the model
reproduces the scored run exactly: 22,661 server-hours and case break-even 0.2261 for 4 servers.

## 3. Result for the reference house (honest)

Base inputs: fill rate 0.35, wear and installation 0, heat pump replaced, 2026 Basel electricity.

**Without premium work nothing pays** (`levers.csv`, `p = 0`, heat-led):

| Profile | 1 server: surplus / full-cost BE | 4 servers: surplus / full-cost BE |
|---|---:|---:|
| CPU server | −11,483 CHF / 4.41 | −47,027 / 5.49 |
| RTX 4090 | −3,078 / 1.57 | −13,182 / 1.92 |
| **L4** | **−2,755 / 2.32** | −14,037 / 2.86 |

Filler at φ = 0.35 sells only 35 % of the hours, so hardware is spread over few sold hours. CPU and
RTX 4090 have no premium product (no price, data rules), and their break-even hardware factor is
negative: even free hardware does not cover operations and electricity at their filler prices.

**L4 with premium work (lever B), required premium share** (`required-premium.csv`, heat pump, base):

| Servers (lever A) | Policy (lever G) | Surplus > 0 | Operator win-win | Host-owned win-win | Hardware factor at p = 0 (lever D) |
|---|---|---:|---:|---:|---:|
| **1** | heat-led | 0.11 | **0.13** | 0.13 | **0.52** |
| 2 | heat-led | 0.13 | 0.15 | 0.15 | 0.50 |
| 4 (draft scenario) | heat-led | 0.16 | 0.18 | 0.19 | 0.39 |
| 4 | always-on (dump) | 0.12 | 0.13 | 0.14 | 0.51 |

How to read it: with one L4 server in the reference house, **DigitalHeat and the host both win if
at least 13 % of the server's capacity is sold as premium sovereign AI at the Swiss on-demand price**
(≈ 1,140 server-hours ≈ 13,000 L4 GPU-hours, ≈ CHF 4,000 of premium revenue per house and year) and
the filler sells 35 % of the rest at CHF 1.42. Alternatively, **without premium, refurbished L4
hardware at ≤ 52 % of the new price** (and no extra wear) brings the system to zero; the host's free
heat (≈ CHF 330) then still has to come out of DigitalHeat's margin.

Lever by lever:

- **A (size for base load) helps:** 1 server needs 13 % premium, 4 servers 18 %. The price: the
  host's heat coverage drops to 8–12 % (CHF 330–480 per year), so the house keeps its full heater.
- **B (premium) is the decisive lever.** Every positive case needs premium demand, which is
  `TODO(source)`. Decision 0020 warns that non-personal batch AI competes with batch APIs at about
  half of list price; at such prices the required share rises well above these values.
- **D (refurbished) is necessary if premium stays small:** the break-even factor 0.52 says what a
  T-35 profile must reach, including its wear. Note: `profitability-inputs.md` reports a newer L4
  card price (profile 44,179 CHF/kW instead of 34,244), which would raise all required shares; the
  profile file is T-15/T-35's to update.
- **G (route instead of dump) costs this house money:** always-on with dumping needs less premium
  (0.13 vs 0.18 at 4 servers) because the L4 filler price is far above electricity. Routing is right
  for the heat story and the case house has no dump path, but its value appears only at the site
  that receives the routed work (network model, T-10/T-12, decision 0024), not in this table.
- **Heat alternative:** replacing oil or gas instead of a heat pump raises the host's gain by
  17–28 % but changes the required premium share by ≤ 0.01.
- **Fill rate:** at φ = 0.2 no grid cell without premium is win-win; at φ = 0.6, one L4 server pays
  on filler alone from a filler price of CHF 1.6 per server-hour (inside the sourced range 0.85–2.20).

**Does not pay off:** CPU and consumer-GPU homes under any sourced price; any L4 home without premium
demand or cheaper hardware; every result here ignores installation (affordable budget in
`affordable_installation_chf_per_site`: about CHF 21,700 per house over 6 years in the worked example,
negative where the surplus is negative) and wear.

## 4. Files and how to read the win-win chart

All files follow the T-13 conventions (snake_case, UTF-8, LF, dot decimals, round-trip precision,
empty field = undefined). `run.json` carries git commit, cleanliness and input SHA-256.

- `levers.csv`: one row per hardware profile × server count × policy × heat alternative × premium
  share, with every money term, both splits and break-evens.
- `required-premium.csv`: required premium shares and the hardware price factor, for the base and
  the labelled variants.
- `win-win.csv`: grid for the chart (L4, heat-led, heat pump): filler price 0–3 CHF (step 0.2) ×
  electricity 0.10–0.50 CHF/kWh (step 0.05), for 1 and 4 servers, premium 0 and 0.25, fill rates
  0.2 / 0.35 / 0.6. Columns `operator_win_win` / `host_owned_win_win` are true where both actors are
  > 0.
- `parameters.json`: the full parameter snapshot.

**Chart:** x = filler price, y = electricity price, one panel per (servers, premium share, fill rate);
colour cells where `operator_win_win` is true. The coloured region is the win-win zone; its left edge
is the filler price DigitalHeat needs. Mark the sourced filler range (0.85–2.20) and today's Basel
electricity (0.3325). Reading: with no premium the zone starts at CHF 2.2–3.0 (above the sourced range
for φ = 0.35); with 25 % premium it starts at CHF 0–0.8, i.e. the premium carries the house.

## 5. Open `TODO(source)`

Premium (Type 1) demand per house · wear/replacement (T-35) · installation bill of materials · GPU
idle power · gas boiler efficiency and current gas CO₂ factor · PV-surplus hours (variant) · CO₂-credit
eligibility of single houses · confirmation of the L4 card price (34,244 vs 44,179 CHF/kW).
