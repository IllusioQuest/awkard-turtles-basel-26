# Source: Swiss 10 km weather grid 2025 (T-51)

Produced by `scripts/fetch_swiss_grid.py` (steps `grid`, `weather`, `forecast`, `check`). Raw API
responses are cached in `data/external/swiss-grid-cache/` (not committed); the committed files are
the same values reshaped into one long table each.

## Grid points — `grid-10km.csv`

| Field | Value |
|---|---|
| Points | 409 regular points + 26 cantonal capitals = 435 |
| Regular grid | Cell centres every 10 km in Swiss LV95 (EPSG:2056), E 2'485'000–2'835'000, N 1'075'000–1'295'000; kept if the point lies in Switzerland |
| Inside-CH test and municipality | geo.admin.ch identify API, layers `ch.swisstopo.swissboundaries3d-land-flaeche.fill` (country) and `…-gemeinde-flaeche.fill` (current municipality, BFS number), https://api3.geo.admin.ch/rest/services/api/MapServer/identify |
| Capitals | geo.admin.ch SearchServer (`origins=gg25`), municipality label point; **Basel uses the case's reference coordinates 47.56 N, 7.59 E** so `cap-bs` equals the reference house weather |
| LV95 → WGS84 | swisstopo approximate formulas (~1 m), see script docstring |
| Columns | `point_id` (`g0000…` grid, `cap-<canton>` capitals), `e_lv95_m`, `n_lv95_m`, `lat_deg`, `lon_deg` (requested), `bfs_municipality_nr`, `municipality`, `canton`, `model_lat_deg`, `model_lon_deg`, `model_elevation_m` (grid cell Open-Meteo snapped to) |
| Retrieved | 2026-11-21 |
| Licence | geo.admin.ch data: free use with source (swisstopo), https://www.geo.admin.ch/en/general-terms-of-use-fsdi |

Why 26 capitals: a 10 km grid has no point in Basel-Stadt (37 km²) and only two in Geneva. Why
10 km: the archive's underlying models are ~9–25 km, finer grids add little real information.

## Hourly weather 2025 — `weather-2025-hourly.csv.gz`

| Field | Value |
|---|---|
| Provider | Open-Meteo Historical Weather API, https://archive-api.open-meteo.com/v1/archive |
| Request | `start_date=2025-01-01`, `end_date=2025-12-31`, `hourly=temperature_2m,shortwave_radiation`, `timezone=Europe/Zurich`, `format=csv`, 20 locations per request, default model (`best_match`) — same as `../open-meteo/basel-2025-hourly.csv` |
| Columns | `point_id`, `time_local` (Europe/Zurich, 24 rows per local day as in the case file), `temperature_2m` (°C), `shortwave_radiation` (W/m², preceding-hour mean) |
| Rows | 435 points × 8,760 h |
| Licence | CC BY 4.0, "Weather data by Open-Meteo.com" |

## Archived forecasts 2025 — `forecast-2025-hourly-lead0-3.csv.gz`

| Field | Value |
|---|---|
| Provider | Open-Meteo Previous Runs API, https://previous-runs-api.open-meteo.com/v1/forecast (https://open-meteo.com/en/docs/previous-runs-api) |
| Request | as above, `hourly=temperature_2m,temperature_2m_previous_day1,temperature_2m_previous_day2,temperature_2m_previous_day3`, `models=best_match` |
| Meaning | `temperature_2m_previous_dayN` = the forecast for that hour as it was issued N days (N×24 h) earlier. Use it as "what the scheduler knew N days ahead" |
| Columns | `point_id`, `time_local`, `temperature_2m` (forecast model at lead 0, i.e. its own "actual"), the three forecast columns (°C) |
| Caveat | The forecast models are not the archive reanalysis. Against the archive (`weather-2025-hourly`), all leads show ≈ +1.2 K bias and ≈ 2.6 K RMSE, i.e. mostly model difference, not forecast skill. **Measure forecast error against `temperature_2m` (lead 0) in this file**, and apply it to the archive temperatures if the scheduler needs archive-consistent forecasts |

Provenance: batches 0–11 (240 points) fetched on Ennio's laptop; the remaining 195 points were fetched
on a teammate's machine (Open-Meteo hourly limit reached here) and transferred as the finished
file on 2026-11-21. Checked: the 240 overlapping points are identical value-for-value (2,102,400 rows).

Forecast error (all 435 points, against lead 0 of the same model): 1 day ≈ 1.0 K RMSE, 2 days
≈ 1.5 K, 3 days ≈ 1.7 K, bias ≤ 0.15 K. The lead-0 vs archive difference (≈ +0.7 K, RMSE 2.2 K;
above 1,500 m ≈ +2.0 K, 3.6 K) is model/elevation difference, not forecast error.

## Checks (2026-11-21)

- `python3 scripts/fetch_swiss_grid.py check`: every point has 8,760 hourly rows in both files.
- `cap-bs` vs `../open-meteo/basel-2025-hourly.csv`: identical temperature in all 8,760 hours;
  206 heating days, 26,851 kWh space heat, coldest hour −5.6 °C (case values).
- Rate limit: one location-year costs ≈ 5.2 Open-Meteo API calls (header `X-RateLimit-Cost`), so a
  full refetch is ≈ 4,500 calls (free limit 10,000/day); the script pauses 15 s per batch.
