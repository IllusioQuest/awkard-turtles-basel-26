# Routing as an optimisation problem: formal model (T-11)

> **Status: proposed** (draft by Claude subagent for the team lead, 2026-11-21; team review
> pending). Decision record: [0014](../decisions/0014-routing-optimisation-model.md).
> Builds on the first assessment [`routing-complexity.md`](routing-complexity.md).
> **Model only, no code.** Implementation belongs to
> [T-12](../tickets/T-12-routing-algorithms.md). Nothing here changes a scored reference-house
> figure: the case figures keep coming from the existing greedy scheduler (AGENTS.md §5.1);
> routing results are **variants**.

## 0. In plain words

Every hour, DigitalHeat decides for every site (house, hotel, pool ...) how many servers run, and
which work runs where. Work comes in two kinds:

- **premium jobs** (e.g. transcription for a law firm): paid well, have a deadline, may only run
  where the customer's data rules allow;
- **filler load**: interruptible capacity sold on a market at a lower price, no deadline.

Running a server costs electricity. Its heat is worth something only if a building needs it, now
or later through its tank; otherwise it is dumped. The model chooses loads that make the **system
surplus** (computing income + value of heat that replaces the heat pump / oil / gas − electricity −
penalties) as large as possible, without breaking any physical or contractual rule.

If work may be split into small pieces (audio segments, pages), this is a **linear programme
(LP)**: fast and exact. It becomes a **mixed-integer programme (MILP)** only when we add on/off
servers, indivisible jobs and similar yes/no rules. We solve it again every hour over the next
48 hours with the latest forecast (**rolling horizon**) and only carry out the first hour. The
existing greedy scheduler is the special case "one site, one hour ahead".

One honest consequence (§5.4): **at the adopted filler price of CHF 0.66 per server-hour, running
all servers all the time is economically optimal, even when heat is dumped**, because 0.66 is more
than the CHF 0.3325 of electricity per server-hour. Heat-led behaviour (the case's ranking) then
needs an explicit dump penalty or a no-dump rule. The team must choose (§11, question 1).

## 1. Sets and indices

| Symbol | Meaning | Where it comes from |
|---|---|---|
| `s ∈ S` | sites | T-10 scenario (`config/scenarios/*.json`, planned); today one site = reference house |
| `t ∈ T = {τ, …, τ+H−1}` | hours of the planning window that starts at the current hour `τ` | hourly steps, Europe/Zurich, 24 per day (`data/raw/open-meteo/SOURCE.md`, AGENTS.md §5.5) |
| `k ∈ K = K_prem ∪ {φ}` | job classes: premium classes (deadline, price, data policy) and the filler class `φ` | job catalog T-01, `config/jobs/*.json`; filler: `docs/research/computing-price.md` |
| `c ∈ C` | **cohorts**: premium work of one class with the same origin site, arrival hour, deadline and data policy (aggregated jobs, §6.3) | `JobScenario` arrivals (decision 0010) |
| `(i, j) ∈ L` | directed links between sites over which work may move | T-10 network graph (planned) |

## 2. Parameters

All values are passed in; none is fixed in the model (AGENTS.md §6). Units in names.

### 2.1 Per site `s`

| Symbol | Unit | Meaning | Source in the repo |
|---|---|---|---|
| `N_s` | servers | servers installed | `ServerCount` (`config/reference-house.json`) |
| `P_s` | kW per server | electric power at full load | `ServerElectricKw` (case: 1 kW) |
| `a_s` | kWh per server-hour | recoverable heat per server-hour = `ServerElectricKw × HeatRecoveryShare × HoursPerStep` | `HeatSignal.RecoverableKwhPerServerAtFullLoad` (case: 0.8) |
| `μ_s` | fraction | minimum load share while on | `MinimumLoadShare` |
| `B_s` | kWh | usable tank capacity | `TankCapacityKwh` (case: 35, no losses) |
| `S⁰_s` | kWh | tank content at the start of hour `τ` (realised state) | `HeatSignal.TankStoredKwh` |
| `λ_s` | fraction per hour | tank standing loss | case: 0 ("no losses"); variants `TODO(source)` |
| `D_{s,t}` | kWh | gross heat demand (space + hot water) | `t = τ`: `HeatSignal.DemandKwh`; `t > τ`: `HeatForecast.Hours` (decision 0008) |
| `e_{s,t}` | CHF/kWh | electricity price | `ElectricityPriceChfPerKwh` (ElCom 2026 IWB H4: 0.3325, flat) |
| `v_s` | CHF per kWh heat | value of one kWh of server heat used = cost of the heat it replaces (§2.4) | heating technology of the site |
| `ω_{s,t}` | server-hours | must-run load of always-on services placed at the site (e.g. webhosting) | T-01 representation open; default 0 |
| `country_s`, site id | – | trusted execution metadata | T-10 / T-24 (`JobPlacementContext`) |

### 2.2 Per cohort `c` (premium work)

| Symbol | Unit | Meaning | Source |
|---|---|---|---|
| `k(c)`, `o_c` | – | class and origin site (where the data is) | job arrivals |
| `α_c` | hour index | arrival hour (jobs arrive at local 00:00 in decision 0010) | `JobScenario` |
| `d_c` | hour index | **exclusive** deadline: work must run in hours `t < d_c` (= `α_c + DeadlineHours`) | `DeadlineHours`, decision 0010 |
| `W_c` | server-hours | work **still left at the start of hour `τ`** (at arrival: jobs × `WorkUnitsPerJob` ÷ `WorkUnitsPerServerHour`) | `JobScenario`, queue state |
| `p_c` | CHF per server-hour | income per server-hour of this class (work-unit price × throughput) | `docs/research/computing-price.md` §3 B; `TODO(source)` per class |
| `π_c` | CHF per server-hour missed | penalty for work not done by `d_c` (SLA) beyond lost income | **open, team question 2**; default 0 |
| `E_c ⊆ S` | – | eligible sites: `s ∈ E_c ⇔ JobPlacement.IsEligible(policy_c, context(o_c, s, country_s))` and latency `ℓ_{o_c,s} ≤ ℓ^max_{k(c)}` | decision 0013 / T-24; latency T-10 |
| `g_c` | servers | maximum parallelism (servers one cohort may use at once) | default `∞` (fully parallel work units) |

### 2.3 Filler, links and policy

| Symbol | Unit | Meaning | Source |
|---|---|---|---|
| `p^φ_{s,t}` | CHF per server-hour | filler (spot capacity) price | adopted 0.66 (`docs/research/computing-price.md`, team decision 2026-11-21; snapshot T-28); sensitivity 0.25–1.80 |
| `F_{s,t}` | server-hours | filler the market takes at site `s` in hour `t` | default `N_s` ("unlimited", must be stated as in must-have 1); **team question 3** |
| `δ_{ij}` | hours (integer ≥ 0) | transfer delay = ⌈(latency + data volume ÷ bandwidth) ÷ 1 h⌉ | T-10 links; `TODO(source)` |
| `κ_{ij}` | CHF per server-hour moved | transfer cost (network energy, egress) | default 0, `TODO(source)` |
| `ℓ_{ij}`, `ℓ^max_k` | ms | link latency and class latency limit (interactive services only) | T-10; T-01 |
| `π^dump` | CHF per kWh dumped | policy penalty for dumped heat (not a market price) | **team question 1** |
| `θ` | fraction | value of heat left in the tank at the window end, as a share of `v_s` | method parameter, default 0 (§6.2) |
| `H`, `R` | hours | planning horizon and commit interval | default `H = 48`, `R = 1` (`routing-complexity.md`) |

### 2.4 Value of heat per site (heating cost replaced)

`v_s` is what the site would otherwise pay for one kWh of useful heat. Server heat that covers
demand saves exactly that; backup heat `D − h` costs it.

| Heating at the site | `v_s` [CHF per kWh heat] | Inputs |
|---|---|---|
| heat pump (case, reference house) | `e_s ÷ COP_s` = 0.3325 ÷ 2.5 = **0.133** | `HeatPumpEfficiency` 2.5 (`docs/case/challenge.md`); variant COP 2.5–3.0 (`heat-ag-air-water-jaz-*` in `data/raw/market/costs.csv`) |
| gas boiler | `price_gas ÷ η_s` = 0.1398 ÷ η | `fuel-bazg-natural-gas-sep-2026` (0.1398 CHF per kWh fuel) in `data/raw/market/energy-prices.csv`; boiler efficiency `η`: `TODO(source)` |
| oil boiler | `price_oil ÷ (H_oil × η_s)` = 1.5394 ÷ (H_oil × η) | `fuel-bazg-heating-oil-sep-2026` (1.5394 CHF per litre); calorific value `H_oil` [kWh/l] and `η`: `TODO(source)` (see `docs/research/costs-and-energy.md`) |

The scored reference house always uses the heat-pump row (case rule).

## 3. Decision variables

All continuous and `≥ 0` in the LP.

| Variable | Unit | Meaning |
|---|---|---|
| `x_{c,s,t}` | server-hours | premium work of cohort `c` processed at site `s` in hour `t`. **Exists only** if `s ∈ E_c` and `α_c + δ_{o_c,s} ≤ t < d_c` (eligibility, latency and deadline are enforced by the variable not existing) |
| `f_{s,t}` | server-hours | filler load at site `s` in hour `t` |
| `u_{s,t}` | server-hours | total load (`= N_s × load share × 1 h`; the per-server load `u ÷ N_s` is what `SchedulerDecision.TargetLoadPerServer` carries) |
| `h_{s,t}` | kWh | server heat used for demand (directly or from the tank) |
| `z_{s,t}` | kWh | heat dumped |
| `S_{s,t}` | kWh | tank content at the start of hour `t` (`t = τ … τ+H`) |
| `m_c` | server-hours | premium work missed (cohorts with `d_c ≤ τ+H` only) |

Derived, not decided: backup heat `b_{s,t} = D_{s,t} − h_{s,t}`, server electricity
`P_s × u_{s,t}` [kWh], produced heat `a_s × u_{s,t}` [kWh].

## 4. Constraints

```
(1) load            u_{s,t} = Σ_c x_{c,s,t} + f_{s,t} + ω_{s,t}
(2) capacity        μ_s · N_s · Δt ≤ u_{s,t} ≤ N_s · Δt               [server-hours; Δt = HoursPerStep = 1 h]
(3) filler market   f_{s,t} ≤ F_{s,t}
(4) heat balance    S_{s,t+1} = (1 − λ_s) · S_{s,t} + a_s · u_{s,t} − h_{s,t} − z_{s,t}
(5) tank            0 ≤ S_{s,t} ≤ B_s,      S_{s,τ} = S⁰_s
(6) demand          h_{s,t} ≤ D_{s,t}                                 (rest is backup heat)
(7) work            Σ_{s,t} x_{c,s,t} + m_c = W_c        if d_c ≤ τ + H  (deadline inside the window)
                    Σ_{s,t} x_{c,s,t}       ≤ W_c        if d_c > τ + H  (rest carried to the next solve)
(8) parallelism     Σ_s x_{c,s,t} ≤ g_c                                (optional)
(9) no-dump mode    z_{s,t} = 0                                        (only in heat-led "hard" mode, §5.3)
```

How each requirement of the ticket appears:

- **Heat demand incl. forecast:** (6), with `D_{s,τ}` measured and `D_{s,t>τ}` from `HeatForecast`
  (perfect foresight or synthetic error, labelled, decision 0008).
- **Tank dynamics:** (4)–(5). With `λ = 0` this is the case's lossless tank. (4) merges direct use,
  charge and discharge into one balance; §6.4 explains why the realised values still come from the
  existing heat-balance code.
- **Server capacity / utilisation:** (2). Part load is continuous (as in `GreedyHeatScheduler`).
- **Deadlines:** variable domain `t < d_c` plus (7). Soft through `m_c` (penalty `π_c` + lost
  income) so the LP is always feasible.
- **Data eligibility / locality:** variable domain `s ∈ E_c`, computed with T-24's
  `JobPlacement.IsEligible` **before** the LP is built. No price or heat term can override it
  (T-12 rule).
- **Latency:** two different things. (a) Batch work: transfer delay `δ` shifts the earliest hour at
  the target site. (b) Interactive services: a latency limit `ℓ^max_k` removes sites from `E_c`.
  Sub-hour latency cannot be represented in time with hourly steps, only as eligibility.
- **Splittability:** the LP assumes work divisible into fractions of a server-hour and runnable in
  parallel; (8) limits the parallelism linearly. Indivisible jobs need integers (§7).

## 5. Objective

### 5.1 System surplus (default)

Maximise over the window:

```
Σ_{s,t} [ Σ_c (p_c − κ_{o_c,s}) · x_{c,s,t}       computing income, premium (minus transfer cost)
        + p^φ_{s,t} · f_{s,t}                       computing income, filler
        + v_s · h_{s,t}                             value of heat used (heating cost replaced)
        − e_{s,t} · P_s · u_{s,t}                   electricity
        − π^dump · z_{s,t} ]                        dump penalty (policy)
− Σ_c π_c · m_c                                     missed-deadline penalty (SLA)
+ θ · Σ_s v_s · S_{s,τ+H}                           value of heat left in the tank (§6.2)
```

This is the same surplus the year simulation already reports (`YearSimulation`: income =
server-hours × price, heat value = used heat × electricity ÷ COP, electricity cost), plus the
penalty terms. Equivalent minimisation form: minimise electricity + backup heating cost
`Σ v_s (D_{s,t} − h_{s,t})` + penalties − income; the two differ by the constant `Σ v_s D_{s,t}`.

Hardware depreciation and operations are fixed per year and do not depend on the hourly decision,
so they are **not** in the hourly objective; they enter the economic model per actor (T-14) and
the full-cost break-even (`docs/research/computing-price.md` §4).

### 5.2 Marginal value of one server-hour (reading aid)

With the case values (`P = 1 kW`, `a = 0.8 kWh`, `e = 0.3325`, `v = 0.133`):

| Where the heat goes | Value of one more server-hour at price `p` |
|---|---|
| used now or later | `p − e·P + v·a` = `p − 0.3325 + 0.1064` = **`p − 0.2261`** (the case break-even 0.226) |
| dumped | `p − e·P − π^dump·a` = `p − 0.3325 − 0.8·π^dump` |

### 5.3 Modes

| Mode | Setting | Use |
|---|---|---|
| economic | `π^dump = ε` (tiny, only to break the tie "dump vs. store" when the tank still has room) | pure money view (what an operator maximises) |
| heat-led, soft | `π^dump > (max_k (p_k + π_k) − e·P) ÷ a` | never dumps for income or to avoid a deadline penalty; stays feasible with must-run load or min load |
| heat-led, hard | constraint (9) | never dumps, **even to meet a deadline**: matches the case's "wait" policy (decision 0010); can be infeasible if `ω` or `μ` forces heat that cannot be used: then use the soft mode |
| case ranking | lexicographic: coverage, then dumped heat, then utilisation and missed deadlines (`docs/case/key-figures.md`) | solve in stages, or weights that respect the order; for the scored comparison table |

### 5.4 Consequence at the adopted prices

At `p^φ = 0.66 > e·P = 0.3325`, a dumped filler server-hour still earns `0.66 − 0.3325 = 0.3275`
CHF. In the **economic** mode the optimum is therefore "all servers on all the time" (baseline B0),
and routing adds nothing. Heat-led behaviour needs `π^dump > (0.66 − 0.3325) ÷ 0.8 = 0.409` CHF per
kWh (more if deadline penalties `π_c > 0` apply, §5.3), or the hard mode. Below `p^φ = 0.2261` filler does not pay even when all heat is used; between
0.2261 and 0.3325 the economic and heat-led modes agree (filler runs only where heat is used).
This is a statement about the business model, not a modelling bug, and should be said in the
pitch: *the heat-led schedule is a choice (heat comfort, grid, ecology), and it costs money when
filler is well paid*.

## 6. Solution approach

### 6.1 Time-expanded network view (why it is an LP)

Nodes `(s, t)`. Work of cohort `c` enters at `(o_c, α_c)`; edges: **wait** `(s,t) → (s,t+1)` (only
while `t+1 < d_c`), **move** `(i,t) → (j, t+δ_{ij})` (only if `j ∈ E_c`), **compute** `(s,t) →
sink` (cost `e·P − p`, joint capacity `N_s` with all other work at `(s,t)`), **miss** `→ sink`
(cost `π_c + 0`). Heat is a second commodity per site: produce `a·u` at `(s,t)`, **use** up to
`D_{s,t}`, **store** `(s,t) → (s,t+1)` up to `B_s`, **dump**. The coupling "work computed ⇒
heat produced" is the linear equation (4). Because wait and move edges carry no capacity and their
cost does not depend on time, every path of a cohort is described by where and when it is computed,
which is exactly `x_{c,s,t}`: the §3 variables are the path form of this network. If links get a
bandwidth limit, explicit move-flow variables `y_{c,i,j,t}` and a capacity row per link are added
(still an LP).

The positive flows `x_{c,s,t}` with `s ≠ o_c` are the **arrows on the map** in the pitch.

### 6.2 Rolling horizon

For each hour `τ` of the simulated period:

1. Read the realised state: tank `S⁰_s`, pending cohorts with remaining work, current demand.
2. Build the window `τ … τ+H−1`: demand forecast from `HeatForecast` (decision 0008), known
   arrivals (see the information table below), prices, eligible sites per cohort.
3. Solve the LP (or MILP).
4. **Commit** only the first `R` hours: target load per site (`u_{s,τ} ÷ N_s`), premium work per
   cohort and site, filler per site, moves.
5. Execute through the existing path: per site `SchedulerController` → `IExecutor`
   (simulated or Docker); the executor may achieve less (e.g. no work available).
6. Compute the realised heat balance with the **existing heat-balance code** from the actual
   demand (not the forecast), update tanks and queues, record dumps, backup and misses.

`R = 1` gives 8,760 solves per year; `R = 24` gives 365 (the figure in `routing-complexity.md`).
With `θ = 0` the LP empties the tank towards the window end; this is harmless as long as
`H ≫ R` (48 vs 1), and `θ` can be raised (e.g. `θ = 1`, valuing stored heat like heat used) as a
sensitivity. Ties (several optima, see the worked example) are broken deterministically: fixed
variable order and a documented tie rule (e.g. a tiny preference for later filler, `ε` per hour, and the tiny dump penalty `ε` of the economic mode),
so runs are reproducible (AGENTS.md §6).

**Information used — always label it:**

| Input | Realistic (non-anticipative) | Perfect foresight (upper bound, label it!) |
|---|---|---|
| demand, current hour | measured (`HeatSignal`) | same |
| demand, future hours | synthetic-error forecast (decision 0008), later a weather forecast | actual 2025 weather (`HeatForecastKind.PerfectForesight`) |
| premium arrivals | only arrived cohorts (optionally expected arrivals from scenario rates) | all future arrivals in the window |
| prices | tariff (known) | same |

Plans are made on the forecast; metrics are reported on the realised physics. A **full-year single
LP** (`H = 8,760`, perfect foresight of everything) gives an upper bound for all strategies: useful
to say "the optimiser reaches x % of the best possible", never a scored result.

### 6.3 Size

Cohorts with the same class, deadline hour and eligible set are interchangeable and can be
aggregated; the origin only matters through `E_c` and `δ`. Per solve: `Σ_c |E_c| · (d_c − α_c)`
premium variables + 5 per site-hour. With 50 sites, 48 h and 3 classes this is of the order of the
~7,000 variables in `routing-complexity.md`; an LP solver (e.g. GLOP in Google OR-Tools for .NET)
solves it in well under a second. The library choice is T-12's decision.

### 6.4 Planning view vs. realised physics

The LP's `h` and `z` are only a plan. For a **given** load plan, the existing hourly rule in
`YearSimulation` (use heat directly, then charge the tank, then discharge for the rest, dump only
what does not fit) maximises heat used when the tank is lossless and `v_s` is constant: using heat
now never reduces what can be used later. So under perfect foresight the realised balance
reproduces the LP's **total** heat use (not necessarily its hour-by-hour `h` and `z`, which can
have ties), and with forecast error the realised dump/backup shows the cost of the error.
Regression tests therefore compare per-hour **loads** and yearly totals, not planned `h` and `z`. **Do not write a second heat model**: factor the existing balance into one pure function
that the year simulation, the network simulation and the tests share (AGENTS.md §9a).

## 7. When the LP becomes a MILP (or harder)

| Extension | Why integer | Effect |
|---|---|---|
| servers switched fully off, but a minimum part load when on (`μ > 0` only when on) | semi-continuous load per server | MILP (binary on/off per server-group and hour) |
| idle power `P_idle` when on, start-up cost, minimum run / down time | on/off state links hours | MILP (unit commitment) |
| indivisible job (must run on one site) | binary site assignment per job | MILP |
| non-preemptive job / checkpoint cost per interruption | contiguous hours, count of interruptions | MILP |
| all-or-nothing payment (job paid only if complete) | binary "job done" | MILP |
| fixed cost per transfer, link capacity in whole jobs | binary per move | MILP |
| forecast error handled explicitly | scenarios or uncertainty sets | larger LP (robust / stochastic), still LP if scenario-based |

Note: `μ > 0` with servers that are **always** on is still linear (constraint (2)), which is the
current `MinimumLoadShare` meaning. With MILP, OR-Tools' CP-SAT or SCIP are options, and for very
large networks a heuristic (network greedy + LP polishing) — paper material (`routing-complexity.md`).

## 8. Strategies to compare (T-12)

All strategies get the same scenario, the same jobs, the same forecast and the same realised
physics, and report: coverage, heat dumped, utilisation, missed deadlines (% of jobs, decision
0010), system surplus, solver runtime.

| ID | Strategy | Rule |
|---|---|---|
| B0 | always on | `u_{s,t} = N_s`; premium EDF first, filler for the rest; dumps |
| B1 | local greedy (exists) | per site `GreedyHeatScheduler`: `u = clamp((D + B − S) ÷ a, μN, N)`; premium EDF from its own origin only, then filler; heat-led wait policy (decision 0010) |
| B2 | network greedy | each hour, premium cohorts in EDF order go to the eligible site with the largest remaining heat need `(D + B − S) ÷ a − load already assigned`, ties to the origin site; then filler fills each site's remaining greedy target |
| LP | rolling-horizon LP | §6.2, labelled "perfect foresight" or "forecast error" |
| UB | full-year LP | perfect foresight upper bound, for reference only |

### 8.1 The existing greedy scheduler is a special case

Take one site, `H = 1`, `R = 1`, no premium work (unlimited filler, as in the current year run),
terminal value `θ`. In that one-hour LP an extra server-hour is worth `p^φ − eP + v·a` while it
covers demand, `p^φ − eP + θ·v·a` while it charges the tank and `p^φ − eP − π^dump·a` when it is
dumped. If

```
p^φ − e·P + θ·v·a > 0      (charging the tank pays)       and
p^φ − e·P − π^dump·a < 0   (dumping does not pay)
```

the LP runs exactly until the tank is full: `a·u = D + B − S`, clamped to `[μN, N]`, which is
`GreedyHeatScheduler.Decide`. With the case values and the config's illustrative price 0.30 this
needs `θ > (0.3325 − 0.30) ÷ 0.1064 = 0.31` and any `π^dump ≥ 0`; with 0.66 it needs
`π^dump > 0.409`. Finite premium work in that LP runs before filler because `p_c > p^φ`, and EDF is
a valid tie rule; the "run-and-dump" policy of decision 0010 is the economic mode with `p > eP`.
So the greedy is the LP with a one-hour view and a "keep the tank full" rule of thumb in place of a
forecast; the LP generalises it in three directions: **time** (forecast, `H > 1`), **space**
(moves, `|S| > 1`), **markets** (premium vs filler prices).

## 9. Worked example: 2 sites × 3 hours

Illustrative numbers chosen so everything can be checked by hand. Demands are **not** reference
house values; prices: premium 0.66 (adopted computing price, used here as a premium price for
illustration), filler 0.30 (the illustrative value in `config/reference-house.json`). Physics and
electricity as in the case.

**Inputs**

| | Site A | Site B |
|---|---|---|
| servers `N` | 1 | 1 |
| heat per server-hour `a` | 0.8 kWh | 0.8 kWh |
| tank `B`, start `S⁰` | 0 (no tank) | 0.8 kWh, empty |
| demand `D` in hours 0, 1, 2 | 0.8, 0.4, 0 kWh | 0, 0, 1.6 kWh |
| heating replaced | heat pump, `v = 0.133` | heat pump, `v = 0.133` |
| electricity | 0.3325 CHF/kWh, `P = 1 kW` | same |
| minimum load `μ` | 0 (as in the config) | 0 |

- Premium cohort `J`: origin A, arrives at the start of hour 1, work `W = 2.0` server-hours,
  exclusive deadline 3 (may run in hours 1 and 2), eligible at A and B (both CH, policy
  `country-allowlist [CH]`), `δ_AB = 0` h, `p_J = 0.66`, `π_J = 0`.
- Filler: `p^φ = 0.30`, unlimited at both sites.
- Mode: heat-led (no dumping), except B0.

Total demand 0.8 + 0.4 + 1.6 = 2.8 kWh = 3.5 server-hours of heat. Site B can use at most
2 server-hours (one into the tank in hour 0 **or** 1 — the tank holds one server-hour of heat and
there is no demand before hour 2 — plus one in hour 2); site A at most 1 + 0.5 = 1.5.

**Results** (surplus = income + 0.133 × heat used − 0.3325 × server-hours)

| Strategy | Schedule (server-hours) | Server-h | Heat used / dumped [kWh] | Coverage | `J` done / missed | Income | Electricity | Heat value | **Surplus** |
|---|---|---|---|---|---|---|---|---|---|
| B0 always on | A: F 1, J 1, J 1 · B: F 1, F 1, F 1 | 6.0 | 2.8 / 2.0 | 100 % | 2.0 / 0 | 2×0.66 + 4×0.30 = 2.52 | 1.995 | 0.3724 | **0.8974** |
| B1 local greedy | A: F 1, J 0.5, – · B: F 1, –, F 1 | 3.5 | 2.8 / 0 | 100 % | 0.5 / 1.5 | 0.33 + 0.90 = 1.23 | 1.16375 | 0.3724 | **0.43865** |
| B2 network greedy | A: F 1, J 0.5, – · B: F 1, –, J 1 | 3.5 | 2.8 / 0 | 100 % | 1.5 / 0.5 | 0.99 + 0.60 = 1.59 | 1.16375 | 0.3724 | **0.79865** |
| LP, perfect foresight | A: F 1, J 0.5, – · B: –, J 1, J 0.5 + F 0.5 | 3.5 | 2.8 / 0 | 100 % | 2.0 / 0 | 1.32 + 0.45 = 1.77 | 1.16375 | 0.3724 | **0.97865** |

(F = filler, J = premium cohort; three entries per site = hours 0, 1, 2.)

**How to check it**

- B1, site B, greedy formula `(D + B − S) ÷ a`: hour 0: (0 + 0.8 − 0) ÷ 0.8 = 1 → tank full;
  hour 1: (0 + 0.8 − 0.8) ÷ 0.8 = 0; hour 2: (1.6 + 0.8 − 0.8) ÷ 0.8 = 2 → clamped to 1, 0.8 kWh
  direct + 0.8 kWh from the tank. Site A: 0.8 ÷ 0.8 = 1, 0.4 ÷ 0.8 = 0.5, 0. `J` arrives in hour 1
  and finds only 0.5 server-hours of heat-led capacity at its origin: 1.5 missed.
- B2: in hour 0 nobody knows `J` yet, so B's tank is filled with filler (as in B1). In hour 1, A
  needs 0.5, B needs 0 (tank full) → `J` 0.5 at A; hour 2: B needs 2 (clamped to 1) → `J` 1 at B.
  0.5 missed.
- LP: knows that `J` will arrive (perfect foresight of the arrival), so it keeps B's tank **empty**
  in hour 0 and charges it with `J` in hour 1. All 3.5 server-hours of useful heat are used and
  `J` is complete. It is optimal: no-dump caps server-hours at 3.5, each useful server-hour adds
  `−0.3325 + 0.1064 = −0.2261`, and income is maximal when all 2.0 of `J` is done:
  3.5 × (−0.2261) + 2 × 0.66 + 1.5 × 0.30 = −0.79135 + 1.77 = 0.97865.
- B0: 6 server-hours, A dumps 0.4 + 0.8 kWh, B dumps 0.8 kWh (tank full in hour 1).

**What it shows**

- Moving work (B1 → B2: +0.36 CHF) and planning ahead (B2 → LP: +0.18 CHF) both pay, and both
  avoid missed deadlines without dumping.
- The LP's advantage here comes **entirely from knowing the arrival in advance**. A
  non-anticipative LP at hour 0 sees "charge B's tank in hour 0 or hour 1" as a tie and may do
  what B2 does; a tie rule "store heat as late as possible" (§6.2) or expected arrivals from the
  scenario rates recover it. This is why every LP result must say what it knew.
- B0 has a higher surplus than B2 (0.90 vs 0.80) while dumping 2.0 kWh: with dumping allowed and
  premium at 0.66, running everything is profitable (§5.4). In the economic mode with filler at
  0.30 the LP would give the same schedule as above (dumped filler loses 0.0325 CHF per hour, and
  `J` fits into useful hours); at filler 0.66 it would give B0.
- Several schedules reach 0.97865 (e.g. `J` and filler swapped between B's hours). Deterministic
  tie-breaking is needed for reproducible runs.

## 10. Mapping to the existing interfaces

| Today (on `main`) | What the routing model needs | Change |
|---|---|---|
| `HeatSignal`, `SiteHeatState` + `HeatForecast` (0008) | per-site state and forecast | **reuse unchanged**; one `SiteHeatState` per site |
| `IScheduler.Decide(HeatSignal)` → `SchedulerDecision(TargetLoadPerServer)` | one decision for all sites and the work allocation | new `INetworkScheduler.Decide(NetworkState) → NetworkDecision`; `NetworkState` = site states + per-site economics (§2.1, §2.4) + pending cohorts (§2.2) + links; `NetworkDecision` = per site a `SchedulerDecision` + premium allocation (cohort → server-hours) + filler + moves |
| `SchedulerController.Apply(signal, scheduler, executor)` → `IExecutor.ApplyTargetLoad` | the **same** actuation path per site, also for the live Docker job (AGENTS.md §5.3) | keep; feed each site's `SchedulerDecision` through it (a small overload taking a decision, or an adapter `IScheduler` that returns the site's committed decision) |
| `ISiteHeatScheduler` + `CurrentHeatSchedulerAdapter` | baselines B1 | adapter `LocalNetworkScheduler(ISiteHeatScheduler)` that runs every site independently; with one site it must reproduce today's reference-house results exactly (regression gate) |
| `JobQueueExecutor` (EDF, one site, 0010) | per-site queues that accept an allocation and receive moved work | extend: allocation-driven dequeue, transfer of remaining work between site queues, `JobPlacement.IsEligible` re-checked on enqueue at a non-origin site |
| `JobPlacement.IsEligible` (0013) | eligible sets `E_c` | **reuse unchanged**, evaluated before the LP is built |
| `YearSimulation` (one site) and its heat balance | multi-site loop with realised physics | T-10 network loop; factor the hourly heat balance into one shared pure function (no second heat model) |
| `config/reference-house.json` | per-site, per-class and policy parameters with unit and source | T-10 scenario files; new parameters: heating type + fuel price + efficiency, filler price and availability, class price and penalty, link delay/latency/cost, `π^dump`, `θ`, `H`, `R`, mode |
| result export (0011) | per-site rows, flows between sites | extend the bundle (T-13 owner) |

**Decision records.** This document is recorded as decision 0014 (the model). The interface change
(`INetworkScheduler`, `NetworkState/NetworkDecision`, queue changes) and the solver library
(e.g. Google.OrTools) are hard to reverse and get **their own decision record in T-12**, reviewed
before implementation. The reference house's scored figures stay on B1.

## 11. Open questions for the team

1. **Objective mode** (§5.3–5.4): economic surplus (then always-on wins at filler 0.66 and heat is
   dumped) or heat-led (hard no-dump even if a deadline is missed, or a dump penalty above
   0.409 CHF/kWh plus any deadline penalty)? Which one is "our"
   strategy in the pitch comparison?
2. **Premium job terms:** missed-deadline penalty `π_c` (SLA) and whether partly finished jobs earn
   income (LP) or only complete jobs (MILP).
3. **Filler market:** is filler available at every site in every hour (`F = N`, "unlimited"), at
   which price (constant 0.66 or a time series, T-28), and under which data rules?

Smaller, can be decided in T-12: `H`, `R`, `θ`, tie rule, whether expected arrivals are used.
