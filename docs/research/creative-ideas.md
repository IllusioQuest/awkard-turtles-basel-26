# Creative ideas: making residential houses pay off

> **Status: brainstorm with a strict rating, exploration only** (Claude subagent, 2026-11-21). Not
> reviewed or confirmed by the team. Nothing here changes a scored figure: the reference house stays
> as in [`../case/challenge.md`](../case/challenge.md); every idea below is a **variant**.
>
> **How sources were read.** Web searches only; facts come from **search-result snippets** of the
> named pages (all accessed 2026-11-21), not from fully opened pages. Each must be checked on the
> full page before it is used in the pitch. Our own assumptions are marked **[A]**; missing numbers
> are `TODO(source)`.
>
> Ideas already in [`service-brainstorm.md`](service-brainstorm.md), [`competitors.md`](competitors.md)
> and [`../vision.md`](../vision.md) (confidential computing, Alp-Cloud, heat-labelled compute,
> thermal-aware pricing, summer routing, refurbished hardware as such) are not repeated; where an
> idea here builds on one of them, it says so.

## 1. Starting point (our numbers)

Per 1 kW server-hour: electricity CHF 0.3325, heat value if used CHF 0.106
([`../case/key-figures.md`](../case/key-figures.md)). L4 profile: purchase CHF 34,244 per kW,
depreciation CHF 1.007 + operations 0.125 per sold hour at 5,665 sold h/yr, full-cost break-even
**CHF 1.36** ([`hardware.md`](hardware.md)). Filler marketplaces fill ≈ 35 % of offered hours
([`filler-demand.md`](filler-demand.md)); sourced premium demand fills < 1 %
([`job-demand.md`](job-demand.md)). Competitors failed in single homes because of seasonality and
fixed costs per site ([`competitors.md`](competitors.md) §4).

So a residential idea only matters if it does one of four things: **raise sold hours per server**,
**cut hardware cost per sold hour**, **lower the marginal electricity price**, or **add a second
income that does not need compute buyers**.

## 2. All ideas, rated

Scores 1 (bad) – 5 (good): **P** profit impact for houses (host + DigitalHeat), **F** feasibility in
Switzerland, **E** evidence available now, **D** demo-ability in a 5-minute pitch, **N** novelty vs.
competitors and our own earlier notes. Strict rater: a 5 needs a mechanism *and* a source.

| # | Idea (one line) | P | F | E | D | N | Σ |
|---|---|---|---|---|---|---|---|
| 1 | **"One house in January = 20 flats every day":** size multi-family houses / cooperatives by hot water, utilisation ≈ 100 % | 5 | 4 | 4 | 4 | 3 | **20** |
| 2 | **Sun-compute:** summer filler runs on own PV surplus, marginal electricity = forgone feed-in (≈ 6 Rp) instead of 33 Rp | 3 | 5 | 4 | 5 | 3 | **20** |
| 3 | **Firm + spot split:** sell the predictable hot-water base of the fleet as reserved capacity, only the winter peak as spot | 4 | 3 | 3 | 4 | 4 | **18** |
| 4 | **Cascade heat pump:** server loop (35–45 °C) is the source of a water-to-water heat pump → COP ≈ 5, 60 °C hot water (Legionella) | 3 | 4 | 3 | 3 | 4 | **17** |
| 5 | **GPU retirement home:** low-utilisation sites get low-capex second-life GPUs; hardware leased from data centres for its tail years | 5 | 3 | 3 | 2 | 3 | **16** |
| 6 | **Abwärme compliance + CO₂ credits:** Basel's renewable-replacement duty accepts non-fossil waste heat; KliK pays ≈ CHF 100/t CO₂ | 3 | 3 | 3 | 3 | 4 | **16** |
| 7 | **Heating-season compute pass** for open-model evaluation / research batch (non-personal, legal in homes), sold Nov–Mar | 3 | 3 | 3 | 3 | 4 | **16** |
| 8 | **LEG cluster:** single-family houses join a local electricity community (since 2026) and buy neighbours' PV for the servers at a grid-fee discount | 3 | 3 | 3 | 3 | 3 | **15** |
| 9 | Larger tank (e.g. 3,000 l) instead of more servers: cheapest kWh of flexibility | 2 | 5 | 4 | 3 | 1 | 15 |
| 10 | Deadline price menu (1 h / 24 h / 7 d) — builds on enabler #17 | 3 | 4 | 2 | 4 | 2 | 15 |
| 11 | Balancing / winter-reserve pool: servers are the fastest sheddable load (Swissgrid pools ≥ 5 MW) — builds on parked "grid flexibility" | 2 | 3 | 2 | 4 | 3 | 14 |
| 12 | Municipal anchor tenant: canton digitises archives on servers in its own social housing (customer = host, data stays public property) | 3 | 3 | 2 | 2 | 4 | 14 |
| 13 | Dynamic network tariffs (allowed from 2026): run in low-tariff hours, tank shifts heat | 2 | 3 | 3 | 3 | 2 | 13 |
| 14 | Weekly Legionella flush scheduled as a "hot job" (servers + booster heat the tank to 60 °C) | 1 | 4 | 2 | 3 | 3 | 13 |
| 15 | Heat subscription ("Wärme-Abo") below heat-pump cost, contracting finances tank and backup | 2 | 4 | 3 | 2 | 1 | 12 |
| 16 | Confidential computing badge (attested VM) in the homeowner view | 3 | 2 | 3 | 2 | 2 | 12 |
| 17 | Gamified homeowner app ("your house rendered a film") | 1 | 5 | 1 | 4 | 1 | 12 |
| 18 | Utility as sales channel (IWB sells it where no district heating is planned) | 3 | 3 | 2 | 1 | 2 | 11 |
| 19 | Host-owned servers financed via green-mortgage discount, like PV | 2 | 2 | 2 | 1 | 3 | 10 |
| 20 | Nomadic hardware: GPU cartridges move to pools/data centre in summer | 2 | 1 | 1 | 2 | 4 | 10 |
| 21 | Winter-peaking workloads (tax season, snow forecasts) — repeat of brainstorm | 2 | 3 | 1 | 2 | 2 | 10 |
| 22 | Insurer partner: building insurer discounts houses with monitored tank/boiler | 1 | 2 | 1 | 1 | 3 | 8 |

## 3. Top 8

### 3.1 "One house in January = 20 flats every day" (Σ 20)

**Mechanism.** The case's own tension (12.5 servers in the coldest hour vs. 0.6 in summer) turns
around in a multi-family building: hot water is year-round, so 12.5 servers are needed **every
day**. 12.5 servers × 19.2 kWh = 240 kWh/day = **20 households × 12 kWh/day** (case's hot-water
value per household of four; smaller flats need more of them **[A]**). Sized to hot water, servers
run ≈ 8,760 h/yr instead of 5,665. **Impact (our formula, `hardware.md` §1):** L4 depreciation
34,244 ÷ 6 ÷ 8,760 = CHF 0.65, operations 0.08 → full-cost break-even 0.33 + 0.65 + 0.08 − 0.106 =
**CHF 0.96 instead of 1.36 (−30 %)**, before space heating, which adds winter hours on top.
**Evidence:** Leafcloud only accepts sites with ≥ 500 GJ/yr hot water; Infomaniak runs a data
centre in the basement of the Geneva housing cooperative La Bistoquette (snippet, ABB/Infomaniak
news). **Risk:** central hot water in multi-family houses must reach ≈ 60 °C (Legionella), above
raw server heat → needs 3.4; per-flat hot-water profile `TODO(source)`; peak draw needs a larger
tank. **Pitch:** one picture: the single house's server bar collapses in July, the 20-flat bar
stays full all year. "Residential works — it is the cooperative, not the villa."

### 3.2 Sun-compute: summer filler on own PV (Σ 20)

**Mechanism.** In non-heating hours the hardware is sunk and the marginal cost of a filler hour is
electricity ([`hardware.md`](hardware.md) §3). A house with PV gets only the feed-in tariff for its
midday surplus: national minimum **6 Rp/kWh for systems ≤ 30 kW from 2026** (Swissolar summary,
pv-magazine), EKZ 1.2–6.2 Rp (IWB rate `TODO(source)`). Running the server on that surplus cuts the
marginal electricity cost from CHF 0.3325 to ≈ 0.06 per server-hour. **Impact:** filler margin per
hour rises from 0.33 to 0.60 (CPU, CHF 0.66) or 1.09 to 1.36 (L4, CHF 1.42); with ≈ 750 surplus
hours per year **[A]** (5 h × 150 days; computable from the Open-Meteo irradiance already in
`data/raw/open-meteo/`) ≈ **CHF 200 per server-year** more, and the summer heat charges the
hot-water tank instead of being dumped. **Risk:** PV is not part of the reference house; filler is
still sold only ≈ 35 % of the time; only houses with PV. **Pitch:** the July demo day: the real job
starts when the sun curve crosses the house load and pauses at sunset — same scheduler, and it is
the case's own "Prepper" stretch goal.

### 3.3 Firm + spot split: sell what heat lets us promise (Σ 18)

**Mechanism.** Buyers pay for *reliable* capacity; heat-led home capacity is the unreliable
surplus ([`filler-demand.md`](filler-demand.md) §4). But part of our capacity *is* predictable: the
year-round hot-water base. Reference house: 4,380 kWh/yr hot water ÷ 0.8 = **5,475 server-hours/yr
(24 % of 22,661)** that run whatever the weather. Across a fleet with multi-family houses (3.1) and
pools this base grows. Sell it as **reserved capacity** (fixed monthly price, φ ≈ 1), and only the
winter peak as spot/filler (φ ≈ 0.35). **Impact [A]:** at L4 CHF 1.42 per hour, moving 5,475 h from
φ 0.35 to φ 1.0 adds 5,475 × 1.42 × 0.65 ≈ **CHF 5,000 per house-year**, i.e. roughly the L4
depreciation of one server. **Evidence:** Leafcloud gives discounts for ≥ 1-month commitments;
RunPod community vs. secure L4 ratio 0.64 shows the reliability premium (`computing-price.md` C1).
**Risk:** no named buyer for reserved home capacity (`TODO(source)`); the base needs N+1
redundancy across houses. **Pitch:** the year heat map split into a solid band ("sold in advance")
and a winter cloud ("sold on the spot market").

### 3.4 Cascade heat pump: server heat as heat-pump source (Σ 17)

**Mechanism.** The reference house keeps a backup heater anyway (`vision.md`). Make it a
water-to-water heat pump whose source is the server cooling loop (35–45 °C) instead of outdoor
air. A 35 → 65 °C lift has a Carnot COP ≈ 11, so a real machine at 40–50 % of Carnot gives
**COP ≈ 4.5–5.5 [A]** (to check against manufacturer data, `TODO(source)`). **Impact:** in the
4-server run the backup supplies 13,102 kWh; at COP 2.5 that is 5,241 kWh = CHF 1,743, at COP 5 it
is CHF 871 → **≈ CHF 870/yr saved for the host**, and hot water reaches 60 °C, which answers the
case owner's Legionella concern (`deep-dive/notes-structured.md`). **Evidence:** high-temperature
heat pumps boosting data-centre heat are studied (ORNL); AEE INTEC models data-centre heat pumps.
**Risk:** breaks the case simplification "no heat pump" (so variant only); extra hydraulic cost;
the server loop must be liquid-cooled. **Pitch:** a Sankey diagram: 1 kWh electricity → 0.8 kWh
server heat → lifted to 1 kWh at 60 °C for 0.2 kWh more.

### 3.5 GPU retirement home (Σ 16)

**Mechanism.** Builds on "refurbished hardware" in our direction, with a sharper rule: **the site
with the lowest utilisation should hold the hardware with the lowest capital cost.** Data centres
need GPUs at near-full use in years 1–3; a heat-led house can use them in years 4–8 when their
capital is mostly written off. DigitalHeat leases the tail years from a data-centre operator or
buys at resale. **Impact (our formula):** depreciation is linear in purchase price; at 50 % of new
price **[A]** the L4 full-cost break-even falls from CHF 1.36 to ≈ 0.86 per sold hour.
**Evidence:** used A100 40 GB PCIe trade at ≈ USD 4,200–6,000, −30 % in a year (AMCompute,
Aliteq snippets); Qarnot used refurbished OCP servers. Used L4 prices `TODO(source)`. **Risk:**
shorter remaining life and failure rate; older GPUs earn less per hour; resale market volatile.
**Pitch:** hard to demo; one line on the hardware slide: "the house is where good GPUs retire".

### 3.6 Abwärme compliance and CO₂ credits (Σ 16)

**Mechanism.** In Basel-Stadt a replaced heating system must switch to renewables where feasible
without extra cost, and **waste heat not from fossil processes counts** (cantonal brochure, energy
law definition of Abwärme). Server heat on Swiss electricity is a legal compliance path for houses
leaving oil or gas, a ready customer pipeline. Swiss compensation foundation KliK paid
**CHF 100 per certified t CO₂** for waste-heat and heat-network programmes until 2030, and a trade
article mentions a server-waste-heat project at that rate (snippet). **Impact [A]:** 18,129 kWh of
server heat replacing oil at ≈ 0.29 kg CO₂/kWh heat (`TODO(source)`: BAFU emission factor and boiler
efficiency) ≈ 5.3 t → **≈ CHF 530/yr per house** (≈ CHF 0.02 per server-hour); plus possible
cantonal subsidy (up to 40 % of investment for renewable heating; whether server heat qualifies is
`TODO(source)`). **Risk:** additionality and minimum project size for credits; heat-pump subsidies
in district-heating areas were halved in 2025, so target houses outside them. **Pitch:** a map
filter "oil-heated houses outside the district-heating area" as the launch market.

### 3.7 Heating-season compute pass (Σ 16)

**Mechanism.** Sell the winter peak (Nov–Mar), when a house can absorb 12.5 servers, as a
**season allocation** to buyers whose work is non-personal and batch: evaluation of open models
(e.g. Apertus), benchmark sweeps, synthetic data on public data, research simulations. These are
legal in private homes (no personal data, `filler-demand.md` §5) and checkpoint well. **Evidence:**
the Swiss AI Initiative distributes compute in open calls (10–15 M GPU-hours per call; large grants
> 500 k GPU-hours; Apertus training > 6 M GPU-hours on CSCS Alps). **Impact [A]:** one 4-server L4
house offers ≈ 46 L4 cards × 5,665 h ≈ 263,000 L4-hours/yr; a seasonal contract at φ ≈ 1 in winter
lifts winter income the same way as 3.3. **Risk:** academic compute is subsidised at CSCS (low
price); L4-hours are not GH200-hours; needs one institutional anchor. **Pitch:** "this winter, Basel
living rooms evaluated a Swiss language model" with a counter in the homeowner view.

### 3.8 LEG cluster of single-family houses (Σ 15)

**Mechanism.** Since 1 January 2026, local electricity communities (LEG) may sell PV power within a
neighbourhood or municipality with a **40 % grid-fee discount (20 % across a transformer)**
(EKZ tariff sheet, energiezukunft.eu); ≈ 1,860 LEGs were founded in H1 2026 (Swissolar via
solarserver.de). A single-family house with servers becomes the LEG's flexible buyer of
neighbours' summer PV surplus (extends 3.2 to houses without own PV), and the LEG is the natural
unit for routing jobs between houses (T-10/T-12). **Impact:** LEG price and grid-fee share of the
IWB H4 price `TODO(source)`; an assumption-free claim is only that the electricity price falls by
part of the grid fee. **Risk:** gains are modest; LEG metering and billing overhead; no heat
network between houses (trenching cost). **Pitch:** a small street map: PV roofs feed one
"compute-heated" house at noon.

## 4. Recommendation

- **Bet the pitch on 3.1 and 3.2.** 3.1 answers the meta-question ("why did residential fail, and
  what works?") with one picture built only from the case's own numbers. 3.2 turns the weak July
  day of the must-have demo into a strong one, uses data already in the repo, and matches the
  case's Prepper stretch goal.
- 3.3 is the strongest income lever but has no buyer yet; show it as the "next conversation" for
  DigitalHeat. 3.4 is the best answer to a Legionella question in Q&A.
- Cheap wins without a pitch slot: 9 (tank size sensitivity) and 14 (Legionella flush).
- Everything here is a variant; the scored reference house and the scored break-even stay as the
  case defines them.

## 5. Sources (all accessed 2026-11-21, search snippets)

- Basel-Stadt energy subsidies 2026 and heat-pump rules: https://media.bs.ch/original_file/a85f31f102af942f4dc1451cbc6dd0f8cad4d582/broschuere-foerderbeitraege-energie-bs-2026-ua.pdf ; https://www.bs.ch/wsu/aue/abteilung-energie/foerderbeitraege-energie
- Basel energy law, renewable replacement, definition of Abwärme: https://www.gesetzessammlung.bs.ch/api/de/versions/4366/pdf_file ; https://media.bs.ch/original_file/df578364dbe57ee43c6592cb12567c5bf5adacd6/flyer-antworten-auf-fragen-zum-heizungsersatz-2019-0.pdf
- KliK CHF 100/t for waste heat: https://s3.amazonaws.com/a.storyblok.com/f/246794/cd61d05ad2/fachartikel_8-haustech_industrielle-abwaerme_19.pdf ; https://www.thermdis.eawag.ch/static/templates/files/ProgrammWaermeverbuende_Vogelsanger_Aqua&Gas_2017-09.pdf
- LEG rules and adoption: https://www.energiezukunft.eu/buergerenergie/einfache-regeln-netzentgeltrabatte-und-neu-gewonnene-freiheiten ; https://www.ekz.ch/dam/ekz/angebote/strom/produzenten-und-gemeinschaften/leg/EKZ-Einsiedeln-Tarifblatt-LEG-ENP26.pdf ; https://www.solarserver.de/2026/09/02/schweiz-gruendet-1-860-lokale-stromgemeinschaften-leg-im-ersten-halbjahr-2026/
- PV feed-in 2026: https://www.pv-magazine.de/2026/01/12/swissolar-veroeffentlicht-neuregelungen-fuer-photovoltaik-anlagen-2026-auf-einen-blick/ ; https://www.ekz.ch/dam/ekz/privatkunden/strom/tarife-und-agb/Tarifdokumente/tarife-2026/ekz-rueckliefertarife-2026.pdf
- Dynamic network tariffs: https://www.uvek.admin.ch/dam/uvek/de/dokumente/dasuvek/abstimmungen/faktenblatt-5-vorlage-sichere-stromversorgung-stromnetz.pdf.download.pdf/faktenblatt-5-vorlage-sichere-stromversorgung-stromnetz.pdf ; https://www.srf.ch/news/schweiz/dynamische-stromtarife-ekz-bieten-dynamische-tarife-an-viele-andere-firmen-noch-nicht
- Balancing pools (tiko, ≥ 5 MW): https://heatpumpingtechnologies.org/?p=54362 ; https://www.swissgrid.ch/content/dam/swissgrid/about-us/newsroom/newsfeed/2026/260115-MM-Regelenergiemarkt-de.pdf
- Winter reserve ordinance (consumer side): https://www.swissgrid.ch/dam/jcr:cbc77205-fb55-4be0-a229-304812908430/20260803-Swissgrid-Stellungnahme-SResV.pdf
- Infomaniak data centre in a housing cooperative: https://new.abb.com/news/de/detail/124445/abgesichert-mit-usv-von-abb-infomaniak-rechenzentrum-wird-tausende-genfer-haushalte-heizen
- Data-centre heat with high-temperature heat pumps: https://ornl.gov/publication/district-heating-utilizing-waste-heat-data-center-high-temperature-heat-pumps ; https://www.aee-intec.at/wp-content/uploads/2026/05/Zotter_G.pdf
- Used GPU prices: https://www.amcompute.com/appraisals/a100-pcie-40gb ; https://aliteq.com/a100-price ; https://hashrateindex.com/blog/used-gpu-market-pricing-deprecation-secondary-ai/
- GPU confidential computing (H100, not L4): https://developer.nvidia.com/blog/confidential-computing-on-h100-gpus-for-secure-and-trustworthy-ai/
- Swiss AI Initiative compute: https://swiss-ai.org/ ; https://aic-reo-call.epfl.ch/ ; https://arxiv.org/abs/2604.12973
