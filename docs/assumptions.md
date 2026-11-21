# Assumptions

> **Interim solution.** This list exists so that every assumption is visible right now (a jury
> criterion). The project will become more general (any location, house, hardware, price), and
> parameters will then live in a central, machine-readable configuration that this list is
> generated from or replaced by. Until that exists, every new assumption goes here.

How to add an entry: one row per assumption, a stable ID, the value **with unit**, where it comes
from, and its status. Do not delete rows that are no longer used; mark them `replaced` and say by what.

Status: `case` = fixed by the case for the reference house · `sourced` = from an external source ·
`measured` = our own measurement · `ours` = our own choice / estimate · `replaced`.

| ID | Assumption | Value | Source | Status |
|---|---|---|---|---|
| A-001 | Electrical power per rack server | 1 kW | case (DigitalHeat) | case |
| A-002 | Heat produced per server | ≈ 1 kW (≈ all electrical power) | case | case |
| A-003 | Share of server heat recoverable into the water system | 80 % | case | case |
| A-004 | Recovered heat usable for space heating and hot water without a heat pump | yes (simplification) | case | case |
| A-005 | Location and weather | Basel 47.56 N 7.59 E, Open-Meteo 2025 hourly, Europe/Zurich | case; [`data/raw/open-meteo/`](../data/raw/open-meteo/SOURCE.md) | case |
| A-006 | Space-heating demand per hour | `max(0, 10 kW × (20 − T) ÷ 25.6)` on heating days | case | case |
| A-007 | Heating day | local day with mean of 24 hourly `temperature_2m` < 12 °C | case | case |
| A-008 | Hot-water demand | 12 kWh/day, all year | case | case |
| A-009 | Heat storage | 35 kWh usable, no losses | case | case |
| A-010 | Electricity price | 0.3325 CHF/kWh flat (ElCom 2026, IWB Basel, H4, total) | [`data/raw/elcom/`](../data/raw/elcom/SOURCE.md), retrieved 2026-11-20 | sourced |
| A-011 | Value of used heat | electricity price ÷ 2.5 = 0.133 CHF/kWh | case (heat pump efficiency 2.5) | case |
| A-012 | Heat not covered by servers | supplied by a backup heater outside the model | case | case |
| A-013 | Draft presentation baseline and comparison counts, not a chosen installation | 4 servers as draft headline; 1, 3, 5 and 13 as comparisons | 4 = explorer comparison in [`config/reference-house.json`](../config/reference-house.json), chosen for presentation by Jona/Codex after user delegation on 2026-11-21; 3 and 5 requested by the user; 1 and 13 derived from [`challenge.md`](case/challenge.md) | ours (presentation, not hardware selection) |
| A-014 | Minimum server load in the comparison | 0 fraction/server | [`config/reference-house.json`](../config/reference-house.json), sourced to the year explorer; permits zero heat dump under heat-led waiting | ours (comparison) |
| A-015 | Job supply for the current published year figures | unlimited immediately available jobs; missed deadlines undefined | [`challenge.md`](case/challenge.md) allows this if declared; [`config/job-scenarios/unlimited.json`](../config/job-scenarios/unlimited.json) | ours (comparison) |
| A-016 | Computing income rate in the current published year figures | CHF 0.30/server-hour | explorer slider scenario in [`config/reference-house.json`](../config/reference-house.json); no verified customer contract or job demand | ours (illustrative) |
| A-017 | Hardware-only cost sensitivity, not selected hardware or full cost | Dell R570 CHF 8,248.03/server divided by Microsoft accounting life 6 years | WP2 rows `cost-server-dell-r570` and `lifetime-microsoft-server-accounting` in [`costs.csv`](../data/raw/market/costs.csv), accessed 2026-11-20; user approved the clearly labelled example on 2026-11-21; air-cooled product equivalence unverified | ours (illustrative) |
| A-018 | Filler fill rate (economics, T-14) | 0.35 fraction of offered server-hours (range 0.2–0.6) | [`filler-demand.md`](research/filler-demand.md) §6; [`config/economics/reference-house.json`](../config/economics/reference-house.json) | sourced estimate |
| A-019 | Premium (Type 1) share of server capacity | 0 in the base case; swept 0–1 and reported as the required share | `TODO(source)`: no sourced premium demand ([`job-demand.md`](research/job-demand.md)) | ours (sweep) |
| A-020 | Premium price, L4 profile | CHF 3.55/server-hour (Infomaniak L4 on-demand CHF 0.3053/h × 11.63 cards) | `cp-ch-infomaniak-l4` in [`compute-prices.csv`](../data/raw/market/compute-prices.csv) | sourced (medium) |
| A-021 | Filler price, L4 profile | CHF 1.42/server-hour (range 0.85–2.20) | [`filler-demand.md`](research/filler-demand.md) §6 | sourced estimate |
| A-022 | Heat fee paid by the host (operator model) | CHF 0/kWh (free heat) | market practice, [`competitors.md`](research/competitors.md) §4 lesson 2 | sourced (practice) |
| A-023 | Platform commission (host-owned variant) | 0.15 of computing income | vast.ai host share ≈ 85 %, [`filler-demand.md`](research/filler-demand.md) §2 (low) | sourced (low) |
| A-024 | Wear/replacement and installation | 0 (optimistic placeholders) | `TODO(source)`: T-35 (wear), installation bill of materials | ours (placeholder) |
| A-025 | Gas / oil boiler efficiency | 0.9 kWh heat/kWh fuel | KTBL heating-oil sheet (90–95 %), via search summary (low) | sourced (low) |
| A-026 | Unsold server-hours | servers off, no idle draw (GPU idle power `TODO(source)`) | ours, decision 0028 | ours |
