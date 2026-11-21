# R3: Compute capacity from real households

> Status: first version, 2026-11-21 (research agent, ticket T-37). Numbers live in
> [`data/raw/market/household-capacity.csv`](../../data/raw/market/household-capacity.csv) (ids `hh-*`).
> Basel-Stadt figures are counted from the Federal Register (GWR) export on data.bs.ch; Swiss figures are
> rounded BFS shares. Everything derived is a **prototype estimate**, not verified.

## 1. Building stock

**Basel-Stadt** (dataset [100230](https://data.bs.ch/explore/dataset/100230/), GWR buildings, export of 2026-11-20,
32,263 records; filter and command in each CSV row). Existing residential buildings (classes 1 dwelling,
2 dwellings, 3+ dwellings, community homes): **23,251** (`hh-bs-res-buildings`), 116,476 dwellings
(`hh-bs-res-dwellings`), 16.0 million m² energy reference area (`hh-bs-res-ebf`).

| Class | Buildings | Dwellings |
|---|---|---|
| 1 dwelling (`hh-bs-res-sfh`) | 9,935 | 9,936 |
| 2 dwellings (`hh-bs-res-2dw`) | 1,045 | 2,097 |
| 3+ dwellings (`hh-bs-res-mfh`) | 12,128 | 104,203 |
| community (`hh-bs-res-community`) | 143 | 240 |

Primary heating energy (buildings, `hh-bs-en-*`): district heating 10,181 (43.8 %), gas 8,422 (36.2 %), oil 1,854
(8.0 %), heat pump air 1,307 + ground probe 1,007 (+ 29 groundwater) (10.1 %), wood 290, electricity 153.
The building count by construction period is in `hh-bs-per-*`; 55 % were built before 1946
(5,546 + 7,312 of 23,251).

**Switzerland** (BFS press release of 2026-09-15 for end of 2025, `hh-ch-*`): 1.8 million residential buildings,
4.88 million dwellings, more than 1 million single-family houses; heating oil 33 %, gas 17 %, heat pump 25 %,
wood 12 %, electricity 7 % of buildings. The district-heating share and counts by construction period are not in
the release: TODO(source) (full tables on BFS STAT-TAB / housing-stat.ch were not reachable by the tools used).

### Basel's phase-out rules and our addressable market

- Since 1 Oct 2017 the Energy Act requires a **renewable system when an oil or gas heating is replaced**, if
  technically possible and without extra cost (`hh-pol-replace`). Installing a fossil heating must be reported.
- Net zero by **2037**; natural gas for heating is to end by 2037 and IWB builds 60 km of new district heating
  (`hh-pol-2037`). So gas buildings (36 % of the stock) must change within about 11 years.
- Oil has no fixed end date found. A binding replacement date (2035 or 2037) is under discussion in the Grand
  Council: unconfirmed, TODO(source). Heating age (installation year) is not in the GWR export, so "replaced
  by 2030" cannot be counted: TODO(source).
- District heating is where the cantonal plan sends most replacements. A server-heated house competes with it;
  buildings on a district-heating line are therefore **not** counted as suitable.

**Definition of "suitable" (S1):** existing residential building whose primary heating energy in the GWR is gas or
oil. Reasoning: these must replace their heating system anyway (gas by 2037), have a hydronic boiler that a
server heat loop can replace or support, and are not on district heating. Not checked: roof/cellar space, owner
consent, building size, tenants. Treat S1 as an upper bound for the technical market.

| S1, Basel-Stadt | Buildings | Dwellings | Area m² EBF |
|---|---|---|---|
| all (`hh-bs-fossil-bldgs`, `-dwellings`, `-ebf`) | **10,276** (44.2 % of stock) | 36,320 | 5,176,481 |
| 1 dwelling (`hh-bs-fossil-sfh`) | 5,411 | | 1,289,959 |
| 2 dwellings (`hh-bs-fossil-2dw`) | 513 | | 211,913 |
| 3+ dwellings (`hh-bs-fossil-mfh`) | 4,352 | | 3,674,609 |

**S1, Switzerland** (`hh-ch-fossil-bldgs`): 1.8 M × (33 % + 17 %) = **900,000** buildings (±3 % from rounding).
No national replacement deadline researched.

## 2. Heat and hot-water demand

Source: EnergieSchweiz / BFE module 3, [Abbildung 2](https://pubdb.bfe.admin.ch/de/publication/download/12342)
(page 7), specific final energy for space heating and hot water, stock state 2016, canton Zürich (proxy for
Basel). **Read by eye from the chart (±5 kWh/m²)**, `hh-ek-*`:

| Period | until 1920 | 1920-1960 | 1961-1980 | 1981-1990 | 1991-2000 | 2001-2010 | from 2011 |
|---|---|---|---|---|---|---|---|
| kWh/m²·yr | 125 | 135 | 135 | 110 | 90 | 93 | 96 |

It is final energy (fuel), so delivered heat is lower by the boiler loss (TODO(source), perhaps 10-15 %).
Hot water is inside these values. The case gives 12 kWh/day = 4,380 kWh/yr for the reference house (`hh-hw-case`);
a Minergie example gives ~18 kWh/m²·yr (`hh-hw-minergie`, search snippet only, not used).
Typical floor area comes straight from the GWR (S1 mean 504 m², SFH 238 m², MFH 844 m²).

**Formula (demand of a building):** `demand_kWh = EBF_m2 × ek(period)`, mapped GWR period → table row.
Worked example: a Basel gas SFH of 238 m² built 1946-1960: 238 × 135 = 32,130 kWh/yr, close to the reference
house (31,231 kWh = 26,851 space + 4,380 water).

**Result, S1 Basel-Stadt** (Python sum over 10,276 buildings, [`hh-dem-bs-fossil`]):
**665.0 GWh/yr** (mean 128.5 kWh/m², `hh-ek-bs-fossil-mean`): 1 dwelling 166.4 GWh, 2 dwellings 26.9 GWh,
3+ dwellings 471.6 GWh. Mean per building 64,710 kWh (SFH 30,755, MFH 108,371).

## 3. Network capacity

**Ratio (assumption, `hh-ratio-srvh-per-kwh`):** the repo year run sells 22,661 server-hours on 31,231 kWh
total heat demand (4 servers, 58.0 % coverage, [`demand.md`](demand.md) §2):
22,661 / 31,231 = **0.7256 server-hours per kWh of heat demand**.
Assumption: every joining building gets servers in proportion to its demand, 4 per 31,231 kWh = 0.128 servers per
MWh (`hh-srv-per-mwh`), each 1 kW electrical, 0.8 kW heat. A large MFH (108 MWh) gets ~14 servers. The ratio is
not valid for other server densities, because coverage saturates.

**Formula:** `server_hours = demand_kWh(S1) × participation × 0.7256`;
`servers = demand_kWh × participation ÷ 31,231 × 4`; `installed_kW = servers × 1 kW`.
Worked example (Basel, 5 %): 665,000,000 × 0.05 × 0.7256 = 24.1 million server-hours;
servers = 33.25 GWh ÷ 31,231 kWh × 4 = 4,259.

| Participation of S1 | Buildings | Servers / installed kW | **Server-hours / yr** | Single-family only | 3+ dwellings only |
|---|---|---|---|---|---|
| 1 % | 103 | 852 | **4.8 M** (`hh-cap-bs-all-1`) | 1.2 M | 3.4 M |
| 5 % | 514 | 4,259 | **24.1 M** (`hh-cap-bs-all-5`) | 6.0 M | 17.1 M |
| 10 % | 1,028 | 8,517 | **48.3 M** (`hh-cap-bs-all-10`) | 12.1 M | 34.2 M |

Average heat output at 10 %: 66,496 MWh ÷ 8,760 h = 7.6 MW (winter peak is higher).
GPU-equivalents: not computed. A GPU server's draw and the number of GPUs per 1 kW server are not measured
(TODO(source), see [`hardware.md`](hardware.md)).

**Switzerland** (`hh-cap-ch-1/5/10`): 900,000 × participation × 31,231 kWh × 0.7256.
Using the reference house per building (a single-family house) is the **conservative** choice: 1 % = 204 M,
5 % = 1,020 M, 10 % = 2,039 M server-hours. With the Basel S1 mean (64,710 kWh per building, MFH-heavy city) the
result is 2.07 times higher (423 M, 2,113 M, 4,226 M). Switzerland has more small houses than Basel, so the
truth lies between; national mean demand per fossil building is a gap.

How to read: the figures are **capacity if all servers sell all heat-led hours**. Compare with demand
(`demand.md`): the low and base job scenarios there use ~13-39 server-hours per house, so capacity is far
larger than the demand we modelled.

## 4. Seasonality

Method: When2Heat Switzerland 2022 hourly profiles ([`when2heat-ch-2022.csv`](../../data/raw/sites/when2heat/SOURCE.md),
8,759 hours, columns `CH_heat_profile_space_SFH` and `CH_heat_profile_water_SFH`). Scale the space profile to
26,851 kWh and the water profile to 4,380 kWh (reference house), add per hour. Per hour,
`server_hours = min(demand_kWh ÷ 0.8, 4)` (4 servers, 0.8 kWh heat per server-hour). Sum by month and rescale to
the repo's 22,661 (the profile gives 23,440; coverage 60.0 % vs 58.0 % in the repo run, `hh-seas-calib`, because
weather year and profile differ).

| Month | Jan | Feb | Mar | Apr | May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Share of year heat demand | 18.7 % | 13.9 % | 12.8 % | 9.3 % | 3.7 % | 2.0 % | 1.6 % | 1.7 % | 4.4 % | 4.2 % | 11.0 % | 16.8 % |
| Server-hours (reference house) | 2,865 | 2,586 | 2,762 | 2,462 | 1,376 | 753 | 612 | 654 | 1,543 | 1,565 | 2,642 | 2,840 |

Winter/summer capacity: **January / July = 4.7** (`hh-seas-ratio-janjul`); **Dec-Feb / Jun-Aug = 4.1**
(`hh-seas-ratio-djfjja`). Multi-family profile: 4.4 and 3.9. Raw heat demand falls by 11.5 times, but capacity only
by 4.7 times, because winter is capped by the 4 servers (about 4.0 busy servers per hour in January, 0.85 in July).
In July only hot water remains: 4,380 kWh ÷ 12 = 365 kWh per month ≈ 456 server-hours if it were all
used, the rest of the 612 comes from the profile shape. Consequence for scale-up: the same network sells ~4.7
times more in January than in July; to sell steady summer jobs the heat must be dumped or other demand (filler
jobs, see [`demand.md`](demand.md)) is needed. Basel heating-degree days from MeteoSwiss were not
used: TODO(source) if the team wants a Basel-specific shape.

## 5. Main gaps

1. District-heating share for CH and counts by period for CH (TODO(source)); numbers for CH are rounded shares.
2. Specific demand is read from a chart for canton Zürich 2016 in final energy; need SIA 380/1 or Basel data and boiler efficiency.
3. Heating age and a binding Basel replacement date (2035/2037) are unconfirmed; S1 is not time-limited.
4. Proportional-server assumption (0.128 servers/MWh) and 100 % willingness of buildings to host servers are untested; large MFH need a shared installation concept.
5. CH demand per building uses the single-family reference house: factor 2 uncertainty.
