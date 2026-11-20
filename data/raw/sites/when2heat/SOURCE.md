# Source: When2Heat heating profiles, Switzerland 2022 (extract)

| Field | Value |
|---|---|
| File | `when2heat-ch-2022.csv` (~0.96 MB, 8,759 rows + header) |
| Provider | Open Power System Data, data package *When2Heat Heating Profiles*, version 2023-07-27: https://doi.org/10.25832/when2heat/2023-07-27 |
| Original file | https://data.open-power-system-data.org/when2heat/2023-07-27/when2heat.csv (328,400,976 bytes, SHA-256 `f1f71790158d1de08403eea32dea7a2732050870c499938135606d9d7faac0fa`) |
| Retrieved | 2026-11-20 |
| Licence | Data: CC BY 4.0. Attribution required: Ruhnau, O., Hirth, L., Praktiknjo, A. (2019), *Time series of heat demand and heat pump efficiency for energy system modeling*, Scientific Data 6, 189, https://doi.org/10.1038/s41597-019-0199-y; Ruhnau, O., Muessel, J. (2023), *When2Heat Heating Profiles*, Open Power System Data, https://doi.org/10.25832/when2heat/2023-07-27 |
| Used by | [`docs/research/sites-and-profiles.md`](../../../../docs/research/sites-and-profiles.md) (WP3) |

## What the extract is

The original CSV has 28 countries, 2008–2022, hourly. This file keeps **only** the two timestamp
columns and all 15 `CH_*` columns, for rows whose local timestamp (`cet_cest_timestamp`) is in
2022 (the latest year in the package). Values are copied byte for byte: separator `;`, decimal
comma (`2,8`), as in the original. Nothing else was changed.

Command used (Python 3, standard library; run from the repository root, original CSV downloaded
to a git-ignored folder such as `data/external/`):

```bash
curl -o data/external/when2heat.csv https://data.open-power-system-data.org/when2heat/2023-07-27/when2heat.csv
python3 - data/external/when2heat.csv data/raw/sites/when2heat/when2heat-ch-2022.csv 2022 <<'EOF'
import csv, sys
src, dst, year = sys.argv[1], sys.argv[2], sys.argv[3]
with open(src, newline='') as f, open(dst, 'w', newline='') as g:
    r = csv.reader(f, delimiter=';'); w = csv.writer(g, delimiter=';', lineterminator='\n')
    h = next(r); idx = [0, 1] + [i for i, c in enumerate(h) if c.startswith('CH_')]
    w.writerow([h[i] for i in idx])
    for row in r:
        if row[1].startswith(year):
            w.writerow([row[i] for i in idx])
EOF
```

## Columns

| Column | Unit | Meaning |
|---|---|---|
| `utc_timestamp` | ISO 8601, UTC | start of the hour |
| `cet_cest_timestamp` | ISO 8601 with offset | same hour in CET/CEST (local Swiss time) |
| `CH_heat_profile_space_SFH` / `_MFH` / `_COM` | MW per TWh/a | normalised **space-heating** demand, single-family houses / multi-family houses / commercial buildings; each year sums to ≈ 1,000,000 (= 1 TWh) |
| `CH_heat_profile_water_SFH` / `_MFH` / `_COM` | MW per TWh/a | normalised **hot-water** demand, same building types |
| `CH_COP_{ASHP,GSHP,WSHP}_{floor,radiator,water}` | – | heat-pump COP for air-, ground-, groundwater-source pumps with floor heating, radiators, or water heating |

To scale a profile to a site: `heat_kw(t) = profile(t) × annual_heat_kwh ÷ 1e9`
(profile in MW/TWh = kW per GWh·a; dividing by the yearly sum ≈ 1e6 gives the share of the year).

## How the data was made (from the package README)

National hourly time series. Heat demand combines the **German BDEW/BGW gas standard load
profiles** (daily sigmoid in temperature, plus hourly factors) with ERA5 temperature and wind
reanalysis, weighted by population geodata. COP from heat-pump COP curves and heating curves,
corrected with field measurements.

## Caveats

- **National aggregate**, population-weighted over Switzerland, not Basel. Use it for **shapes**
  (daily, weekly, seasonal), not for absolute values; scale with a site's own annual demand.
- **Year 2022**, not 2025. Shapes are representative; for weather-consistent simulation of 2025
  apply the BDEW function (see `site-types.csv`, rows `slp_*`) to Basel 2025 temperatures instead.
- **Hot water is nearly flat over the year** (monthly 7.8–8.7 %) and **identical on all days**
  for households, because it comes from the summer base of gas profiles. Real hot-water demand
  dips somewhat in summer; that dip is not in this data.
- Space-heating profiles are not zero in summer (≈ 0.6 % of the year in July): artefact of the
  gas-profile method, unlike the case's hard 12 °C heating-day cutoff.
- **DST:** the original file has **no row for 2022-10-30 01:00 UTC** (the repeated local hour at
  the end of summer time); the extract therefore has 8,759 hourly rows, not 8,760. The spring
  local hour that does not exist is also absent (rows are continuous in UTC).
