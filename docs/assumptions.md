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
