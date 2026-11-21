# Source: Open-Meteo weather and geocoding for the Swiss network test dataset (T-12 step 3)

Used by `scripts/build_network_dataset.py` and `config/network/switzerland-test.json`
(decision 0025 §8). The Basel file `basel-2025-hourly.csv` is the case's file (see `SOURCE.md`) and is
reused for all Basel sites.

## Geocoding (city centres)

| Field | Value |
|---|---|
| Files | `geocoding-2026-11-21/<city>.json` (raw API responses, unchanged; file names normalised to ASCII slugs) |
| Provider | Open-Meteo Geocoding API, https://open-meteo.com/en/docs/geocoding-api (data based on GeoNames, CC BY 4.0) |
| Request | `https://geocoding-api.open-meteo.com/v1/search?name=<city>&count=1&language=en&format=json&countryCode=CH` with `<city>` = Basel, Zürich, Bern, Genève, Lausanne, Luzern, St. Gallen, Lugano, Chur |
| Retrieved | 2026-11-21 with PowerShell `Invoke-WebRequest` |

## Hourly weather 2025

| Field | Value |
|---|---|
| Files | `zurich-2025-hourly.csv`, `bern-…`, `geneva-…`, `lausanne-…`, `lucerne-…`, `st-gallen-…`, `lugano-…`, `chur-2025-hourly.csv` |
| Provider | Open-Meteo Historical Weather API, https://open-meteo.com/en/docs/historical-weather-api |
| Request | same parameters as `basel-2025-hourly.csv` (`start_date=2025-01-01`, `end_date=2025-12-31`, `hourly=temperature_2m,shortwave_radiation,direct_radiation,diffuse_radiation`, `timezone=Europe/Zurich`, `format=csv`), with `latitude`/`longitude` = the geocoded city centre rounded to 2 decimals |
| Coordinates requested | Zurich 47.37, 8.55 · Bern 46.95, 7.45 · Geneva 46.20, 6.15 · Lausanne 46.52, 6.63 · Lucerne 47.05, 8.31 · St. Gallen 47.42, 9.37 · Lugano 46.01, 8.96 · Chur 46.85, 9.53 |
| Retrieved | 2026-11-21 with PowerShell `Invoke-WebRequest` (Python could not verify TLS on the hackathon network) |
| Licence | CC BY 4.0, "Weather data by Open-Meteo.com" |

Checks (2026-11-21): every file has the same columns as the Basel file, 8,760 rows, exactly 24 rows
per local day (as in Basel, also on the DST days), no empty values. Grid cells snapped by Open-Meteo
are in line 2 of each file (e.g. Lausanne 46.57 N, 6.66 E, 483 m).
