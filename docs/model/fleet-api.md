# Fleet results API (dashboard backend)

Read-only HTTP API that serves fleet-simulation runs (`data/derived/fleet/<run-id>/`, T-58) to the
web dashboard. JSON shapes: [`web/src/data/types.ts`](../../web/src/data/types.ts) (the contract;
field names camelCase, units in the names).

```
dotnet run --project src/DigitalHeat.FleetApi -- [--runs data/derived/fleet] [--port 5080]
cd web && npm run dev        # Vite proxies /api to http://localhost:5080
```

| Endpoint | Returns | Built from |
|---|---|---|
| `GET /api/runs` | `RunInfo[]`, sorted by id | every `<run-id>/run.json` that exists |
| `GET /api/runs/{id}/houses` | `HouseInfo[]` in houses-summary order | `houses-summary.csv` + the run's house file (`run.json` inputs) + `grid-10km.csv` (municipality) |
| `GET /api/runs/{id}/network` | `NetworkHour[]`, one per run hour | `network-hourly.csv` |
| `GET /api/runs/{id}/snapshots?from=h0&to=h1` | `Snapshot[]`, one per hour in `[h0, h1)` (max 744) | `houses-timeseries.csv` |
| `GET /api/runs/{id}/houses/{houseId}/series` | `HousePeriod[]`, all periods | `houses-timeseries.csv` |
| `GET /api/weather/points` | `WeatherPoint[]` (435 grid points) | `data/raw/swiss-grid/grid-10km.csv` |
| `GET /api/runs/{id}/weather?from=h0&to=h1` | `WeatherHours` (max 744 hours) | `data/raw/swiss-grid/weather-2025-hourly.csv.gz`, offset by the run start |

Rules:

- Nothing is recomputed that the simulation already logged; the API only reshapes. Derived values:
  `load` = electric kWh ÷ (servers × max kW × hours); `temperatureC` of a house = mean archive
  temperature of its `weather_ref` (grid point, or the Open-Meteo CSV for the reference house) over
  the period; kW = kWh ÷ period hours (the UI does that).
- A run without `houses-timeseries.csv` (`house_timeseries: none`) returns 404 for snapshots and series.
- Runs are loaded lazily on first use and cached in memory (the 173 MB hourly timeseries of
  `ch-100` takes a few seconds once).
- Unknown run or house: 404 with a short message. Bad range: 400.
