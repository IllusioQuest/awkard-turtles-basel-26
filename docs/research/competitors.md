# Competitors: how heat-reusing compute companies make (or lose) money

**Status: desk research, first version (2026-11-21).** Not reviewed by the team. All facts come
from web search result snippets (all URLs accessed 2026-11-21); direct page fetches were blocked
in this session for most hosts, so many claims rest on secondary press. Confidence per row:
**H** = several independent sources or a primary document, **M** = one credible source or the
company's own claim, **L** = snippet only or sources disagree. Company claims about savings or CO₂
are not independently verified. No number here is used in a scored figure.

Context for reading: our own findings so far are that hardware cost dominates
([`hardware.md`](hardware.md)), sourced premium demand fills < 1 % of capacity
([`job-demand.md`](job-demand.md)), and a pure profit maximiser runs always-on and dumps heat
([`../model/routing-optimisation.md`](../model/routing-optimisation.md) §5.4).

## 1. Overview

| Company | Heat sink | Status (Nov 2026) | Funding / scale | Conf. |
|---|---|---|---|---|
| **Qarnot** (FR, 2010) | First room radiators (QH-1), then "digital boilers" (QBx) feeding hot water / heating networks at up to 65 °C | Radiators discontinued 2023 because of seasonality; HPC activities placed in *redressement judiciaire* (court-supervised reorganisation) in 2026 and **acquired by Scaleway (iliad)**, announced 10 Jul 2026 | €35 M round 2022/23 (€12.5 M equity + €22.5 M credit for sites); ~70 staff; Finland pilot 100 kW | H (acquisition), M (receivership: one French outlet + CFNews) |
| **Heata** (UK) | Domestic hot-water cylinder (heat bridge, 30 min install) | Still trial stage: 80 units (2023), British Gas trial with 10 employee homes (Feb 2025) | Seed ≈ £1 M / $1.23 M (2023), British Gas investor | M |
| **Deep Green** (UK) | Swimming pools (immersion-cooled servers, heat exchanger to pool water) | Growing, but pivoting to larger **AI/HPC colocation** sites (Urmston 400 kW in 150 m²); 24 MW Michigan plan withdrawn Apr 2026 | £200 M from Octopus Energy (Jan 2024) for a 300 MW plan | H (funding), M (rest) |
| **Cloud&Heat** (DE, 2011) | Liquid-cooled racks in residential buildings, heat to building / district heating | Residential micro data centres closed; company says the **business model "ultimately proved unsuccessful"**; reoriented 2022/23 to cloud solutions | €10 M Series B; e.g. 20 cabinets in a Dresden car park from 2014 | H (own statement) |
| **Leafcloud** (NL) | Hot water in apartment blocks, care homes, pools ("Leaf Sites") | Operating; ISO 27001 certified; sells GPU cloud | Minority stake by The Sharing Group; size not public | M |
| **Nerdalize** (NL, failed) | Room "eRadiator" / CloudBox in homes | **Bankrupt 27 Dec 2018** (Hague court); assets taken over by investor Catena | ~€0.75–1 M crowdfunding (Symbid), Eneco investor; 5 homes (2015 pilot), target 50; loss ≥ €882 k in 2017 | H (bankruptcy), M (loss) |
| **Thermify** (UK) | HeatHub boiler (500 Raspberry Pi modules) in social housing | Grant-funded pilots (UKPN SHIELD: 100 HeatHubs planned) | Network-innovation and housing grants | M |
| **Windcloud** (DE) | Data-centre exhaust air (~36 °C) to an algae greenhouse | Showcase pilot; no revenue from heat reported | Small regional hoster | L |

## 2. Business model per company

| Company | Owns hardware | Host pays / gets | Electricity | Compute customers and pricing | Hardware | Conf. |
|---|---|---|---|---|---|---|
| Qarnot | Qarnot | Building got heat **free** (e.g. Gironde Habitat social housing "heated free of charge"); heat also sold cheaply to heating partners | Paid by Qarnot (offset against compute price) | Banks: BNP Paribas moved 5 % of its risk calculations (2015), Société Générale (pricing models, 1,000 cores in Finland), later Natixis, ATR, Alpine, MaiaSpace; also 3D rendering. No public price list; "up to 50 % cheaper" claim only | CPU servers, later CPU+GPU; QBx uses **refurbished OCP hardware** (ITRenew) | M |
| Heata | Heata | Host gets **free hot water**, up to ~4–4.8 kWh/day (~80 % of UK household use); host provides broadband | **Heata reimburses** the unit's electricity (in 2023 co-funded by Innovate UK) | Mainly **3D rendering** studios (animation, visualisation); in 2025 trial British Gas's own workloads | Small server, VM-isolated, VPN tunnel to Heata | M |
| Deep Green | Deep Green | Pool gets heat **free**, hardware installed free; Exmouth: ~139,000 kWh/yr heat, ~62 % of pool gas, ≈ £20,000/yr saving (FOI figure) | **Deep Green pays** / refunds the electricity | Colocation and cloud for corporates (post-production, AI, "sovereign UK compute") | Immersion-cooled (mineral oil) racks, high density | H (Exmouth FOI), M |
| Cloud&Heat | Cloud&Heat | Building got heat; in Dresden the utility **DREWAG bought the heat** and resold it to tenants | Cloud&Heat | Distributed OpenStack cloud | Liquid-cooled racks | M |
| Leafcloud | **Leafcloud owns all equipment**; host has no upfront cost | Host gives 10–50 m², 3-phase power, water loop; gets heat for hot water (free heat for rent-free space and cooling) | **Metered and paid by Leafcloud** | Public cloud at list prices: A30 €0.60/h, A100 €1.61/h, H100 €3.15/h; discounts for ≥ 1-month commitments; a **"dynamic" tier schedules GPU work up to 48 h ahead by electricity price** | New GPU servers | M |
| Nerdalize | Nerdalize | Host got **free heat**, paid ~€300 installation/maintenance; savings claimed ~€400/yr | **Nerdalize paid** the host's extra electricity | Businesses buying cloud compute; revenue partly shared with homeowners | Room radiators with servers | M |
| Thermify | Thermify / ESCo | Heat sold "as a service" to households; ESCo finances install for landlords | – | Containerised workloads; data services are the main revenue | 500 Raspberry Pi CM per unit | M |

## 3. The hard parts

| Company | (a) Summer / no heat demand | (b) Deadlines, interruptions | (c) Data security / sovereignty | Profitability |
|---|---|---|---|---|
| Qarnot | Radiators suffered from seasonality → discontinued 2023. Moved to boilers that run **all year** on hot water / network base load, with a **complementary source for cold peaks**. Also throttles CPUs in summer and keeps a task surplus; gave spare winter capacity free to universities | Own heat-aware scheduler (Q.ware); hybrid with Data4 data centres (30 MW, 13 DCs) for central capacity. **No public detail on checkpointing or migration** | Pitches European jurisdiction; scheduler designed for "security and resilience in a non-regulated environment" (DATE 2021 paper) | Not profitable enough to survive alone: receivership and sale in 2026 despite > €35 M raised |
| Heata | Sidesteps it: **hot water is year-round**; one small unit per home | Rendering frames are short and restartable; no SLA details found | Workloads in VMs, only a VPN tunnel to Heata; server not accessible to the host; critics raised home-network concerns | Unknown; still pilot scale after ~3 years |
| Deep Green | Pools need heat **all year**; data centre covers pool ~60 % of the time | Colocation (customer runs own SLA); not heat-led interruption | Sells "sovereign UK compute" | Unknown; capital from Octopus, shift to larger AI sites |
| Cloud&Heat | Residential heat was seasonal; heat sold to utility | Distributed cloud "never that simple to make work right" (co-founder) | German cloud | **Failed** for residential: "business model ... ultimately proved unsuccessful"; investors wanted cash from day one for a two-market product |
| Leafcloud | Only accepts sites with **year-round hot water ≥ 500 GJ/yr** (~139 MWh/yr) | Standard cloud SLA; dynamic tier = deadline-flexible work scheduled by price | **Core selling point:** Dutch B.V., no US parent, ISO 27001:2022, SOC 2 Type II claimed, NEN 7510 | Unknown; still operating |
| Nerdalize | Planned to **vent heat outdoors in summer**; critics asked what happens on a cold day with no compute buyers | Technician visit needed for repairs; host left without heat if company fails | – | **Failed:** ≥ €882 k loss 2017, bankruptcy Dec 2018; cause never published (press: unsustainable losses, weak demand); shifted focus from heat to cloud in 2016 |

**Gap:** none of the companies publishes how interrupted jobs are billed (partial work, checkpoint,
migration). This is open ground for our scheduler story; we must not claim market precedent.

## 4. Lessons for our business model (5–8 most transferable)

1. **Size to year-round base load, not to winter peak.** Every survivor moved from space heating to
   hot water, pools or heating-network base load (Qarnot radiators → boilers, Heata, Leafcloud's
   500 GJ/yr minimum, Deep Green pools). For the reference house this means sizing the server
   count near the summer/hot-water load (our 0.6 servers in summer) and leaving the winter peak to
   the backup heater, which raises utilisation and cuts hardware cost per sold hour, our dominant
   cost.
2. **The operator owns the hardware and pays the electricity; the host gets free heat.** This is
   the model of every company whose terms we found (Qarnot, Heata, Deep Green, Leafcloud,
   Nerdalize; Cloud&Heat also sold the heat to a utility). We found no host-owned example (Nerdalize hosts paid only
   ~€300 installation). Our host-owned variant is therefore untested in the market, and we should
   label it as such.
3. **Heat is a cost reducer, not the main revenue.** Compute is sold at ordinary cloud prices
   (Leafcloud's GPU list prices). The heat trade buys free space and cooling (Leafcloud, Deep
   Green). Heat sales to a utility existed (Cloud&Heat, DREWAG) but did not save the model. The
   system surplus must close on compute revenue.
4. **Filling capacity is the real problem, confirming our < 1 % premium-demand finding.** Deep
   Green says it needs more corporate customers, and Qarnot gave spare capacity to universities
   for free. Anchor customers were large batch buyers (banks' risk runs, rendering studios).
   Sovereignty (Leafcloud, Deep Green, Scaleway's reason for buying Qarnot) is the premium
   positioning used in the market; we found no evidence of a premium price.
5. **Single homes are the hardest case; the market moved to larger sites.** Cloud&Heat and
   Nerdalize failed in homes, Heata is still at pilot scale, Qarnot went to sites of hundreds of kW
   to MW, Deep Green to 400 kW, Leafcloud to 10–50 m² rooms. Each site has fixed costs (install,
   broadband, maintenance visits) that tiny units cannot carry. Our pitch should show the
   reference house honestly, and multi-family or pool sites as the variant that pays.
6. **Refurbished hardware is used to cut capex** (Qarnot QBx on refurbished OCP servers). This is a
   lever worth a variant in `config/hardware/`, with a `TODO(source)` for refurbished prices.
7. **Always-on plus dumping heat is what Nerdalize planned, and it failed; throttling or a
   price-led schedule is the market answer.** Qarnot throttles in summer, and Leafcloud sells a
   price-scheduled "dynamic" tier (up to 48 h ahead). This supports a heat- and price-aware
   scheduler with deadline-flexible work as a product, rather than a pure always-on maximiser.
8. **Even well-funded players struggle; state the risk.** Qarnot (> €35 M raised) went into
   receivership, Cloud&Heat closed its residential sites, and Nerdalize went bankrupt. Two-market
   products (heat and cloud) need patient capital. Our "when it does not pay off" statement should
   say this plainly.

## 5. Sources (all accessed 2026-11-21)

Qarnot
- Scaleway acquisition: https://www.scaleway.com/en/news/scaleway-acquires-qarnot-to-strengthen-its-european-cloud-and-ai-platform-with-low-carbon-hpc-technology/ ; https://www.datacenterdynamics.com/en/news/scaleway-acquires-hpc-specialist-qarnot-to-expand-cloud-platform/
- Receivership: https://next.ink/247072/scaleway-rachete-qarnot-et-ses-activites-hpc-recemment-placees-en-redressement-judiciaire ; https://www.cfnews.net/L-actualite/Retournement/Operations/M-A/Qarnot-se-rechauffe-aupres-d-un-repreneur-721207
- Funding, QBx, refurbished OCP: https://www.datacenterdynamics.com/en/news/qarnot-raises-35-million-to-expand-digital-boiler-ambitions/ ; https://tech.eu/2023/01/11/reclaiming-data-centre-waste-heat-nets-qarnot-eur35-million
- Seasonality, boilers, throttling, free university capacity: https://techcrunch.com/2023/01/09/qarnot-creates-green-data-centers-by-putting-servers-in-central-heating-boilers/amp ; https://en.reset.org/qarnot-turning-data-flows-home-heating-04252021/ ; https://www.polytechnique-insights.com/en/braincamps/planet/how-to-reduce-carbon-emissions-of-the-digital-sector/low-carbon-computing-is-possible
- Bank customers, free heat: https://group.bnpparibas/en/press-release/social-housing-office-building-gironde-habitat-heated-free-charge-qarnot-technology-bnp-paribas-calculations ; https://www.datacenterdynamics.com/en/news/bnp-paribas-risk-calculations-will-heat-paris-homes/ ; https://wholesale.banking.societegenerale.com/en/insights/news-press-room/news-details/news/decarbonizing-cloud-computing/
- Data4 hybrid: https://www.datacenterknowledge.com/servers/qarnot-s-home-heating-servers-now-plugged-into-data-centers
- Scheduler paper: https://past.date-conference.com/proceedings-archive/2021/html/18001.html

Heata
- https://www.techradar.com/news/this-company-will-pay-for-your-heating-if-you-let-it-install-a-server-in-your-house
- https://www.cnx-software.com/2023/02/15/install-a-server-in-your-house-get-free-hot-water/
- https://www.centrica.com/media-centre/news/2025/british-gas-partners-with-heata-on-trial-to-reuse-waste-heat-from-data-processing/
- https://industrialnews.co.uk/british-gas-partners-with-heata-on-waste-heat-reuse-for-water-trial/
- https://businesscloud.co.uk/news/envirotech-to-heat-up-with-1m-funding/ ; https://www.cbinsights.com/company/heata/financials
- https://www.businessgreen.com/interview/4521705/heat-heata-virtual-centres-warm-homes (paywalled; headline only)

Deep Green
- https://www.theregister.com/2024/01/16/deep_green_gets_200m_from/
- https://www.rbwm.gov.uk/council-and-democracy/strategies-and-policies/freedom-information/information-requests/196501-332465-eir-leisure (FOI figures)
- https://eastdevon.gov.uk/news/2023/03/exmouth-swimming-pool-is-heated-by-a-uk-first-in-technology-heating-solutions/
- https://www.capacitymedia.com/article/2bk13yjhuj9qedmkvxszk/news/deep-green-repurposes-data-centre-heat-to-heat-public-swimming-pool
- https://www.intelligentcio.com/eu/2026/03/16/deep-green-announces-uks-fastest-ai-colocation-deployment/ ; https://www.datacenterdynamics.com/en/company/Deep-Green/

Cloud&Heat
- https://www.cloudandheat.com/en/news-press/once-upon-a-time-there-was-a-cloud-that-heated-homes-worldwide/
- https://spectrum.ieee.org/heat-your-house-with-someone-elses-computers
- https://www.datacenterdynamics.com/en/news/cloudheat-gets-10-million-to-develop-compact-liquid-cooled-data-centers/

Leafcloud
- https://leaf.cloud/leafsites/ ; https://leaf.cloud/resources/heat-reuse/ ; https://www.leaf.cloud/faq ; https://leaf.cloud/resources/dynamic-gpu-pricing
- https://europeanpurpose.com/tool/leafcloud ; https://getdeploying.com/leaf-cloud
- https://www.leaf.cloud/digital-sovereignty ; https://leaf.cloud/blog/leafcloud-iso27001-certification-2025
- https://siliconcanals.com/the-sharing-group-acquires-stake-in-leafcloud/

Nerdalize
- https://nltimes.nl/2019/01/02/startup-using-servers-heat-homes-files-bankruptcy
- https://siliconcanals.com/innovative-e-radiator-startup-nerdalize-files-for-bankruptcy-another-painful-end-in-dutch-startup-ecosystem/
- https://bits-chips.com/artikel/doorstart-voor-nerdalize/
- https://tech.eu/2016/02/03/nerdalize-wants-to-heat-your-home-with-computer-servers/ ; https://cleantechnica.com/2015/07/14/nerdalize-heater-provides-heating-solution/
- https://mtsprout.nl/groei/warmte-uit-de-cloud

Thermify, Windcloud
- https://www.theregister.com/2025/10/03/thermify_heathub_raspberry_pi/ ; https://smarter.energynetworks.org/projects/10127935
- https://www.netzwoche.ch/news/2020-09-06/warum-in-einem-nordfriesischen-rechenzentrum-algen-wachsen ; https://www.hortidaily.com/article/9308296/algae-greenhouse-neutralizes-carbon-footprint-data-centre
