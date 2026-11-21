# Web dashboard

Vite + React + TypeScript, Recharts for charts, MapLibre for the map, react-day-picker for date
ranges. All dashboard data is **mock data** for now (electricity prices: ElCom per canton).

```
npm install
npm run dev      # development server
npm run build    # type check and production build
```

| Route | Page | Ticket |
|---|---|---|
| `/admin` (`/` redirects here) | Network dashboard: map, heat maps, weather, time machine, charts, missed deadlines | T-17 |
| `/household`, `/household/:siteId` | Per-household dashboard with date range, forecast and live mode | T-18 |

- `src/params.ts`: ElCom canton electricity prices
- `src/admin/mock.ts`: deterministic mock network (sites, weather, load, jobs); replace with exported runs (T-13)
- `src/household/series.ts`: household time series in range-dependent buckets
- `src/index.css`, `src/admin/admin.css`, `src/admin/chartStyle.tsx`: shared styling

The map uses CARTO dark tiles and needs internet; without it, markers and overlays draw on a
plain background.
