# WP2 — Costs and energy prices

Owner: Codex (GPT-6 Astra). Status: **first version**, collected 2026-11-20.

The snapshot has sourced observations in every WP2 category. It supports cost
comparisons, but is not a quotation for a heat-producing server installation.
The decisive gaps are measured whole-server power, complete hydraulic integration,
insurance and workload-specific economic lifetime. No hardware, supplier, depreciation
policy or economic model has been selected.

Data: [`costs.csv`](../../data/raw/market/costs.csv),
[`energy-prices.csv`](../../data/raw/market/energy-prices.csv).
The tables below cite row IDs (or ID families); those rows contain exact source URLs,
access dates, conditions and confidence. Retrieval queries, licences and unit rules
are in [`SOURCE.md`](../../data/raw/market/SOURCE.md).

## Method and comparability

We used official agencies, manufacturers, Swiss retailers and service providers.
Published indicative ranges remain ranges, with separate endpoint rows; we have not
estimated missing values. A blank currency means a physical or time observation.
Power ratings, accounting lives and tariff eligibility are supporting observations,
not additional costs. Prices are dated snapshots; no runtime fetching is required.

All monetary costs and fuel/retail observations were published in CHF. The hourly
spot example is EUR/MWh. Its conversion uses **1 EUR = 0.9232 CHF**, dated
**2026-11-20**, from the [SNB](https://www.snb.ch/en/srv/current_interest_exchange_rates),
and divides by 1000 to obtain CHF/kWh. The FX source and factor are also in every
affected `spot-ch-2025-01-15-*` row. This common comparison rate is not historical FX.
Annual SwissIX averages are already CHF. Rappen and per-hundred-litre conversions
divide by 100. No fuel-to-useful-heat conversion is made without efficiency data.

VAT treatment differs: ElCom is net, some supplier prices are gross, others do not
explicitly state it. Preserve the row's tax basis before comparing or summing costs.
The reference tariff remains the case's published ElCom value; this research does
not change scored assumptions. Retail tariffs, wholesale prices and PV compensation
serve different purposes and cannot be substituted for one another.

## Hardware-server

| Purchase observation | CHF | Power evidence | Row IDs |
|---|---:|---|---|
| Dell PowerEdge R570 CPU rack server | 8,335.43 | 1,100 W PSU capacity only | `cost-server-dell-r570`, `power-server-dell-r570-psu` |
| PcCom complete RTX 4090 desktop | 9,757 | GPU TGP 450 W; system draw missing | `cost-gpu-pccom-rtx4090-system`, `power-gpu-nvidia-rtx4090-tgp` |
| brentford S240 GPU rack server, advertised starting price | 28,720 | 3,000 W redundant PSU rating only | `cost-server-brentford-s240-from`, `power-server-brentford-s240-psu` |
| PNY RTX 6000 Ada card only | 7,538 | 300 W board specification | `cost-gpu-pny-rtx6000-ada`, `power-gpu-pny-rtx6000-ada` |

These are CPU/GPU purchase anchors, not demonstrated equivalents of the case server.
The desktop is a consumer alternative; the card excludes its host system. The
brentford starting configuration needs a binding configured quotation. None of the
power ratings is a measured workload draw; redundant PSU capacities must not be
summed to estimate consumption. Liquid heat recovery is not included.

## Hardware-lifetime

| Published accounting observation | Years | Row ID |
|---|---:|---|
| Microsoft server/network estimated useful life, effective fiscal 2023 | 6 | `lifetime-microsoft-server-accounting` |
| CoreWeave technology-equipment useful life, annual report 2025 | 6 | `lifetime-coreweave-technology-accounting` |

These are company accounting estimates, not warranties or physical GPU lifetime.
They do not establish DigitalHeat's depreciation period, residual value or future
competitiveness. A GPU-only economic or physical lifetime remains `TODO(source)`.

## Installation

| Component observation | CHF | Scope | Row ID |
|---|---:|---|---|
| WilTec plate heat exchanger | 44.99 | Component only | `cost-install-wiltec-exchanger` |
| Alphacool GPU water block | 420 | Component only | `cost-install-alphacool-gpu-block` |

These components do not form a compatible or sized bill of materials. Complete
liquid cooling, pumps, valves, controls, pipework, electrical work, commissioning and
heating integration remain `TODO(source)`. Do not present their sum as installation cost.

## Operations

| Published service | CHF | Basis | Row ID |
|---|---:|---|---|
| Green Business Connect S | from 69.90 | site/month; address eligibility | `cost-ops-green-connect-s` |
| BIG IT basic monitoring | from 20 | first server/month, excluding VAT | `cost-ops-bigit-monitoring-base` |
| BIG IT setup | from 75 | one-time, excluding VAT | `cost-ops-bigit-monitoring-setup` |
| Smart Solution Server Basic | 59 | server/month, VAT included | `cost-ops-smartsolution-server-basic` |
| Smart Solution firmware service | 390 | annual service, VAT included | `cost-ops-smartsolution-firmware` |
| brentford repair/installation labour | 160 | hour, VAT included; travel/materials extra | `cost-ops-brentford-labour` |

Monitoring and managed-service bundles overlap. Service rates do not establish labour
hours or annual incident frequency. Insurance and heat-recovery maintenance remain
`TODO(source)`. BIG IT states a Swiss monitoring-server location; other remote-support
processing locations are unverified. A Swiss sales address is not a sovereignty guarantee.

## Heat-alternative

| Published guide observation | Value | Row IDs |
|---|---:|---|
| Air-water heat pump installed investment | CHF 45,000–60,000 | `heat-es-air-water-capex-lower/upper` |
| Ground-source heat pump installed investment | CHF 55,000–80,000 | `heat-es-ground-water-capex-lower/upper` |
| Air-water electricity expense | CHF 1,000–1,600/year | `heat-es-air-water-electricity-lower/upper` |
| Ground-source electricity expense | CHF 800–1,400/year | `heat-es-ground-water-electricity-lower/upper` |
| Air-water annual performance factor, Aargau guidance | 2.5–3.0 | `heat-ag-air-water-jaz-lower/upper` |
| Ground-source achieved annual factor, EnergieSchweiz | 4.6 | `heat-es-ground-water-jaz` |
| Oil / gas installed boiler investment | CHF 27,000 / 15,000 | `heat-vaillant-oil-installed-capex`, `heat-vaillant-gas-installed-capex` |
| Oil / gas annual fuel expense guides | CHF 2,500 / 3,500 | `heat-vaillant-oil-energy`, `heat-vaillant-gas-energy` |
| Oil / gas maintenance upper guides | CHF 1,000/year each | `heat-vaillant-oil-maintenance`, `heat-vaillant-gas-maintenance` |
| Air-water maintenance upper guide | CHF 500/year | `heat-vaillant-air-water-maintenance` |

EnergieSchweiz gives indicative investment and electricity-expense ranges. Vaillant's
boiler comparison uses an illustrative house; it is not the case house, and its
table differs from some surrounding prose. The CSV identifies the table and its
context. Annual performance factors include seasonal effects and differ from a
test-point COP. The reference case's efficiency remains unchanged.

No quoted annual guide expense should be added to a separately calculated energy
bill. Site-specific sizing, taxes, subsidies and actual seasonal performance need
verification. A backup heater remains necessary, so server heat normally displaces
running energy expense rather than the entire heater investment. Boiler observations
are comparisons, not a claim that new fossil installations are permitted in Basel.

## Electricity-retail

Basel H4 **standard** history uses raw ElCom LINDAS totals, excluding VAT. Every year
below has a row `retail-iwb-basel-h4-YYYY`. The complete unfiltered source series begins
in 2011; earlier observations are absent. The already published 2027 tariff is shown
as prospective and does not replace the case's 2026 anchor.

| Year | Rp./kWh excl. VAT | CHF/kWh excl. VAT |
|---|---:|---:|
| 2011 | 23.112 | 0.23112 |
| 2012 | 23.156 | 0.23156 |
| 2013 | 22.847 | 0.22847 |
| 2014 | 24.133 | 0.24133 |
| 2015 | 25.223 | 0.25223 |
| 2016 | 25.957 | 0.25957 |
| 2017 | 26.551 | 0.26551 |
| 2018 | 27.911 | 0.27911 |
| 2019 | 27.831 | 0.27831 |
| 2020 | 27.647 | 0.27647 |
| 2021 | 27.647 | 0.27647 |
| 2022 | 28.406 | 0.28406 |
| 2023 | 31.722 | 0.31722 |
| 2024 | 33.976 | 0.33976 |
| 2025 | 33.856 | 0.33856 |
| 2026 | 33.250 | 0.3325 |
| 2027 (prospective) | 33.420 | 0.3342 |

| Standard H4 comparison, 2026 | Rp./kWh excl. VAT | Row ID |
|---|---:|---|
| EKZ, Dietikon | 24.136 | `retail-elcom-486-h4-2026` |
| Energie Wasser Bern, Bern | 33.260 | `retail-elcom-519-h4-2026` |
| ewz, Zürich | 24.758 | `retail-elcom-565-h4-2026` |
| St.Galler Stadtwerke, St. Gallen | 30.590 | `retail-elcom-672-h4-2026` |

Product selection was checked against the LINDAS product dimension; cheaper-product
rows are excluded. H4 comparisons do not establish the marginal electricity tariff
of a continuously operating server. Obtain the actual contract and charge breakdown
before using these observations in a full-cost installation budget.

## Electricity-spot

| Observation | Published value | Normalised value | Row IDs |
|---|---:|---:|---|
| SwissIX annual mean 2024, ElCom | 7.6 Rp./kWh | CHF 0.076/kWh | `spot-swissix-mean-2024` |
| SwissIX annual mean 2025, ElCom | 10.2 Rp./kWh | CHF 0.102/kWh | `spot-swissix-mean-2025` |
| CH day-ahead example, 2025-01-15 00:00 +01:00 | EUR 123.19/MWh | CHF 0.11372901/kWh | `spot-ch-2025-01-15-00` |

The CSV contains the entire hourly winter-day example, with timezone offsets and
source timestamps. Annual means are the regulator's rounded published figures, not
derived from this example. The profile comes from Bundesnetzagentur/SMARD through
Fraunhofer ISE Energy-Charts under CC BY 4.0. Wholesale prices omit retail delivery
costs; no access to a wholesale supply arrangement is assumed.

## Fuel-heating

| Published observation | Value | Row ID / family |
|---|---:|---|
| Swiss oil average, September 2026 | CHF 153.94/100 litres = CHF 1.5394/litre | `fuel-bazg-heating-oil-sep-2026` |
| Swiss household gas average, September 2026 | 13.98 Rp./kWh fuel | `fuel-bazg-natural-gas-sep-2026` |
| Federal CO₂ levy | CHF 120/tonne CO₂ | `fuel-co2-levy-per-tonne` |
| Oil / gas levy before VAT | CHF 31.80/100 litres / 2.158 Rp./kWh | `fuel-co2-heating-oil-2026`, `fuel-co2-natural-gas-2026` |
| IWB gas M energy / CO₂ / BS concession, VAT included | 11.57 / 2.333 / 0.11 Rp./kWh | `fuel-iwb-gas-m-energy-2026`, `fuel-iwb-gas-m-co2-2026`, `fuel-iwb-gas-m-concession-2026` |
| IWB gas M annual capacity charge / annual minimum | CHF 12.43/kW / CHF 194.58 | `fuel-iwb-gas-m-capacity-2026`, `fuel-iwb-gas-m-minimum-2026` |

BAZG republishes BFS absolute average fuel prices, including VAT; these are not price
indices. Purchase-quantity/profile conditions are in the CSV. CO₂ levy components
are already contained in all-in prices. IWB's capacity minimum replaces a smaller
capacity charge; it is not an additional fee. Fuel input energy differs from useful
heat; boiler efficiency and oil calorific basis remain `TODO(source)`.

## Feed-in

| Basel PV observation | Value | Row ID |
|---|---:|---|
| Cantonal remuneration, eligible plants up to 100 kW | 14 Rp./kWh exported | `feed-in-iwb-bs-small` |
| Same cohort, above 100 kW | 11 Rp./kWh exported | `feed-in-iwb-bs-large` |
| Cantonal entitlement duration | 12 years from commissioning | `feed-in-iwb-bs-entitlement-years` |
| After cantonal entitlement, below 30 kWp | minimum 6 Rp./kWh | `feed-in-iwb-post-entitlement-minimum-small` |

The cantonal rows concern commissioning from October 2017 and include VAT, energy,
HKN and cantonal top-up. Eligibility and remaining entitlement must be checked.
Afterward the national reference-price mechanism applies; its floor is not a flat
annual tariff. VAT/HKN treatment of that floor remains unresolved. Export compensation
is the revenue forgone when surplus PV is consumed locally; PV is not automatically
free electricity for the computing operator.

## Costs omitted by the case break-even

The [case formula](../case/challenge.md) subtracts the value of heat actually used
from electricity cost and divides by server-hours sold. It does not recover server
and GPU depreciation, replacement/residual-value risk, cooling and hydraulic
installation, commissioning, internet, monitoring, maintenance labour, repairs,
insurance, administration or financing. Any additional pump/control electricity
also needs a measured boundary; it must not be silently hidden inside server power.

For the [vision's system surplus](../vision.md#who-must-win), these collected costs
must eventually accompany computing income and heat actually displaced. Low summer
utilisation increases the burden of fixed costs on sold computing hours; a purchase
price or an accounting lifetime alone cannot establish profitability. Host payments
to DigitalHeat divide the system surplus and are internal transfers when computing
the combined result. Actual cost allocation and demand remain team/model decisions.

The host's heat-pump comparison should distinguish avoidable running expense from
the investment and standing maintenance of the backup system still required. Show
the scored case result and a later full-cost result separately, with a consistent
VAT basis. This research implements neither calculation and changes no case values.

## Remaining gaps and handoff

- `TODO(source)`: a fully specified CPU/GPU system with measured wall power under the
  selected workloads, including idle and paused states; the sampled offers have no
  demonstrated equivalence to the reference server.
- `TODO(source)`: a complete, compatible liquid-cooling and heating integration quote,
  installed labour and actual auxiliary energy demand.
- `TODO(source)`: insurance, mechanical heat-loop maintenance, incident frequency,
  replacement costs, resale value and GPU-specific economic/physical lifetime.
- `TODO(source)`: site-specific heating quotations, seasonal efficiency, useful-heat
  fuel conversion and applicable supply-contract fixed/variable costs.
- `TODO(source)`: unresolved supplier VAT/shipping/contract conditions, remote-support
  processing geography and the post-entitlement PV minimum's VAT/HKN treatment.

Coverage criterion: every category has a sourced numeric observation, the full
available Basel standard-tariff history is present, and omitted break-even costs are
listed. Installation is component evidence only, and hardware power comparability
is incomplete; **first version** must not be read as a complete investment dataset.

Validation: exact CSV header/order, unique IDs, numeric fields, mandatory provenance,
category coverage, unit/FX conversions, source-history coverage and hourly timestamps
were checked. `python scripts/fetch_data.py --check` passed for the existing case
data. No application code, model or selected assumption was introduced.
