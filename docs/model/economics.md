# Economics per actor (T-14)

**Status: model implemented and tested; all results are variants, not scored figures.** Decision
[0028](../decisions/0028-economics-per-actor.md) (reviewed: go with changes, applied), aligned on
2026-11-21 with the job model of [0020](../decisions/0020-business-model-operator-two-job-types.md)
and the hardware rule of [0027](../decisions/0027-datacenter-licensed-gpus-only.md). The scored
reference-house figures (case break-even CHF 0.226 per server-hour etc.) are unchanged and still
come from `YearSimulation` with `config/reference-house.json`.

- Code: `src/DigitalHeat.Core/Economics.cs` (pure functions), bundle writer
  `src/DigitalHeat.YearRun/EconomicsBundle.cs`, tests `src/DigitalHeat.Core.Tests/EconomicsTests.cs`.
- Parameters: [`config/economics/reference-house.json`](../../config/economics/reference-house.json)
  (every value `{value, unit, source}` or `TODO(source)`), hardware from
  [`config/hardware/*.json`](../../config/hardware/) (T-15; T-35 refurbished profiles plug in by file).
- Result: [`data/derived/economics-reference-house/`](../../data/derived/economics-reference-house/),
  reproduced by

      dotnet run --project src/DigitalHeat.YearRun -- . --economics config/economics/reference-house.json --output results/economics

Business frame (decision 0020, operator model): DigitalHeat owns and runs GPU nodes in homes.
**Type 1** = batch LLM service on non-personal documents (Apertus 8B class), priced **per token**,
served first. **Type 2** = research simulations as the filler, priced **per server-hour** at the
pinned CHF 0.66. **Hardware (decision 0027):** datacenter-licensed GPUs only; **L4 is the default
profile**, refurbished L4 and T4 are variants, consumer GeForce (RTX 4090) is **not permitted
(licence)** and excluded. Levers: **A** size for base load (fewer servers), **B** Type 1 share,
**D** cheaper (refurbished) hardware, **G** route work away instead of dumping heat.

## 1. Method

### 1.1 Physics: the unchanged year simulation with a demand cap

Paid work per server and hour is limited to `a = p + φ·(1 − p)` (`p` = Type 1 share of server
capacity, `φ` = Type 2 fill rate of the rest). A `DemandLimitedExecutor` returns `min(target, a)` to
the unchanged `GreedyHeatScheduler` and `YearSimulation.Run`, so heat used, tank, backup and dump come
from the same hourly balance as the case figures. Unsold hours are not run (server off; GPU idle draw
`TODO(source)`). Per hour, Type 1 is served first: Type 1 load = `min(actual, p)`, Type 2 = the rest.
Type 1 capacity the heat-led schedule leaves unused is reported as `unserved_type1_server_hours`
(those buyers must be served elsewhere in that hour, or wait within their 24 h deadline).

- **Type 2 fill rate:** base `φ = 1` = "unlimited jobs" (case-permitted, decision 0020 must-have 1
  table); sensitivity `φ = 0.35` (sourced, range 0.2–0.6, `filler-demand.md` §6). Both are in every
  lever table.
- **`p` is a share of capacity, not of sold hours.** With heat-led running the load is often below
  `p`, so Type 1 makes up more than `p` of the sold hours (e.g. 4 L4 servers, `p` = 0.5: 55 % of sold
  hours).

Policies (lever G): **heat-led** = run only for heat (minimum load 0, as in the case scenario);
**always-on** = minimum load 1, i.e. run whenever work is sold and dump surplus heat.

### 1.2 Type 1 income per server-hour (price per token → CHF per server-hour)

`p_1 = tokens per server-hour × price per token` (`Economics.Type1PriceChfPerServerHour`).

| Profile | M tokens per server-hour | How | Source |
|---|---:|---|---|
| **L4** (and L4 refurbished) | **14.36** | L4 boards per 1 kW server 11.6313 × 0.072 kW = 0.8375 kW; × 3.6 MJ ÷ 0.21 J per token | **proxy:** batched vLLM 8B on RTX 4090, conservative reading (450 W ÷ 2,148 tok/s, `pe-rtx4090-vllm-iog-qps8`, `profitability-inputs.md` §3); same Ada Lovelace generation; `TODO(source)` measured L4 |
| T4 refurbished | 5.74 | L4 value × 0.4 (L4 ≈ 2.5 × T4 work per kWh at the same 70–72 W) | `refurbished-hardware.md` §3, not measured |
| CPU server (comparison only) | 3.94 | 1 kWh ÷ 0.254 kWh per 1M tokens at the measured 91 % input mix | `docs/jobs/llm-summary.md` (T-33, own measurement, CPU package) |

Token prices (lever axis): **list CHF 0.126 per 1M tokens** (Mistral Ministral 3 8B, USD 0.15 ×
0.8402, `ai-llm-mistral-ministral3-8b`) as the base, and the **batch-API level CHF 0.063** (−50 %)
that decision 0020 expects without fixed contracts. On L4: **CHF 1.81** and **CHF 0.90** per
server-hour.

Throughput readings not in the table, by linearity (`p_1` scales with tokens): batch 1 (0.237 J per
token) gives 12.7 M tokens and CHF 1.60 at list; the optimistic batched reading of T-37 (only output
tokens at 0.21 J, input tokens 0.0305 J) gives about 65 M tokens and ≈ CHF 8.2 at list, ≈ 4.1 at
batch-API level. Until an L4 is measured, read every Type 1 result below as a **proxy whose
direction of error is unknown** (the L4 has a much smaller memory system than the RTX 4090, and both
J/token figures divide board power ratings, not measured power). Sanity bound: AWS Zurich spot rents
the same L4 capacity for CHF 2.23 per server-hour. Measurement plan: one hour on Google Cloud
g2-standard-4 in Zurich (CHF 0.82/h) with `vllm bench serve` + `nvidia-smi` (decision 0027).

### 1.3 Money: system surplus first, split second

Per site and year, with `S_1`, `S_2` Type 1 and Type 2 server-hours, `N` servers:

| Term | Formula | Inputs (source) |
|---|---|---|
| Type 1 income | `S_1 × p_1` | §1.2 (L4: 1.81 at list, 0.90 at batch-API level) |
| Type 2 income | `S_2 × p_2` | `p_2` = 0.66 CHF/server-h pinned (decisions 0018/0020, `computing-price.md` §5), all profiles |
| Heat value | `Q_used × c_heat` | heat pump: electricity ÷ 2.5 = 0.133 CHF/kWh (case); gas 0.1398 ÷ 0.9 = 0.155; oil 1.5394 ÷ (10.08 × 0.9) = 0.170 (BAZG Sept 2026, KTBL) |
| Electricity | `E_kWh × p_el` | 0.3325 CHF/kWh (ElCom 2026, IWB H4) |
| Depreciation | `N × purchase ÷ lifetime` | hardware profile (L4: 34,244 CHF, 6 years) |
| Wear / replacement | `N × purchase × w` | `w` = 0, `TODO(source)` (T-35 wear model not yet wired in) |
| Operations | `N × ops` | hardware profile (L4: 708 CHF/server-year) |
| Installation | `I_site ÷ lifetime` | 0, `TODO(source)`; the bundle reports the affordable budget instead |

`surplus = Type 1 + Type 2 income + heat value − electricity − depreciation − wear − operations − installation`

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
- **Required Type 1 share:** smallest `p` (step 0.01) with surplus > 0 / operator win-win /
  host-owned win-win.
- **Break-even hardware price factor (lever D):** purchase price at which surplus = 0 without Type 1,
  as a share of the profile's price: `(income + heat − electricity − ops − installation) ÷ (N (1/L + w)) ÷ purchase`.
  Negative = even free hardware does not pay.

### 1.4 Variants from `creative-ideas.md` (labelled, off in the base case)

- `variant-co2-credit`: `Q_used × CO₂ factor ÷ boiler efficiency × CHF 100/t` (KliK, low; BAFU oil
  73.7 t/TJ, gas 56.5 t/TJ (2014 value)); zero for the heat-pump alternative.
- `variant-own-pv`: 750 PV-surplus hours per server-year (`TODO(source)`, [A]) bought at the forgone
  feed-in CHF 0.06/kWh instead of 0.3325; expected sold share `a` of those hours.

Both shift the required Type 1 share by at most 0.03; they are not levers that decide the case.

## 2. Worked example (check by hand)

L4 profile, **1 server** (lever A), heat-led, Type 1 share **p = 0.5** (lever B) at the list price,
Type 2 unlimited (φ = 1), heat pump replaced, electricity 0.3325 CHF/kWh. Row in `levers.csv`.

1. Cap `a = 0.5 + 1 × 0.5 = 1`; the heat-led schedule gives `S = 7,631.25` server-hours. The house
   needs at least 0.5 server in every hour (hot water alone needs 0.625 on average), so Type 1 gets
   `0.5 × 8,760 = 4,380` server-hours (none unserved), Type 2 the remaining `3,251.25`.
2. Type 1 price `14.3563 × 0.126 = 1.8089` CHF/server-h. Income `4,380 × 1.8089 + 3,251.25 × 0.66 =
   7,922.96 + 2,145.83 = 10,068.78` CHF.
3. Heat used `7,631.25 × 0.8 = 6,105` kWh × 0.133 = **811.97** CHF (19.5 % coverage).
4. Electricity `7,631.25 × 0.3325 = 2,537.39`; depreciation `34,244.06 ÷ 6 = 5,707.34`; operations 708.
5. Surplus `10,068.78 + 811.97 − 2,537.39 − 5,707.34 − 708 = ` **1,928.01 CHF/year**.
6. Operator model: host **+811.97** (free heat), DigitalHeat **+1,116.05** → win-win.
7. Case break-even `(2,537.39 − 811.97) ÷ 7,631.25 = 0.2261`; full-cost break-even
   `(2,537.39 + 5,707.34 + 708 − 811.97) ÷ 7,631.25 = 1.067` CHF/server-h (income here: 1.32).

Cross-checks: test `UncappedRunReproducesTheScoredCaseFigures` (φ = 1, `p` = 0 reproduces the
scored run: 22,661 server-hours, case break-even 0.2261 for 4 servers). By hand, 4 L4 servers with
every heat-led hour sold as Type 1 at list: `22,661.03 × (1.8089 − 0.2261) − 4 × (5,707.34 + 708) =
35,868 − 25,661 = ` **+10,206** CHF/year, the `p = 1` row of `levers.csv`.

## 3. Result for the reference house (honest)

Base inputs: L4, Type 2 at CHF 0.66, wear and installation 0, heat pump replaced, 2026 Basel
electricity. All Type 1 numbers use the conservative L4 throughput proxy (§1.2).

**Without Type 1 nothing pays** (`levers.csv`, `p = 0`, heat-led):

| Profile | 1 server, φ = 1: surplus / full-cost BE | 4 servers, φ = 1 | 4 servers, φ = 0.35 |
|---|---:|---:|---:|
| CPU server | −9,502 CHF / 1.91 | −41,419 / 2.49 | −47,027 / 5.49 |
| **L4** | **−3,104 / 1.07** | −15,829 / 1.36 | −21,437 / 2.86 |
| L4 refurbished | −3,332 / 1.10 | −16,740 / 1.40 | −22,348 / 2.96 |
| T4 refurbished | −598 / 0.74 | −5,804 / 0.92 | −11,412 / 1.83 |
| RTX 4090 | not permitted (licence, decision 0027) | | |

**Required Type 1 share** (`required-type1.csv`, L4, heat pump, base variant):

| Servers (A) | Policy (G) | Type 1 price | φ = 1: surplus > 0 / **operator win-win** / host-owned | φ = 0.35: operator win-win | HW factor at p = 0, φ = 1 (D) |
|---|---|---|---:|---:|---:|
| **1** | heat-led | list (1.81/h) | 0.31 / **0.39** / 0.37 | 0.46 | 0.46 |
| 2 | heat-led | list | 0.40 / 0.51 / 0.49 | 0.58 | 0.36 |
| **4** (draft scenario) | heat-led | list | 0.57 / **0.67** / 0.66 | 0.72 | 0.31 |
| 4 | always-on (dump) | list | 0.30 / 0.36 / 0.37 | 0.46 | 0.48 |
| any | any | batch-API (0.90/h) | never | never | — |

How to read it: in the reference house with **4 L4 servers (draft scenario), DigitalHeat and the
host both gain if at least 67 % of the server capacity is sold as Type 1 batch LLM at the list token
price**, with Type 2 research simulations at CHF 0.66 filling the rest. With **one server (sized for
base load), 39 %** is enough. **At the batch-API price (−50 %) no Type 1 share pays** on any
profile, because CHF 0.90 per server-hour is below the L4 full-cost break-even (1.07–1.36 at φ = 1).
The host gains in every row (free heat: CHF 812 per year with 1 server, CHF 2,411 with 4).

Lever by lever:

- **A (size for base load) helps:** 1 server needs 39 %, 4 servers 67 %. The price: heat coverage
  drops from 58 % to 19.5 %, so the house keeps its full heater.
- **B (Type 1 share) is the decisive lever.** Every positive case needs Type 1 demand near list
  price, which is `TODO(source)` (decision 0020: unlimited jobs, no sourced volume).
- **D (refurbished):** new L4 would have to cost ≤ 31–46 % of its price to pay on Type 2 alone.
  `gpu-inference-l4-refurbished` has a *higher* yearly depreciation than new L4 (CHF 5,935 vs 5,707,
  3 years remaining card life), so it needs slightly more Type 1 (0.42 vs 0.39 at 1 server).
  `gpu-inference-t4-refurbished` is the cheapest heater (full-cost break-even 0.74 at 1 server) but
  earns only 0.72 CHF per Type 1 server-hour at list (0.4 × L4 work), so the operator never wins in
  the heat-led cases.
- **G (route instead of dump) costs this house money:** always-on with dumping needs 36 % instead of
  67 % at 4 servers, because Type 1 at list pays far more than electricity. Routing is right for the
  heat story and the case house has no dump path; its value appears only at the site that receives
  the routed work (network model, T-10/T-12, decision 0024), not in this table.
- **Type 2 fill rate:** at φ = 0.35 the required Type 1 share rises by 5–10 points. In the win-win
  grid (`win-win.csv`, electricity 0.35 CHF/kWh) Type 2 alone pays one L4 server from CHF 1.2 per
  server-hour at φ = 1 (1.6 at φ = 0.6, 2.6 at φ = 0.35), i.e. above the pinned 0.66.
- **Heat alternative:** replacing oil or gas instead of a heat pump raises the host's gain but does
  not change the required Type 1 share (≤ 0.01).

**Card price sensitivity (decision 0027 review):** the L4 profile uses CHF 2,235 per card (low
confidence). At the verified CHF 3,089 a server costs CHF 44,179, the full-cost break-even of the
draft scenario rises from 1.358 to 1.651 and Type 1 at list (1.81) clears it by only about 10 %, so
all required Type 1 shares above rise (4 servers, operator win-win: Type 1 must take about 95 % of the sold hours, by hand as in `business-model.md`). The profile default is a team
decision (T-15/T-35 own the file).

**Does not pay off:** any profile at batch-API token prices with the conservative L4 throughput;
Type 2 alone at the pinned CHF 0.66; CPU homes under any sourced price; every result ignores
installation (affordable budget in `affordable_installation_chf_per_site`), wear and falling prices
(token and compute prices fall about 25–29 % a year, `profitability-inputs.md` §5, so the year-1
shares above are a lower bound over a 6-year life). The optimistic batched throughput (≈ 4.5× the
proxy) would make batch-API prices pay as well; it is the decisive measurement.

## 4. Files and how to read the win-win chart

All files follow the T-13 conventions (snake_case, UTF-8, LF, dot decimals, round-trip precision,
empty field = undefined). `run.json` carries git commit, cleanliness, input SHA-256, the profiles
without a price set and the **not-permitted profiles with their reason**.

- `levers.csv`: one row per hardware profile × server count × policy × heat alternative × Type 1
  token price × Type 2 fill rate × Type 1 share, with every money term, both splits and break-evens.
- `required-type1.csv`: required Type 1 shares and the hardware price factor, for the base and
  the labelled variants (replaces `required-premium.csv`).
- `win-win.csv`: grid for the chart (L4, heat-led, heat pump, Type 1 at list): Type 2 price 0–3 CHF
  (step 0.2) × electricity 0.10–0.50 CHF/kWh (step 0.05), for 1 and 4 servers, Type 1 share 0 and
  0.5, fill rates 0.2 / 0.35 / 0.6 / 1. Columns `operator_win_win` / `host_owned_win_win` are true
  where both actors are > 0.
- `parameters.json`: the full parameter snapshot.

**Chart:** x = Type 2 price, y = electricity price, one panel per (servers, Type 1 share, fill rate);
colour cells where `operator_win_win` is true. Its left edge is the Type 2 price DigitalHeat needs.
Mark the pinned 0.66 and today's Basel electricity (0.3325). Reading: with no Type 1 the zone starts
at CHF 1.2–3.0 (above the pinned 0.66); with 50 % Type 1 and one server it starts at CHF 0.4.

## 5. Open `TODO(source)`

Measured L4 LLM throughput and power (every Type 1 number) · Type 1 demand per house · wear and
replacement (T-35 wear model not wired in) · installation bill of materials · GPU idle power · gas
boiler efficiency and current gas CO₂ factor · PV-surplus hours (variant) · CO₂-credit eligibility
of single houses · confirmation of the L4 card price (34,244 vs 44,179 CHF/kW; the higher price
raises all required shares) · L4 data-centre driver licence terms (decision 0027).
