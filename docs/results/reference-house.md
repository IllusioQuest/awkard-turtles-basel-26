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
| Missed deadlines | undefined: unlimited jobs | undefined: unlimited jobs | undefined: unlimited jobs |
| Electricity cost, CHF | 5,911 | 7,535 | 9,069 |
| Value of used heat, CHF | 1,892 | 2,411 | 2,902 |
| Computing income at adopted CHF 0.66/server-hour, CHF | 11,734 | 14,956 | 18,002 |
| Case-only balance after electricity and heat value, CHF | +7,714 | +9,833 | +11,835 |
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

**Finite-demand check, kept separate:** the sourced Basel medical-dictation scenario for the same
4-server house has 156,220 jobs due within 2025, all completed, **0% missed deadlines**, 38.79
server-hours, 0.11% utilisation, 0.10% heat coverage and zero dumped heat. These values come from
the same C# year model with [`transcription-basel.json`](../../config/job-scenarios/transcription-basel.json)
and are exported as the [finite-only diagnostic](../../data/derived/reference-house/transcription-basel-finite-only.json).
Its demand and throughput assumptions are in [T-26 research](../research/job-demand.md). It is
**not** the agreed premium-plus-filler model: that combined queue is T-31. The generic CHF 0.66/h
income in the JSON is not a quoted transcription-service tariff.

## How to read the money

The **scored case formula** is `(electricity cost − value of heat actually used) / sold
server-hours`. For 4 servers: `(CHF 7,534.79 − CHF 2,411.13) / 22,661.03 = CHF 0.2261/h`.
Heat value uses only heat delivered to the house at `CHF 0.3325 / 2.5 = CHF 0.133/kWh`.
The team-adopted CHF 0.66 computing price is anchored to a dedicated-server comparison in
[`computing-price.md`](../research/computing-price.md) A3, **not a verified customer price**.
Its CHF 14,956.28 income is `22,661.03 × 0.66` and assumes every offered hour is sold.
The case-only balance is `14,956.28 + 2,411.13 − 7,534.79 = CHF 9,832.62` before fixed costs.
The versioned T-13 `reference-house-v1` bundle still records the earlier CHF 0.30/h input and
must not be used for this income figure; its physical heat trace is unchanged. Use the updated
T-06 JSON files for the current money figures. T-25 uses only the bundle's physical heat trace.

The case break-even omits hardware, heating integration and operations. The true full-cost
break-even is `(electricity − used-heat value + annual hardware depreciation + annual operations
+ annualized installation) / sold server-hours`; it is **unknown** because a compatible,
installed heat-recovering server and its ongoing costs have not been sourced. The user approved
one clearly labelled example: the [Dell R570 offer](../../data/raw/market/costs.csv) is CHF
8,248.03 **per physical unit**, while the case server is 1 kW. The
[`cpu-server` profile](../../config/hardware/cpu-server.json) estimates 0.1625 kW per R570 from
CPU TDP plus memory power. Thus `1 / 0.1625 = 6.1538` units and about CHF 50,757 purchase cost
per case-equivalent 1 kW server. Fractional units are a cost normalization, not an installation
plan. Using the separate six-year accounting-life observation, four case servers imply
`4 × 50,757.11 / 6 = CHF 33,838.07/year` straight-line depreciation. The WP2 source rows are
`cost-server-dell-r570` and `lifetime-microsoft-server-accounting`, accessed 2026-11-20.
The power estimate is **not measured wall power** (fans, storage and PSU losses omitted); the
air-cooled Dell has not been shown to recover heat like the case's server. The life is another
company's accounting estimate. VAT basis, installation and operations remain open.

| Illustrative hardware-only sensitivity | 3 servers | 4 servers | 5 servers |
|---|---:|---:|---:|
| Annual depreciation, CHF | 25,379 | 33,838 | 42,298 |
| Break-even incl. this depreciation only, CHF/server-hour | 1.6536 | 1.7193 | 1.7769 |
| Combined surplus at CHF 0.66/h after this depreciation only, CHF/year | −17,664 | −24,005 | −30,463 |

Under this **non-equivalent hardware illustration**, CHF 0.66/h would not cover even the shown
depreciation. For four servers, `0.2261 + 33,838.07 / 22,661.03 = CHF 1.7193/h` before
installation and operations. In the case-only balance, a computing price below break-even fails
before fixed costs; lower usable heat or fewer sold hours also worsen the result. A positive combined
surplus would still need a host/operator split that benefits both. The backup heater remains
necessary, so host savings concern avoided running heat cost rather than its whole investment.
See [WP2](../research/costs-and-energy.md) for the missing cost categories.

## Reproduce and complete

Run `python scripts/export_reference_house.py` with the .NET 8 SDK installed. The script copies the
committed raw weather, ElCom snapshot and job scenarios into temporary roots, varies only the
sourced server count for the unlimited comparisons, invokes `DigitalHeat.YearRun`, and writes
five comparison JSON files plus the finite-only diagnostic. Its exact variant inputs are in
[`config/reference-house-results.json`](../../config/reference-house-results.json). Repeating
the command produced identical SHA-256 hashes for all six outputs. The year model and the
JavaScript explorer agree on the 4-server comparison (58.0%, zero dump, 22,661 server-hours,
CHF 0.2261/h).

**Open for the scored draft:** T-31's combined premium-plus-filler queue and its missed-deadline
rate (the unlimited comparison cannot yield one); proof that unlimited filler hours can actually
be sold; a comparable complete hardware and heating-system quote; operations/insurance; the team's
actual server deployment decision; and the chosen
host/operator payment split. Do not present this page as a full-cost profitability claim.
