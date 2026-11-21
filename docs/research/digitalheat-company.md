# DigitalHeat: what the company is and what it has done (desk research)

> **Question:** What is DigitalHeat (case owner, Basel GreenTech 2026) and what have they really done so far?
> **Method:** web search (2026-11-21, about 20 queries in English and German: name variants, commercial
> register, press, startup databases, grants, Basel server-heating pilots, used GPUs). Direct page fetches
> were **blocked** in this session (digitalheat.ch, zefix.ch, moneyhouse, iwb.ch, luma.com, b2match.com,
> heatingbits.epfl.ch did not resolve), so everything from the web below comes from **search-result snippets**
> and is marked with confidence. All URLs accessed 2026-11-21.
> **Status:** exploration, not verified. A human with normal internet should re-check §5 (10 minutes).

## 1. Short answer

- **We found no public trace of a company called "DigitalHeat" / "Digital Heat"** (AG, GmbH or otherwise):
  no commercial-register hit, no website in the search index, no press, no LinkedIn page, no startup
  database or grant entry. Zefix and Moneyhouse could not be queried directly, so a register entry is
  *not excluded*, only *not found*.
- Everything we know about DigitalHeat comes from **the case material itself** (`challenge.md`,
  `DigitalHeat.pdf`, the deep dive).
- **Public financial statements: no.** None found, and Swiss private AG/GmbH are not required to publish
  them. If a register entry exists, it would show at most legal form, seat, purpose, share capital,
  board/signatories and capital changes (via SHAB/SOGC) — none of this could be retrieved.
- **Used GPUs: unknown.** Nothing public says what hardware DigitalHeat uses, new or refurbished. The
  deck only says "CPU and GPU work produces heat", and the deep dive says *we* decide the hardware.
- Basel already has a **history of server-heating pilots by others** (IWB with Neutral-IT, IWB with
  DeepSquare). These are easy to confuse with DigitalHeat but are not the same name (§4).

## 2. Facts from the case material (high confidence — primary sources in the repo)

| Fact | Source |
|---|---|
| Brand forms "DigitalHeat" (English) and "Digital Heat", tagline "Wärme aus Rechenleistung", logo "DH" | `DigitalHeat.md` p. 1, p. 5 |
| Positioning: "Server heat and sovereign AI", "Distributed servers for computing, AI and building heat in Switzerland", "Switzerland — local data and operations" | `DigitalHeat.md` p. 1 |
| Unit concept: server rack 5–20 kW heat per installed unit, up to 40 servers per rack, 50–70 °C target "system dependent", heat exchanger to storage / heating / hot water | `DigitalHeat.md` p. 1–2, 5 |
| Picture of one rack next to a large tank (no location, size or caption: not stated to be an own installation) | `DigitalHeat.md` p. 1 |
| Scale levels house → Basel → Northwestern Switzerland → Switzerland | `DigitalHeat.md` p. 5 |
| Starting assumptions are "simplified, provided by DigitalHeat" (1 kW per server, 80 % recovery, 10 kW house) | `challenge.md` §Data |
| Goal stated by the case: "show that computing can heat homes in a useful way"; result is "a basis for DigitalHeat's next conversations and decisions"; success = "without destroying the profitability of the hardware" | `challenge.md` |
| The case owner asked teams to research prices (image tagging, cloud GPU hour) and to "create a business model for Digital Heat. Make sure we have a good reason for selling it." | `notes-verbatim.md` |
| "We decide about exact Hardware, CPUs / GPUs" | `notes-verbatim.md` |
| Data sovereignty and local sensitive data (law office, CCTV in a grocery shop) as selling points; "We can probably NOT compete with LLMs" | `notes-verbatim.md` |
| District-heating feed-in is out of scope | `challenge.md` |
| PDF metadata: created with Keynote on macOS, **2026-11-20 19:51 UTC** (the evening the hackathon started) | `pdfinfo docs/case/DigitalHeat.pdf` |

## 3. Inferences (medium/low confidence — our reading, not stated anywhere)

1. **Early stage, probably pre-revenue.** The case material contains no installed base, no customer, no
   site, no measured figure, and the starting assumptions are round "simplified" numbers. Together with
   no web presence, DigitalHeat is most likely an idea or very early venture (a founder or small team),
   not an operating fleet. *Medium.*
2. **The business model is open on their side too.** "Create a business model … a good reason for
   selling it" and "Who pays, and how much?" (slide 6) are asked of us; they do not present an existing
   one. *High that it is not public; medium that it does not exist internally.*
3. **Why a residential house case:** the house is the hardest and most familiar heat sink (strong
   seasonality: 12.5 servers in the coldest hour vs. 0.6 in summer), it gives comparable numbers for all
   teams, and Basel's utility IWB has already piloted server heating in residential buildings (§4). The
   house may be a test case rather than their target market: the deep dive itself names farming, hotels
   with a spa, law offices and grocery shops. *Low–medium.*
4. **"Sovereign AI" is the intended differentiator** against cloud prices, more than raw cost per
   GPU-hour. *Medium (stated twice in the material, but no customer evidence).*
5. **Rack size 5–20 kW and 50–70 °C** suggest liquid or immersion cooling at building scale (a single
   rack in a basement or technical room), similar to the IWB pilots. *Low — no hardware statement.*
6. The German slide 5 with its own logo and a 2026 swisstopo credit looks like existing brand/pitch
   material reused in the deck, so the name is probably an intended brand. *Low.*

## 4. Similar Swiss players that could be confused with DigitalHeat

All from search snippets (confidence medium unless noted). None uses the name DigitalHeat.

| Player | What (as reported) | Source |
|---|---|---|
| **IWB + Neutral-IT** (Basel) | Pilot "Serverheizung": mini data centre (servers in an oil bath, < 1 m³) in a 1932 rental building with six flats, Neubad quarter; heat exchanger to heating and hot water storage (old oil tank reused); gas kept as backup/peak; ≈ 33 MWh heat/year, gas use expected −75 %; one trade article gives 24 kW; Neutral-IT (French start-up) distributes the computing tasks; reported around 2021. | [IWB press release](https://www.iwb.ch/ueber-uns/newsroom/medienmitteilungen/artikel~_archiv_Mini-Rechenzentren-heizen-Basler-Wohnungen~), [SRF](https://www.srf.ch/news/panorama/digitale-innovation-computer-heizt-basler-mehrfamilienhaus), [bz Basel](https://www.bzbasel.ch/basel/basel-stadt/ein-computerserver-als-heizung-fur-wohnungen-die-iwb-macht-s-vor-ld.2084296), [energate](https://www.energate-messenger.ch/news/203296/iwb-installiert-serverheizungen), [Swisspower](https://swisspower.ch/themen-und-standpunkte/abw%C3%A4rme-von-mini-rechenzentren-heizt-wohnungen-in-basel/), [green.ch clipping 2023](https://www.green.ch/fileadmin/user_upload/Pressespiegel/DE/GR_CL_2023_01_00_Phase5-Abwaerme-aus-RZ-S18-21_mit_Green.pdf) |
| **IWB + DeepSquare** (Basel, Sion) | Swiss decentralised-cloud project; immersion cooling; ≈ 5 kW heat for a three-storey townhouse in Basel, fossil fuel use roughly halved (company claim); proofs of concept in Sion and Basel; raised USD 2.5 M (headline). Current status unknown. | [HackerNoon](https://sia.hackernoon.com/converting-digital-heat-from-waste-to-commodity), [EPFL IPESE](https://ipese-web.epfl.ch/project-proposal/05-LCA/deep_square.html), [startupticker](https://www.startupticker.ch/en/news/deepsquare-completes-2-million-round) |
| **K51** (Eschenz TG) | Server heating placed in greenhouses/hotels, needs a storage tank; with BKW since 2025, data centres switched by spot-price forecast and for grid stability. | [K51 technical description](https://k51-strategiepapier.pages.dev/K51-GreenHeat-Container-Technische-Beschreibung-DE.pdf), [immo-invest](https://immo-invest.ch/en/kooperation-bei-optimierung-von-rechenzentren) |
| **Infomaniak D4** (Geneva) | Large data centre under a housing district, heat via heat pumps into district heating, ≈ 6,000 homes, inaugurated Jan 2025; GPUs Nvidia L4/A100/H100 (new or used not stated). | [Infomaniak](https://news.infomaniak.com/en/infomaniak-inaugurates-a-revolutionary-data-center-that-recovers-100-of-its-energy-for-building-heating), [swissinfo](https://www.swissinfo.ch/eng/science/a-data-center-that-also-heats-homes/88787019) |
| **EPFL Heating Bits** (Lausanne) | Research project: data-centre heat to campus district heating; demonstrator with **dismissed (second-life) EPFL servers**; Phase II 2026–2029. | [EPFL](https://www.epfl.ch/research/heating-bits), [CERN poster 2026](https://indico.cern.ch/event/1526482/contributions/7069199/attachments/3267175/5835471/20260504_heating_bits_poster_sc4sr.pdf) |
| Digital Thermic Solutions SA (Neuchâtel, CHE-150.167.654) | Similar name only; no evidence of server heating. *Low relevance.* | [onlineinfo.ch](https://onlineinfo.ch/company/digital-thermic-solutions-sa---che-150-167-654/pdf) |

Foreign comparators (Qarnot — uses refurbished OCP servers, Cloud & Heat, Hestiia, Deep Green, ThermAI)
are covered in [`competitors.md`](competitors.md).

The hackathon itself: "sprintd Basel · GreenTech Edition", FHNW Campus Dreispitz, 20–22 Nov 2026, real
green-tech cases from Basel organisations ([luma listing, snippet](https://luma.com/gzimrrxk)). No public
case list naming DigitalHeat was found.

## 5. Unknowns and how to close them

| Unknown | How to check |
|---|---|
| Legal entity, founding date, capital, board, seat, purpose | Search "Digital Heat" / "DigitalHeat" on zefix.ch and moneyhouse.ch (blocked here); SHAB archive for capital increases |
| Founders, team, investors, grants (Innosuisse, Climate Fund) | Ask the case owner; LinkedIn |
| Any installation (site, building, racks, kW) — is the photo on slide 1 theirs? | Ask the case owner |
| Hardware (CPU/GPU, new vs. refurbished) and cooling (water, immersion) | Ask; deep dive says we decide |
| Relation to IWB / Neutral-IT / DeepSquare pilots | Ask ("Do you work with IWB?") |
| Who pays whom today; any compute customer | Ask |

Suggested three questions for the case owner (one minute): *Do you have a live installation and what
hardware? Is the target the single-family house or other sites? Is there a utility partner such as IWB?*

## 6. Implications for our pitch (what DigitalHeat probably needs from us)

1. **A business model, not only a scheduler.** They asked for "a good reason for selling it". Show who
   pays whom (host, DigitalHeat, compute customer) and the break-even computing price, with the honest
   case where it does not pay off — that is what an early-stage founder can take into investor or
   utility conversations.
2. **Credibility through auditable numbers.** With no public track record, our transparent,
   reproducible yearly balance is the evidence base they lack. Every figure sourced; the reference house
   unchanged.
3. **Learn from the Basel pilots.** IWB's pilots (oil bath, existing tank reused, gas kept as backup,
   ≈ 5–24 kW per building) are local precedent: cite them as proof the physics works in Basel, and
   position DigitalHeat's difference (scheduling that follows heat demand, sovereign/local AI jobs,
   several site types) instead of claiming novelty.
4. **Sovereignty is the sellable edge.** "Local data and operations" and the law office / CCTV examples
   suggest customers who pay for compute that stays in Switzerland, rather than competing with
   hyperscaler GPU prices. Make this a scenario, with a price source.
5. **Hardware choice is ours — make it explicit.** Whether used GPUs or refurbished servers (as EPFL
   Heating Bits and Qarnot do) lower the capital cost enough is an open question DigitalHeat has not
   answered publicly; a clearly labelled variant would directly serve "without destroying the
   profitability of the hardware".
