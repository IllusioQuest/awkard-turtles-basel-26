import { useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router'
import { useAsync, useSource } from '../data/source'
import type { DataSource, HouseInfo, HousePeriod, NetworkHour, RunInfo } from '../data/types'
import MapView, { type HeatLayer, type MapState } from './MapView'
import MockBanner from './MockBanner'
import SitePanel from './SitePanel'
import { EconomicsChart, HeatChart, LoadChart, MissedChart, type MissedPoint, type MonthlyPoint, type SeriesPoint } from './charts'
import { DAY_MS, HOURS_PER_DAY, alignHour, chf, dateTime, dayTick, dec1, lastHour, monthTick, num, pct, wallMs } from './format'
import {
  TEMP_LEGEND_TICKS_C,
  TEMP_SCALE_MAX_C,
  TEMP_SCALE_MIN_C,
  makeInterpolator,
  temperatureGradientCss,
  type WeatherField,
  type WeatherInterpolator,
} from './weather'
import './admin.css'

const CHART_WINDOW_DAYS = 3
// Playback speeds in simulated periods (hours, or days for daily runs) per second. The view redraws
// at most MAX_FPS times per second; faster speeds advance several periods per frame and skip the
// ones in between, so the browser never queues more work than it can draw.
const PLAY_SPEEDS = [1, 4, 12, 48, 168] as const
const MAX_FPS = 10

const RANGES = [
  { id: '24h', label: '24 h', hours: 24 },
  { id: '7d', label: '7 days', hours: 7 * 24 },
  { id: '30d', label: '30 days', hours: 30 * 24 },
  { id: '1y', label: '1 year', hours: 365 * 24 },
] as const
type RangeId = (typeof RANGES)[number]['id']

const HEAT_LAYERS: { id: HeatLayer; label: string }[] = [
  { id: 'none', label: 'Houses' },
  { id: 'load', label: 'Load' },
  { id: 'capacity', label: 'Free capacity' },
]

export default function AdminPage() {
  const { runId = '' } = useParams()
  const source = useSource()
  const runs = useAsync(source ? 'runs' : null, () => source!.runs())
  const houses = useAsync(source ? `houses:${runId}` : null, () => source!.houses(runId))
  const network = useAsync(source ? `network:${runId}` : null, () => source!.network(runId))

  const error = runs.error ?? houses.error ?? network.error
  if (runs.data && !runs.data.some((r) => r.id === runId)) return <Navigate to="/admin" replace />
  if (error) return <main className="page admin"><p className="banner">{error.message}</p></main>
  if (!source || !runs.data || !houses.data || !network.data || houses.loading || network.loading)
    return <main className="page admin"><p className="note">Loading…</p></main>
  const run = runs.data.find((r) => r.id === runId)!
  return <RunView key={run.id} source={source} run={run} runs={runs.data} houses={houses.data} network={network.data} />
}

interface Props {
  source: DataSource
  run: RunInfo
  runs: RunInfo[]
  houses: HouseInfo[]
  network: NetworkHour[]
}

const interpolators = new WeakMap<object, WeatherInterpolator>()

function RunView({ source, run, runs, houses, network }: Props) {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const step = run.periodHours
  const maxT = lastHour(run)
  const [t, setT] = useState(() => alignHour(run, Number(params.get('h') ?? 0)))
  const [heatLayer, setHeatLayer] = useState<HeatLayer>('none')
  const [showWeather, setShowWeather] = useState(false)
  const [rangeId, setRangeId] = useState<RangeId>('24h')
  const rangeHours = RANGES.find((r) => r.id === rangeId)!.hours
  const [selectedId, setSelectedId] = useState<string | null>(() => {
    const id = params.get('house')
    return houses.some((h) => h.id === id) ? id : null
  })
  const [playing, setPlaying] = useState(false)

  const [speed, setSpeed] = useState<number>(PLAY_SPEEDS[1])
  useEffect(() => {
    if (!playing) return
    // Advance by elapsed wall time, not by tick count: when drawing is slow, ticks come late and
    // more periods are skipped, so the playback speed stays what the user picked.
    const intervalMs = Math.max(1000 / MAX_FPS, 1000 / speed)
    let last = performance.now()
    let carry = 0 // fraction of a period not yet shown
    const timer = setInterval(() => {
      const now = performance.now()
      carry += (speed * (now - last)) / 1000
      last = now
      const periods = Math.floor(carry)
      if (periods < 1) return
      carry -= periods
      setT((prev) => {
        if (prev >= maxT) {
          setPlaying(false)
          return prev
        }
        return Math.min(maxT, prev + periods * step)
      })
    }, intervalMs)
    return () => clearInterval(timer)
  }, [playing, step, maxT, speed])

  const togglePlay = () => {
    if (!playing && t >= maxT) setT(0)
    setPlaying((p) => !p)
  }

  // Map states: every house's period at hour t.
  const snapshot = useAsync(`snap:${run.id}:${t}`, () => source.snapshots(run.id, t, t + 1))
  const states = useMemo(() => {
    const map = new Map<string, MapState>()
    const snap = snapshot.data?.[0]
    if (!snap) return map
    houses.forEach((h, i) => {
      const p = snap.houses[i]
      if (!p) return
      map.set(h.id, {
        load: p.load,
        tankLevel: h.tankKwh > 0 ? p.tankEndKwh / h.tankKwh : 0,
        heatProducedKw: p.heatProducedKwh / snap.periodHours,
        heatUsedKw: p.heatUsedKwh / snap.periodHours,
        heatDumpedKw: p.heatDumpedKwh / snap.periodHours,
      })
    })
    return map
  }, [snapshot.data, houses])
  const snapshotOf = (id: string): HousePeriod | undefined => snapshot.data?.[0]?.houses[houses.findIndex((h) => h.id === id)]

  const weather = useWeatherField(source, run, t, showWeather)

  // Stats over the hours (t − range, t].
  const totals = useMemo(() => {
    const sum = { load: 0, used: 0, dumped: 0, completions: 0, misses: 0, income: 0, cost: 0, n: 0 }
    for (let h = Math.max(0, t - rangeHours + 1); h <= t && h < network.length; h++) {
      const n = network[h]
      sum.load += n.load
      sum.used += n.heatUsedKwh
      sum.dumped += n.heatDumpedKwh
      sum.completions += n.completions
      sum.misses += n.misses
      sum.income += n.incomeChf
      sum.cost += n.electricityCostChf
      sum.n++
    }
    return sum
  }, [network, t, rangeHours])

  const series = useMemo<SeriesPoint[]>(() => {
    const from = Math.max(0, t - CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const to = Math.min(network.length - 1, t + CHART_WINDOW_DAYS * HOURS_PER_DAY)
    return network.slice(from, to + 1).map((n) => ({
      t: wallMs(run, n.h),
      load: n.load,
      heatUsedKw: n.heatUsedKwh,
      heatDumpedKw: n.heatDumpedKwh,
      heatDemandKw: n.heatDemandKwh,
    }))
  }, [network, t, run])

  const monthly = useMemo<MonthlyPoint[]>(() => {
    const byMonth = new Map<number, MonthlyPoint>()
    for (const n of network) {
      const d = new Date(wallMs(run, n.h))
      const start = Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1)
      let m = byMonth.get(start)
      if (!m) byMonth.set(start, (m = { month: monthTick.format(start), start, incomeChf: 0, electricityChf: 0 }))
      m.incomeChf += n.incomeChf
      m.electricityChf += n.electricityCostChf
    }
    return [...byMonth.values()]
  }, [network, run])

  const missed = useMemo<MissedPoint[]>(() => {
    const from = Math.max(0, t - CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const to = Math.min(network.length - 1, t + CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const byDay = new Map<number, MissedPoint>()
    for (let h = from; h <= to; h++) {
      const start = Math.floor(wallMs(run, h) / DAY_MS) * DAY_MS
      let d = byDay.get(start)
      if (!d) byDay.set(start, (d = { day: dayTick.format(start), start, misses: 0 }))
      d.misses += network[h].misses
    }
    return [...byDay.values()]
  }, [network, t, run])

  const selected = houses.find((h) => h.id === selectedId) ?? null
  const houseSeries = useAsync(selected ? `series:${run.id}:${selected.id}` : null, () => source.houseSeries(run.id, selected!.id))

  const dumpedShare = totals.dumped / Math.max(1, totals.used + totals.dumped)
  const missedShare = totals.misses / Math.max(1, totals.completions + totals.misses)
  const mapHouses = useMemo(() => houses.map((h) => ({ id: h.id, name: `${h.municipality || 'House'} ${h.id}`, lat: h.lat, lon: h.lon, servers: h.servers })), [houses])
  const wall = wallMs(run, t)

  return (
    <main className="page admin">
      {run.mock && <MockBanner />}
      <div className="page-head">
        <div>
          <h1>Network</h1>
          {run.note && <span className="note">{run.note}</span>}
        </div>
        <div className="head-controls">
          <select aria-label="Run" value={run.id} onChange={(e) => navigate(`/admin/${encodeURIComponent(e.target.value)}`)}>
            {runs.map((r) => (
              <option key={r.id} value={r.id}>
                {r.id}
              </option>
            ))}
          </select>
          <div className="segmented compact" role="radiogroup" aria-label="Time range">
            {RANGES.map((r) => (
              <button key={r.id} type="button" role="radio" aria-checked={rangeId === r.id} className={rangeId === r.id ? 'active' : ''} onClick={() => setRangeId(r.id)}>
                {r.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <section className="stats">
        <Stat label="Houses" value={num.format(run.houses)} note={`${num.format(run.servers)} servers`} />
        <Stat label="Average load" value={pct.format(totals.n ? totals.load / totals.n : 0)} note={`at selected hour ${pct.format(network[t]?.load ?? 0)}`} />
        <Stat label="Heat delivered" value={totals.used > 1000 ? dec1.format(totals.used / 1000) + ' MWh' : dec1.format(totals.used) + ' kWh'} />
        <Stat label="Heat dumped" value={pct.format(dumpedShare)} note={`${num.format(totals.dumped)} kWh`} />
        <Stat label="Missed deadlines" value={pct.format(missedShare)} note={`${num.format(totals.misses)} of ${num.format(totals.completions + totals.misses)} deadlines`} />
        <Stat label="Computing income" value={chf.format(totals.income)} />
        <Stat label="Electricity cost" value={chf.format(totals.cost)} />
      </section>

      <section className="map-card">
        <MapView
          houses={mapHouses}
          states={states}
          weather={weather}
          heatLayer={heatLayer}
          showWeather={showWeather}
          selectedId={selectedId}
          onSelect={setSelectedId}
        />

        <div className="map-controls">
          <div className="segmented compact">
            {HEAT_LAYERS.map((l) => (
              <button key={l.id} type="button" className={heatLayer === l.id ? 'active' : ''} onClick={() => setHeatLayer(l.id)}>
                {l.label}
              </button>
            ))}
          </div>
          <button type="button" role="switch" aria-checked={showWeather} className="switch" onClick={() => setShowWeather((v) => !v)}>
            <span className="switch-track">
              <span className="switch-knob" />
            </span>
            Weather
          </button>
        </div>

        {weather && showWeather && (
          <div className="map-legend">
            <span>Temperature (°C)</span>
            <div className="legend-bar" style={{ background: temperatureGradientCss() }} />
            <div className="legend-ticks fixed">
              {TEMP_LEGEND_TICKS_C.map((c) => (
                <span key={c} style={{ left: `${((c - TEMP_SCALE_MIN_C) / (TEMP_SCALE_MAX_C - TEMP_SCALE_MIN_C)) * 100}%` }}>
                  {c}
                </span>
              ))}
            </div>
          </div>
        )}

        {selected && (
          <SitePanel
            run={run}
            house={selected}
            state={snapshotOf(selected.id)}
            series={houseSeries.data}
            t={t}
            onMore={() => navigate(`/admin/${encodeURIComponent(run.id)}/houses/${encodeURIComponent(selected.id)}?h=${t}`)}
            onClose={() => setSelectedId(null)}
          />
        )}
      </section>

      <section className="timeline">
        <div className="timeline-head">
          <span className="timeline-time">{dateTime.format(wall)}</span>
          <span className="timeline-mode">
            {step === HOURS_PER_DAY ? 'Day' : 'Hour'} {num.format(t / step + 1)} of {num.format(maxT / step + 1)}
          </span>
        </div>
        <div className="timeline-row">
          <button type="button" className="play" onClick={togglePlay} aria-label={playing ? 'Pause' : 'Play'}>
            {playing ? (
              <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                <path d="M7 5h4v14H7zm6 0h4v14h-4z" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                <path d="M8 5v14l11-7z" />
              </svg>
            )}
          </button>
          <div className="segmented compact speed" role="radiogroup" aria-label="Playback speed">
            {PLAY_SPEEDS.map((v) => (
              <button key={v} type="button" role="radio" aria-checked={speed === v} className={speed === v ? 'active' : ''} onClick={() => setSpeed(v)}>
                {v} {step === HOURS_PER_DAY ? 'd' : 'h'}/s
              </button>
            ))}
          </div>
          <div className="timeline-track">
            <input
              type="range"
              min={0}
              max={maxT}
              step={step}
              value={t}
              onChange={(e) => {
                setPlaying(false)
                setT(Number(e.target.value))
              }}
              className="timeline-slider"
            />
            <div className="timeline-ticks">
              <span>{dateTime.format(wallMs(run, 0))}</span>
              <span>{dateTime.format(wallMs(run, maxT))}</span>
            </div>
          </div>
        </div>
      </section>

      <section className="charts">
        <div className="card">
          <span className="label">Network load</span>
          <LoadChart data={series} t={wall} />
        </div>
        <div className="card">
          <span className="label">Heat</span>
          <HeatChart data={series} t={wall} />
        </div>
        <div className="card wide">
          <span className="label">Computing income and electricity cost per month</span>
          <EconomicsChart data={monthly} />
        </div>
        <div className="card wide">
          <div className="card-head">
            <span className="label">Missed deadlines per day</span>
            <span className="note">
              {num.format(totals.misses)} of {num.format(totals.completions + totals.misses)} in the selected range
            </span>
          </div>
          <MissedChart data={missed} />
        </div>
      </section>
    </main>
  )
}

/** Temperature field at hour t from the real weather points; null while loading or when the overlay is off. */
/**
 * Weather field at hour t. For runs with day granularity (periodHours ≥ 24) it is the mean over the
 * local day containing t, like the case's heating-day rule and the daily house temperatures; a
 * single hour (e.g. midnight) would show the day/night cycle (≈ 8.7 K per day in 2025) instead of
 * the weather. Hourly runs show the hour.
 */
function useWeatherField(source: DataSource, run: RunInfo, t: number, enabled: boolean): WeatherField | null {
  const daily = run.periodHours >= HOURS_PER_DAY
  const fromH = daily ? Math.floor(t / HOURS_PER_DAY) * HOURS_PER_DAY : t
  const toH = Math.min(run.hours, fromH + (daily ? HOURS_PER_DAY : 1))
  const points = useAsync(enabled ? 'weather-points' : null, () => source.weatherPoints())
  const hours = useAsync(enabled ? `weather:${run.id}:${fromH}:${toH}` : null, () => source.weather(run.id, fromH, toH))
  return useMemo(() => {
    const rows = hours.data?.temperatureC
    if (!enabled || !points.data || !rows?.length) return null
    let interpolator = interpolators.get(points.data)
    if (!interpolator) interpolators.set(points.data, (interpolator = makeInterpolator(points.data)))
    if (rows.length === 1) return interpolator.fill(rows[0])
    const mean = new Array<number>(rows[0].length).fill(0)
    for (const row of rows) for (let i = 0; i < row.length; i++) mean[i] += row[i] / rows.length
    return interpolator.fill(mean)
  }, [enabled, points.data, hours.data])
}

function Stat({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="stat">
      <span className="label">{label}</span>
      <span className="stat-value">{value}</span>
      {note && <span className="note">{note}</span>}
    </div>
  )
}
