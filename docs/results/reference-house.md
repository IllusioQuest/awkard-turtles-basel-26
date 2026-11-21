# Reference house: year balance for the draft

**Status: comparison variants, not a team-selected design.** Basel 2025 hourly weather and the
case's fixed house yield 206 heating days, 26,850.55 kWh space heat plus 4,380 kWh hot water,
or **31,230.55 kWh heat demand**. The case uses a 1 kW electric server, 80% recoverable heat,
a lossless 35 kWh tank and the ElCom IWB Basel H4 2026 price of CHF 0.3325/kWh. Sources and
units are in [`config/reference-house.json`](../../config/reference-house.json),
[`docs/assumptions.md`](../assumptions.md), the [weather source](../../data/raw/open-meteo/SOURCE.md)
and the [ElCom snapshot](../../data/raw/elcom/SOURCE.md).

| Year result | 1 server: summer hot-water sizing | 4 servers: explorer comparison | 13 servers: cold-hour sizing |
|---|---:|---:|---:|
| Heat demand covered by server heat | 19.5% | 58.0% | 100.0% |
| Backup heat, kWh | 25,126 | 13,102 | 0 |
| Heat dumped, kWh | 0 | 0 | 0* |
| Utilisation of installed server-hours | 87.1% | 64.7% | 34.3% |
| Full-load-equivalent server-hours (assumed sold) | 7,631 | 22,661 | 39,082 |
| Missed deadlines | undefined | undefined | undefined |
| Electricity cost, CHF | 2,537 | 7,535 | 12,995 |
| Value of used heat, CHF | 812 | 2,411 | 4,154 |
| Computing income at illustrative CHF 0.30/server-hour, CHF | 2,289 | 6,798 | 11,725 |
| **Case break-even, CHF/server-hour** | **0.2261** | **0.2261** | **0.2262** |

*With 13 servers, approximately 35 kWh remain in the tank at year end. This heat is not credited
as used, which raises its break-even slightly despite essentially zero dumped heat. The 1-server
and 13-server counts are rounded from the case's summer 0.6-server and coldest-hour 12.5-server
calculations; 4 servers is the existing explorer scenario. None is approved as the team's chosen
installation. The 12.5-server figure covers cold-hour **space heating**; hot water is additional
and the tank bridges short gaps. Exact values: [1 server](../../data/derived/reference-house/summer-hot-water-minimum.json),
[4 servers](../../data/derived/reference-house/explorer-default.json),
[13 servers](../../data/derived/reference-house/coldest-hour-capacity.json).

## How to read the money

The **scored case formula** is `(electricity cost − value of heat actually used) / sold
server-hours`. For 4 servers: `(CHF 7,534.79 − CHF 2,411.13) / 22,661.03 = CHF 0.2261/h`.
Heat value uses only heat delivered to the house at `CHF 0.3325 / 2.5 = CHF 0.133/kWh`.
The CHF 0.30 computing price is an explorer slider setting, **not a verified customer price**;
its income assumes unlimited billable work and no missed jobs.

The case break-even omits hardware, heating integration and operations. The true full-cost
break-even is `(electricity − used-heat value + annual hardware depreciation + annual operations
+ annualized installation) / sold server-hours`; it is **unknown** because a compatible,
installed heat-recovering server and its ongoing costs have not been sourced. As a sensitivity
only, a [Dell R570 offer](../../data/raw/market/costs.csv) at CHF 8,248.03/server and a separate
[six-year accounting-life observation](../../data/raw/market/costs.csv) would add the following
annual straight-line depreciation. These are WP2 rows `cost-server-dell-r570` and
`lifetime-microsoft-server-accounting`, accessed 2026-11-20. The air-cooled Dell product is **not
verified as equivalent** to the case's heat-recovering 1 kW server; the life is another company's
accounting estimate. VAT basis, installation and operations remain open.

| Illustrative hardware-only sensitivity | 1 server | 4 servers | 13 servers |
|---|---:|---:|---:|
| Annual depreciation, CHF | 1,375 | 5,499 | 17,871 |
| Break-even incl. this depreciation only, CHF/server-hour | 0.4062 | 0.4687 | 0.6835 |
| Combined surplus at CHF 0.30/h after this depreciation only, CHF/year | −811 | −3,824 | −14,987 |

Under this **non-equivalent hardware illustration**, CHF 0.30/h would not cover even the shown
depreciation. In the case-only balance, a computing price below the case break-even fails before
fixed costs; lower usable heat or fewer sold hours also worsen the result. A positive combined
surplus would still need a host/operator split that benefits both. The backup heater remains
necessary, so host savings concern avoided running heat cost rather than its whole investment.
See [WP2](../research/costs-and-energy.md) for the missing cost categories.

## Reproduce and complete

Run `python scripts/export_reference_house.py` with .NET 8 installed. The script copies the
committed raw weather, ElCom snapshot and unlimited-job scenario into temporary roots, varies
only the sourced server count, invokes `DigitalHeat.YearRun` for each, and writes the three small
JSON files above. Its exact variant inputs are in
[`config/reference-house-results.json`](../../config/reference-house-results.json). Repeating
the command produced identical SHA-256 hashes for all three outputs. The year model and the
JavaScript explorer agree on the 4-server comparison (58.0%, zero dump, 22,661 server-hours,
CHF 0.2261/h).

**Open for the scored draft:** team-selected server count; sourced T-01/T-02 arrivals,
throughput and deadlines (the present unlimited scenario cannot yield a missed-deadline rate);
a comparable complete hardware and heating-system quote; operations/insurance; and the chosen
host/operator payment split. Do not present this page as a full-cost profitability claim.
