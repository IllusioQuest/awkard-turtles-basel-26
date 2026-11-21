# Reference house: year balance for the draft

**Draft presentation baseline: 4 servers.** The user delegated this choice on 2026-11-21;
we use 4 because the C# core and independent JavaScript explorer both reproduce it. This is
**not an optimized server count or a hardware purchase decision**. Basel 2025 hourly weather and the
case's fixed house yield 206 heating days, 26,850.55 kWh space heat plus 4,380 kWh hot water,
or **31,230.55 kWh heat demand**. The case uses a 1 kW electric server, 80% recoverable heat,
a lossless 35 kWh tank and the ElCom IWB Basel H4 2026 price of CHF 0.3325/kWh. Sources and
units are in [`config/reference-house.json`](../../config/reference-house.json),
[`docs/assumptions.md`](../assumptions.md), the [weather source](../../data/raw/open-meteo/SOURCE.md)
and the [ElCom snapshot](../../data/raw/elcom/SOURCE.md).

| Year result | 3 servers | **4 servers: draft baseline** | 5 servers |
|---|---:|---:|---:|
| Heat demand covered by server heat | 45.5% | 58.0% | 69.9% |
| Backup heat, kWh | 17,007 | 13,102 | 9,410 |
| Heat dumped, kWh | 0 | 0 | 0 |
| Utilisation of installed server-hours | 67.7% | 64.7% | 62.3% |
| Full-load-equivalent server-hours (assumed sold) | 17,779 | 22,661 | 27,275 |
| Missed deadlines | undefined | undefined | undefined |
| Electricity cost, CHF | 5,911 | 7,535 | 9,069 |
| Value of used heat, CHF | 1,892 | 2,411 | 2,902 |
| Computing income at illustrative CHF 0.30/server-hour, CHF | 5,334 | 6,798 | 8,183 |
| **Case break-even, CHF/server-hour** | **0.2261** | **0.2261** | **0.2261** |

The user asked to compare 3 and 5; 4 is the existing explorer scenario and a regression check,
**not an optimum**. All three remain visible, and none is approved as the installation. Exact values:
[3 servers](../../data/derived/reference-house/three-server-comparison.json),
[4 servers](../../data/derived/reference-house/explorer-default.json),
[5 servers](../../data/derived/reference-house/five-server-comparison.json).
The case-derived sizing bounds are also exported: [1 server](../../data/derived/reference-house/summer-hot-water-minimum.json)
covers 19.5% at 87.1% utilisation; [13 servers](../../data/derived/reference-house/coldest-hour-capacity.json)
cover 100% at 34.3% utilisation. The 13-server count rounds up the case's cold-hour
**space-heating** requirement of 12.5 servers; hot water is additional and the tank bridges
short gaps. About 35 kWh remain in that tank at year end and are not credited as used heat.

## How to read the money

The **scored case formula** is `(electricity cost − value of heat actually used) / sold
server-hours`. For 4 servers: `(CHF 7,534.79 − CHF 2,411.13) / 22,661.03 = CHF 0.2261/h`.
Heat value uses only heat delivered to the house at `CHF 0.3325 / 2.5 = CHF 0.133/kWh`.
The CHF 0.30 computing price is an explorer slider setting, **not a verified customer price**;
its income assumes unlimited billable work and no missed jobs.

The case break-even omits hardware, heating integration and operations. The true full-cost
break-even is `(electricity − used-heat value + annual hardware depreciation + annual operations
+ annualized installation) / sold server-hours`; it is **unknown** because a compatible,
installed heat-recovering server and its ongoing costs have not been sourced. The user approved
one clearly labelled example: as a sensitivity only, a
[Dell R570 offer](../../data/raw/market/costs.csv) at CHF 8,248.03/server and a separate
[six-year accounting-life observation](../../data/raw/market/costs.csv) would add the following
annual straight-line depreciation. These are WP2 rows `cost-server-dell-r570` and
`lifetime-microsoft-server-accounting`, accessed 2026-11-20. The air-cooled Dell product is **not
verified as equivalent** to the case's heat-recovering 1 kW server; the life is another company's
accounting estimate. VAT basis, installation and operations remain open.

| Illustrative hardware-only sensitivity | 3 servers | 4 servers | 5 servers |
|---|---:|---:|---:|
| Annual depreciation, CHF | 4,124 | 5,499 | 6,873 |
| Break-even incl. this depreciation only, CHF/server-hour | 0.4581 | 0.4687 | 0.4781 |
| Combined surplus at CHF 0.30/h after this depreciation only, CHF/year | −2,810 | −3,824 | −4,858 |

Under this **non-equivalent hardware illustration**, CHF 0.30/h would not cover even the shown
depreciation. In the case-only balance, a computing price below the case break-even fails before
fixed costs; lower usable heat or fewer sold hours also worsen the result. A positive combined
surplus would still need a host/operator split that benefits both. The backup heater remains
necessary, so host savings concern avoided running heat cost rather than its whole investment.
See [WP2](../research/costs-and-energy.md) for the missing cost categories.

## Reproduce and complete

Run `python scripts/export_reference_house.py` with .NET 8 installed. The script copies the
committed raw weather, ElCom snapshot and unlimited-job scenario into temporary roots, varies
only the sourced server count, invokes `DigitalHeat.YearRun` for each, and writes five small
JSON files. Its exact variant inputs are in
[`config/reference-house-results.json`](../../config/reference-house-results.json). Repeating
the command produced identical SHA-256 hashes for all five outputs. The year model and the
JavaScript explorer agree on the 4-server comparison (58.0%, zero dump, 22,661 server-hours,
CHF 0.2261/h).

**Open for the scored draft:** sourced T-01/T-02 arrivals,
throughput and deadlines (the present unlimited scenario cannot yield a missed-deadline rate);
a comparable complete hardware and heating-system quote; operations/insurance; the team's actual
server deployment decision; and the chosen
host/operator payment split. Do not present this page as a full-cost profitability claim.
