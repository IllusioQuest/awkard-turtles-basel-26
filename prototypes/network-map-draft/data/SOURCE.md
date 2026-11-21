# Source: Swiss canton boundaries for the network map

| Field | Value |
|---|---|
| File | `switzerland-cantons.geojson` (26 cantons, ISO 3166-2 code and name) |
| Provider | Natural Earth, Admin 1 – States, Provinces, 1:10m, via https://github.com/nvkelso/natural-earth-vector (`geojson/ne_10m_admin_1_states_provinces.geojson`, master) |
| Original | 40,726,851 bytes, SHA-256 `22d0e3ad85eb3e27f17cabf8ba2d50e554fbc27a87796ff891d958185da62fb5`, downloaded 2026-11-21 to git-ignored `data/external/naturalearth/` |
| Licence | Public domain (Natural Earth terms of use, https://www.naturalearthdata.com/about/terms-of-use/) |
| Processing | `tools/extract_cantons.py`: keep features with `iso_a2 = CH`, keep name and ISO code, round coordinates to 4 decimals. No simplification beyond rounding |

Natural Earth 1:10m is generalised: boundaries are fine for an overview map, not for survey use.
