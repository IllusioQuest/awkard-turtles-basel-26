# Source: Open-Meteo historical weather, Basel 2025

| Field | Value |
|---|---|
| File | `basel-2025-hourly.csv` |
| Provider | Open-Meteo, Historical Weather API: https://open-meteo.com/en/docs/historical-weather-api |
| Endpoint | `https://archive-api.open-meteo.com/v1/archive` |
| Parameters | `latitude=47.56`, `longitude=7.59`, `start_date=2025-01-01`, `end_date=2025-12-31`, `hourly=temperature_2m,shortwave_radiation,direct_radiation,diffuse_radiation`, `timezone=Europe/Zurich`, `format=csv` |
| Retrieved | 2026-11-20 by `scripts/fetch_data.py` |
| Licence | Open-Meteo data is published under CC BY 4.0 (attribution: "Weather data by Open-Meteo.com"). Check the provider's terms page before publishing. |
| Required by | Case, "Reference house" ([`docs/case/challenge.md`](../../../docs/case/challenge.md)) |

## Contents

The first three lines are metadata (grid cell, elevation, time zone), followed by a blank line and
the hourly table:

| Column | Unit | Meaning |
|---|---|---|
| `time` | local time, ISO 8601 | start of the hour |
| `temperature_2m (°C)` | °C | air temperature 2 m above ground; **`T` in the case's heating formula** |
| `shortwave_radiation (W/m²)` | W/m² | global horizontal irradiance (GHI), mean over the preceding hour |
| `direct_radiation (W/m²)` | W/m² | direct part on a horizontal plane |
| `diffuse_radiation (W/m²)` | W/m² | diffuse part on a horizontal plane |

## Caveats

- **Grid cell:** Open-Meteo snaps the coordinates to its model grid: 47.557117 N, 7.549342 E, 247 m.
- **Time zone / DST:** The file has exactly 24 rows for every local day (also on 2025-03-30 and
  2025-10-26) and the metadata reports `utc_offset_seconds=3600`. Treat the file as 8,760 equally
  spaced hours with 24 values per calendar day, which matches the case's heating-day definition
  ("mean of the 24 hourly values").
- **Data revisions:** reanalysis data can be revised by the provider, so a later download may
  differ slightly. The committed file is the reference; see the validation in
  [`data/README.md`](../../README.md).
- The solar columns are not needed for the must-haves; they are kept for the optional solar
  stretch goal.
