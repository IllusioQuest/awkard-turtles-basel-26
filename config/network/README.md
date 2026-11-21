# Network scenarios

## `switzerland-test.json`: synthetic Swiss test network (T-12 step 3, decision 0025 §8)

**What it is:** 33 heat sites in 9 Swiss cities, used to test the network scheduler and to drive
the live map. It is **synthetic test data built from sourced building blocks**. It is not a market
study, and it does not change the scored reference house, which is one of its sites, unchanged.

Rebuild (offline, standard library, deterministic with seed 20251121):

```bash
python scripts/build_network_dataset.py
```

Outputs: this JSON, hourly demand files `data/derived/network/switzerland-test/<siteId>-demand.csv`
(`time,space_heat_kwh,hot_water_kwh,temperature_c`, 8,760 rows, 24 per local day) and
`site-totals.csv` (annual totals per site, for checking).

### Sites

| City (canton) | Sites |
|---|---|
| Basel (BS) | reference house (case), single-family house, apartment building, hotel with spa, indoor pool |
| Zurich (ZH), Bern (BE), Geneva (GE), Lugano (TI) | single-family house, apartment building, hotel with spa, indoor pool |
| Lausanne (VD), Lucerne (LU), St. Gallen (SG), Chur (GR) | single-family house, apartment building, hotel with spa |

### Where each value comes from

Every number in the JSON is either `{value, unit, source}` or `{value, unit, synthetic: true, note}`.
The map shows synthetic fields as such.

| Value | Source or rule |
|---|---|
| Weather per city | Open-Meteo archive 2025 at the geocoded city centre (`data/raw/open-meteo/SOURCE-network-2025.md`) |
| Reference house | the case, unchanged (`"model": "case-linear"`, `config/reference-house.json`) |
| Single-family house, space heat | case formula (10 kW design load, 20 °C, divisor 25.6, heating days < 12 °C) applied to the city's weather. The divisor stays the case's Basel constant, so colder cities slightly exceed 10 kW in their coldest hour |
| Single-family house, hot water | 4 persons (case household) × 50 m²/person × 13.5 kWh/(m²·a), SIA 2024 via BFE report 9686 = 2,700 kWh/a. Monthly and hourly shares from When2Heat CH SFH (`data/raw/sites/site-types.csv`). Lower than the case's 12 kWh/day; both are sourced |
| Apartment building, space heat | case formula with the case house's design load per m² (10 kW ÷ 200 m²) × floor area |
| Apartment building, hot water | residents × 30 m²/person × 19.8 kWh/(m²·a) (SIA 2024 via BFE). Hourly shares from When2Heat CH MFH, flat over the months |
| Hotel with spa | 95 kWh/(m²·a) heat (SIA 380/1 target, BFE hotel guide), 42 % hot water (new buildings, same guide). Space heat is shaped by the case formula on the city's weather. Hot water follows the When2Heat CH commercial hot-water profile (2022, aligned by month-day-hour; a **proxy**, no hotel-specific open profile) |
| Indoor pool | 627 MWh/a (Hallenbad Allschwil after renovation, BFE report 816), flat over the year (weakly weather dependent per BFE guide 9728; no open hourly profile). All pools are modelled as this example |
| Server, recovery, minimum load | case / `config/reference-house.json` (1 kW, 0.8, 0) |
| Heat value | electricity price ÷ 2.5 (case rule) |
| **Synthetic** | site positions (city centre + seeded offset up to about 1.5 km); building sizes (residents 16–40, hotel 1,500–4,000 m²); servers per site (= mean demand ÷ 0.8 kWh, sized for the year-round average); tanks (8.75 kWh per server, as the reference house); electricity price outside Basel (Basel IWB H4 as a proxy); Type 1 volume (30 % of heat-led server-hours), deadline (24 h), job size (1,000 LLM documents = 0.92 server-hours, from T-33); placement (country allowlist CH) |

### Known limits

- Hot-water and commercial shapes are When2Heat 2022 aligned to 2025 dates, so weekdays do not
  match.
- Pools are flat and identical.
- All prices are the Basel household tariff.
