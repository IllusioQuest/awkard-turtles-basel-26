# Source: building register and heat demand map (T-47)

Both sources are large (~1 GB) and are **not committed**; `python3 scripts/build_buildings_heat.py
--download` stores them in `data/external/`, and the script writes the committed aggregates to
`data/derived/buildings/`.

## GWR — federal register of buildings and dwellings

| Field | Value |
|---|---|
| Provider | Federal Statistical Office (BFS), https://www.housing-stat.ch/ (public extract) |
| File | https://public.madd.bfs.admin.ch/ch.zip (949,806,326 bytes, last-modified 2026-11-20, export date `GEXPDAT` 2026-11-20); used: `gebaeude_batiment_edificio.csv`, code list `kodes_codes_codici.csv` |
| Retrieved | 2026-11-21 |
| Fields used | `GGDENR` municipality, `GKODE/GKODN` LV95 coordinates, `GSTAT` (1004 = existing), `GKAT` (1020/1030/1040 = residential), `GKLAS` (1110 single-family, 1121 two-family, 1122 multi-family), `GAREA` footprint m², `GASTW` floors, `GEBF` energy reference area m², `GANZWHG` dwellings, `GBAUP` construction period, `GENH1` heating energy source |
| Licence | BFS open data, free use with source (see `license.pdf` in the zip) |

## BFE heat demand map "Wärme-/Kältenachfrage Wohnen und DL"

| Field | Value |
|---|---|
| Provider | Swiss Federal Office of Energy (BFE), https://www.bfe.admin.ch/waerme-kaeltenachfrage-wohnen-und-dl; STAC collection `ch.bfe.fernwaerme-nachfrage_wohn_dienstleistungsgebaeude` on https://data.geo.admin.ch |
| File | `…/fernwaerme-nachfrage_wohn_dienstleistungsgebaeude_2056.gpkg` (68 MB, table `HOMEANDSERVICES`, 227,594 hectare cells, layer date 2022-01-19) |
| Retrieved | 2026-11-21 |
| Attributes | `NEEDHOME` heat demand residential, `NEEDSERVICE` services, `NEEDTOTAL`, all **MWh/a per hectare** (BFE, "Thermische Netze … Dokumentation Geodatenmodell", 04.01.2022, https://pubdb.bfe.admin.ch/de/publication/download/9777) |
| Base data | GWS 2014 (building and dwelling statistics) and STATENT 2013 (geocat record 32a8a3cd-d269-4915-b983-54fd979ca486) |
| Licence | Opendata BY: free use, source must be named (https://opendata.swiss/en/terms-of-use/#terms_by) |
| Totals | residential 47.3 TWh/a, services 26.9 TWh/a |

## Assumptions and caveats

- **A-T47-1:** floor area of a building = `GEBF` if recorded, else `GAREA × GASTW` (gross floor
  area as a proxy for the energy reference area; our assumption, not from a source).
- **Heat per building is modelled, not measured:** each hectare's `NEEDHOME` is split among its
  residential buildings in proportion to floor area. No public source gives measured heat demand per
  building in Switzerland.
- The BFE documents read do not say whether `NEEDHOME` is useful or final energy, nor how climate
  is accounted for; space heating and hot water are not separated. TODO(source). Some alpine values
  look low (e.g. Davos single-family 37.5 kWh/m²·a), consistent with a model that is not fully
  climate-corrected or with holiday homes.
- Base year 2014 (BFE) versus GWR 2026: buildings built after 2014 get a share of their hectare's
  2014 demand.
- Comparison with the case: the median Basel single-family house is ≈ 15 MWh/a, about half of the
  case's reference house (≈ 27 MWh space heat + 4.4 MWh hot water). The reference house stays fixed
  for scored figures (AGENTS.md §5.1).
