# WP3: Sites and heat-demand profiles

> Status: **first version**, 2026-11-20, Claude subagent. Research only, nothing implemented.
> Numbers live in [`data/raw/sites/site-types.csv`](../../data/raw/sites/site-types.csv)
> (113 rows, one value per row with source and confidence). An hourly profile extract for
> Switzerland is in [`data/raw/sites/when2heat/`](../../data/raw/sites/when2heat/SOURCE.md).
> This file explains the structure, the context and the gaps; values quoted here all have a CSV row.

## A. Generic site description

Goal ([`vision.md`](../vision.md), building block 3): a site is **a list of parameters plus,
optionally, time series**, so that any location and building type can be simulated with the same
code. The list below is a proposal for the team; the file format is now JSON in `config/` (`AGENTS.md` §12).

Rules behind the list:
- **Demand is a model choice, not a fixed formula.** Each heat use (space heating, hot water,
  process heat) names a model and its parameters, or points to a time series. The case's formula
  is one model among several.
- **Shapes and totals are separate.** A daily/weekly/seasonal shape (normalised) is combined with
  an annual or design value. This lets one sourced shape (e.g. When2Heat, BDEW) serve many sites.
- **Temperature level is explicit**, because it decides whether server heat (without a heat pump)
  can serve a use at all.
- Units in names, as in `AGENTS.md` §5.

### Parameter list

Req. = required (R) or optional (O). "Model part": D = heat demand, S = storage, X = scheduler /
executor, E = economics, N = network of sites, M = market sizing.

| Group | Parameter | Unit | Meaning | Req. | Default / source of a default | Model part |
|---|---|---|---|---|---|---|
| Identity | `site_id`, `site_type` | – | unique id; type from the list in part B | R | – | all |
| Location | `lat`, `lon`, `elevation_m` | °, °, m | position; selects the weather series | R | geocoding; Open-Meteo returns grid elevation | D, N |
| Location | `weather_source` | – | which hourly temperature series to use | R | Open-Meteo archive, as in [`data/raw/open-meteo/`](../../data/raw/open-meteo/SOURCE.md) | D |
| Location | `timezone` | IANA | local time for daily shapes and heating days | R | `Europe/Zurich` | D, X |
| Space heating | `space_heating_model` | enum | `none`, `linear_cutoff` (case), `bdew_siglinde`, `time_series` | R | `linear_cutoff` for the reference house | D |
| Space heating | `design_load_kw`, `indoor_temp_c`, `design_temp_c` | kW, °C, °C | linear model: `load = design_load × (indoor − T) ÷ (indoor − design)`; equivalent to a heat-loss coefficient `design_load ÷ (indoor − design)` in kW/K | R if linear | case: 10 kW, 20 °C, −5.6 °C | D |
| Space heating | `heating_day_threshold_c` | °C | no space heating on days with daily mean ≥ threshold | O | case: 12 °C | D |
| Space heating | `annual_space_heat_kwh` | kWh/a | total, used to scale a normalised shape | R if shape-based | SIA 380/1 / GEAK per building (paywalled); measured consumption | D |
| Space heating | `slp_profile_code`, `slp_coefficients` | – | BDEW SigLinDe daily function `h(T)` and weekday factors | R if `bdew_siglinde` | BDEW guideline, coefficients per type in the CSV | D |
| Space heating | `space_hourly_shape` | % of day × 24 | distribution of a day's demand over hours (may differ by month and weekday) | O | When2Heat CH SFH/MFH/COM | D |
| Hot water | `hot_water_model` | enum | `flat_daily`, `per_person`, `per_area`, `time_series` | R | `flat_daily` for the reference house | D |
| Hot water | `hot_water_kwh_per_day` | kWh/d | daily hot-water heat | R if flat | case: 12 kWh/d | D |
| Hot water | `persons`, `hot_water_litres_per_person_day` | –, l/(person·d) | per-person model; energy = litres × 0.058 kWh/l (10→60 °C) | R if per-person | SIA 2024 via BFE: SFH 40, MFH 35 | D |
| Hot water | `hot_water_hourly_shape` | % of day × 24 | daily shape of draw-offs | O | When2Heat CH SFH water (peak 06:00–07:00) | D, S |
| Hot water | `hot_water_monthly_factor` | – × 12 | seasonal modulation | O | flat (When2Heat is ≈ flat) | D |
| Process heat | `process_heat_model`, `process_heat_kwh_per_unit`, `units_per_day` | kWh/unit, unit/d | e.g. kg of laundry, tonnes dried, m³ pool water held at temperature | O | per type, CSV | D |
| Calendar | `operating_calendar` | dates / weekdays / hours | when the site needs heat at all (season, opening hours, harvest) | O | always | D, X |
| Calendar | `weekday_factors` | – × 7 | demand by weekday | O | BDEW per type; households = 1 | D |
| Temperatures | `supply_temp_space_c`, `supply_temp_hot_water_c`, `supply_temp_process_c` | °C | temperature each use needs | R | hot water 60 °C (normlitre definition); type values in CSV | D, X |
| Storage | `storage_usable_kwh`, `storage_loss_kwh_per_day` | kWh, kWh/d | thermal buffer between servers and uses | R | case: 35 kWh, no losses | S |
| Storage | `storage_volume_l`, `storage_delta_t_k` | l, K | alternative way to state the usable capacity | O | case: 1000 l, 30 K | S |
| Servers | `server_count`, `server_power_kw`, `heat_recovery_share` | –, kW, – | installed computing and how much heat reaches the water | R | case: 1 kW, 80 % | X, E |
| Servers | `max_servers`, `space_racks`, `max_electrical_kw` | –, U, kW | physical and electrical limits of the site | O | site survey | X |
| Servers | `heat_dump_possible` | bool | can unused heat be rejected (dry cooler, ventilation)? | R | case behaviour to be defined by us | X |
| Network | `uplink_mbps`, `latency_ms_to_hub` | Mbit/s, ms | connectivity; limits data-heavy jobs | O | ISP offer | N, X |
| Sovereignty | `allowed_job_classes`, `data_location_constraint` | list, region code | which jobs may run here (e.g. "CH only", "no personal data") | R | `any` | X, N |
| Sovereignty | `local_compute_demand` | job types × rate | jobs the host itself produces (CCTV, documents) | O | WP1 | X, E |
| Economics | `replaced_heat_source`, `replaced_heat_efficiency` | enum, – | what the server heat displaces (heat pump, oil, gas, district heat) | R | case: heat pump, 2.5 | E |
| Economics | `heat_value_chf_per_kwh` | CHF/kWh | value of used heat; derived from energy price ÷ efficiency or given | R | case: 0.3325 ÷ 2.5 = 0.133 | E |
| Economics | `electricity_price_chf_per_kwh` (or tariff id) | CHF/kWh | price paid for server electricity | R | ElCom per operator | E |
| Market | `count_in_region` | sites | how many such sites exist, to size the market | O | CSV (`count_*`) | M |
| Time series | `ts_space_heat_kw`, `ts_hot_water_kw`, `ts_process_kw` | kW per hour | if given, they **replace** the corresponding model | O | measured data, When2Heat scaled | D |

The scheduler only needs, per hour: heat demand by temperature level, storage state, server
limits and the allowed jobs. Everything above reduces to that, which keeps simulated and real sites
behind one interface (`AGENTS.md` §6).

### The reference house as one instance

All values from the case ([`key-figures.md`](../case/key-figures.md)); CSV `site_type =
reference-house-case`.

| Parameter | Value |
|---|---|
| `site_type` | `residential-sfh` (variant: reference house, fixed for scored figures) |
| `lat`, `lon`, `timezone` | 47.56, 7.59, `Europe/Zurich` |
| `weather_source` | Open-Meteo archive 2025, hourly `temperature_2m` |
| `space_heating_model` | `linear_cutoff` |
| `design_load_kw`, `indoor_temp_c`, `design_temp_c` | 10, 20, −5.6 (heat-loss coefficient 10 ÷ 25.6 = 0.39 kW/K) |
| `heating_day_threshold_c` | 12 (daily mean of 24 hourly values) |
| `hot_water_model`, `hot_water_kwh_per_day` | `flat_daily`, 12 |
| `hot_water_hourly_shape`, `weekday_factors`, `operating_calendar` | none (flat, every day) |
| `supply_temp_*` | not given by the case (simplification: no heat pump needed) |
| `storage_usable_kwh`, `storage_loss_kwh_per_day` | 35, 0 |
| `server_power_kw`, `heat_recovery_share` | 1, 0.8 |
| `replaced_heat_source`, `replaced_heat_efficiency` | heat pump, 2.5 |
| `electricity_price_chf_per_kwh`, `heat_value_chf_per_kwh` | 0.3325 (ElCom 2026, IWB, H4), 0.133 |
| `allowed_job_classes`, `count_in_region` | open (our decision), not used |

Check: the case's 12 kWh/day hot water equals 4 persons × 50 l × 0.058 kWh/l = 11.6 kWh/day,
so the case uses a per-person value above the SIA 2024 standard (40 l/person·day for SFH). With
the SIA value the same house would need ≈ 9.3 kWh/day. Space heating 26,851 kWh/a + hot water
4,380 kWh/a: hot water is 14 % of the house's heat.

## B. Site types: summary

Details, coefficients and hourly shares are in the CSV. "Summer factor" = BDEW daily demand at
20 °C ÷ demand at −5 °C (derived from the published coefficients; how much of a cold winter day's
heat is still needed on a warm day).

| Site type | Yearly demand (example) | Seasonality | Daily / weekly shape (source) | Temperature | Count |
|---|---|---|---|---|---|
| `reference-house-case` | 26,851 kWh space + 4,380 kWh hot water | space 0 % in Jun–Aug, 11.7 % Apr–Sep | flat hot water; hourly space from T | not given | 1 |
| `residential-sfh` | hot water 13.5 kWh/m²·a (SIA 2024) | space 18.5 % Apr–Sep, summer factor 0.064 | When2Heat SFH: hot water peak 06–07 h (9.4 % of the day), night ≈ 0.5 %/h; no weekday effect | hot water 60 °C | > 1 M SFH in CH (low) |
| `residential-mfh` | hot water 19.8 kWh/m²·a (SIA 2024) | summer factor 0.092 | When2Heat MFH (flatter, peak 1.37× mean); measured peaks 05–10, 18–22 h | hot water 60 °C | 1.8 M residential buildings CH, 23,593 buildings Basel-Stadt (all types) |
| `hotel-spa` | 95 kWh/m²·a target new; 22 hotels measured ≈ 20.7 l oil/m²·a | 26 % of heat in Apr–Sep; hot water 32–42 % of heat; summer factor 0.141 | BDEW GBH (flat weekdays); hourly only via commercial proxy | hot water 60 °C | 63 hotels, 4,726 rooms in Basel-Stadt 2025 |
| `indoor-pool` | Allschwil: 627 MWh/a heat after renovation (1,100 before) | year-round, weakly weather dependent | opening hours; no open hourly profile | pool water > 28 °C | 470 public + 350 school + 1,000 hotel/hospital pools CH |
| `outdoor-pool` | TODO(source) | summer only | opening hours; cover required outside them | 22–26 °C | 600 CH |
| `agriculture-hay-drying` | ≈ 36,000 kWh/a heat equivalent per 100 m² hay box | summer (grass harvests) | after harvest, weather dependent; gap | air +6–10 K over ambient | gap |
| `agriculture-grain-drying` | Landi plant: 10 GWh gas/a for 4,000 t maize + 2,200 t grass, 2,500 h/a | harvest campaigns | continuous during campaign | drum inlet 300–800 °C | 46,270 farms CH (low) |
| `greenhouse` | 500 m² house: 268 MWh/a at 18 °C, 116 at 12 °C, 13 at 3 °C | summer factor 0.040 | mainly night heating (set by night temperature); BDEW GGB | air 12–22 °C; heat pump ≤ 55 °C supply | gap |
| `office-law-firm` | Minergie limit 40 kWh/m²·a (low); below residential | summer factor 0.048 | BDEW GKO: Sat 0.886, Sun 0.944; When2Heat COM hourly | – | 9,254 law/notary offices CH, 2018 (low) |
| `retail-grocery` | TODO(source) | summer factor 0.027 (most weather-driven) | BDEW GHA: Sun 0.894 | – | gap |
| `laundry` | 1.6 kWh/kg average (12 large CH laundries), target 1.0 | summer factor 0.62 | BDEW GWA: Mon–Thu ≈ 1.25, Sat 0.39, Sun 0.46 | gap | gap |
| `bakery` (other) | – | summer factor 0.61 | BDEW GBA: Sat 0.49 | ovens far above server heat | gap |

### Observations for heat-led computing

1. **Year-round sinks exist, and some are low-temperature.** Indoor pools (water > 28 °C, all
   year, demand barely weather dependent), hotels (hot water 32–42 % of heat, 26 % of heat in
   Apr–Sep), laundries (summer day still 62 % of a cold day) and bakeries keep heat demand in
   summer, unlike homes (6 % of a cold day). These are the natural "summer job" sites in a
   neighbourhood (vision tier 3).
2. **Outdoor pools and hay drying are pure summer sinks.** In Switzerland outdoor pools may be
   heated only with renewable energy or otherwise unusable waste heat (MuKEn, cited by the VHF
   guide), so server heat could be a legal way to heat them; whether server heat counts as
   "otherwise unusable waste heat" is open. Hay ventilation needs only +6–10 K warm air, about
   12.5 kW per K per 100 m² box (≈ 100 kW for +8 K), exactly when homes need none.
3. **Storage decouples the daily hot-water peak.** Household hot water peaks at 06–07 h (2.3× the
   hourly mean, near zero at night), but the reference tank (35 kWh) holds ≈ 3 days of the case's
   hot water (12 kWh/day). In summer the daily shape barely matters; in winter (design load 10 kW,
   i.e. up to 240 kWh/day) the tank covers only a few hours, so the space-heating shape dominates.
4. **Weekly patterns matter for non-residential sites.** Laundries drop to 0.39 on Saturdays,
   offices to 0.89, shops to 0.89 on Sundays; households have none. A network can shift weekend
   jobs to homes, hotels and pools.
5. **Temperature excludes some sites.** Industrial maize/grass drum dryers run at 300–800 °C;
   server heat can at most preheat air (the Landi plant preheats to 85–92 °C with exhaust heat).
   Greenhouses (12–22 °C air) and pools are the opposite: easy temperatures.
6. **Grocery stores are poor heat hosts.** They have their own refrigeration waste heat
   (condenser heat recovery replaces oil), competing with server heat; they fit better as
   **compute customers** (e.g. CCTV), not as heat sinks.
7. **The case model is a special case of a smooth curve.** The BDEW household function gives
   6 % of winter demand on a 20 °C day and no hard cutoff; the case's 12 °C cutoff sets summer
   space heating to exactly 0 (0 % Jun–Aug, 11.7 % Apr–Sep for Basel 2025 vs 18.5 % Apr–Sep in
   When2Heat CH 2022). Results for "summer utilisation" depend on this choice; report it.

## Profile sources

| Source | What | Access | In repo |
|---|---|---|---|
| **When2Heat** (OPSD, 2023-07-27, CC BY 4.0) | hourly CH space/water heat profiles SFH, MFH, commercial, 2008–2022; heat-pump COP | open, 328 MB CSV | CH 2022 extract (0.96 MB), see [`SOURCE.md`](../../data/raw/sites/when2heat/SOURCE.md) |
| **BDEW/VKU/GEODE gas SLP guideline** (status 2026-03-27) | daily function `h(T)` (SigLinDe) and weekday factors for households and 11 commercial types | open PDF | coefficients in CSV (`slp_*` rows) |
| **SIA 2024 / SIA 385/2** | standard use: persons per m², litres hot water per person | paywalled | values via the open BFE report (2019) |
| **SIA 380/1** | heating demand limits, typical energy indicators per building category | paywalled | hotel values via the BFE hotel guide |
| **VDI 4655:2021-07** | reference load profiles SFH/MFH, typical days, 1-min / 15-min | paywalled | cited only |
| BFE/EnergieSchweiz guides (hotels, pools, greenhouses, laundries, drying, retail) | type-specific energy indicators | open (pubdb.bfe.admin.ch) | values in CSV |

For a weather-consistent simulation of any location: daily demand from the BDEW function with that
location's temperatures, distributed over hours with the When2Heat shape, scaled to the site's
annual demand. The case's linear model remains the scored default for the reference house.

## Gaps and blocked hosts

- **Blocked** (proxy or DNS): `www.bfs.admin.ch` (redirects to `statistik.admin.ch`, refused),
  `dam-api.bfs.admin.ch`, `data.bs.ch`, `www.bs.ch`, `www.minergie.ch`, `www.endk.ch`,
  `lfl.bayern.de`, `www.ktbl.de`, `umweltbundesamt.de`, `www.mdpi.com`. WebFetch could not resolve
  BFS at all. BFS and Basel counts therefore come from search snippets (`medium`/`low`).
  `www.statistik.bs.ch` is reachable but a JavaScript app (no tables via curl).
- **Missing values** (rows with `TODO(source)` in the CSV): yearly heat of outdoor pools and grocery
  stores, outdoor-pool season, hay-drying season and energy per litre evaporated, laundry process
  temperatures, hourly shapes for pools, hotels (only a commercial proxy), hay and grain drying.
- **Counts** still missing or weak: exact number of SFH (CH, Basel), laundries, greenhouses, hay
  ventilation systems, grocery stores, offices in Basel (BFS STATENT, NOGA 69.10 for law firms).
  Next step: BFS STAT-TAB / GWR extracts once `www.bfs.admin.ch` is allowed.
- **Absolute office/retail heat demand**: needs SIA 2024 or GEAK statistics (paywalled / blocked).
- **Compute need per site type** (CCTV cameras, documents) belongs to WP1 and is not covered here.
